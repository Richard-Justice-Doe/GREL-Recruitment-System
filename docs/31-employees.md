# 31. Employees: user guide (Phase 28)

Since 11 November 2026 GRSMS keeps an **employee record** for every GREL employee: the master record that leave, movements, performance, payroll and separation will build on (see [30 HR platform impact analysis](30-hr-platform-impact-analysis.md)). Nothing in recruitment, onboarding or probation changed; the record starts where they end.

**Menu → People → Employees.** Grades are under **Administration → People → Grades**.

## Who can do what

| | HR Manager, HR Administrator | Recruitment Officer, MD, Admin | Head of Department | Employee |
|---|:-:|:-:|:-:|:-:|
| See the employee list and records | all | all | own departments | |
| Register existing staff (form or CSV) | ✅ | | | |
| Update job details | ✅ (never their own) | | | |
| Maintain grades | ✅ | | | |

The permissions are `employees.view`, `employees.manage` and `grades.manage` (Roles & permissions, group *Employees*). They are given to these roles once, at the first start-up of this version; the Super Admin can change them.

## Where employee records come from

| How | When | Status |
|---|---|---|
| **Hired through GRSMS** | Automatically, when the Recruitment Officer or HR Manager **activates** the onboarding | *On probation*; becomes **Active** when the HR Manager confirms the probation |
| **Activated before 11 November 2026** | Automatically, at the first start-up of this version (once) | *On probation*, or *Active* if the probation was already confirmed |
| **Hired before GRSMS** | HR registers them: **Register existing employee**, or **Import from CSV** | As entered: *Active* (default) or *On probation* |

The record holds the **staff number**, **position**, **department**, **place of work (site)**, **grade**, **line manager**, **employment type** (and contract end date), the **date joined**, the **confirmation date** and the **status**. The name, email and phone come from the person's GRSMS account and their personal details from the Employee Personal Record: they are not copied.

## The employee page

* **Current job**, with a warning when a contract ends within 60 days or has ended.
* **Job history**: one line per job, newest first, with why it changed and who recorded it. Lines are added, never changed.
* **Employment**: date joined, length of service, confirmation, contact details and whether their account can sign in.
* **Records**: links to the onboarding, the personnel file and the probation. Staff registered by HR show *Hired before GRSMS*.

**Line manager.** Taken from the vacancy's reporting manager at hire. When it is empty, the Head of Department acts as line manager; the page says so. Leave and performance (later phases) will use it.

## Registering existing staff

### One at a time
**Employees → Register existing employee.** Fill in the person (name, email, phone, staff number, date joined, status) and the job (position, department, site, grade, line manager, employment type, contract end date).

* If a **staff account** with that email already exists (for example a Head of Department), it is used and given the staff number, department and site.
* Otherwise a new account is created with the **Employee** role and **no password**: the person sets one with **Forgot password** on the sign-in page.
* A **candidate** account cannot be registered: candidates become employees through onboarding.
* The staff number must be new, and the date joined cannot be in the future (new hires come through onboarding).

### Many at once (CSV)
**Employees → Import from CSV.**
1. **Download the template** and fill in one employee per line in Excel. The page lists the columns; the required ones are StaffNumber, FullName, Email, Position, Department, Site and HireDate.
2. **Save as → CSV UTF-8 (comma delimited)** and **Import**.

Every row is checked first. **If any row is wrong, nothing is imported** and the problems are listed by row number (as in Excel), for example *Row 3 (Yaw Mensah): no active department 'Finanse'*. Fix the file and import it again.

* Department, site and grade: their name or code, as in GRSMS.
* Dates: `2019-03-01` or `01/03/2019`.
* EmploymentType: Permanent (default), Contract, Temporary, Casual, Internship, National Service. Status: Active (default) or On probation.
* ReportsToEmail: the line manager's email: someone already in GRSMS or another person in the same file.
* At most 2,000 employees and 2 MB per file.

## Updating job details

**Employee page → Update job details** (HR). Change the position, department, site, grade, line manager, employment type or contract end date, give the **date it takes effect** (today or earlier, not before the current job line) and **why**. Saving closes the current job history line the day before and adds a new one.

* A change of **department or site** also moves the person's account, so what a Head of Department sees follows the employee. Recruitment requests, vacancies, onboardings and probations keep their own department.
* You cannot change your own job details; another member of HR must do it.
* If someone else saved the same employee a moment earlier, you are asked to reload the page.
* Promotions, transfers, acting appointments and contract renewals with approvals come with the **Movements** module (Phase 31). Until then HR records the outcome here.

## Grades

**Administration → Grades.** Add GREL's grades lowest first: a code (e.g. `G5`), a name (*Senior Officer*), the order and, for reference, a monthly salary range. A grade can be deactivated: employees who have it keep it, but it can no longer be given. The list shows how many current employees hold each grade (click to see them).

## The personnel file

Since Phase 28 the personnel file also receives, in *Recruitment Documents*:
* **Offer of employment** (the accepted offer, frozen as accepted), and
* **Interview results**: one per validated interview round: the panel's scores and recommendations and who validated them.

They are filed at activation, and for employees activated earlier at the first start-up of this version.

## For developers

| Piece | File | Notes |
|---|---|---|
| Entities | `src/Grsms.Domain/Employees/Employee.cs` | `Employee` (one per user; current job copied from the open assignment), `EmployeeAssignment` (history; closed, never edited), `Grade`, `EmployeeStatus`, `AssignmentSource` |
| Tables | `Persistence/Configurations/EmployeeConfigurations.cs`, migration `EmployeeMasterRecord`, `database/28_phase28_employee_master_record.sql` | New tables only: Employees, EmployeeAssignments, Grades |
| Hooks | `Application/Employees/EmployeeLifecycle.cs` | `OnActivatedAsync` (last line of `OnboardingService.ActivateAsync`), `OnConfirmedAsync` (last line of the probation decision), `BackfillAsync` (start-up, `DbInitializer`). They never throw: a failure is logged and the next start-up's backfill catches up |
| Rules | `Application/Employees/EmployeeService.cs`, `GradeService.cs` | Visibility through `AccessScope`; accounts through `IEmployeeAccounts` (`RegisterStaffAsync`, `UpdatePlacementAsync`, `FindByEmailAsync`, `GetAsync`) |
| Pages | `Web/Controllers/EmployeesController.cs`, `GradesController.cs`, `Views/Employees`, `Views/Grades` | Menu group *People* in `_Sidebar.cshtml`; Grades on the Administration page |
| Tests | `tests/Grsms.Tests/EmployeeServiceTests.cs`, `EmployeeWebTests.cs` | 12 tests; the 197 earlier tests run unchanged |

**Not in Phase 28** (see docs/30): a personal record for staff registered directly (needs `EmployeePersonalRecords.OnboardingRecordId` nullable: Phase 29, *My HR*), a personnel file for them (same), and approvals for job changes (Phase 31, Movements).
