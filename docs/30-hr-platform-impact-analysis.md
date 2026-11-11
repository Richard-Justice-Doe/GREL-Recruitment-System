# 30. GRSMS Phase 2, the HR platform: impact analysis

**Status:** analysis (11 November 2026). No code has been changed. Questions 1, 2, 3 and 11 were decided on 11 November 2026 (section 13). Questions 4 to 10 have suggested answers that still need confirming. Building starts with Phase 28 once it is approved.

**Goal:** extend GRSMS from recruitment and selection into the whole employee lifecycle (employee record, movements, leave, performance, payroll, self-service, separation). It is **additive only**: the recruitment modules, their URLs, permissions, approval chains, tables and tests stay as they are.

---

## 1. Summary

* **Five of the ten requested modules already exist, fully or largely:** onboarding, document verification, the personal record (next of kin, emergency contacts, beneficiaries, bank), departmental induction, the personnel file, and probation with confirmation. They are kept unchanged, and the small gaps are closed by adding to them (section 4).
* **New:** the employee master record (with grades and job history), employee movements, leave, performance, payroll, employee self-service and separation.
* **Database:** about 30 **new tables** and no renamed, dropped or retyped columns. **One** optional widening of an existing column: `EmployeePersonalRecords.OnboardingRecordId` becomes nullable, and only if staff hired before GRSMS are brought in (question 1).
* **Existing code is touched in three places only.** Each is one added call after the existing work has been saved: at activation, at confirmation, and the personnel file's list of categories. Everything else lives in new folders, new controllers and new URLs.
* **Suggested order:** employee record → self-service → leave → movements → performance → separation → payroll (last, because it carries the most risk). That is seven phases, 28 to 34, each with its own migration, SQL script, tests, guide and commit.

---

## 2. The current flow (as built)

```
Recruitment request (QF-81-11)  HOD/staff raise → HOD → HR Manager → MD
   ↓ approved
Vacancy                         RO drafts → HR Manager approves → published
   ↓
Job advert                      RO → HR Manager review → IT Manager approves and publishes
   ↓
Candidate portal / applications → ATS screening → shortlisting (HOD's view) → interviews (panel scores, RO validates)
   ↓
Hiring recommendation           RO selects and approves
   ↓
Offer                           sent → accepted / declined / expired (background job)
   ↓ accepted, RO clicks "Click to proceed to onboarding"
Onboarding (ONB-…)              letter, new-hire documents (candidate uploads, RO verifies), personal record declaration;
                                stages worked out automatically
   ↓ Activate (RO / HR Manager, staff number from payroll)
   ├─ the account becomes a staff account (Employee role, StaffNumber, DepartmentId, SiteId)
   ├─ personnel file (PF-…) opened and filled
   ├─ probation (PRB-…) started
   └─ departmental induction tasks given to their owners
   ↓ induction closed
Probation                       initial meeting, objectives, development plan → 3-month assessment → final assessment → HR decision
   ↓
Confirmed (confirmation letter filed) / extended / not confirmed
```

**This is where the system ends today.** After confirmation nothing more happens to the employee.

### Where "the employee" lives today

| Fact | Stored in |
|---|---|
| Name, email, phone, sign-in, active or not | `Users` (ApplicationUser) |
| Staff number | `Users.StaffNumber` and `OnboardingRecords.EmployeeNumber` |
| Department and site (also used for what an HOD may see) | `Users.DepartmentId`, `Users.SiteId` |
| Job title, monthly salary, start date | `OnboardingRecords` (copied from the accepted offer) |
| Reporting manager | `Vacancies.ReportingManagerUserId` (the vacancy, not the person) |
| Personal details, SSNIT, next of kin, spouse, children, dependants, emergency contacts, beneficiaries, languages, bank | `EmployeePersonalRecords` and its child tables (one per onboarding) |
| Onboarding documents | `EmployeeDocuments` → copied into the personnel file |
| Personnel file | `PersonnelFiles`, `PersonnelFileDocuments` (seven categories) |
| Probation and confirmation | `ProbationRecords` and its children, `ConfirmationRecords` |

**Gaps:**
* There is no single employee row.
* There is no grade.
* There is no history of job changes: the current job title only exists on the onboarding.
* Staff hired before GRSMS do not exist, except as plain user accounts.

---

## 3. The extended flow

```
… Offer accepted → Onboarding → Activation ──► Employee record created (EMP status: On probation)   ◄─ NEW, at the existing activation
                                    │
                                    ├─ Induction (exists) ─► Probation (exists) ─► Confirmed ──► Employee status: Active   ◄─ NEW, at the existing confirmation
                                    │
                                    ▼
                      ┌──────────────── Active employment (all NEW) ───────────────────────┐
                      │ Employee self-service: profile, change requests, leave, payslips,  │
                      │                        reviews, training                           │
                      │ Movements: transfer, promotion, acting, contract renewal           │
                      │ Leave: apply → line manager → HR; balances by year                 │
                      │ Performance: goals → mid-year → annual → rating                    │
                      │ Payroll: monthly run → approval → payslips, payroll register       │
                      └────────────────────────────────────────────────────────────────────┘
                                    ▼
                      Separation: resignation, retirement, termination, dismissal, death in service
                        → clearance → last working day (account disabled, employee Separated)
                        → record and personnel file kept for good
```

* **Staff hired before GRSMS** (question 1) would enter at "Employee record created" through **Register existing employee** or a CSV import, with no onboarding.
* An employee whose probation is **not confirmed** goes to a separation of type *Termination (probation not confirmed)*. Probation itself stays as it is.

---

## 4. Module by module: what exists, what is added

| # | Module (from the brief) | Already in GRSMS | To add |
|---|---|---|---|
| 1 | **Onboarding**: documents, HR verification, declaration, beneficiaries, next of kin, emergency contacts, department tasks | ✅ All of it (Phases 9, 18, 19, 20, 27) | Nothing from the brief. *Phase 27 stages B and C are still open; they are separate work.* |
| 2 | **Employee master record** | ❌ (facts spread over several tables, section 2) | New `Employees` table with grades and job history; created at activation; one-off backfill of everyone already activated |
| 3 | **Personnel file** | ✅ The file, seven categories, automatic filing of: application documents and CV, onboarding documents, signed declaration, appointment letter, induction record, probation records, confirmation letter | Also file the **offer letter** and the **interview result summary** when the file opens. New categories for movements, leave, performance and separation, so each new module files its letters and decisions there |
| 4 | **Probation**: initial meeting, objectives, development plan, 3-month review, 6-month review, confirmation | ✅ Initial meeting, objectives, development plan, *First assessment (3 months)*, *Final assessment* (at the end of the probation, which is 6 months by default), extension, HR decision, confirmation letter | Nothing in the workflow. The brief's "6-month review" is today's *Final assessment* (question 10). Confirmation sets the employee to *Active* |
| 5 | **Employee movements**: transfer, promotion, acting appointment, contract renewal | ❌ | New module; job history is never overwritten (section 5) |
| 6 | **Leave**: annual, casual, sick, maternity, paternity, compassionate, study, unpaid | ❌ | New module |
| 7 | **Performance**: goals, KPIs, mid-year, annual, ratings, development plans | ❌ (probation has objectives and ratings, which are reused as the pattern but not shared) | New module |
| 8 | **Payroll**: salary structure, allowances, deductions → payslips, payroll register | ❌ (only the offer's monthly salary) | New, separate module |
| 9 | **Self-service**: profile, update details, leave, payslips, reviews, training | Partly: *Profile* (password, contact) and *My probation* | New **My HR** area; changes to personal details go to HR for approval |
| 10 | **Separation**: resignation, retirement, termination, dismissal, death in service | Partly: an administrator can deactivate an account; onboarding can be cancelled | New module with clearance and full history |

---

## 5. Required entities (all new tables)

Information already stored is **referenced, not copied**:
* Name, email and phone come from `Users`.
* Personal details come from `EmployeePersonalRecords`.
* The link to recruitment is `OnboardingRecordId` → onboarding → application, offer and vacancy.

The only kept copies are "current" values on `Employees`, for quick lists. They are always written from the latest `EmployeeAssignments` row.

### 5.1 Employee master record (Phase 28)

| Table | Main columns | Notes |
|---|---|---|
| **Employees** | Id, EmployeeNumber (unique; the staff number), UserId (unique → Users), OnboardingRecordId (null for staff registered directly), current PositionTitle, DivisionId, DepartmentId, UnitId?, SiteId, GradeId, ReportsToUserId, EmploymentType (the existing enum), HireDate, ConfirmationDate, ContractEndDate, Status (On probation, Active, On notice, Separated), SeparatedAt, Version | One per person. Created by activation, by *Register existing employee*, or by the backfill |
| **EmployeeAssignments** | EmployeeId, EffectiveFrom, EffectiveTo (null = current), PositionTitle, DepartmentId, SiteId, GradeId, ReportsToUserId, EmploymentType, Source (Hire, Movement, Registration), SourceId | The job history. A change **adds** a row and closes the previous one; nothing is overwritten |
| **Grades** | Code, Name, SortOrder, MinSalary, MaxSalary, IsActive | GREL's grade list (question 6) |

### 5.2 Employee movements (Phase 31)

| Table | Main columns |
|---|---|
| **EmployeeMovements** | Number (MOV-2026-0001), EmployeeId, Type (Transfer, Promotion, Acting appointment, Contract renewal), the proposed position, department, site, grade, reporting manager, salary and contract end date, EffectiveDate, EndDate (acting appointment), Reason, Status (Draft → awaiting approvals → Approved → Effected / Rejected / Cancelled), LetterContent, Version |
| **EmployeeMovementActions** | The history line per step (same pattern as the other modules) |

Approved and effective → a new `EmployeeAssignments` row, a new salary row (payroll), and the letter filed in the personnel file. An acting appointment ends by itself on its end date (background job).

### 5.3 Leave (Phase 30)

| Table | Main columns |
|---|---|
| **LeaveTypes** | Code, Name, DefaultDays, Paid, CountsWorkingDaysOnly, AppliesTo (all, female, male), NeedsDocument (e.g. sick note), MaxCarryForwardDays, IsActive. The eight types are seeded |
| **LeaveEntitlementRules** | LeaveTypeId, GradeId?, MinYearsOfService?, Days: entitlement by grade or service, if GREL uses that (question 7) |
| **LeaveBalances** | EmployeeId, LeaveTypeId, Year, Entitled, CarriedForward, Adjusted (with reason); taken and booked days are calculated from the requests |
| **LeaveRequests** | Number (LV-2026-0001), EmployeeId, LeaveTypeId, StartDate, EndDate, Days, Reason, ReliefOfficerUserId, AttachmentId, Status (Submitted → Line manager → HR → Approved / Rejected / Cancelled / Recalled), Version |
| **LeaveRequestActions** | History |
| **PublicHolidays** | Date, Name (kept by HR; used to count working days) |

### 5.4 Performance (Phase 32)

| Table | Main columns |
|---|---|
| **PerformanceCycles** | Year, Name, goal-setting / mid-year / annual windows (dates), Status (Planned, Goal setting, Mid-year, Annual, Closed) |
| **PerformanceAppraisals** | Number, CycleId, EmployeeId, ReviewerUserId, Status, mid-year comments (employee, reviewer), annual self-assessment, reviewer rating, HR-moderated final rating, employee acknowledgement, Version |
| **PerformanceGoals** | AppraisalId, Title, KPI / measure, Target, Weight %, mid-year progress, achieved, rating |
| **PerformanceDevelopmentActions** | AppraisalId, Need, Action, Owner, TargetDate, Status |
| **PerformanceAppraisalActions** | History |

### 5.5 Payroll (Phase 34)

| Table | Main columns |
|---|---|
| **PayComponents** | Code, Name, Kind (Earning, Deduction, Employer contribution), Taxable, CountsForSsnit, Calculation (fixed amount, % of basic), IsActive |
| **EmployeeSalaries** | EmployeeId, EffectiveFrom, MonthlyBasic, Source (Offer, Movement, Adjustment), SourceId: the salary history; the first row comes from the accepted offer |
| **EmployeePayComponents** | EmployeeId, PayComponentId, Amount or Percent, From, To |
| **StatutoryRates** | EffectiveFrom, the PAYE bands and the SSNIT employee / employer rates, kept as data so a change in the law is a data change, not a code change |
| **PayrollRuns** | Period (2026-11), Status (Draft → Calculated → Approved → Closed), prepared / approved by and when |
| **Payslips**, **PayslipLines** | Frozen when the run is approved: gross, each earning and deduction, PAYE, SSNIT, net. Never recalculated afterwards |

### 5.6 Self-service (Phase 29)

| Table | Main columns |
|---|---|
| **ProfileChangeRequests** | Number, EmployeeId, Section (contact, address, next of kin, emergency contacts, beneficiaries, bank, dependants), the old and new values (stored as JSON), supporting attachment, Status (Submitted → Approved / Rejected), reviewer, comment |
| **TrainingRecords** | EmployeeId, Course, Provider, StartDate, EndDate, Certificate (attachment), recorded by HR (question 9) |

### 5.7 Separation (Phase 33)

| Table | Main columns |
|---|---|
| **Separations** | Number (SEP-2026-0001), EmployeeId, Type (Resignation, Retirement, Termination, Dismissal, Death in service), NoticeDate, LastWorkingDay, Reason, exit interview notes, Status (Raised → Approved → Clearance → Completed / Withdrawn), Version |
| **SeparationClearanceItems** | SeparationId, Area (IT equipment and accounts, Finance, Stores, Department, HR), Owner, Done by and when, Note |
| **SeparationActions** | History |

---

## 6. Database impact

| Area | Impact |
|---|---|
| **Existing tables: schema** | None, except one optional change: `EmployeePersonalRecords.OnboardingRecordId` from required to **nullable**, only if existing staff are registered (question 1). It is a widening, so every existing row stays valid and every existing query works as before. |
| **Existing tables: data written by new code** | Three places, all the same kind of write that already happens today: (1) a transfer that takes effect updates `Users.DepartmentId` and `SiteId`, as activation does; (2) a completed separation sets `Users.IsActive = false`, as an administrator's deactivation does; (3) new rows in `PersonnelFileDocuments`, as the existing modules already add. Recruitment records keep their own `DepartmentId` and **do not move** with the employee. |
| **Seed data** | Leave types, pay components, new personnel file categories with **new ids 8 to 11** (ids 1 to 7 untouched), new permissions (section 9). |
| **Backfill** | One migration step creates an `Employees` row and a first assignment for every onboarding already activated (stages *Induction in progress* and *Completed*), with status *Active* if the probation is confirmed, otherwise *On probation*. It is written so it can run twice safely. |
| **Migrations** | EF Core migrations as now, plus a SQL script per phase: `database/28_…` onward. Each `Down` drops only the new tables. |
| **Sensitive data** | Salaries, payslips and bank details: the payroll tables are kept out of the general audit-log export, or masked in it (risk 6). |

---

## 7. Services (Application layer, new folders)

| Folder | Services |
|---|---|
| `Employees/` | `EmployeeService` (list, details, register existing, import CSV), `EmployeeLifecycle` (the hooks: `OnActivatedAsync`, `OnConfirmedAsync`), `GradeService` |
| `Movements/` | `MovementService` (raise, approve, effect, letter) |
| `Leave/` | `LeaveService` (apply, approve, cancel, recall, balances), `LeaveSetupService` (types, rules, public holidays) |
| `Performance/` | `PerformanceCycleService`, `AppraisalService` |
| `Payroll/` | `PayrollSetupService` (components, statutory rates, employee pay), `PayrollRunService` (calculate, approve, close), `PayslipService` (payslip PDF with the existing Chromium PDF service, payroll register export) |
| `SelfService/` | `MyHrService` (my profile, my leave, my payslips, my reviews, my training), `ProfileChangeService` (raise, HR approve, apply) |
| `Separation/` | `SeparationService` (raise, approve, clearance, complete) |
| `Training/` | `TrainingRecordService` |

Each new service uses the existing `AccessScope`, `WorkflowMailer`, `PersonnelFileService.FileLetterAsync`, `Result`, the email template catalogue and the audit log, so the rules work as they do elsewhere.

---

## 8. Controllers and URLs (all new; no existing URL changes)

| Controller | URL | Who |
|---|---|---|
| `EmployeesController` | `/Employees`, `/Employees/Details/{id}` (profile, job history, links to the personnel file, probation, leave, appraisals), `/Employees/Register`, `/Employees/Import` | HR |
| `GradesController` | `/Grades` (on the Administration page) | HR Manager, Super Admin |
| `MovementsController` | `/Movements` | HOD raises, HR / MD approve |
| `LeaveController` | `/Leave` (requests, "awaiting me", calendar, balances) | line managers, HR |
| `LeaveSetupController` | `/LeaveSetup` (types, rules, public holidays) | HR Manager |
| `PerformanceController` | `/Performance` (cycles, appraisals, "awaiting me") | reviewers, HR |
| `PayrollController` | `/Payroll` (runs, register), `/PayrollSetup` | payroll role (question 2) |
| `SeparationsController` | `/Separations` | HOD / HR raise, HR Manager / MD approve, clearance owners |
| `MyHrController` | `/MyHr`, `/MyHr/Profile`, `/MyHr/Leave`, `/MyHr/Payslips`, `/MyHr/Reviews`, `/MyHr/Training` | every employee |
| `ProfileChangesController` | `/ProfileChanges` | HR |

`/Onboarding`, `/PersonnelFiles`, `/Probation`, `/MyProbation`, `/MyApplications` and `/Profile` stay as they are. The menu gets one new section, **People**, for staff, and one item, **My HR**, for employees. Both are hidden from anyone without the permissions, in line with the usability decisions in doc 28.

---

## 9. Permissions

New groups in the catalogue. They are granted once at start-up to their default roles, as with the advert permissions, so permissions the Super Admin has already changed are left alone.

| Group | Permissions | Default roles |
|---|---|---|
| Employees | `employees.view`, `employees.manage`, `employees.register`, `grades.manage` | HR Manager, HR Administrator (view + manage + register); RO (view); HOD (view, own departments); MD (view) |
| Movements | `movements.raise`, `movements.approve.hr`, `movements.approve.md`, `movements.view` | HOD (raise); HR Manager (raise, approve.hr); MD (approve.md) |
| Leave | `leave.apply`, `leave.manage`, `leave.setup` | Employee (apply); HR Administrator / HR Manager (manage); HR Manager (setup). **Line-manager approval follows the reporting line**, not a permission (question 3) |
| Performance | `performance.participate`, `performance.manage` | Employee (participate); HR Manager (manage). Reviewers follow the reporting line |
| Payroll | `payroll.view`, `payroll.prepare`, `payroll.approve`, `payroll.setup` | **No existing role by default** (question 2). Salary data stays hidden from the RO, HOD and IT Manager |
| Self-service | `selfservice.use`, `profilechanges.review`, `training.manage` | Employee (use); HR Administrator / HR Manager (review, training) |
| Separation | `separations.raise`, `separations.approve`, `separations.manage` | HOD (raise); HR Manager (raise, manage); MD (approve) |

No existing permission is renamed, removed or re-granted. A possible new built-in role, **Payroll Officer**, would be added like the IT Manager (`RolesAddedLater`).

---

## 10. Notifications

Emails go through `WorkflowMailer` and the editable email templates. The in-app notification centre is used as now, and the home page to-dos get new task types the same way adverts did.

| Module | Events |
|---|---|
| Employee | Employee record created (HR); details changed by HR (employee) |
| Self-service | Change request submitted (HR); approved / rejected (employee) |
| Leave | Submitted (line manager); line manager approved (HR); approved / rejected / recalled (employee, relief officer); leave starts tomorrow (employee, line manager); waiting more than 3 days (approver) |
| Movements | Raised (next approver); approved (employee, old and new HOD, payroll); effective today (employee, IT for access); acting appointment ending in 14 days (HR, HOD) |
| Contracts | Contract ending in 60 and 30 days (HR, HOD) |
| Performance | Window opens / closes (employees, reviewers); self-assessment submitted (reviewer); rating ready to acknowledge (employee) |
| Payroll | Run ready for approval (approver); payslips available (employees) |
| Separation | Raised (approvers); approved (employee, clearance owners); clearance item overdue (owner); completed (HR, payroll, IT for account closure) |

---

## 11. Background jobs

A new worker, **`HrJobsWorker`**, runs once a day with the same settings pattern (`HrJobs:Enabled`, off in the automated tests). The existing `ScheduledJobsWorker` and its jobs (vacancies, adverts, offers, surveys) are **not touched**.

| Job | What it does |
|---|---|
| Movements take effect | Approved movements whose effective date has come: new assignment, `Users.DepartmentId` and `SiteId` updated, letter filed |
| Acting appointments end | Back to the substantive assignment on the end date |
| Contract reminders | 60 and 30 days before `ContractEndDate`, prompting a contract renewal |
| Leave year | On 1 January: new balances from the rules, carry forward up to each type's maximum |
| Leave reminders | Starting tomorrow; waiting with an approver for 3 days or more |
| Performance windows | Opens and closes the cycle windows, with reminders |
| Separation last day | On the last working day: employee *Separated*, account deactivated (question 5), personnel file closed but kept |

Payroll has **no automatic run**. People prepare, approve and close it, with a reminder near month end.

---

## 12. Risks to existing functionality, and how each is avoided

| # | Risk | Where | Safe integration |
|---|---|---|---|
| 1 | New code makes **activation or confirmation** fail | The two hooks in `OnboardingService.ActivateAsync` and `ProbationService` | The hook runs **after** the existing work is saved and the emails are sent; a failure is logged and never undoes or blocks the activation. A *Repair employee records* action (and the backfill) creates any missing rows. Existing tests stay unchanged; new tests cover the hook. |
| 2 | A **transfer moves an HOD's visibility** | `AccessScope.HodDepartmentIdsAsync` uses `Users.DepartmentId` | This is intended: after a transfer they work in the new department. Recruitment requests, vacancies and onboardings keep their own department and do not move. A test pins this. |
| 3 | **Changing personal details** alters a signed declaration | The personnel file shows the declaration from `EmployeePersonalRecords` (kind *Record*) | Before an approved change is applied, the declaration as signed is **frozen as HTML** in the personnel file, so what was signed never changes. |
| 4 | **Former employees lose access** to their payslips | Separation deactivates the account | Decide whether they keep a read-only *My HR* (question 5). |
| 5 | **Employee role** sees more than before | Menu and permissions | Only the new *My HR* item is added; nothing existing is removed or renamed. |
| 6 | **Salaries and bank details leak** through reports or the audit log | Audit log (it records changed values), reports | Payroll tables masked in the audit trail; payroll permissions given to no existing role by default; payslips visible only to the employee and payroll holders. |
| 7 | **Wrong pay** (tax or SSNIT rates, rounding) | Payroll | Rates as dated data; a parallel run against GREL's current payroll before go-live; payslips frozen after approval; payroll built last. |
| 8 | **Personal record requires an onboarding** | `EmployeePersonalRecords.OnboardingRecordId` | Make it nullable (the only schema change, a widening) only if existing staff are registered; existing rows and queries are unaffected. |
| 9 | **Duplicate employees** | Registration, import, backfill | `Employees.UserId` and `EmployeeNumber` unique; the import matches by staff number and email first. |
| 10 | **Dashboard slows down** | Home page | New widgets are small, separate queries, shown only to the roles concerned; the existing dashboard queries are unchanged. |
| 11 | **Migrations on live MySQL** | Deployment | New tables only; scripts run in order like 26 and 27; take a backup first (the backup page from Phase 16). |
| 12 | **Menu clutter** against the usability work in doc 28 | Sidebar | One *People* section for staff and one *My HR* item for employees, each shown only with the permission. |

**Guard rails for every phase:** the 197 existing tests must pass without being edited, existing URLs are checked by the web tests, and new tests go in new files.

---

## 13. Decisions needed before building

| # | Question | Suggested answer |
|---|---|---|
| 1 | Do staff hired **before GRSMS** come in (needed for leave and payroll), and how? | Yes: *Register existing employee* plus a CSV import; their personal record is filled in through *My HR* |
| 2 | **Payroll**: build it in GRSMS, or produce an export for GREL's current payroll software? Who prepares and approves it? | Decide after phases 28 to 33; if built, a new *Payroll Officer* role prepares and the HR Manager or Finance approves |
| 3 | **Approval chains**: leave, movements, separation | Leave: line manager → HR. Movements: HOD → HR Manager → MD. Separation: HOD or HR → HR Manager (MD for dismissal) |
| 4 | **Line manager**: where does it come from? | The vacancy's reporting manager at hire, then kept on the employee's assignment; the HOD when blank |
| 5 | Do **former employees** keep read-only access to their payslips and documents? | No: the account is disabled on the last day; HR can print payslips for them |
| 6 | GREL's **grade** list | Needed from HR; can be entered later on the Grades page |
| 7 | **Leave entitlements** per type (and by grade or service?), carry-forward rules | Needed from HR's leave policy; entered on the Leave setup page, not in code |
| 8 | **Performance**: cycle (calendar year?) and rating scale (1 to 5?) | Calendar year, 5-point scale |
| 9 | **Training records**: a simple HR-entered list, or a training module? | A simple list for now |
| 10 | Probation "6-month review": keep today's *Final assessment* as it is? | Yes, no change to probation |
| 11 | **Order of the phases** | 28 Employee record → 29 Self-service → 30 Leave → 31 Movements → 32 Performance → 33 Separation → 34 Payroll |

### Decided (11 November 2026)

| # | Decision |
|---|---|
| 1 | **Existing staff come in** through *Register existing employee* and a **CSV import**. They fill in their personal record in *My HR*. `EmployeePersonalRecords.OnboardingRecordId` becomes nullable (risk 8). |
| 2 | **Payroll is decided later and built last.** Phases 28 to 33 come first; the choice between full payroll in GRSMS and an export to GREL's current payroll software is made once they are in use. |
| 3 | **The suggested approval chains.** Leave: line manager → HR. Movements: HOD → HR Manager → MD. Separation: HOD or HR → HR Manager, plus the MD for a dismissal. |
| 11 | **The suggested order:** 28 Employee record → 29 Self-service → 30 Leave → 31 Movements → 32 Performance → 33 Separation → 34 Payroll. |

Phase 28 covers the employee record, grades, the backfill, registering and importing existing staff, and the two personnel-file gaps (the offer letter and the interview results).
