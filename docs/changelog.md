# Change log

Work done outside the numbered phases, newest first. Each entry says what changed, which files, how it was checked, and anything still open. For the phases themselves, see the [phase log](phases.md).

---

## 5 November 2026: Clean-up committed; Phase 14 started

* The 4 November clean-up (unused demo mode code and two methods nothing called) was committed on branch `feature/ats-and-super-admin`. The `_removed-for-review/` copies are still on disk (git-ignored) until they are deleted.
* Phase 14 (Super Admin and permissions) is recorded in the [phase log](phases.md#-phase-14-super-admin-and-granular-permissions-5-november-2026).

---

## 4 November 2026 (22:00): SweetAlert2 for confirmations and messages

**Why:** the user asked to use SweetAlert instead of the browser's plain boxes.

**Changed**
* **Confirmations** (all 35 `data-confirm` buttons: approve, reject, withdraw, deactivate, send offer, ...) now open a SweetAlert2 dialog: *Are you sure?*, the message, **Cancel** and a button named like the one clicked (red for red buttons, with Cancel focused). On *yes* the form is sent with the same button, so `name="decision" value="Approve"` buttons keep working. Without the library (script blocked) the browser's own box is used.
* **Messages after an action** (TempData success / info / error) appear as SweetAlert2 pop-ups: success and info as a notice in the top corner that closes itself after 5 seconds; errors as a dialog with **OK**, so they are not missed. Without JavaScript the old banner shows instead.
* Styled for GRSMS in light and dark mode (Bootstrap buttons, page colours) in `site.css`.
* Files: SweetAlert2 **11.14.5** (MIT licence) in `wwwroot/lib/sweetalert2/`, **served by GRSMS itself**, not from a CDN, because the Content-Security-Policy (Phase 12) only allows the site's own scripts; added to the three layouts; logic in `js/site.js` (section 3); `Views/Shared/_Alerts.cshtml` marks each message with `data-flash`.

**Security:** SweetAlert2 treats `title` and button texts as HTML. Messages can contain names people typed, so they are passed as plain text (`text`, `titleText`) and button labels copied from the page are escaped. The library contains a `new Function` call, used only by a feature GRSMS does not use (dialogs declared in HTML `<template>`); the policy would block it anyway.

**Checked:** 111/111 tests (new: every layout loads SweetAlert2 from `/lib`, and the file is served). In a real browser (headless Edge): the error dialog (dark), the success notice (light), the confirmation for a red *Withdraw offer* button (light and dark), and that answering *yes* submits the form with the clicked button's value and the hidden fields.

---

## 4 November 2026 (12:20): Demo mode removed; local app uses the real database

**Why:** the user asked to remove the demo site and use the actual database and web app.

* Stopped the demo app (port 5014) and deleted its SQLite file `src/Grsms.Web/App_Data/grsms-demo.db` (made-up sample data only).
* Removed the `demo` profile from `launchSettings.json`. `DemoDataSeeder.cs` is still in the code but nothing starts it.
* Set the user-secret `ConnectionStrings:DefaultConnection` to the Docker MySQL database (`localhost:3307`), so `dotnet run` on http://localhost:5014 uses the same data as the Docker app on http://localhost:8080.
* Docs: [01 Getting started](01-getting-started.md) demo section replaced; `CREDENTIALS.local.md` updated.

**Checked:** http://localhost:5014 signs in as `admin@grel.local` and its Sites page shows the MySQL sites, with no demo sites.

**Found:** site 5 **Tsibu** is missing from the database. phpMyAdmin's log shows a change to the Sites table at 12:17 (UTC), made in the database viewer, which bypasses the app's audit trail. Not restored, pending the user's decision.

---

## 4 November 2026 (latest): GREL's 12 sites confirmed

**Why:** GREL confirmed the site list is exactly the 12 sites in its own system, that Tsibu is the TBU factory, and that the APM site is spelt *Apeminim*.

**Changed** (migration `GrelSites`, script `database/06_grel_sites.sql`)
* Site 3 renamed *Apimenim (APM)* → **Apeminim**.
* **Tsibu** gets the type *Rubber Processing Factory (1D1F Expansion Project)*.
* Sites 13 *TBU Factory* and 14 *Nucleus Operations Areas* set **inactive** (kept, not deleted, so ids never change).
* Seed test now checks 12 active sites, the Tsibu type and the Apeminim spelling.

**Checked:** 53/53 tests passed; Docker MySQL shows the 12 active sites and the two inactive ones; the Sites page in the Docker app lists them.

---

## 4 November 2026 (later): Divisions, GREL sites and report filters

**Why:** the GREL requirements call for a two-level structure (Division → Department), for every recruitment record to be linked to a division, department and requesting HOD, and for reports to filter by them. GREL's site list (with site types) arrived at the same time. Full guide: [12 Divisions, departments and sites](12-divisions-and-departments.md).

**Changed**
* **Divisions** (new table): Corporate and Operations. New *Divisions* screen for HR Administrators (`DivisionService`, `DivisionsController`).
* **Departments** now belong to a division (required). The list was set to the 17 departments in the requirements:
  * renamed: Transport & Logistics → **Transport** (reactivated), APM Factory → **Factory APM**, TBU Factory → **Factory TBU**, RUTAP → **RuTAP**;
  * added: **Nucleus Operations** (NUC, id 33);
  * deactivated (kept, not deleted): **Estates, Progress and Quality, Project**.
  * Department screens show and edit the division.
* **Sites** have a new *Site type* field. GREL's 14 sites are pre-loaded (Head Office updated; ids 2–14 added).
* **Recruitment requests** store `DivisionId`. **Vacancies** store `DivisionId` and `RequestingHodUserId`, copied from the request. Details pages show the division and the requesting HOD.
* **Filters:** Division and Period (from / to) added to the request and vacancy lists, next to the existing Department and Status filters, with a Division column. The dashboard has a Division / Department filter (`DashboardFilter`).
* Migration `DivisionsAndSites` and script `database/05_divisions_and_sites.sql`. The migration fills the new columns of existing requests and vacancies from their department and request before switching the foreign keys on.
* **Demo data:** the demo HOD moved from Estates to **Factory APM**; demo login `hod.estates@demo.grel` → **`hod.apm@demo.grel`**; request numbers `RR-EST-…` → `RR-APM-…`. The demo's two made-up sites were removed (GREL's real sites are used). **An existing demo database must be deleted** (`src/Grsms.Web/App_Data/grsms-demo.db`), because demo mode builds its tables once and does not apply migrations.
* Tests: the Estates-based tests now use Factory APM (id 31); 4 new tests (seed structure, department needs an active division, division can't be deactivated while in use, request and vacancy division links and filters).

**Checked**
* Build clean; **53/53 tests passed**.
* Docker MySQL: migration applied; 9 active Corporate and 8 active Operations departments; 14 sites with their types.
* Signed in to the Docker app as the administrator: the Home (with and without filters), Divisions, Departments, Sites, Recruitment requests and Vacancies pages all load, and the Department filter is grouped by division.
* Not checked in a browser screenshot: the layout of the new dashboard filter bar on a phone.

**Open:** see [Open points to confirm with GREL](12-divisions-and-departments.md#open-points-to-confirm-with-grel) (Apimenim spelling, Tsibu vs TBU Factory, unknown site types, the three deactivated departments). The HOD's part in shortlisting, interviews and the hiring recommendation is recorded as a rule for Phases 6–8 in the [phase log](phases.md#later-phases); those modules don't exist yet.

---

## 4 November 2026: Docker, home dashboard, database viewer, GREL departments

Four pieces of work, done in this order. None of it is committed to git yet.

### 1. Code check and Docker set-up

**Why:** to run GRSMS against a real MySQL server without installing MySQL or .NET on the PC.

**Code check:** the solution built cleanly and all 49 automated tests passed before any change. The review covered start-up (`Program.cs`), configuration, database initialisation and the project files: what was needed to containerise the app. It was not a line-by-line review of every module.

**Added**
* `Dockerfile`: multi-stage build (.NET 10 SDK → ASP.NET 10 runtime), non-root user, port 8080, writable `App_Data` and `logs` folders.
* `docker-compose.yml`: `db` (MySQL 8.4 with `utf8mb4_0900_ai_ci` and a health check) and `web` (waits for a healthy database). Data kept in the `mysql-data`, `app-data` and `app-logs` volumes.
* `.dockerignore`.
* `.env` (local only, git-ignored): generated MySQL and first-administrator passwords.

**Decisions**
* The container runs in **`Development`**. In `Production` the sign-in cookie requires HTTPS, so sign-in fails over the container's plain HTTP. `Development` also applies migrations automatically.
* App on host port **8080**, not 5014. A `dotnet run` copy of the app was already listening on `localhost:5014`, and requests to `localhost` were reaching *that* copy instead of the container (diagnosed when sign-in failed and the container logged no requests).
* MySQL exposed on host port **3307** to avoid clashing with a locally installed MySQL.

**Checked:** all three EF migrations applied to MySQL 8.4; the first HR Administrator was created; signing in and opening the Home and Departments pages worked through the container. This is the **first time GRSMS has run on a real MySQL server** (see the Phase 1 "known limits" in the [phase log](phases.md)).

**Docs:** [10 Running with Docker](10-docker.md).

### 2. New home dashboard and visual update

**Why:** the Home page was redesigned to follow a reference dashboard layout supplied by GREL: KPI cards with weekly bar charts, a task list, an interview-style calendar, progress bars and a latest-applicants list.

**Mapping to GRSMS data.** Tasks, interviews and applicants do not exist until later phases, so each panel uses the nearest real data:

| Reference panel | GRSMS panel |
|---|---|
| Total candidates / interviewed / active jobs / offers accepted | Recruitment requests / Approved requests / Open vacancies / Registered candidates, each with 7-day bars and the change vs the previous 7 days |
| Upcoming Task | **My tasks**: items waiting for the signed-in user |
| Upcoming Interview (calendar) | **Vacancy closing dates**, with month navigation |
| Compliance (progress bars) | **Requests in approval** (25 / 50 / 75%) |
| Latest Applicant | **Latest candidates** (HR roles only) |

**Visual update (all pages):** flat grey frame, light sidebar with section headings and count badges, white rounded content panel, white hairline-bordered cards (the `.glass` class kept its name, so every existing page picked up the new look), a search box and a notification bell in the top bar. Light and dark mode both updated. The brand stays **GREL green**. The reference used purple, which is a one-token change (`--grsms-brand`).

**Added**
* `src/Grsms.Application/Dashboard/DashboardService.cs`, `DashboardDtos.cs`
* `src/Grsms.Web/Helpers/NavCounts.cs` (sidebar badges and bell, worked out once per page)
* `docs/11-home-dashboard.md`

**Changed**
* `HomeController.cs`, `Models/HomeViewModel.cs`, `Views/Home/Index.cshtml` (rewritten)
* `Views/Shared/_Layout.cshtml` (search box, bell, icon buttons), `Views/Shared/_Sidebar.cshtml` (sections, counts, footer)
* `wwwroot/css/site.css`: sections 1–4 rewritten, section 8 (dashboard) added, phone rules extended. The previous version was backed up during the work, and `git diff` shows the change.
* `IUserDirectory` / `UserDirectory`: three candidate queries
* `Application/DependencyInjection.cs`, `Program.cs`: service registrations

**Checked**
* Build clean; 49/49 tests passed.
* Screenshots taken in headless Chrome against demo data: HR Manager (all panels), Recruitment Officer (dark mode), Employee (minimal page), a 390 px phone, and the Recruitment requests list (to confirm the restyle on other pages).
* Fixed from those screenshots: the phone task table overflowed (two columns now hidden on small screens); phone KPI cards were one per row with tall empty charts (now two per row, shorter); a lone KPI card stretched full width (grid now keeps a normal card width); 0% change showed a green "up" arrow (now neutral grey); the bell's red dot was too small.

**Open / not done**
* **No automated tests target the dashboard itself.** The existing web tests load `/` as staff and pass, which proves the page renders, but the numbers are not asserted. Worth adding `DashboardService` tests (visibility per role, week-on-week maths, task list per role).
* About 15 small queries per Home page load, plus 2–4 on every other page for the sidebar counts. Fine at GREL's volumes; see the performance note in [11 Home dashboard](11-home-dashboard.md#for-developers).

### 3. Database viewer (phpMyAdmin)

**Why:** to view the database in a web browser.

**Added** a `phpmyadmin` service (`phpmyadmin:5.2`) to `docker-compose.yml` at **http://localhost:8081**, bound to `127.0.0.1` so only this PC can open it. Sign in as `grsms` / `MYSQL_PASSWORD` from `.env`.

**Checked:** signing in as `grsms` showed the `grsms` database.

**Caution recorded:** the `grsms` account can change data, and edits made in phpMyAdmin bypass the app's soft delete and audit trail. See [10 Running with Docker → Viewing the database](10-docker.md#viewing-the-database-in-a-browser).

### 4. GREL's real department list

**Why:** the 24 seeded departments were placeholders from the specification. GREL supplied its real list (a screenshot of the department drop-down from another GREL system), confirmed as complete, with the instruction to **replace** the placeholders and deactivate the extra ones rather than delete them.

**Result:** 18 active departments.

| | |
|---|---|
| Unchanged | Finance, Administration, Human Resources, Corporate Affairs, Information Technology, Supply Chain, Internal Audit, Procurement |
| Renamed | Quality Assurance → **Progress and Quality** (code QA → **PQ**); Projects → **Project** |
| Added (ids 25–32) | Civil & Infrastructure (CIV), Stores (STR), Garage (GAR), Estates (EST), RUTAP, DTC, APM Factory (APM), TBU Factory (TBU) |
| Deactivated (kept) | Sales and Marketing, Production, Engineering, Maintenance, HSE, Research & Development, Legal & Compliance, Security, Transport & Logistics, Warehouse, Medical Services, Community Relations, Sustainability, Business Development (14) |

**Decisions**
* Ids never change: the placeholders stay as inactive rows (an HR Administrator can reactivate any of them), and new departments start at id 25.
* Codes for the new departments were chosen here (CIV, STR, GAR, EST, RUTAP, DTC, APM, TBU). They appear in request numbers (`RR-EST-2026-0001`). **Confirm them with GREL** and change any in the Departments screen before real requests are raised.
* "RUTAP" and "DTC" are kept as GREL writes them. Their full names can be added as the department description.

**Changed**
* `Persistence/Configurations/OrganizationConfigurations.cs`: the seed list, now with an active flag per row
* New migration `Persistence/Migrations/20261104095611_GrelDepartments.cs`, which updates and inserts department rows only
* New script `database/04_grel_departments.sql` for DBA-managed databases
* `DemoDataSeeder.cs`: demo HOD moved from HSE to **Estates**; demo login `hod.hse@demo.grel` → **`hod.estates@demo.grel`**; demo request numbers `RR-HSE-…` → `RR-EST-…`. The "already seeded" check now looks for `recruiter@demo.grel`, which exists in every version, so an old demo database is not seeded twice.
* Tests: the HSE department (now inactive) replaced by Estates (id 28) in the request and vacancy tests; seed and drop-down counts updated (32 rows, 18 active)
* Docs: README, 01, 02, 03, 07, 08 and the phase log

**Checked:** 49/49 tests passed; in the Docker MySQL database exactly the 18 GREL departments are active, 14 are inactive, and the migration is recorded in `__EFMigrationsHistory`.

**Follow-ups**
* The 8 new departments have **no Head of Department** yet. Set one per department before it can raise recruitment requests.
* An existing local demo database (`src/Grsms.Web/App_Data/grsms-demo.db`) still has the old list, because demo mode builds the file once and never migrates it. Delete it to get the new list.
