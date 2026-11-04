# 2. Architecture

This page explains how the code is organised, why, and how to add a new feature.

## The four layers (Clean Architecture)

The solution is split into four projects. Each one may only use the projects **below** it:

```
 ┌───────────────────────────────────────────────┐
 │  Grsms.Web              (what the user sees)  │  controllers, Razor views, CSS/JS, Program.cs
 ├───────────────────────────────────────────────┤
 │  Grsms.Infrastructure   (talking to the world)│  MySQL via EF Core, repositories, Identity, email
 ├───────────────────────────────────────────────┤
 │  Grsms.Application      (business rules)      │  services, DTOs, interfaces
 ├───────────────────────────────────────────────┤
 │  Grsms.Domain           (business objects)    │  Department, Unit, Site, AuditLog, Roles
 └───────────────────────────────────────────────┘
```

| Project | Contains | Must NOT contain |
|---|---|---|
| **Domain** | Plain C# classes for the things GREL talks about (Department, Unit, Site, ...) and constants (role names) | Any database, web or framework code |
| **Application** | The *rules*: "a department code must be unique", "a unit can't be active in an inactive department". The **interfaces** it needs (`IRepository<T>`, `IEmailService`, `ICurrentUserService`). The **DTOs** that screens send and receive. | SQL, Entity Framework, HTTP |
| **Infrastructure** | The *how*: Entity Framework + MySQL, the generic repository, ASP.NET Identity user storage, email sending, the audit interceptor | Business rules, web pages |
| **Web** | Controllers (thin), Razor views, layout, static files, start-up configuration | Business rules or database queries |

**Why bother?** Rules live in one place and can be tested without a database or browser. If GREL ever changes database or email provider, only Infrastructure changes.

## How a request flows

Here is what happens when an HR Administrator saves a department:

```
Browser  ──POST /Departments/Edit/3──►  DepartmentsController.Edit()           (Web)
                                           │  1. anti-forgery token checked (global filter)
                                           │  2. [Authorize(Policy = MasterData)] checked
                                           │  3. form attributes validated ([Required], ...)
                                           ▼
                                        DepartmentService.UpdateAsync()         (Application)
                                           │  4. trims/upper-cases, checks uniqueness and HOD rules
                                           │  5. copies the form onto the Department entity
                                           ▼
                                        IUnitOfWork.SaveChangesAsync()
                                           ▼
                                        AuditSaveChangesInterceptor             (Infrastructure)
                                           │  6. sets UpdatedAt / UpdatedBy
                                           │  7. writes an AuditLogs row (old + new values, IP)
                                           │     in the SAME transaction
                                           ▼
                                        MySQL
```

The controller then redirects to the details page with a green "saved" banner.

## Patterns used

| Pattern | Where | In plain words |
|---|---|---|
| **Dependency injection** | `DependencyInjection.cs` in Application and Infrastructure, `Program.cs` | Classes ask for what they need in their constructor. The framework supplies it, so each piece can be swapped or faked in tests. |
| **Repository** | `IRepository<T>` (Application), `Repository<T>` (Infrastructure) | Services ask "give me department 3" without knowing about SQL. One generic class serves every table. |
| **Unit of work** | `IUnitOfWork` = `ApplicationDbContext` | All changes from one operation are saved together, or not at all. |
| **DTOs** | `*Dto` classes in Application | Forms and lists use their own small classes, so a form can never overwrite fields like `CreatedBy`. |
| **Result object** | `Result`, `Result<T>` | Services return "succeeded" or "failed, here's why" instead of throwing exceptions for normal validation problems. |
| **Interceptor** | `AuditSaveChangesInterceptor` | Code that runs automatically on every save: audit fields, soft delete and audit trail, so no developer can forget them. |
| **Policies** | `Web/Security/Policies.cs` | Who may do what is defined once and referenced by name. |

## Folder structure (Phase 1)

```
src/
├── Grsms.Domain/
│   ├── Common/BaseEntity.cs          ← Id + audit + soft-delete fields shared by every table
│   ├── Organization/                 ← Department, Unit, Site
│   ├── Auditing/AuditLog.cs          ← audit trail row + standard action names
│   └── Security/Roles.cs             ← the 8 role names
├── Grsms.Application/
│   ├── Common/                       ← Result, PagedResult, interfaces (IRepository, IEmailService, ...)
│   ├── Organization/                 ← DTOs + DepartmentService, UnitService, SiteService
│   ├── Users/                        ← user DTOs + IUserDirectory, IUserAdminService
│   ├── Auditing/                     ← IAuditLogService + query/DTO
│   └── DependencyInjection.cs
├── Grsms.Infrastructure/
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs   ← the EF Core context (+ soft-delete filter)
│   │   ├── Configurations/           ← one class per table: lengths, indexes, keys, seed rows
│   │   ├── Migrations/               ← generated by "dotnet ef" (do not edit by hand)
│   │   ├── AuditSaveChangesInterceptor.cs
│   │   ├── Repository.cs
│   │   ├── DbInitializer.cs          ← start-up: migrations + first administrator
│   │   └── DesignTimeDbContextFactory.cs
│   ├── Identity/                     ← ApplicationUser, UserDirectory, UserAdminService
│   ├── Auditing/AuditLogService.cs
│   ├── Email/EmailService.cs
│   └── DependencyInjection.cs
└── Grsms.Web/
    ├── Program.cs                    ← start-up, read top to bottom
    ├── Security/                     ← policies, sign-in manager, current user, password filter
    ├── Controllers/                  ← Account, Home, Departments, Units, Sites, Users, AuditLogs
    ├── Models/                       ← view models for account pages and the home page
    ├── Views/                        ← one folder per controller + Shared (layouts, partials)
    └── wwwroot/                      ← css/site.css, js/site.js, lib/ (Bootstrap, Font Awesome, DataTables, jQuery)
```

## How to add a new feature (worked example)

Say Phase 2 needs **Recruitment Requests**. Follow the same five steps every module uses:

1. **Domain:** add `RecruitmentRequest : BaseEntity` with its fields. Inheriting `BaseEntity` gives it audit fields, soft delete and audit-trail coverage for free.
2. **Infrastructure:**
   * add `public DbSet<RecruitmentRequest> RecruitmentRequests => Set<RecruitmentRequest>();` to `ApplicationDbContext`;
   * add `RecruitmentRequestConfiguration` in `Persistence/Configurations` (lengths, indexes, foreign keys);
   * create the migration and script:
     ```powershell
     dotnet ef migrations add Phase02_RecruitmentRequests --project src/Grsms.Infrastructure --startup-project src/Grsms.Web --output-dir Persistence/Migrations
     dotnet ef migrations script Phase01_Foundation Phase02_RecruitmentRequests --project src/Grsms.Infrastructure --startup-project src/Grsms.Web -o database/02_phase02_recruitment_requests.sql
     ```
3. **Application:** add DTOs and `IRecruitmentRequestService` / `RecruitmentRequestService` using `IRepository<RecruitmentRequest>` + `IUnitOfWork`. Register it in `Application/DependencyInjection.cs`.
4. **Web:** add a policy in `Policies.cs` if new roles are involved. Add a thin controller and views, and a sidebar link in `Views/Shared/_Sidebar.cshtml`.
5. **Tests + docs:** add tests in `tests/Grsms.Tests` and update `docs/03-database.md` and `docs/phases.md`.

You don't need to write `IRepository<RecruitmentRequest>` yourself: the generic repository already covers every `BaseEntity`.

## Technology choices

The original brief listed a few tools that were deliberately replaced:

| Brief said | Used instead | Why |
|---|---|---|
| ASP.NET Core 8 | **ASP.NET Core 10** | Installed on the development PC and supported until Nov 2028. .NET 8 support ends Nov 2026. |
| Pomelo MySQL provider (implied) | **Oracle `MySql.EntityFrameworkCore` 10** | Pomelo does not yet support EF Core 10. Oracle's official provider does. |
| MediatR | **Plain services behind interfaces** | MediatR now needs a paid commercial licence. Services keep the same separation without the cost. |
| Bootstrap 5 + Tailwind | **Bootstrap 5 only** | The two frameworks' styles conflict. Bootstrap works with DataTables and includes dark mode. |
| AutoMapper (common default) | **Manual mapping** (`Apply(...)` methods) | Also now commercially licensed, and manual mapping keeps every field visible. |

All front-end libraries are stored in `wwwroot/lib`, so the site works on a network without internet access.
