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

## Workflows (Phase 2 onwards)

Approval workflows follow the same three-part design, shown here with recruitment requests:

| Part | File | Holds |
|---|---|---|
| **The map** | `Domain/Recruitment/RecruitmentWorkflow.cs` | The stages, the role that decides at each one, what comes next on approval, and which statuses are editable or final. Pure rules, no database. Change the workflow here. |
| **The record** | `Domain/Recruitment/RecruitmentRequest.cs` → `RecordStep(...)` | Moving to a new status always writes a history row and refreshes the `Version` concurrency stamp, in one method, so the two can't get out of step. |
| **The gatekeeper** | `Application/Recruitment/RecruitmentRequestService.cs` | Who may act (role, "not your own request", department visibility), the stage-specific checks (budget tick, recruiter assignment, comments), saving, and notification emails. |

Shared building blocks added in Phase 2, reusable by later modules:
* `INumberGenerator`: concurrency-safe reference numbers (`RR-APM-2026-0001`; vacancies and offers will use it too).
* `AttachmentService` + `IFileStorage`: validated uploads, stored outside the website, for any record (`OwnerType` + `OwnerId`).
* `ConcurrencyConflictException`: raised by the unit of work when two people save the same record at once.
* `IAppLinks`: full links for emails.

Added in Phase 3:
* `AccessScope` (Application/Common): the single "who sees which department's records" rule, used by requests and vacancies.
* `WorkflowMailer` (Application/Common): the shared notification email (no self-emails, same layout everywhere, failures logged but never undoing a saved step).
* `VacancyWorkflow` + `VacancyService`: the same map / record / gatekeeper pattern as requests.
* **Background jobs** live in `Grsms.Web/Workers`. `ScheduledJobsWorker` runs every hour in its own scope with no signed-in user (recorded as "System") and simply calls `VacancyService.CloseExpiredAsync`, so the rule itself stays testable in the Application layer.
* Modules avoid depending on each other directly: the request page asks `IVacancyLookup` ("is there a vacancy for this request?") instead of using the whole vacancy service.

Added with the home dashboard (November 2026, see [11 Home dashboard](11-home-dashboard.md#for-developers)):
* `DashboardService` (Application/Dashboard): read-only, builds the whole Home page in one call and reuses the module services for their "awaiting me" rules. The web layer passes in what the user may see as a `DashboardAccess` flag set, because authorization policies are a web concern.
* `NavCounts` (Web/Helpers): scoped per request, so the sidebar badges and the bell share one set of count queries.

## Folder structure (Phase 1)

```
src/
├── Grsms.Domain/
│   ├── Common/BaseEntity.cs          ← Id + audit + soft-delete fields shared by every table
│   ├── Organization/                 ← Division, Department, Unit, Site
│   ├── Auditing/AuditLog.cs          ← audit trail row + standard action names
│   └── Security/Roles.cs             ← the 8 role names
├── Grsms.Application/
│   ├── Common/                       ← Result, PagedResult, interfaces (IRepository, IEmailService, ...)
│   ├── Organization/                 ← DTOs + DivisionService, DepartmentService, UnitService, SiteService
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

**Added in Phase 2:**

```
Grsms.Domain/Recruitment/            ← RecruitmentRequest (+ history), enums, RecruitmentWorkflow
Grsms.Domain/Documents/Attachment.cs
Grsms.Application/Recruitment/       ← DTOs, IRecruitmentRequestRepository, RecruitmentRequestService
Grsms.Application/Documents/         ← AttachmentService (upload checks)
Grsms.Application/Common/Interfaces/ISupportInterfaces.cs  ← INumberGenerator, IFileStorage, IAppLinks
Grsms.Infrastructure/Persistence/    ← RecruitmentRequestRepository, NumberGenerator, RecruitmentConfigurations
Grsms.Infrastructure/Storage/        ← LocalFileStorage
Grsms.Web/Controllers/RecruitmentRequestsController.cs
Grsms.Web/Views/RecruitmentRequests/ ← Index, Form, Details, _StatusBadge, _WorkflowStepper
Grsms.Web/Helpers/EnumExtensions.cs  ← friendly enum names on screen
```

**Added in Phase 3:**

```
Grsms.Domain/Vacancies/              ← Vacancy (+ requirements, history), enums, VacancyWorkflow
Grsms.Application/Common/            ← AccessScope, WorkflowMailer (shared by all workflows)
Grsms.Application/Vacancies/         ← DTOs, IVacancyRepository, VacancyService
Grsms.Infrastructure/Persistence/    ← VacancyRepository, VacancyConfigurations
Grsms.Web/Controllers/VacanciesController.cs
Grsms.Web/Views/Vacancies/           ← Index, Form, Details, _StatusBadge
Grsms.Web/Workers/ScheduledJobsWorker.cs
```

**Added in Phase 4:**

```
Grsms.Domain/Advertisements/         ← JobAdvert (+ channels, history), enums, AdvertWorkflow
Grsms.Application/Adverts/           ← DTOs, IAdvertRepository, AdvertService, AdvertText (default and copy-ready text)
Grsms.Application/Common/EnumText.cs ← friendly enum names in text the services write
Grsms.Infrastructure/Persistence/    ← AdvertRepository, AdvertConfigurations
Grsms.Web/Controllers/               ← AdvertsController (staff), CareersController (public, no sign-in)
Grsms.Web/Views/Adverts/             ← Index, Form, Details, _StatusBadge
Grsms.Web/Views/Careers/             ← Index, Details, NotFound (use Shared/_PublicLayout)
Grsms.Web/Workers/ScheduledJobsWorker.cs ← now also runs AdvertService.RunScheduleAsync (publish scheduled, close expired)
```

**Added in Phase 5:**

```
Grsms.Domain/Applications/          ← CandidateProfile, JobApplication (+ history), enums, ApplicationWorkflow
Grsms.Application/Applications/     ← DTOs, IApplicationRepository, CandidateProfileService, ApplicationService
Grsms.Infrastructure/Persistence/    ← ApplicationRepository, ApplicationConfigurations
Grsms.Web/Controllers/               ← ProfileController + MyApplicationsController (any signed-in person),
                                       ApplicationsController (staff)
Grsms.Web/Views/Profile, MyApplications, Applications/
Grsms.Web/Views/Shared/              ← _DocumentList, _ApplicationStatus
```

**Added in Phase 6:**

```
Grsms.Domain/Applications/CvScreening.cs   ← the scoring rules (pure, unit-tested) and ScreeningResult
Grsms.Application/Shortlisting/             ← DTOs, ShortlistingService (board, screening, recommend, HOD view, shortlist)
Grsms.Web/Controllers/ShortlistingController.cs
Grsms.Web/Views/Shortlisting/Vacancy.cshtml ← the ranked board
Grsms.Web/Views/Shared/_ShortlistActions   ← the actions, shared by the board and the application page
```

**Added in Phase 7:**

```
Grsms.Domain/Interviews/             ← Interview (+ panel, criteria, scorecards, ratings), enums, InterviewWorkflow
Grsms.Application/Interviews/        ← DTOs, IInterviewRepository, InterviewService
Grsms.Infrastructure/Persistence/    ← InterviewRepository, InterviewConfigurations
Grsms.Web/Controllers/InterviewsController.cs  (Staff policy: anyone can be on a panel)
Grsms.Web/Views/Interviews/          ← Index, Schedule, Details (scorecard), Results
```

**Added in Phase 8:**

```
Grsms.Domain/Hiring/                 ← BackgroundCheck, HiringRecommendation, Offer, enums, HiringWorkflow
Grsms.Application/Hiring/            ← DTOs, HiringService (checks, recommendation and approvals, offers, expiry)
Grsms.Infrastructure/Persistence/    ← HiringConfigurations (generic repositories are enough)
Grsms.Web/Controllers/HiringController.cs; MyApplicationsController gains Offer + RespondOffer
Grsms.Web/Views/Hiring/              ← Index, Case;  Views/MyApplications/Offer (printable offer letter)
Grsms.Web/Workers/ScheduledJobsWorker.cs ← now also expires unanswered offers
```

**Added in Phase 9:**

```
Grsms.Domain/Onboarding/             ← OnboardingRecord (letter + first day), OnboardingTask, enums, OnboardingWorkflow (default checklist)
Grsms.Application/Onboarding/        ← DTOs, OnboardingService (start, checklist, letter, signature, complete, cancel, "my tasks")
Grsms.Infrastructure/Persistence/    ← OnboardingConfigurations (generic repositories are enough)
Grsms.Web/Controllers/OnboardingController.cs (Staff policy: checklist items can go to anyone, e.g. IT);
    MyApplicationsController gains Appointment + SignLetter; HiringController links to onboarding
Grsms.Web/Views/Onboarding/          ← Index, Start, Details, Letter;  Views/MyApplications/Appointment;
    Views/Shared/_AppointmentLetter (one letter for the new employee and the staff preview)
```

**Added in Phase 10:**

```
Grsms.Domain/Common/INotAudited.cs   ← marker: rows the audit interceptor skips (notifications, reminder keys, survey answers)
Grsms.Domain/Notifications/          ← Notification, SentReminder (plain tables: no soft delete)
Grsms.Domain/Surveys/                ← SurveyInvitation, SurveyAnswer, SurveyQuestions (fixed questions, by key)
Grsms.Application/Notifications/     ← INotificationCenter, ISmsSender, ReminderService
Grsms.Application/Surveys/           ← SurveyService (invitations, answers, results)
Grsms.Application/Common/            ← WorkflowMailer now also adds a notification per email and sends SMS (TextAsync); PhoneNumbers
Grsms.Infrastructure/Notifications/  ← NotificationCenter (writes in its own scope), SmsSender (files when off, Hubtel when on)
Grsms.Web/Controllers/NotificationsController.cs, SurveysController.cs;  Views/Notifications, Views/Surveys
Grsms.Web/Workers/ScheduledJobsWorker.cs ← now also runs reminders, survey invitations and the notification clean-up
```

**Added in Phase 11:**

```
Grsms.Application/Reports/           ← ReportDtos (filter, generic table, KPIs, charts), ReportService (all reports, live from the records)
Grsms.Web/Controllers/ReportsController.cs (Reports policy; CSV export with formula protection)
Grsms.Web/Views/Reports/Index.cshtml ← one page renders every report (generic table + one-colour charts)
```

Each report is a method returning the same `ReportResult` shape, so a new report needs one method in `ReportService` and one line in `ReportKeys`; the page and the CSV export need no change.

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
