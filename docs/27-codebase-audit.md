# 27. Codebase audit and cleanup plan

**Status:** APPROVED and carried out on 10 November 2026 (decisions §15, results §16), except B6 (splitting `OnboardingService`), which waits for Phase 27 stages B and C.
**Date:** 10 November 2026 · **Branch:** `feature/ats-and-super-admin` (after commit 93f6385, Phase 27 stage A)

**How it was done.** Every type declared in `src` (557) and every public method in Domain, Application and Infrastructure (497) was searched for by whole word across all `.cs` and `.cshtml` files in `src` and `tests` (excluding `bin`, `obj` and migrations). Views were matched to controller actions; partials, scripts and stylesheets to the pages that load them; CSS class selectors to every view, script and C# file; DI registrations to constructor parameters; permission constants to their checks. EF: `dotnet ef migrations has-pending-model-changes` and a match of the 25 migrations against the 24 SQL scripts. The build has 0 warnings and the 188 tests pass.

**The short answer:** the codebase is in good shape. There are **no dead classes, services, views, scripts or migrations**. What there is: **6 dead methods** (5 of them left over from last week's Phase 27 change), **~11 unused CSS rules**, **~8 MB of unused vendor files** in `wwwroot/lib`, a few **files holding several controllers**, **duplicated small helpers** in 9 to 24 files, **one permission that nothing checks**, and some **N+1 queries on the onboarding page**. Nothing in the security review is urgent.

---

## 1. Solution inventory

| Project | Contents |
|---|---|
| **Grsms.Domain** (44 files) | **39 entities** deriving from `BaseEntity` (+ the Identity user in Infrastructure); **55 enums**; **8 workflow classes** (`AdvertWorkflow`, `ApplicationWorkflow`, `HiringWorkflow`, `InterviewWorkflow`, `OnboardingWorkflow`, `ProbationWorkflow`, `RecruitmentWorkflow`, `VacancyWorkflow`); catalogues (`Permissions`, `Roles`, `SettingCatalogue`, `EmailTemplateCatalogue`, `DefaultDocumentTypes`, `PersonnelFileCategories`); `CvParser` (Domain/Applications). No value objects (simple types are used throughout). |
| **Grsms.Application** (70 files) | **30 services** (largest: `OnboardingService` 952 lines, `HiringService` 767, `InterviewService` 712, `RecruitmentRequestService` 639, `VacancyService` 618); **33 interfaces** (repositories, `ICurrentUserService`, `IEmailService`, `IFileStorage`, `IVirusScanner`, `ISettingsStore`, `IUserDirectory`, …); **125 records** (DTOs) and **105 classes** (forms, filters, options); helpers in `Common` (`Result`, `EnumText`, `RichText`, `AccessScope`, `WorkflowMailer`). Validation is in the services (no FluentValidation), as the conventions say. |
| **Grsms.Infrastructure** (47 files + migrations) | **8 repositories** (generic `Repository<T>` + `Advert`, `Application`, `Interview`, `PersonalRecord`, `Probation`, `RecruitmentRequest`, `Vacancy`); **68 EF configurations** in 14 files; **52 DbSets**; `AuditSaveChangesInterceptor`; storage `LocalFileStorage`, `NoVirusScanner`; email `EmailService`; `SmsSender`, `NotificationCenter`; ATS `CvTextExtractor`, `ClaudeCvReader`; Identity (`UserAdminService`, `RoleAdminService`, `PermissionStore`, `PermissionSeeder`, `EmployeeAccounts`, `UserDirectory`); settings `SettingsStore`, `BackupStore`; `RichTextSanitizer`; 25 migrations. |
| **Grsms.Web** (163 files) | **32 controllers** in 30 files; **112 views** (27 partials, 4 layouts); 3 view-model files (`AccountViewModels`, `ErrorViewModel`, `HomeViewModel`); helpers `NavCounts`, `EnumExtensions`, `DatabaseHealthCheck`; security `Policies`, `CurrentUserService`, `AppSignInManager`, `MicrosoftSignIn`, `MustChangePasswordFilter`, `RateLimits`, `SecurityHeaders`, `AppLinks`; worker `VacancyAutoCloseWorker`; **5 scripts** (`site.js`, `theme-init.js`, `repeater.js`, `rich-text-editor.js`, `recruitment-request-form.js`), **2 stylesheets** (`site.css` 852 lines, `motion.css` 156); 8 vendor libraries. **No custom tag helpers.** |
| **tests/Grsms.Tests** | 37 files, 188 tests (service tests on SQLite in memory + web tests through `WebApplicationFactory`). |

## 2. Dependency map (summary)

* **All 557 types are reachable.** The 103 types no other file names are all found by the framework: 32 controllers (MVC discovery), 68 EF configurations (`ApplyConfigurationsFromAssembly`), `DesignTimeDbContextFactory` (EF tools), `EnumExtensions` (extension methods, called as `.DisplayName()`), `PermissionHandler` (registered in its own file), and private nested records used inside their own file (`AiJob`, `AiCv`, `PendingAudit`, `MonitorRow`, `Section`, …).
* **All 63 DI registrations are injected somewhere** (or are framework options and hosted services: `OpenIdConnectOptions`, `VacancyAutoCloseWorker`).
* **Every non-partial view has an action.** `AuditMonitor`, `Backups` and `EmailTemplates` views look orphaned but are served by controllers that live inside `SystemSettingsController.cs` (see §5).
* **Every partial is used** (1 to 17 pages each).
* **Every script and stylesheet is loaded** by at least one page or layout.
* **Every migration has a SQL script** (script 18 covers two migrations) and the model has **no pending changes**.

## 3. Dead code report

### Safe to remove (zero callers in `src` and `tests`)

| # | Item | File | Evidence | Impact |
|---|---|---|---|---|
| D1 | `DocumentVerificationService.IsSettled(EmployeeDocument)` | Application/Onboarding/DocumentVerificationService.cs:51 | the name appears only at its definition | none; replaced by `OnboardingWorkflow.AwaitsReview` / `CheckReadiness` in Phase 27 |
| D2 | `OnboardingDocumentTypeService.AllByCodeAsync` | Application/Onboarding/OnboardingDocumentTypeService.cs:52 | definition only | none (written for Phase 27, never needed) |
| D3 | `PersonalRecordService.IsSignedAsync` | Application/Onboarding/PersonalRecordService.cs:66 | definition only | none; `OnboardingProgressService.SnapshotAsync` answers this |
| D4 | `OnboardingWorkflow.Settled(EmployeeDocumentStatus)` | Domain/Onboarding/OnboardingWorkflow.cs:102 | definition only (the "Settled" hits elsewhere are a DTO property and a test comment) | none |
| D5 | `IRoleAdminService.GetCustomRoleNamesAsync` + implementation | Application/Users/RoleDtos.cs:58, Infrastructure/Identity/RoleAdminService.cs:149 | interface + implementation, no caller | none |

### Used by tests only

| # | Item | Evidence | Proposal |
|---|---|---|---|
| D6 | `DocumentVerificationService.PersonalRecordSignedAsync` | only `PersonalRecordServiceTests.cs:108` calls it; production uses the snapshot | remove it and change that one test line to check the snapshot (`OnboardingDetailsDto.PersonalRecordSigned`) |

### Needs verification (never delete automatically)

| # | Item | Why it looks unused | Why it may be needed |
|---|---|---|---|
| V1 | `OnboardingStatus.AwaitingHrApproval`, `OnboardingAction.HrApproved`, `OnboardingAction.ReturnedForVerification` | no code sets them since Phase 27 | **old history rows** store these values as text; removing them would break reading those rows. **Keep.** |
| V2 | Legacy document type `SchoolCertificates` | not on new checklists | running onboardings started before 10 Nov still have it. **Keep.** |
| V3 | `OnboardingWorkflow.HrRoles` | onboarding moved to permissions | still used by `ProbationService`, `WorkforceDashboardService`, `WorkforceReportService` (see Q3) |
| V4 | `IVirusScanner` / `NoVirusScanner` | always passes | a deliberate hook for a real scanner (Phase 27 decision). **Keep.** |
| V5 | Status-transition helpers such as `OnboardingWorkflow.DocumentsMayBeReviewed`, `LetterMayBeIssued` that just return `BeforeActivation` | trivially the same | they name a rule each; the planned Phase 22-26 automation may set them apart. **Keep.** |

## 4. Unused file report

| Folder | Unused | Used | Size | Notes |
|---|---|---|---|---|
| `wwwroot/lib/bootstrap/dist` | 42 files: grid / reboot / utilities / rtl / non-minified CSS, `bootstrap.js`, `esm`, all `.map` files | `css/bootstrap.min.css`, `js/bootstrap.bundle.min.js` | ~8.4 MB (almost all of it) | safe to delete (F1) |
| `wwwroot/lib/jquery/dist` | `jquery.js`, `jquery.slim*.js`, `.map` files | `jquery.min.js` | ~800 KB | safe (F2) |
| `wwwroot/lib/jquery-validation/dist` | `additional-methods*.js`, `jquery.validate.js` | `jquery.validate.min.js` | ~120 KB | safe (F3) |
| Everything else in `wwwroot`, `Views`, `src` | none | | | |

The licence files stay. The vendor files are committed to git (72 files), so deleting them shrinks the repository and the published site but not the history.

## 5. Duplicate code report

| # | Pattern | Where | Proposal |
|---|---|---|---|
| DU1 | `private static string? Clean(string? s)` (trim, empty → null) | **11** services | one `Text.Clean` in Application/Common |
| DU2 | `SaveAsync` wrapper catching `ConcurrencyConflictException` → `Result.Failure(ConflictMessage)` | **15** services | one extension `unitOfWork.TrySaveAsync(message, ct)` |
| DU3 | `SameName(a, b)` (typed-name signatures) | 3 services (Onboarding, PersonalRecord, Probation) | one `Signatures.SameName` |
| DU4 | `private static DateOnly Today`, `CultureInfo Gb = en-GB` | 11 files each | shared `Clock.Today`, `Formats.Gb` (keep the per-file names if you prefer readability) |
| DU5 | `private async Task<IActionResult> Act(id, action, message)` (run, flash, redirect) | **9** controllers | move into `AppController` once, with the redirect target as a parameter |
| DU6 | `Error(string.Join(" ", result.Errors))` | 24 files | `AppController.Fail(result)` |
| DU7 | Names lookup: `users.GetDisplayNamesAsync(...)` then `string? Name(uid)` local function | 16 services | fine as is; only extract if DU1/DU2 are done |
| DU8 | Four partials named `_StatusBadge` (Shared = active/inactive; Adverts, RecruitmentRequests, Vacancies = their status enums) | Views | not duplicates (different models), but confusing: rename to `_ActiveBadge`, `_AdvertStatusBadge`, … (Category B) |
| DU9 | Email "HR owners" lookup (vacancy recruiter + creator) | `OnboardingProgressService.HrOwnersAsync` and a near-copy removed in Phase 27; similar in `ProbationService` | reuse `OnboardingProgressService.HrOwnersAsync` |

No duplicate repositories, DTOs or EF configurations were found. `ReportService` and `WorkforceReportService` share the report DTOs rather than copying them.

## 6. Refactoring recommendations

**Controllers.** They are thin: they call one service method each and redirect, and none of them queries the database. The exceptions:
* `AccountController` (464 lines, 20 actions) uses `SignInManager`/`UserManager` directly. That is normal for ASP.NET Identity in the Web layer; leave it, but the password/registration rules could move to `UserAdminService` if they grow.
* `UsersController` uses `UserManager` only to build a password-reset link (needs `Url`). Fine.
* `SystemSettingsController.cs` holds **four controllers** (`SystemSettings`, `EmailTemplates`, `Backups`, `AuditMonitor`): split into four files (R1, no behaviour change).

**Application layer.**
* **R2: `OnboardingService` is a god class**: 952 lines, 25 constructor dependencies, covering start, first day, letter, checklist, corrections, activation, induction, lists. Split along the seams already visible: `OnboardingStartService` (start + start form), `AppointmentLetterService` (issue/sign/preview), `OnboardingChecklistService` (tasks), and keep `OnboardingService` for the page, lists, activation and cancel. Do this **after** Phase 27 stages B and C, which touch the same file.
* **R3:** `HiringService` (767 lines, 17 dependencies) and `InterviewService` (712) are large but cohesive; no split proposed now.
* **R4:** the DU1-DU6 helpers above.
* **R5:** `Application/Probation` holds `WorkforceDashboardService` and `WorkforceReportService`, which report on onboarding **and** probation: move them to `Application/Workforce` (or `Reports`).

**Infrastructure layer.**
* No duplicate repositories or configurations. The generic `IRepository<T>.ListAsync` always loads whole entities; this is the root of the performance notes in §11.

## 7. Folder structure recommendations

The Clean Architecture layering is respected: Domain has no dependencies, Application depends only on Domain, Infrastructure implements Application's interfaces, and Web composes them. Feature folders are consistent across the three inner projects (Applications, Hiring, Interviews, Onboarding, Probation, Recruitment, Vacancies, …).

| # | Finding | Proposal |
|---|---|---|
| S1 | 4 controllers in `SystemSettingsController.cs` | one file each (R1) |
| S2 | Workforce dashboard/report services under `Probation` | `Application/Workforce` (R5) |
| S3 | `VacancyAutoCloseWorker` runs **every** hourly job (vacancy closing, offer expiry, advert publishing, reminders, surveys) | rename to `ScheduledJobsWorker` (Category B: name only) |
| S4 | `RoleDtos.cs` also holds the `IRoleAdminService` interface | move the interface to `IRoleAdminService.cs` |
| S5 | `Domain/Applications/CvParser.cs` (308 lines) is text parsing with no entity | acceptable in Domain (pure, no I/O); optional move to `Application/Applications` (Category C) |
| S6 | `OnboardingWorkflow.cs` mixes document rules and the induction templates | split into `OnboardingWorkflow` and `InductionDefaults` when R2 is done |
| S7 | Large views: `Onboarding/Details.cshtml` 494, `Home/Index` 436, `Hiring/Case` 433, `Interviews/Details` 414 | extract the per-panel blocks into partials (e.g. `_NextStepPanel`, `_FirstDayPanel`) when they are next changed |

## 8. Naming review

No placeholder names (`Temp…`, `Test…`, `Helper2`, `…Final`) exist. Inconsistencies:

| Current | Issue | Suggested |
|---|---|---|
| `VacancyAutoCloseWorker` | does much more than close vacancies | `ScheduledJobsWorker` |
| `EmployeeDocument`, `EmployeeDocumentEvent` vs the spec's `OnboardingDocument`, `OnboardingDocumentVerification` | deliberate (Phase 18 table names), documented in phases.md | keep (renaming tables is risky) |
| `OnboardingStatus.AwaitingDepartmentalInduction` now shown as "Induction in progress"; `AwaitingEmployeeSubmission` as "Awaiting the new employee" | enum names are stored as text in the database | keep the stored names; labels already correct |
| `RoleDtos.cs` containing an interface | file name | see S4 |
| `IsHr` private property in several services now means "has onboarding.view" (PersonalRecordService) or "is RO/HR Manager" (ProbationService) | same name, two meanings | `MayView` / `IsHrRole` |
| four `_StatusBadge` partials | same name, different models | DU8 |

## 9. Frontend cleanup

* **Scripts** are small and distinct: no duplicate functions found. `site.js` sets up DataTables, confirmations (SweetAlert2), toasts, the sidebar and dark mode **once** for every `<table class="datatable">`: the DataTables configuration is already shared, with no per-page copies.
* **Unused CSS rules** in `site.css` (no view, script or C# file uses the class, including built-up names such as `tone-@x` / `step-@x`, which were checked): `.stat-tile`, `.stat-icon`, `.stat-label` (lines 198-208), `.task-link`, `.task-title` (603-606), `.sidebar .nav-section` (243), `.dash-greeting-row` (552), `.badge-soft-purple`, `.badge-soft-teal` (430-431), `.alert-light` override (469). Leftovers from before the redesign: **safe to remove (F4)**. `.stat-value` is still used. The `swal2-*`, `dt-*` and `pagination` rules style library markup: keep.
* **Vendor libraries:** all eight are used (Plus Jakarta Sans through `site.css`); only their unused file variants can go (F1-F3).

## 10. Database recommendations (no table or column will be dropped)

| # | Finding | Recommendation |
|---|---|---|
| DB1 | All 52 DbSets are used; no table is unused. The 25 migrations match the 24 scripts; the model has no pending changes. | none |
| DB2 | Single-column `IsDeleted` index on **every** table (39 in the scripts): very low selectivity, used only with other filters. | leave (harmless); consider removing in a later maintenance window (Category C) |
| DB3 | `Vacancies.Status` single index is the leading prefix of `(Status, ClosingDate)`: redundant. | drop it in a future migration (Category B, low value) |
| DB4 | `EmployeeDocuments.Status` single index: the queries always filter by onboarding first. | leave; revisit with stage C's dashboard query |
| DB5 | Legacy values (`AwaitingHrApproval`, `HrApproved`, `SchoolCertificates`) remain in old rows. | keep (V1, V2) |
| DB6 | Columns written but not yet read: `Onboardings.DocumentsDueDateExtended` (stage C), `EmployeeDocuments.RejectionCount` (shown to the RO). | keep: in use by the planned stage C |
| DB7 | No missing index found for the current queries (FKs are indexed by EF; the list and dashboard filters have composite indexes). Stage C's tracking dashboard will need `(Status, DocumentsDueDate)` on Onboardings. | add with stage C |

## 11. Security findings

**Strong points (no action):** a **fallback policy** requires sign-in on every page, and only `Careers`, the account pages and the error page are anonymous; **anti-forgery is validated globally** (`AutoValidateAntiforgeryTokenAttribute`), with no `IgnoreAntiforgeryToken`; every file is downloaded through a controller that checks the owner record (`AttachmentService.OpenAsync` matches owner type and id); `LocalFileStorage` resolves paths with `GetFullPath` under its root; backup downloads accept only names matching a strict pattern; uploads check extension, size, **magic bytes** and the virus-scan hook, and are stored under random names outside wwwroot with SHA-256; there are rate limits, HSTS and a strict Content-Security-Policy; the 4 uses of `Html.Raw` are all sanitised or encoded (`RichTextSanitizer`, encoded email templates, frozen letters built from encoded values).

| # | Finding | Severity | Proposal |
|---|---|---|---|
| SE1 | Permission **`users.manage-privileged`** ("Create privileged accounts", Super Admin only) is in the catalogue but **no code checks it**: privileged role assignment is enforced by `Roles.AssignableByHrAdministrator` + the Super Admin role instead. It shows on Roles & permissions without effect. | Low (misleading, not a hole) | Q1 |
| SE2 | Probation still authorises by **role** (`HrRoles`) while onboarding now uses **permissions**: an HR Manager given only `onboarding.view` keeps full probation rights. | Low | Q3 |
| SE3 | `OnboardingController`, `ProbationController` use the broad `Staff` policy and rely on the service checks. These are tested, but a new action added without a service check would be open to all staff. | Low | add `[Authorize(Policy = …)]` per action group when R2 is done |
| SE4 | The virus scanner is a placeholder that passes everything. | Known, by decision | connect a real scanner before go-live (Q5) |

## 12. Performance findings

| # | Finding | Where | Impact | Proposal |
|---|---|---|---|---|
| P1 | **N+1 on the onboarding page**: 2 attachment queries per document (current + replaced) × 25 documents, and the snapshot (documents, types, attachments, personal record) is built **three times** per page (`GetAsync`, `GetForHrAsync`, `PersonalRecordSignedAsync`) | `DocumentVerificationService.ChecklistAsync`, `OnboardingService.GetAsync` | ~60 queries per page view | load all attachments of the onboarding in one query grouped by owner; build the snapshot once and pass it in |
| P2 | `SetRequirementsAsync` and `PersonnelFileService.OpenForOnboardingAsync` query attachments per document | same files | 25 queries per action | one grouped query |
| P3 | Whole-table loads (`ListAsync(ct: ct)`) followed by filtering in memory: departments/divisions/sites lookups (fine: small tables), but also **all onboardings** in `OnboardingDocumentTypeService.GetAllAsync` and `ApplyToRunningAsync`, and **all onboardings / documents** in `WorkforceReportService` (7 calls) | Application | grows with history | filter in the query (`BeforeActivation` as a list of statuses); a `ListAsync` overload with projection |
| P4 | `ReminderService` loads the application per offer / interview / onboarding inside the loop | Notifications/ReminderService.cs:55-99 | hourly job, small sets | one `ListAsync(a => ids.Contains(a.Id))` before the loop |
| P5 | Lists (onboarding, hiring, applications) are not paged on the server; DataTables pages them in the browser. Vacancies already page (`PageSize`). | Web | fine at GREL's volumes (hundreds) | add server paging when a list passes ~2,000 rows |
| P6 | `NavCounts` runs about 8 queries on **every** page (sidebar badges). `MyTasksAsync` loads the tasks and their onboardings just to count. | Helpers/NavCounts.cs | small | a `CountAsync` version; cache per user for 30-60 s |
| P7 | `AtsService.cs:309`, `ShortlistingService.cs:226` look up the vacancy per group | Application | small | prefetch the vacancies |

## 13. Cleanup plan

### Category A: safe immediate cleanup (near-zero risk)
| # | Change | Files |
|---|---|---|
| A1 | Remove dead methods D1-D5 | DocumentVerificationService, OnboardingDocumentTypeService, PersonalRecordService, OnboardingWorkflow, RoleDtos + RoleAdminService |
| A2 | Remove D6 and adjust the one test | DocumentVerificationService, PersonalRecordServiceTests |
| A3 | Remove the unused CSS rules (F4) | wwwroot/css/site.css |
| A4 | Remove unused vendor variants (F1-F3), keeping licences | wwwroot/lib/bootstrap, jquery, jquery-validation |
| A5 | Split `SystemSettingsController.cs` into four files (S1); move `IRoleAdminService` to its own file (S4) | Web/Controllers, Application/Users |

### Category B: refactor recommended
| # | Change | Risk |
|---|---|---|
| B1 | Fix the onboarding page N+1 and triple snapshot (P1, P2) | low; covered by the onboarding tests |
| B2 | Shared helpers: `Text.Clean`, `TrySaveAsync`, `Signatures.SameName`, `AppController.Act/Fail` (DU1-DU6) | low, but touches ~30 files |
| B3 | Filter whole-table loads in the query (P3) and prefetch in `ReminderService` (P4, P7); `NavCounts` counts (P6) | low |
| B4 | Rename `VacancyAutoCloseWorker` → `ScheduledJobsWorker`; `_StatusBadge` partials; `IsHr` → `MayView` / `IsHrRole` (naming §8) | low |
| B5 | Move the workforce services to `Application/Workforce` (R5, S2) | low (namespaces) |
| B6 | Split `OnboardingService` (R2) and `OnboardingWorkflow` (S6), **after Phase 27 B and C** | medium |
| B7 | Drop the redundant `Vacancies.Status` index (DB3) in the next migration | low |
| B8 | Per-action authorization policies on Onboarding / Probation (SE3) | low |

### Category C: needs your confirmation
See §14, questions 1 to 6.

### Category D: leave alone (intentional)
* Legacy enum values and the legacy document type (V1, V2), and the Phase 18 table names (`EmployeeDocuments`, …).
* The `IVirusScanner` placeholder (V4) and the small named rule helpers (V5).
* `AccountController` using ASP.NET Identity directly.
* DataTables client-side paging at current volumes (P5).
* The `IsDeleted` indexes (DB2) until a maintenance window.
* Domain `CvParser` in Domain (pure logic) unless you prefer it moved (S5).
* Validation inside the services (project convention, no FluentValidation).

### Files recommended for deletion (whole files)
Only vendor variants (A4): `wwwroot/lib/bootstrap/dist/css/` everything except `bootstrap.min.css`; `wwwroot/lib/bootstrap/dist/js/` everything except `bootstrap.bundle.min.js`; `wwwroot/lib/jquery/dist/` everything except `jquery.min.js`; `wwwroot/lib/jquery-validation/dist/` everything except `jquery.validate.min.js`. **No application source file is recommended for deletion.**

### Files recommended for refactoring
`OnboardingService.cs`, `DocumentVerificationService.cs`, `OnboardingProgressService.cs` (P1), `PersonnelFileService.cs` (P2), `OnboardingDocumentTypeService.cs` (P3), `WorkforceReportService.cs`, `WorkforceDashboardService.cs` (P3, R5), `ReminderService.cs` (P4), `AtsService.cs`, `ShortlistingService.cs` (P7), `NavCounts.cs` (P6), `SystemSettingsController.cs` (S1), `RoleDtos.cs` (S4), `VacancyAutoCloseWorker.cs` (S3), the 9 controllers with `Act` and the 11-15 services with `Clean` / `SaveAsync` (B2), `site.css` (A3).

### Proposed order (each step a separate commit, tests run after each)
1. **Analysis**: this document (done).
2. **Safe deletions**: A1-A4.
3. **Refactoring**: A5, B1, B3, B2, B4, B5, B7, B8.
4. **Folder reorganisation**: B5 and S-items not done in step 3.
5. **Testing**: full suite after each step; add a query-count test for the onboarding page (P1).
6. **Documentation**: phases.md / changelog entries, docs 02 (architecture) and 03 (database) updated.

B6 (splitting `OnboardingService`) waits until Phase 27 stages B and C are finished.

## 14. Questions before cleanup

1. **`users.manage-privileged`** (SE1) is shown on Roles & permissions but nothing checks it. Should it **be removed from the catalogue**, or should GRSMS **start enforcing it** (instead of the Super Admin role check) when privileged roles are given?
2. **Vendor files (A4):** OK to delete the unused Bootstrap/jQuery variants from the repository (≈ 9 MB)? Some teams keep the full distribution for debugging with source maps.
3. **Probation authorisation (SE2):** should probation also move to **permissions** (e.g. `probation.view`, `probation.decide`) like onboarding, or stay role-based (RO / HR Manager)?
4. **Shared helpers (B2)** touch ~30 files in one go. Do you want them now, or only in files being changed anyway (less churn)?
5. **Virus scanner (SE4):** is a real scanner (ClamAV, Microsoft Defender) planned before go-live, so that I document how to plug it in?
6. **Order with Phase 27:** do the cleanup **before** stages B and C (cleaner base, small delay), or **after** (no merge friction)? My suggestion: steps 2 and 3 now except B6, then stages B and C, then B6.

## 15. Decisions (user's answers, 10 November 2026)

1. **`users.manage-privileged`: remove** it from the catalogue (behaviour unchanged: the Super Admin role rule protects privileged roles).
2. **Vendor variants: delete** them (licences and the loaded files stay).
3. **Probation moves to permissions** (`probation.view`, `probation.decide`), like onboarding.
4. **Shared helpers: apply everywhere now.**
5. **A real virus scanner is planned**: keep the placeholder and document how to connect one.
6. **Cleanup first**, then Phase 27 stages B and C, then the `OnboardingService` split (B6).
7. **Approved:** A (safe deletions), B1/B3 (performance), B4/B5 (names and folders), B7/B8 (index and policies). Category D is left alone.

## 16. Results (10 November 2026)

Each step was its own commit; the full suite ran after each (188 → **190 tests**, all passing; a full rebuild has no warnings).

| Commit | Step | What changed |
|---|---|---|
| `3b4d76b` | A1-A5 | 6 dead methods removed (D1-D6), plus 2 found by a full rebuild (an unread `PersonnelFileService` constructor parameter and an unused view helper); permission `users.manage-privileged` removed; 11 unused CSS rules and ~9.3 MB of unused vendor files removed; `SystemSettingsController.cs` split into 4 files; `IRoleAdminService` in its own file |
| `0a79ee1` | B1 | Onboarding page **71 → 16 queries** (measured with the new `QueryCounter` test helper; a test keeps it at 20 or fewer); requirements and personnel-file filing use one attachments query; dead `DocumentVerificationService.LoadAsync` removed |
| `6054193` | B3 | Pre-activation onboardings, "waiting to start" and workforce reports filtered in the query; reminders, ATS summary and HOD shortlisting tasks prefetch their lookups; lighter sidebar count |
| `f433d5e` | B2 | `Text.Clean`, `Signatures.SameName`, `TrySaveAsync`, `AppController.Fail` / `RunAsync` replace 11 + 3 + 12 copies, 9 `Act` helpers and 54 error banners |
| `05e3f6d` | B4/B5 | `ScheduledJobsWorker`; `_ActiveBadge`, `_AdvertStatusBadge`, `_RequestStatusBadge`, `_VacancyStatusBadge`; `Application/Workforce`; `IsHr` → `MayView` |
| (this commit) | B7/B8, Q3, Q5 | `IX_Vacancies_Status` dropped (migration `AuditDropRedundantVacancyStatusIndex`, script 25); per-action policies on Onboarding (23 actions) and Probation (`HrDecide`); **probation permissions** `probation.view` (RO, HR Manager) and `probation.decide` (HR Manager), `OnboardingWorkflow.HrRoles` removed; virus scanner guide in [06 Configuration](06-configuration.md#virus-scanning-of-uploads-connect-before-go-live) |

**Still open:** B6 (split `OnboardingService`, after Phase 27 B and C); scripts 24 and 25 not yet run on MySQL; the HR-decision email still goes to the HR Manager role (there is no "email every holder of a permission" yet), so a role given *Decide probation outcomes* later is not emailed.
