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

## The employee directory (17 November 2026)

**Menu → Employees** shows **cards** by default; **List** switches to a table, and **Grid** switches back.

**Each card shows:**
* the **photo**, or the person's initials on a colour;
* name, staff number, status (*On probation*, *Active*, *Left GREL*) and, when not permanent, the employment type;
* position, department, grade, site and **manager**;
* the **profile completion** (e.g. *71% complete*) with a bar: green from 85%, amber from 60%, red below.

Hover over the bar to see what is missing. **View profile** opens the profile. The **⋯** menu offers:
* Edit employee (HR);
* Documents, Leave, Performance and Training (the profile tabs);
* Employee report.

The tabs (current staff, on probation, active, left), the search and the department, site and grade filters work in both views.

## The employee profile

**The header shows:**
* the large photo;
* name and status;
* employee ID, position, department and site;
* date joined, years of service, *reporting to* (the line manager, or the Head of Department when none is set);
* email and phone (both clickable).

**Profile completion** (right of the header) shows the percentage, what is missing and **Complete profile**, which opens the first place to fix it. It counts seven parts:

| Part | Complete when |
|---|---|
| Personal information | Date of birth, address, SSNIT number and mobile given, and the declaration signed (the onboarding's personal record) |
| Emergency contact | At least one |
| Bank information | At least one account |
| Education | Highest qualification given |
| Documents | Every required onboarding document verified; for staff hired before GRSMS, at least one document in the personnel file |
| Employment details | A grade and a line manager (or a Head of Department) |
| Photo | Uploaded, or the onboarding passport photograph verified |

Staff hired before GRSMS have no personal record yet, so the first four parts show *Not in GRSMS yet*. Employee self-service (docs/30, Phase 29) will collect them.

**Pending actions:**
* what is missing (parts with the same reason make one line);
* a contract that has ended or ends within 30 days (red first), or within 90 days;
* a probation that has ended without a decision, ends within 30 days, or whose initial meeting is overdue;
* overdue onboarding tasks.

Each has a **Fix** link where one exists.

**Photo:** *Add photo* or *Change photo* under the picture (HR only, JPEG or PNG, up to 3 MB); *Remove photo* goes back to initials. Earlier photos are kept as history.
* Without an uploaded photo, the passport-size photograph from onboarding is used, once verified.
* Photos are shown to whoever may open the employee's record, and to the employee.

**Tabs** (the page remembers the last one):

| Tab | Shows |
|---|---|
| Personal | Date of birth and age, address, home town, mobile, email, SSNIT, qualification, next of kin, emergency contacts, bank account (only the last 4 digits). HR only (onboarding.view). Nationality is not recorded in GRSMS. Marital status shows *Married* when a spouse is given, otherwise *Not recorded*. |
| Employment | Employee number, position, department, site, grade, manager, date joined, contract, confirmation, sign-in; links to the onboarding, personnel file and probation; the job history |
| Documents | The new-hire documents with their status and **Preview** (PDF, pictures, Word, inside the page) and Download; the personnel file for the rest. HR only. |
| Onboarding | The 9-stage onboarding journey (docs/39) |
| Performance | The probation (period, status, link to its objectives and assessments). Annual appraisals come with Phase 32. |
| Training | The induction's progress. Training records come with Phase 29. |
| Leave | Leave comes with Phase 30. |
| Organisation | Manager → this employee → direct reports, with photos, and a link to the department's organisation chart |
| Timeline | The whole story, newest first: applications, onboarding, probation and job changes |
| Audit trail | The record's changes (who, when, which fields) and a link to the full trail. For people who may read the audit trail. |

**Employee report:** a one-page summary to print or save as PDF, with the letterhead, photo, employment, personal summary (HR only), job history and what is outstanding.

**Line manager.** Taken from the vacancy's reporting manager at hire. When it is empty, the Head of Department acts as line manager, and the page says so. Leave and performance (later phases) will use it.

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

**Administration → Grades.** Add GREL's grades lowest first: a code (e.g. `G5`), a name (*Senior Officer*) and the order. Grades carry no pay band (docs/37 CR1). A grade can be deactivated: employees who have it keep it, but it can no longer be given. The list shows how many current employees hold each grade (click to see them).

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
