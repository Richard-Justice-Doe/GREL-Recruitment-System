# GREL Recruitment & Selection Management System (GRSMS)

A web application that moves GREL's whole recruitment process online, from a department's request for a new hire through to the new employee's onboarding. It is built in **13 phases**. This repository currently contains **Phases 1–9**: foundation, recruitment requests, vacancies, job adverts, the candidate portal with applications, CV screening with shortlisting, interviews with scorecards, hiring with offers, and appointment letters with onboarding. See the [phase log](docs/phases.md).

## What Phase 9 adds

| Area | What works today |
|---|---|
| **Onboarding** | Started by HR for each accepted offer, with a standard checklist for HR, the department, IT and the new employee, each item with an owner and a due date |
| **Appointment letters** | Issued by the HR Manager, read and signed online by the new employee (typed-name signature), printable |
| **First day** | Date, time, place, who meets them and what to bring, on the new employee's application page and in their emails |

## What Phase 8 adds

| Area | What works today |
|---|---|
| **Background checks** | References, education, employment, criminal record and medical checks, each pending → cleared / concerns / failed |
| **Hiring recommendation** | The hiring report: proposed salary and start date, endorsed by the department's HOD, approved by the HR Manager and the MD |
| **Offers** | Offer letters within the approved salary and free positions; the candidate accepts or declines online; unanswered offers expire |

## What Phase 7 adds

| Area | What works today |
|---|---|
| **Interviews and assessments** | Interviews, written tests and practical assessments for shortlisted applicants, in rounds, with invitations by email and on the applicant's tracker |
| **Panels** | 2-8 staff with a chair, always including the department's Head of Department |
| **Scorecards** | Weighted criteria rated 1-5, recommendations and comments; independent scoring; results ranked per vacancy |

## What Phase 6 adds

| Area | What works today |
|---|---|
| **CV screening** | Every application scored out of 100 against the vacancy's requirements, with a meets-the-minimum check and the missing items |
| **Shortlisting board** | Applications ranked per vacancy, with recommendations and actions |
| **HOD participation** | Recruiter recommends → Head of Department agrees or disagrees → recruiter or HR Manager shortlists (HR Manager override with a reason) |

## What Phase 5 adds

| Area | What works today |
|---|---|
| **My profile** | Every account keeps contact details, education, experience, skills, certifications, a CV and certificates |
| **Apply online** | From the careers page, once per vacancy (numbered `APP-2026-00001`); staff apply for internal vacancies the same way |
| **Tracker** | Received → Under review → Shortlisted → Interview → Offer → Hired, with withdrawal and emails |
| **Applications (staff)** | New / Under review / All lists with division, department, vacancy, source, staff/external and period filters; applicant details and documents; start review, mark not successful |

## What Phase 4 adds

| Area | What works today |
|---|---|
| **Job adverts** | Adverts for published vacancies (numbered `ADV-2026-0001`), pre-filled from the vacancy, with start and end dates |
| **Channels** | Internal portal and company website published automatically; LinkedIn, Indeed, Jobberman and newspapers get copy-ready text and are tracked per channel |
| **Scheduling** | Publish now or on a later start date; adverts close by themselves after their end date or when the vacancy closes; clone to re-advertise |
| **Careers page** | Public `/Careers` page listing live jobs (no sign-in needed); staff also see internal jobs |

## Since Phase 3 (4 November 2026)

| Area | What works today |
|---|---|
| **Home dashboard** | KPI cards with 7-day charts, *My tasks*, a vacancy closing-date calendar, requests in approval and the latest candidates, with sidebar counts, a search box and a notification bell. See [11 Home dashboard](docs/11-home-dashboard.md). |
| **Docker** | App + MySQL 8.4 + phpMyAdmin (a database viewer) with one command. See [10 Running with Docker](docs/10-docker.md). |
| **Divisions, departments and sites** | Two divisions (Corporate, Operations) over GREL's 17 departments, and GREL's 12 sites. Requests and vacancies record their division and requesting HOD, and every list and the dashboard can be filtered by division, department, status and period. See [12 Divisions and departments](docs/12-divisions-and-departments.md). |

What changed and why: [change log](docs/changelog.md).

## What Phase 3 adds

| Area | What works today |
|---|---|
| **Vacancies** | Created from fully approved requests only (one each, numbered `VAC-2026-0001`), with job description, structured education/experience/skill/certification requirements, salary range and closing date |
| **Approval and life cycle** | Draft → Pending approval → Published (HR Manager approves) → Closed (automatically after the closing date, or by hand) → Archived, with reopening and return-for-changes |
| **Work queue** | "Approved requests awaiting a vacancy" for recruiters, "Pending approval" for HR Managers, home-page tiles and email notifications |

## What Phase 2 adds

| Area | What works today |
|---|---|
| **Recruitment requests** | Heads of Department raise requests (numbered `RR-APM-2026-0001`) with position, unit, site, employment type, reason, budget code and cost, and attach the job description and other documents |
| **Approval workflow** | Draft → Recruitment Review → HR Approval (budget validated) → MD Approval → Approved. Each reviewer can approve, return for changes or reject. Nobody can approve their own request. |
| **Work queue** | An "Awaiting my action" list and home-page tile for each approver, with email notifications at every step |
| **Progress and history** | A progress stepper and a full history timeline on every request |

## What Phase 1 gives you

| Area | What works today |
|---|---|
| **Sign-in** | Email + password sign-in for staff and candidates, account lockout after 5 wrong passwords, a 20-minute idle timeout, forgot/reset password, change password, and a forced password change for new staff. The *Sign in with Microsoft 365* button appears once IT provides the Azure IDs. |
| **Candidate registration** | Public "Create a candidate account" page with email confirmation |
| **Roles** | 8 roles: HR Administrator, HR Manager, Managing Director, Recruitment Officer, Head of Department, Interview Panel Member, Employee, Candidate |
| **Divisions and departments** | Two divisions (Corporate, Operations) and GREL's 17 departments (Finance, Transport, Factory APM, Factory TBU, ...) are pre-loaded. Departments can be added, edited, activated or deactivated, and each can have a Head of Department. |
| **Units** | Sub-sections of departments (e.g. Finance → Payroll) |
| **Sites** | Work locations, with a site type (GREL's 12 sites are pre-loaded) |
| **Users** | HR Administrators can create staff accounts, assign roles, and deactivate or unlock accounts |
| **Audit trail** | Every change to every record (who, when, old value, new value, IP address) and every sign-in is recorded and searchable |
| **Look and feel** | Sidebar navigation, glass-style cards, light/dark mode, phone-friendly layout, searchable and sortable tables |

## Quick start

**With Docker** (nothing else to install): create `.env` as described in [docs/10-docker.md](docs/10-docker.md#first-run), then run

```powershell
docker compose up -d --build     # app: http://localhost:8080 · database viewer: http://localhost:8081
```

**Without Docker** you need the **.NET 10 SDK** and **MySQL 8**. The full step-by-step guide is in [docs/01-getting-started.md](docs/01-getting-started.md).

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
| [08 Recruitment requests guide](docs/08-recruitment-requests.md) | raise, approve, return or reject recruitment requests (non-technical) |
| [09 Vacancies guide](docs/09-vacancies.md) | create, approve, close, reopen and archive vacancies (non-technical) |
| [10 Running with Docker](docs/10-docker.md) | run the app, MySQL and the database viewer in Docker; back up; troubleshoot |
| [11 Home dashboard](docs/11-home-dashboard.md) | understand every panel on the Home page and who sees what |
| [12 Divisions and departments](docs/12-divisions-and-departments.md) | understand the Corporate / Operations structure, the department and site lists, and the report filters |
| [13 Job adverts guide](docs/13-job-adverts.md) | create, publish, schedule, post, close and clone job adverts; the careers page (non-technical) |
| [14 Applications guide](docs/14-applications.md) | apply online, follow an application, and review applications (non-technical) |
| [15 Shortlisting guide](docs/15-shortlisting.md) | how screening scores work and how to shortlist with the HOD (non-technical) |
| [16 Interviews guide](docs/16-interviews.md) | schedule interviews and assessments, score them and review the results (non-technical) |
| [17 Hiring guide](docs/17-hiring.md) | background checks, the hiring recommendation and its approvals, offers (non-technical) |
| [18 Onboarding guide](docs/18-onboarding.md) | start onboarding, the checklist, the appointment letter and the first day (non-technical) |
| [Phase log](docs/phases.md) | see what each phase delivered and what comes next |
| [Change log](docs/changelog.md) | see work done between phases, with reasons and checks |

## Folder layout

```
GREL Recruitment System/
├── Grsms.slnx                  ← open this in Visual Studio / VS Code / Rider
├── Dockerfile                  ← builds the web app image
├── docker-compose.yml          ← app + MySQL + phpMyAdmin (passwords in .env, not in git)
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
