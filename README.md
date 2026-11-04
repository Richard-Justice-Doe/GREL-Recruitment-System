# GREL Recruitment & Selection Management System (GRSMS)

A web application that moves GREL's whole recruitment process online, from a department's request for a new hire through to the new employee's onboarding. It is built in **13 phases**. This repository currently contains **Phase 1: the foundation**.

## What Phase 1 gives you

| Area | What works today |
|---|---|
| **Sign-in** | Email + password sign-in for staff and candidates, account lockout after 5 wrong passwords, a 20-minute idle timeout, forgot/reset password, change password, and a forced password change for new staff. The *Sign in with Microsoft 365* button appears once IT provides the Azure IDs. |
| **Candidate registration** | Public "Create a candidate account" page with email confirmation |
| **Roles** | 8 roles: HR Administrator, HR Manager, Managing Director, Recruitment Officer, Head of Department, Interview Panel Member, Employee, Candidate |
| **Departments** | The 24 GREL departments are pre-loaded. Departments can be added, edited, activated or deactivated, and each can have a Head of Department. |
| **Units** | Sub-sections of departments (e.g. Finance → Payroll) |
| **Sites** | Work locations (Head Office is pre-loaded) |
| **Users** | HR Administrators can create staff accounts, assign roles, and deactivate or unlock accounts |
| **Audit trail** | Every change to every record (who, when, old value, new value, IP address) and every sign-in is recorded and searchable |
| **Look and feel** | Sidebar navigation, glass-style cards, light/dark mode, phone-friendly layout, searchable and sortable tables |

## Quick start

You need the **.NET 10 SDK** and **MySQL 8**. The full step-by-step guide is in [docs/01-getting-started.md](docs/01-getting-started.md).

```powershell
# 1. Tell the app how to reach MySQL and who the first administrator is (stored outside the code)
dotnet user-secrets --project src/Grsms.Web set "ConnectionStrings:DefaultConnection" "server=localhost;port=3306;database=grsms;user=grsms;password=YOUR_PASSWORD"
dotnet user-secrets --project src/Grsms.Web set "SeedAdmin:Email" "you@grel.example"
dotnet user-secrets --project src/Grsms.Web set "SeedAdmin:Password" "Choose#A-Strong1"

# 2. Run it (in Development the database tables are created automatically)
dotnet run --project src/Grsms.Web

# 3. Run the automated tests (no MySQL needed)
dotnet test
```

## Documentation

| Document | Read it if you want to... |
|---|---|
| [01 Getting started](docs/01-getting-started.md) | install, configure and run the system for the first time |
| [02 Architecture](docs/02-architecture.md) | understand how the code is organised and how to add a feature |
| [03 Database](docs/03-database.md) | see the tables, columns, keys and how to apply the SQL scripts |
| [04 Security and roles](docs/04-security-and-roles.md) | know who can do what, the password rules, and how to switch on Microsoft 365 sign-in |
| [05 Administrator guide](docs/05-admin-guide.md) | manage departments, units, sites and users day to day (non-technical) |
| [06 Configuration](docs/06-configuration.md) | look up every setting |
| [07 Testing](docs/07-testing.md) | run and extend the automated tests |
| [Phase log](docs/phases.md) | see what each phase delivered and what comes next |

## Folder layout

```
GREL Recruitment System/
├── Grsms.slnx                  ← open this in Visual Studio / VS Code / Rider
├── database/                   ← MySQL scripts, one per phase, for the DBA
├── docs/                       ← all documentation
├── src/
│   ├── Grsms.Domain/           ← business objects (Department, Unit, ...), no framework code
│   ├── Grsms.Application/      ← business rules (services), DTOs, interfaces
│   ├── Grsms.Infrastructure/   ← MySQL/EF Core, repositories, Identity, email, audit
│   └── Grsms.Web/              ← web pages: controllers, Razor views, CSS/JS
└── tests/
    └── Grsms.Tests/            ← automated tests
```

## Technology

ASP.NET Core 10 MVC · Entity Framework Core 10 · MySQL 8 (Oracle `MySql.EntityFrameworkCore` provider) · ASP.NET Core Identity · Bootstrap 5.3 · Font Awesome 6 · jQuery · DataTables 2 · Serilog · xUnit

See [docs/02-architecture.md](docs/02-architecture.md#technology-choices) for why some tools in the original brief (MediatR, Tailwind, the Pomelo MySQL provider) were swapped for others.
