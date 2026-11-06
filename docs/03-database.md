# 3. Database

GRSMS uses **MySQL 8** with the `utf8mb4` character set. Tables are defined in C# (Entity Framework Core) and turned into SQL scripts for the DBA.

## Rules that apply to every business table

Every business table (Departments, Units, Sites, and every future module's tables) has these standard columns, inherited from `BaseEntity`:

| Column | Type | Meaning |
|---|---|---|
| `Id` | INT, AUTO_INCREMENT, **primary key** | Unique number of the row |
| `CreatedAt` / `CreatedBy` | DATETIME(6) / VARCHAR(256) | When and by whom the row was created (filled in automatically) |
| `UpdatedAt` / `UpdatedBy` | DATETIME(6) / VARCHAR(256) | When and by whom it was last changed (filled in automatically) |
| `IsDeleted` | TINYINT(1), indexed | **Soft delete** flag |
| `DeletedAt` / `DeletedBy` | DATETIME(6) / VARCHAR(256) | When and by whom it was deleted |

**Soft delete** means rows are never physically removed. "Deleting" sets `IsDeleted = 1`, and the application then hides that row everywhere. History, reports and the audit trail stay complete. To see deleted rows in SQL, query them directly: `SELECT * FROM Units WHERE IsDeleted = 1;`

**All times are stored in UTC.** Ghana uses GMT (UTC+0) with no daylight saving, so the stored times are also local time in Ghana.

## Phase 1 tables

### Divisions
The top level of the organisation. Every department belongs to one division.

| Column | Type | Rules |
|---|---|---|
| Name | VARCHAR(100) | required, **unique** |
| Code | VARCHAR(20) | required, **unique** |
| Description | VARCHAR(1000) | optional |
| IsActive | TINYINT(1) | cannot be set to inactive while the division has active departments |
| + standard columns | | |

Seeded with **Corporate (CORP)**, id 1, and **Operations (OPS)**, id 2.

### Departments
| Column | Type | Rules |
|---|---|---|
| Name | VARCHAR(150) | required, **unique** |
| Code | VARCHAR(20) | required, **unique**, stored upper-case |
| DivisionId | INT | required, **FK → Divisions.Id** (ON DELETE RESTRICT) |
| Description | VARCHAR(1000) | optional |
| HeadOfDepartmentUserId | VARCHAR(255) | optional, **FK → Users.Id** (ON DELETE SET NULL) |
| IsActive | TINYINT(1) | inactive = cannot be used for new work |
| + standard columns | | |

Seeded with GREL's 17 departments (code in brackets):

* **Corporate:** Human Resources (HR), Corporate Affairs (CA), Information Technology (IT), Procurement (PROC), Supply Chain (SCM), Finance (FIN), Transport (TRANS), Administration (ADMIN), Internal Audit (IA).
* **Operations:** Factory APM (APM), Factory TBU (TBU), Stores (STR), Garage (GAR), RuTAP (RUTAP), DTC (DTC), Nucleus Operations (NUC), Civil & Infrastructure (CIV).

The department code appears in request numbers, e.g. `RR-APM-2026-0001`.

Ids are permanent. Ids 1–24 hold the original placeholder list from the specification; GREL's departments that were missing are ids 25–33. Departments GREL does not have are kept as **inactive** rows so ids never change, and each still has a division: SALES, PROD, ENG, MAINT, HSE, PQ (Progress and Quality), RND, LEGAL, SEC, WH, PROJ (Project), MED, CR, SUS, BD and EST (Estates). An HR Administrator can reactivate any of them from the Departments screen.

### Units
| Column | Type | Rules |
|---|---|---|
| DepartmentId | INT | required, **FK → Departments.Id** (ON DELETE RESTRICT) |
| Name | VARCHAR(150) | required, unique **within the department** |
| Code | VARCHAR(20) | required, unique **within the department** |
| Description | VARCHAR(1000) | optional |
| IsActive | TINYINT(1) | |
| + standard columns | | |

### Sites
| Column | Type | Rules |
|---|---|---|
| Name | VARCHAR(150) | required, **unique** |
| Code | VARCHAR(20) | required, **unique** |
| SiteType | VARCHAR(150) | optional, e.g. "Rubber Plantation Estate" |
| Location | VARCHAR(300) | optional (town / region / address) |
| IsActive | TINYINT(1) | |
| + standard columns | | |

Seeded with GREL's 12 sites: Head Office (HQ, Corporate Headquarters, Takoradi), Abura and Subri (Rubber Plantation Estates), Apeminim (Rubber Processing Factory), Tsibu (the TBU factory: Rubber Processing Factory, 1D1F Expansion Project), RuTAP (Outgrower & Technical Assistance Programme Operations), and Allabokazo, Assin Adubease, Manso Amenfi, Awodua, Nsuame and Agona Abream (site type not yet known). Ids 13 (TBU Factory) and 14 (Nucleus Operations Areas) are inactive: they were briefly separate sites before GREL confirmed the list.

### Users (ASP.NET Identity, extended)
The standard Identity columns (`Email`, `PasswordHash`, `LockoutEnd`, `AccessFailedCount`, ...) plus:

| Column | Type | Rules |
|---|---|---|
| FullName | VARCHAR(150) | required |
| StaffNumber | VARCHAR(30) | optional, **unique** (many NULLs allowed) |
| DepartmentId | INT | optional, **FK → Departments.Id** (RESTRICT) |
| SiteId | INT | optional, **FK → Sites.Id** (RESTRICT) |
| IsCandidate | TINYINT(1) | 1 = external candidate, 0 = GREL staff; indexed |
| IsActive | TINYINT(1) | 0 = cannot sign in |
| MustChangePassword | TINYINT(1) | 1 = must choose a new password at next sign-in |
| CreatedAt / LastSignInAt | DATETIME(6) | |

Passwords are stored only as a salted hash (PBKDF2), never as readable text.

### Roles, UserRoles
`Roles` holds the 9 built-in roles (Super Admin added in Phase 14), with readable ids such as `hr-manager` and `head-of-department`, plus any custom roles a Super Admin adds (ids `custom-...`). `UserRoles` links users to roles (many-to-many). The other Identity tables are `UserClaims`, `UserLogins` (Microsoft 365 links), `UserTokens` and `RoleClaims`.

### RoleClaims and KnownPermissions (Phase 14)
`RoleClaims` holds **which permissions each role has**: one row per role and permission, `ClaimType` = `grsms:permission`, `ClaimValue` = the permission name (e.g. `vacancies.view`). A custom role's description is a row with `ClaimType` = `grsms:description`. A unique index on (RoleId, ClaimType, ClaimValue) stops duplicates. The Super Admin holds every permission without rows.

`KnownPermissions` (Name, AddedAt) lists the permissions whose default grants have already been applied. At start-up GRSMS gives any permission **not** listed here to its default roles, then lists it, so a permission the Super Admin removed is never given back. To re-apply every default (for example after a mistake), delete the `grsms:permission` rows of the built-in roles and every `KnownPermissions` row, then restart GRSMS. A built-in role added later (the **Admin**, 5 November 2026) is listed as `role:Admin` once it has received all its defaults; existing roles are not touched when that happens.

### AuditLogs
Append-only, never edited or deleted. It does **not** inherit the standard columns.

| Column | Type | Meaning |
|---|---|---|
| Id | BIGINT, AUTO_INCREMENT | |
| Timestamp | DATETIME(6), indexed | when (UTC) |
| UserId / UserName | VARCHAR(255) / VARCHAR(256), indexed | who |
| Action | VARCHAR(50), indexed | Created, Updated, Deleted, SignedIn, SignInFailed, LockedOut, SignedOut, PasswordReset, PasswordChanged |
| EntityName / EntityId | VARCHAR(100) / VARCHAR(255), indexed together | which record (e.g. `Department` / `3`) |
| OldValues / NewValues | LONGTEXT (JSON) | the changed fields before and after |
| IpAddress | VARCHAR(45) | browser's IP address |

Password hashes and security stamps are **never** copied into the audit trail.

## Phase 2 tables

### RecruitmentRequests
| Column | Type | Rules |
|---|---|---|
| RequestNumber | VARCHAR(40) | required, **unique**, e.g. `RR-APM-2026-0001` |
| DivisionId | INT | **FK → Divisions** (RESTRICT). Copied from the department when the request is saved and again when it is submitted, so reports by division stay correct if the department later moves. |
| DepartmentId | INT | **FK → Departments** (RESTRICT) |
| UnitId | INT | optional, **FK → Units** (RESTRICT) |
| SiteId | INT | **FK → Sites** (RESTRICT) |
| PositionTitle | VARCHAR(150) | required |
| NumberOfPositions | INT | 1-100 |
| EmploymentType | VARCHAR(30) | "Engagement type": `Permanent` or `FixedTermContract` (Contract) on the form; `Temporary`, `Casual`, `Internship`, `NationalService` only on older requests |
| StaffCategory | VARCHAR(20) | `Executive`, `Senior`, `Junior` (NULL on requests from before QF-81-11) |
| RequestDate / ProposedStartDate | DATE | from the form; the start date must be after today |
| PostDescription | TEXT | "Description of the post": formatted text, **cleaned by the HTML sanitiser** before saving (only p, br, strong/b, em/i, u, ul, ol, li, h3, h4, blockquote; no attributes). At most 10,000 characters of text. |
| EducationLevel | VARCHAR(30) | same values as Vacancies.MinimumEducation |
| QualificationCategory / TrainingLevel | VARCHAR(200) | optional |
| CertificateRequired | VARCHAR(500) | optional, one per line |
| MinYearsExperience | INT | optional, 0-40 |
| ProfessionalExperience / Remarks | TEXT | optional |
| DrivingLicence / DrivingLicenceClass | VARCHAR(20) / VARCHAR(50) | `NotNeeded` (default), `Desirable`, `Mandatory`; the class is optional and kept only when a licence is desirable or mandatory |
| VacancyReason | VARCHAR(30) | "Purpose": `Restructuring` (internal restructuring), `Replacement`, `Expansion`, `NewPosition`, `Other`; `TemporaryCover` only on older requests |
| PurposeOther | VARCHAR(200) | required when the purpose is Other |
| ReplacedEmployee | VARCHAR(150) | required when the reason is Replacement |
| Justification | VARCHAR(4000) | required |
| BudgetCode | VARCHAR(50) | upper-case; only on requests from before QF-81-11 (empty on new ones: the form has no budget fields) |
| EstimatedAnnualCost | DECIMAL(18,2) | GHS; only on requests from before QF-81-11 (0 on new ones) |
| BudgetValidated / BudgetValidatedByUserId | TINYINT(1) / VARCHAR(255) **FK → Users** | set by the HR Manager |
| RequestedByUserId | VARCHAR(255) **FK → Users** | who raised it: a staff member of the department or its HOD |
| AssignedRecruiterUserId | VARCHAR(255) **FK → Users** | set at the earlier workflow's Recruitment Review; empty on new requests (the vacancy owner is chosen on the vacancy) |
| Status | VARCHAR(30), indexed | `Draft`, `Returned`, `DepartmentHeadReview`, `HrApproval`, `MdApproval`, `Approved`, `Rejected`, `Cancelled`; `RecruitmentReview` only on requests still in the earlier workflow |
| DateRequested | DATETIME(6) | first submission |
| StatusChangedAt | DATETIME(6) | when the current status began (used for "waiting N days") |
| ApprovedAt | DATETIME(6) | MD's final approval |
| Version | CHAR(36) | **concurrency token**: changes on every step, so two simultaneous decisions can't both be saved |
| + standard columns | | |

Indexes: `RequestNumber` (unique), `Status`, `RequestedByUserId`, (`DepartmentId`, `Status`).

### RecruitmentRequestActions (workflow history)
| Column | Type | Meaning |
|---|---|---|
| RecruitmentRequestId | INT **FK → RecruitmentRequests** (RESTRICT) | |
| Action | VARCHAR(30) | `Created`, `Submitted`, `Resubmitted`, `Approved`, `Returned`, `Rejected`, `Cancelled` |
| FromStatus / ToStatus | VARCHAR(30) | the move this step made |
| ActorUserId / ActorName | VARCHAR(255) **FK → Users** / VARCHAR(150) | who acted (name kept as it was at the time) |
| Comments | VARCHAR(2000) | required for Returned / Rejected |
| ActionAt | DATETIME(6) | |
| + standard columns | | |

### Attachments (all modules)
| Column | Type | Meaning |
|---|---|---|
| OwnerType / OwnerId | VARCHAR(50) / INT, indexed together | which record the file belongs to, e.g. `RecruitmentRequest` / `12` |
| Category | VARCHAR(40) | Recruitment request documents (QF-81-11): `JobDescription`, `MainAccountabilities`, `OrganisationalChart` (organisational structure), `DepartmentJustification`, `BudgetApproval` (PDF, DOCX or XLSX only); `InductionProgramme` and `Other` on older requests; more added by later modules |
| OriginalFileName | VARCHAR(200) | name shown to users |
| StoredFileName | VARCHAR(200), **unique** | random name on disk, e.g. `2026/11/3f2a…e9.pdf` |
| ContentType | VARCHAR(150) | MIME type |
| SizeBytes | BIGINT | |
| Sha256 | VARCHAR(64) | fingerprint, to prove a file hasn't been altered |
| + standard columns | | |

There is no database FK on `OwnerId`, because it can point at different tables. The owning module checks it. **The files themselves are not in the database.** They are stored in the folder set by `Storage:RootFolder`, which must be backed up together with the database.

### NumberSequences
| Column | Type | Meaning |
|---|---|---|
| Key | VARCHAR(100), **primary key** | the series, e.g. `RR-APM-2026` |
| LastValue | INT | last number handed out |

A number is used up even if the record that asked for it was never saved, so occasional gaps (0004 → 0006) are normal.

## Phase 3 tables

### Vacancies
| Column | Type | Rules |
|---|---|---|
| VacancyNumber | VARCHAR(40) | required, **unique**, e.g. `VAC-2026-0001` |
| RequisitionNumber | VARCHAR(40) | the **Job Requisition Number**, e.g. `JRN-2026-0001`, given when the request is converted; **unique**; NULL on vacancies created before 5 November 2026 |
| RecruitmentRequestId | INT | **FK → RecruitmentRequests** (RESTRICT), **unique**: one vacancy per approved request |
| JobTitle | VARCHAR(150) | required |
| DivisionId | INT | **FK → Divisions** (RESTRICT), copied from the request |
| DepartmentId / UnitId / SiteId | INT | **FKs** (RESTRICT). The department is copied from the request. |
| RequestingHodUserId | VARCHAR(255) **FK → Users** | the department's HOD (who reviewed the request), or the requester if no HOD is set |
| ReportingManagerUserId | VARCHAR(255) **FK → Users** | optional |
| RecruiterUserId | VARCHAR(255) **FK → Users** | the **Vacancy owner**: the responsible Recruitment Officer |
| EmploymentType | VARCHAR(30) | copied from the request |
| NumberOfPositions | INT | copied from the request |
| VacancyType | VARCHAR(30) | `Internal`, `External`, `InternalAndExternal` |
| JobDescription | LONGTEXT | required (form limit 8,000 characters) |
| MinimumEducation | VARCHAR(30) | `None`, `Basic`, `Secondary`, `Certificate`, `Diploma`, `Hnd`, `Bachelors`, `Masters`, `Doctorate` (lowest to highest) |
| FieldOfStudy / QualificationNotes / ExperienceNotes | VARCHAR(200/1000/1000) | optional |
| MinYearsExperience | INT | 0-40 |
| SalaryMin / SalaryMax | DECIMAL(18,2) | monthly GHS, optional, min ≤ max |
| ShowSalary | TINYINT(1) | show on adverts |
| ClosingDate | DATE | last day to apply (inclusive) |
| Status | VARCHAR(30) | `Draft`, `PendingApproval`, `Published`, `Closed`, `Archived` |
| StatusChangedAt / PublishedAt / ClosedAt | DATETIME(6) | |
| Version | CHAR(36) | concurrency token |
| + standard columns | | |

Indexes: `VacancyNumber` (unique), `RecruitmentRequestId` (unique), `Status`, (`DepartmentId`, `Status`), (`Status`, `ClosingDate`) for automatic closing.

### VacancyRequirements
| Column | Type | Meaning |
|---|---|---|
| Id | INT, AUTO_INCREMENT | |
| VacancyId | INT **FK → Vacancies** (**CASCADE**) | |
| Type | VARCHAR(20) | `Skill` or `Certification` |
| Name | VARCHAR(100), indexed | e.g. `NEBOSH IGC` |
| IsEssential | TINYINT(1) | 1 = must have, 0 = desirable |

These rows are **replaced as a set** whenever a draft vacancy is edited, so they are really deleted rather than soft-deleted. Every change is still in the audit trail.

### VacancyActions (history)
Same shape as RecruitmentRequestActions: `VacancyId` (FK), `Action` (`Created`, `Submitted`, `Published`, `Returned`, `Closed`, `AutoClosed`, `Reopened`, `Archived`), `FromStatus`, `ToStatus`, `ActorUserId` (FK → Users, **NULL = the system**), `ActorName`, `Comments`, `ActionAt`, plus the standard columns.

## Phase 4 tables

### JobAdverts
| Column | Type | Rules |
|---|---|---|
| AdvertNumber | VARCHAR(40) | required, **unique**, e.g. `ADV-2026-0001` |
| VacancyId | INT | **FK → Vacancies** (RESTRICT). A vacancy can have several adverts over time. |
| DivisionId / DepartmentId | INT | **FKs** (RESTRICT), copied from the vacancy |
| RequestingHodUserId | VARCHAR(255) **FK → Users** | copied from the vacancy |
| Title | VARCHAR(200) | required |
| Body | LONGTEXT | required; the advert text (the form allows up to 10,000 characters) |
| HowToApply | VARCHAR(2000) | required |
| StartDate / EndDate | DATE | the advert runs from start to end, inclusive; the end date is never after the vacancy's closing date |
| Status | VARCHAR(20) | `Draft`, `Scheduled`, `Live`, `Closed` |
| StatusChangedAt, PublishedAt, ClosedAt | DATETIME | |
| CreatedByUserId | VARCHAR(255) **FK → Users** | |
| ClonedFromAdvertId | INT | optional, **FK → JobAdverts** (the advert it was copied from) |
| Version | CHAR(36) | concurrency stamp |
| + standard columns | | |

Indexes for the scheduler (`Status, StartDate` and `Status, EndDate`), the vacancy page (`VacancyId, Status`) and reports (`DivisionId, Status`, `DepartmentId, Status`).

### AdvertChannels
One row per channel of an advert (unique on `JobAdvertId, Channel`): `Channel` (`InternalPortal`, `CompanyWebsite`, `LinkedIn`, `Indeed`, `Jobberman`, `Newspaper`), `Status` (`ToPost`, `Posted`, `Removed`), `Reference` (VARCHAR(500): the link, or newspaper name and edition), `PostedAt`, `PostedByName`, `RemovedAt`. Rows are replaced when the channel ticks change on a draft, so they are not soft-deleted (changes are still in the audit trail).

### JobAdvertActions (history)
Same shape as VacancyActions: `JobAdvertId` (FK), `Action` (`Created`, `Cloned`, `Scheduled`, `Unscheduled`, `Published`, `AutoPublished`, `ChannelPosted`, `ChannelRemoved`, `Closed`, `AutoClosed`), `FromStatus`, `ToStatus`, `ActorUserId` (**NULL = the system**), `ActorName`, `Comments`, `ActionAt`.

## Phase 5 tables

### CandidateProfiles
One row per account (unique `UserId`, **FK → Users**): `Phone` VARCHAR(30), `Location`, `HighestEducation` (same values as the vacancy's minimum education), `FieldOfStudy`, `YearsExperience`, `CurrentEmployer`, `CurrentPosition`, `Skills` and `Certifications` (one per line, up to 3,000 characters), `LinkedInUrl`, `Summary`, plus the standard columns. The current CV and certificates are rows in **Attachments** with `OwnerType = 'CandidateProfile'` and category `Cv` or `Certificate`.

### JobApplications
| Column | Type | Rules |
|---|---|---|
| ApplicationNumber | VARCHAR(40) | required, **unique**, e.g. `APP-2026-00001` |
| VacancyId | INT | **FK → Vacancies** (RESTRICT) |
| JobAdvertId | INT | optional, **FK → JobAdverts**: the advert applied through |
| ApplicantUserId | VARCHAR(255) **FK → Users** | **unique together with VacancyId**: one application per person per vacancy |
| IsInternal | TINYINT(1) | the applicant is GREL staff |
| Source | VARCHAR(30) | `CareersPage`, `InternalPortal`, `LinkedIn`, `Indeed`, `Jobberman`, `Newspaper`, `Referral`, `Other` |
| DivisionId / DepartmentId / RequestingHodUserId | | **FKs**, copied from the vacancy |
| FullName, Email, Phone, Location, HighestEducation, FieldOfStudy, YearsExperience, CurrentEmployer, CurrentPosition, Skills, Certifications, LinkedInUrl | | **snapshot** of the profile at the moment of applying |
| CoverLetter | VARCHAR(4000) | optional |
| Status | VARCHAR(30) | `Submitted`, `UnderReview`, `Shortlisted`, `Interview`, `Offer`, `Hired`, `NotSuccessful`, `Withdrawn` |
| SubmittedAt, StatusChangedAt | DATETIME | |
| OutcomeReason | VARCHAR(2000) | internal reason for "not successful" (never shown to the applicant) |
| Version | CHAR(36) | concurrency stamp |
| + standard columns | | |

The documents sent are **copies** in Attachments with `OwnerType = 'JobApplication'`, so removing a file from the profile never removes it from an application. Indexes: `VacancyId, Status`, `DivisionId, Status`, `DepartmentId, Status`, `ApplicantUserId`, `SubmittedAt`.

### JobApplicationActions (history)
Same shape as the other history tables: `JobApplicationId`, `Action` (`Submitted`, `ReviewStarted`, `NotSuccessful`, `Withdrawn`), `FromStatus`, `ToStatus`, `ActorUserId`, `ActorName`, `Comments` (staff-only), `ActionAt`.

## Phase 6 columns (JobApplications)

| Column | Type | Rules |
|---|---|---|
| ScreeningScore | INT | 0-100, null = not screened |
| MeetsMinimum | TINYINT(1) | education and experience minimums met and every essential item found |
| ScreeningDetails | TEXT | JSON breakdown: points per part, matched and missing items |
| ScreenedAt | DATETIME | |
| RecruiterRecommendation / HodRecommendation | VARCHAR(20) | `None`, `Shortlist`, `DoNotShortlist` (existing rows: `None`) |
| RecruiterNote / HodNote | TEXT | up to 2,000 characters (checked by the service) |
| RecruiterRecommendedByUserId / HodRecommendedByUserId | VARCHAR(255) | **FK → Users** |
| RecruiterRecommendedAt / HodRecommendedAt / ShortlistedAt | DATETIME | |

New index `VacancyId, ScreeningScore` for the ranked board. New history actions: `Recommended`, `HodReviewed`, `Shortlisted`, `ShortlistedOverride`.

> **MySQL row size.** MySQL limits a row to 65,535 bytes, and in utf8mb4 each `VARCHAR(n)` reserves up to 4 × n bytes. From Phase 6, JobApplications' long free-text columns (`Skills`, `Certifications`, `CoverLetter`, `OutcomeReason`, `ScreeningDetails`, `RecruiterNote`, `HodNote`) are `TEXT`, which is stored outside the row. New wide tables should do the same for long text.

## Phase 7 tables

### Interviews
| Column | Type | Rules |
|---|---|---|
| JobApplicationId / VacancyId | INT | **FKs** (RESTRICT) |
| DivisionId / DepartmentId / RequestingHodUserId | | **FKs**, copied from the application |
| Round | INT | 1, 2, ...; **unique together with JobApplicationId** |
| Type | VARCHAR(20) | `Interview`, `WrittenTest`, `PracticalTest` |
| Mode | VARCHAR(20) | `InPerson`, `Video`, `Phone` |
| StartsAt | DATETIME | GMT (= UTC) |
| DurationMinutes | INT | 10-480 |
| Location | VARCHAR(500) | room or link |
| CandidateInstructions, StatusReason | TEXT | |
| Status | VARCHAR(20) | `Scheduled`, `Completed`, `Cancelled`, `NoShow` |
| ScheduledByUserId | VARCHAR(255) **FK → Users** | |
| Version | CHAR(36) | concurrency stamp |
| + standard columns | | |

### InterviewPanelMembers, InterviewCriteria
`InterviewPanelMembers`: `InterviewId`, `UserId` (**FK → Users**, unique per interview), `IsChair`. `InterviewCriteria`: `InterviewId`, `Name` VARCHAR(150), `Weight` (weights add up to 100), `SortOrder`. Both are owned by the interview.

### Scorecards, ScorecardRatings
`Scorecards` (standard columns): `InterviewId`, `PanelUserId` (**FK → Users**, unique per interview), `Recommendation` (`StronglyRecommend`, `Recommend`, `WithReservations`, `DoNotRecommend`), `Strengths` and `Concerns` (TEXT), `SubmittedAt`, `WeightedScore` DECIMAL(5,1). `ScorecardRatings`: `ScorecardId`, `CriterionId` (**FK → InterviewCriteria**, RESTRICT; unique per scorecard), `Score` 1-5.

Application history gains `InterviewScheduled`, `InterviewCompleted`, `InterviewCancelled`, `InterviewNoShow`.

## Phase 8 tables

### BackgroundChecks
`JobApplicationId` (FK), `Type` (`References`, `Education`, `Employment`, `CriminalRecord`, `Medical`, `Other`), `Subject` VARCHAR(300), `Status` (`Pending`, `Cleared`, `Concerns`, `Failed`), `Notes` TEXT, `CompletedAt`, `RecordedByUserId` (FK → Users), plus the standard columns.

### HiringRecommendations (the hiring report)
| Column | Type | Rules |
|---|---|---|
| Number | VARCHAR(40) | **unique**, e.g. `HIR-2026-0001` |
| JobApplicationId / VacancyId | INT | **FKs** |
| DivisionId / DepartmentId / RequestingHodUserId | | **FKs**, copied from the application |
| ProposedSalary | DECIMAL(18,2) | monthly, GHS |
| ProposedStartDate | DATE | |
| Justification | TEXT | required |
| CreatedByUserId | VARCHAR(255) **FK → Users** | the proposer |
| Status | VARCHAR(20) | `AwaitingHod`, `AwaitingHr`, `AwaitingMd`, `Approved`, `Rejected`, `Withdrawn` |
| HodDecision / HrDecision / MdDecision | VARCHAR(10) | `None`, `Approve`, `Reject` |
| HodComment / HrComment / MdComment | TEXT | |
| HodUserId / HrUserId / MdUserId | VARCHAR(255) **FK → Users** | who decided |
| HodDecidedAt / HrDecidedAt / MdDecidedAt | DATETIME | |
| Version | CHAR(36) | concurrency stamp |

### Offers
`Number` (unique, `OFF-2026-0001`), `JobApplicationId`, `HiringRecommendationId`, `VacancyId`, `DivisionId`, `DepartmentId`, `RequestingHodUserId` (FKs), `JobTitle`, `MonthlySalary` DECIMAL(18,2), `StartDate`, `ExpiresOn` (reply-by date), `Terms` TEXT, `Status` (`Sent`, `Accepted`, `Declined`, `Withdrawn`, `Expired`), `SentAt`, `SentByUserId` (FK), `RespondedAt`, `Reason` TEXT (decline or withdrawal reason), `Version`, plus the standard columns. Indexes on `VacancyId, Status` (positions taken) and `Status, ExpiresOn` (automatic expiry).

Applications gain the status `OfferDeclined` and the history actions `CheckRecorded`, `HiringRecommended`, `HiringDecided`, `OfferSent`, `OfferAccepted`, `OfferDeclined`, `OfferWithdrawn`, `OfferExpired`.

## Phase 9 tables

### Onboardings
| Column | Type | Rules |
|---|---|---|
| Number | VARCHAR(40) | **unique**, e.g. `ONB-2026-0001` |
| JobApplicationId / OfferId | INT | **FKs**, each **unique**: one onboarding per application and per accepted offer |
| VacancyId / SiteId | INT | **FKs** |
| DivisionId / DepartmentId / RequestingHodUserId | | **FKs**, copied from the application |
| EmployeeUserId | VARCHAR(255) **FK → Users** | the new employee's account (the applicant) |
| EmployeeName / JobTitle | VARCHAR(200) / VARCHAR(150) | |
| MonthlySalary | DECIMAL(18,2) | from the accepted offer, GHS |
| EmployeeNumber | VARCHAR(30) | staff number, once payroll gives one |
| StartDate / ReportTime / ReportLocation | DATE / TIME / VARCHAR(300) | the first day |
| ContactPersonUserId | VARCHAR(255) **FK → Users** | who meets the new employee |
| FirstDayNotes | TEXT | |
| LetterNumber | VARCHAR(40) | **unique** when set, e.g. `APT-2026-0001`; kept when the letter is revised |
| LetterTerms | TEXT | conditions of appointment |
| LetterIssuedAt / LetterIssuedByUserId | DATETIME / **FK → Users** | |
| LetterAcknowledgedAt / LetterAcknowledgedName / LetterAcknowledgedIp | DATETIME / VARCHAR(200) / VARCHAR(64) | the electronic signature: when, the name typed, from which address |
| Status | VARCHAR(40) | the current stage (Phase 18): `Draft`, `AwaitingEmployeeSubmission`, `AwaitingDocumentVerification`, `AwaitingHrApproval`, `AwaitingDepartmentalInduction`, `AwaitingActivation`, `Completed`, `Cancelled` (before Phase 18: `InProgress`, `Completed`, `Cancelled`) |
| ExpectedCompletionDate / CompletedAt / Notes | DATE / DATETIME(6) / TEXT | Phase 18: start date + 30 days by default; when the employee was activated; HR's internal notes |
| StatusChangedAt, CreatedByUserId (**FK**), CancelReason TEXT, Version | | |

### OnboardingTasks (the checklist)
`OnboardingRecordId` (**FK → Onboardings**), `Team` (`HumanResources`, `Department`, `InformationTechnology`, `NewEmployee`), `Title` VARCHAR(300), `DueDate`, `AssignedToUserId` (FK → Users, empty for the new employee's items), `Status` (`Open`, `Done`, `NotNeeded`), `Notes` TEXT, `CompletedAt`, `CompletedByUserId` (FK), `SortOrder`, plus the standard columns. Index on `AssignedToUserId, Status` ("my onboarding tasks").

Application history gains `OnboardingStarted`, `LetterIssued`, `LetterSigned`, `OnboardingCompleted`, `OnboardingCancelled`. The application stays **Hired** throughout.

## Phase 18 tables (onboarding workflow and new-hire documents)

### OnboardingActions (onboarding history)
`OnboardingRecordId` (**FK → Onboardings**), `Action` (`Started`, `LetterIssued`, `Submitted`, `ReturnedToEmployee`, `DocumentsVerified`, `HrApproved`, `ReturnedForVerification`, `InductionCompleted`, `Activated`, `Cancelled`), `FromStatus` / `ToStatus` VARCHAR(40), `ActorUserId` (**FK → Users**), `ActorName` VARCHAR(200), `Comments` TEXT, `ActionAt`, plus the standard columns. Never edited. Index on `OnboardingRecordId, ActionAt`.

### EmployeeDocuments (New Hire Administrative Records)
One row per document per onboarding, created (`Pending`) when onboarding starts.

| Column | Type | Rules |
|---|---|---|
| OnboardingRecordId | INT **FK → Onboardings** (RESTRICT) | unique together with DocumentType |
| EmployeeUserId | VARCHAR(255) **FK → Users** | the new employee |
| DocumentType | VARCHAR(40) | `ApplicationLetter`, `Cv`, `PassportPicture`, `RecommendationLetters`, `MedicalReport`, `PoliceClearance`, `SchoolCertificates`, `BirthCertificate`, `GhanaCard`, `SsnitCard`, `MarriageCertificate`, `ChildBirthCertificates`, `DependantPhotographs`, `BankConfirmationLetter`, `PassportBiodataPage`, `NonCitizenCard` |
| IsRequired | TINYINT(1) | 0 for the five that may be "not applicable" |
| Status | VARCHAR(20), indexed | `Pending`, `Submitted`, `Verified`, `Rejected`, `NotApplicable` |
| SubmittedAt | DATETIME(6) | last upload (or "not applicable" declaration) |
| NotApplicableReason | VARCHAR(500) | the new employee's reason |
| VerifiedAt / VerifiedByUserId | DATETIME(6) / VARCHAR(255) **FK → Users** | who verified, accepted or rejected it, and when |
| RejectionReason | VARCHAR(1000) | shown to the new employee |
| + standard columns | | |

**The files** are rows in `Attachments` with `OwnerType = 'EmployeeDocument'`, `OwnerId = EmployeeDocuments.Id` and category `NewHireDocument`: `OriginalFileName` (as uploaded), `StoredFileName` (the random path on the server, outside the web root), `ContentType`, `SizeBytes`, `Sha256` and `CreatedAt` (the upload date). A document can have several files. Only PDF, JPG and PNG are accepted for these, checked by extension and content.

## Phase 19 tables (Employee Personal Record Declaration)

Only the new employee and HR can read these (family, bank, health and beneficiary details).

### EmployeePersonalRecords
One per onboarding (unique `OnboardingRecordId`, **FK → Onboardings**), `EmployeeUserId` (**FK → Users**). Name, staff number, department, position and service date are **not** repeated here: they come from the onboarding.

| Column | Type | Rules |
|---|---|---|
| Qualification | VARCHAR(200) | optional |
| SsnitNumber | VARCHAR(30) | required to save, upper-case |
| DateOfBirth | DATE | required; age 15-75 (the age is calculated, not stored) |
| ResidentialAddress / HomeTown | VARCHAR(500) / VARCHAR(100) | required |
| Region | VARCHAR(20) | Ghana's 16 regions or `OutsideGhana` |
| Tribe / Religion | VARCHAR(100) | optional |
| Email / MobileNumber | VARCHAR(256) / VARCHAR(30) | the mobile number is required and must be a usable phone number |
| HasDisability / DisabilityDetails | TINYINT(1) / TEXT | details required when there is a disability |
| VisibleMarks | VARCHAR(500) | optional |
| NextOfKinName, NextOfKinTelephone, NextOfKinRelationship, NextOfKinDateOfBirth, NextOfKinAddress | | name, relationship and telephone required |
| DeclaredAt / CurrentDeclarationId | DATETIME(6) / INT | when the current version was signed and which declaration row; both cleared by any change |
| Version | CHAR(36) | concurrency token |
| + standard columns | | |

### The lists (replaced as a whole on every save; rows removed are deleted and recorded in the audit trail)
| Table | Columns | Rules |
|---|---|---|
| EmployeeLanguages | `Language` VARCHAR(60), `Proficiency` (`VeryFluent`, `Fluent`, `Passable`) | at least one; each language once |
| EmployeeBankAccounts | `BankName`, `Branch` VARCHAR(100), `AccountNumber` VARCHAR(40), `IsPrimary` | at least one; **exactly one primary** |
| EmployeeSpouses | `FullName`, `Occupation`, `DateOfBirth`, `Address`, `Telephone` | optional, up to 4 |
| EmployeeChildren | `FullName`, `Gender` (`Male`, `Female`), `DateOfBirth`, `PlaceOfBirth`, `CurrentResidence` | any number |
| EmployeeDependants | `FullName`, `Relationship`, `Age` (0-120) | any number |
| EmployeeEmergencyContacts | `Name`, `Relationship`, `Telephone1`, `Telephone2` | 1 to 3 |
| EmployeeBeneficiaries | `Name`, `Relationship`, `DateOfBirth`, `Percentage` DECIMAL(5,2) | at least one; each > 0; **the total must be exactly 100.00**, checked by the application before anything is saved |

Each list row has `Id` and `PersonalRecordId` (**FK → EmployeePersonalRecords**, CASCADE).

### EmployeeDeclarations (immutable)
`PersonalRecordId` (**FK**), `EmployeeUserId` (**FK → Users**), `DeclarationText` (*"I declare that the above information is accurate and correct to the best of my knowledge."*), `SignatureName` (the typed full name), `SignedAt`, `IpAddress`, `Snapshot` TEXT (the whole record as signed, JSON), `SnapshotSha256`, plus the standard columns. **Insert-only:** any attempt to change or delete a row (even a soft delete) is refused when saving. Signing again after a change adds a new row.

## Phase 10 tables

### Notifications (the bell)
`Id` BIGINT, `UserId` (**FK → Users**, cascade), `Title` VARCHAR(200), `Body` VARCHAR(500), `Link` VARCHAR(500) (a path inside GRSMS only), `CreatedAt`, `ReadAt`. No soft delete and no audit rows: read notifications older than 6 months are deleted by the hourly job. Indexes on `UserId, ReadAt` (unread count) and `UserId, Id`.

### SentReminders
`Id` BIGINT, `Key` VARCHAR(200) **unique** (e.g. `offer-expiring:12`, `onboarding-tasks:<user>:20261105`), `SentAt`. One row per reminder sent, so none is sent twice.

### SurveyInvitations
`Type` (`CandidateExperience`, `NewEmployee`), `UserId` (FK), `JobApplicationId` (FK; **unique with Type**), `OnboardingRecordId` (FK, new-employee survey), `VacancyId`, `DivisionId`, `DepartmentId`, `RequestingHodUserId` (FKs), `Outcome` (the application status when invited), `SentAt`, `ExpiresOn`, `CompletedAt`, plus the standard columns.

### SurveyAnswers
`SurveyInvitationId` (FK), `QuestionKey` VARCHAR(30) (**unique per invitation**), `Rating` 1-5 (null = not applicable), `Text` TEXT, plus the standard columns. **Not copied into AuditLogs** (confidential).

## Phase 16 tables

### SystemSettings
One row per setting saved on the System settings screen: `Key` (e.g. `Email:SmtpHost`), `SettingValue` (TEXT; for secrets, encrypted with the data-protection keys) and `IsSecret`. A setting without a row, or with an empty value, uses the server configuration, then the built-in default. In the audit trail a secret shows only *(secret, changed)*.

### EmailTemplates
One row per candidate email reworded on the Email templates screen: `Key` (e.g. `offer.sent`), `Subject`, `Body` (plain text with placeholders). *Reset* soft-deletes the row, so the built-in wording applies again; editing again adds a new row.

## Phase 15 tables and columns (ATS)

### New columns on JobApplications
| Column | Meaning |
|---|---|
| `AtsClassification` | The band of the overall match (`HighlyRecommended`, `Recommended`, `ReviewRequired`, `LowMatch`, `AutoReject`) |
| `CvText` (MEDIUMTEXT) | The CV's plain text (up to 100,000 characters), used to match requirements. **Not copied into the audit trail.** |
| `CvParsed` (MEDIUMTEXT) | What the ATS read from the CV, as JSON (name, email, phone, education, jobs with dates, skills, certifications, warnings, which reader) |
| `AtsDecision` | `None`, `AutoRejected` (held for HR), `Confirmed`, `Overridden` or `Cleared` |
| `AtsRejectionReason` (TEXT) | Why the ATS rejected it, one reason per line |
| `AtsDecisionByUserId`, `AtsDecisionAt`, `AtsDecisionNote` | Who confirmed or overrode it, when, and their note or reason |

The Phase 6 columns stay: `ScreeningScore` is now the overall match %, `MeetsMinimum` means every requirement and knockout question passed, and `ScreeningDetails` holds the ATS breakdown as JSON (`"Version":2`; older Phase 6 breakdowns are ignored until screening is re-run). The value `AutoRejected` was added to `Status`.

### VacancyKnockoutQuestions
A vacancy's mandatory questions (`VacancyId`, `Text` up to 300 characters). Replaced as a set when the vacancy is edited; deleted with it.

### JobApplicationKnockoutAnswers
The applicant's answers (`JobApplicationId`, `Question` = the wording as asked, `Answer` 1 = Yes). Never edited.

### AtsSettings
One row: the four weights, the four band limits, the six automatic-rejection switches and `UseAiReader`. It is created the first time the Super Admin saves **ATS settings**; until then the defaults in code apply (25 % each; bands 90 / 80 / 70 / 50; every rule on except "required skill"; AI reader off).

## Relationship diagram (Phases 1-8)

```
 Sites 1 ─────────< Users >───────── 1 Departments 1 ─────< Units
   │                 │  ▲                 │                  │
   │                 │  └── HOD ──────────┘                  │
   │                 └──< UserRoles >── Roles                 │
   │                                                         │
   └───────< RecruitmentRequests >───────────────────────────┘  (also → Departments, → Users ×3)
                     │
                     ├──< RecruitmentRequestActions
                     ├──< Attachments  (OwnerType = 'RecruitmentRequest')
                     │
                     └── 1 : 0..1 ── Vacancies  (also → Departments, Units, Sites, Users ×2)
                                        ├──< VacancyRequirements
                                        ├──< VacancyActions
                                        └──< JobAdverts  (also → Divisions, Departments, Users ×2)
                                               ├──< AdvertChannels
                                               └──< JobAdvertActions
                                        └──< JobApplications  (also → JobAdverts, Divisions, Departments, Users ×2; unique per applicant)
                                               ├──< JobApplicationActions
                                               └──< Attachments (OwnerType = 'JobApplication')
 Users 1 ── 0..1 CandidateProfiles ──< Attachments (OwnerType = 'CandidateProfile')
```

## The SQL scripts (`database/` folder)

| Script | Phase | Creates |
|---|---|---|
| `01_phase01_foundation.sql` | 1 | Phase 1 tables, indexes, foreign keys, seed rows (roles, 24 departments, Head Office) |
| `02_phase02_recruitment_requests.sql` | 2 | RecruitmentRequests, RecruitmentRequestActions, Attachments, NumberSequences |
| `03_phase03_vacancies.sql` | 3 | Vacancies, VacancyRequirements, VacancyActions |
| `04_grel_departments.sql` | 3 | No new tables. Replaces the placeholder departments with GREL's 18: adds ids 25–32, renames QA → Progress and Quality (PQ) and Projects → Project, deactivates the 14 placeholders. If someone has already added departments by hand, check first that ids 25–32 are free. |
| `05_divisions_and_sites.sql` | 3 | Divisions table (Corporate, Operations); `DivisionId` on Departments, RecruitmentRequests and Vacancies; `RequestingHodUserId` on Vacancies; `SiteType` on Sites. Sets the 17 departments from the GREL requirements (adds Nucleus Operations, id 33; renames Transport and the factories; reactivates Transport; deactivates Estates, Progress and Quality and Project) and adds GREL's 13 other sites (ids 2–14). Existing requests and vacancies get their division from their department. **Check first that site ids 2–14 and department id 33 are free.** |
| `06_grel_sites.sql` | 3 | No new tables. Sets the site list to GREL's 12 sites: spells site 3 *Apeminim*, gives Tsibu the TBU factory type, and deactivates sites 13 (TBU Factory) and 14 (Nucleus Operations Areas). |
| `07_phase04_job_adverts.sql` | 4 | JobAdverts, AdvertChannels, JobAdvertActions |
| `08_phase05_applications.sql` | 5 | CandidateProfiles, JobApplications, JobApplicationActions |
| `09_phase06_shortlisting.sql` | 6 | No new tables: screening and shortlisting columns on JobApplications; its long text columns become `TEXT` |
| `10_phase07_interviews.sql` | 7 | Interviews, InterviewPanelMembers, InterviewCriteria, Scorecards, ScorecardRatings |
| `11_phase08_hiring.sql` | 8 | BackgroundChecks, HiringRecommendations, Offers |
| `12_phase09_onboarding.sql` | 9 | Onboardings, OnboardingTasks |
| `13_phase10_notifications_surveys.sql` | 10 | Notifications, SentReminders, SurveyInvitations, SurveyAnswers |
| `14_phase14_super_admin_permissions.sql` | 14 | KnownPermissions; the Super Admin role; RoleClaims text columns shortened (the table was empty) and a unique index. The default permissions and the first Super Admin are added by GRSMS at start-up, not by the script. |
| `16_admin_role.sql` | — | The built-in **Admin** role (id `admin`). Its default permissions and the new permission `settings.view` are added by GRSMS at start-up. If someone already added a *custom* role called "Admin", rename it first (role names are unique). |
| `17_phase16_system_settings.sql` | 16 | SystemSettings (values saved on the System settings screen; secrets encrypted) and EmailTemplates (reworded candidate emails). The new permissions `settings.manage` and `backups.manage` are added by GRSMS at start-up. |
| `18_qf8111_recruitment_request_form.sql` | QF-81-11 | No new tables. The QF-81-11 form fields on RecruitmentRequests (StaffCategory, PurposeOther, RequestDate, ProposedStartDate, PostDescription, EducationLevel, QualificationCategory, TrainingLevel, CertificateRequired, MinYearsExperience, ProfessionalExperience, DrivingLicence (`NotNeeded` by default), DrivingLicenceClass, Remarks; all optional, so existing requests are untouched) and `RequisitionNumber` (unique, NULL for older vacancies) on Vacancies. The one-time grant of the request permissions to Employees is made by GRSMS at start-up. |
| `19_phase18_onboarding_workflow.sql` | 18 | OnboardingActions, EmployeeDocuments; `ExpectedCompletionDate`, `CompletedAt`, `Notes` on Onboardings and `Status` widened to 40. **Moves existing onboardings to the new stages** (in progress → Draft, Awaiting employee submission or Awaiting departmental induction, by letter state), sets the expected completion dates, and gives onboardings still waiting for the new employee their 16-document checklist. Checked on MySQL 8.4 against a copy of the development database. |
| `20_phase19_personal_record_declaration.sql` | 19 | EmployeePersonalRecords, EmployeeLanguages, EmployeeBankAccounts, EmployeeSpouses, EmployeeChildren, EmployeeDependants, EmployeeEmergencyContacts, EmployeeBeneficiaries, EmployeeDeclarations. New tables only. Checked on MySQL 8.4 (with script 19) against a copy of the development database. |
| `15_phase15_ats_engine.sql` | 15 | ATS columns on JobApplications (existing rows get `AtsDecision` = `None`), VacancyKnockoutQuestions, JobApplicationKnockoutAnswers, AtsSettings. The new permission `ats.configure` is added by GRSMS at start-up. |

Each script:
* runs **once**, in number order, against an empty database (or one that already has the earlier scripts applied);
* cannot be fully undone if it fails halfway, because MySQL commits each `CREATE TABLE` immediately. **Take a backup first.** If a script fails, restore the backup, fix the cause and run it again;
* records itself in the `__EFMigrationsHistory` table, so the application knows the database is up to date.

```powershell
mysql -u grsms -p grsms < database/01_phase01_foundation.sql
```

### Regenerating a script

The scripts are generated from the C# model. Don't edit them by hand; change the C# configuration and regenerate:

```powershell
# whole history (empty database → latest)
dotnet ef migrations script --project src/Grsms.Infrastructure --startup-project src/Grsms.Web -o database/all.sql

# only the changes between two phases
dotnet ef migrations script Phase01_Foundation Phase02_RecruitmentRequests --project src/Grsms.Infrastructure --startup-project src/Grsms.Web -o database/02_....sql
```

Generating scripts does **not** need a database connection. To apply migrations directly from the command line instead, set the `GRSMS_CONNECTION` environment variable to a connection string and run `dotnet ef database update --project src/Grsms.Infrastructure --startup-project src/Grsms.Web`.

> The `--idempotent` option is **not** used. With the Oracle MySQL provider it generates `IF NOT EXISTS … BEGIN … END` blocks, which are SQL Server syntax and fail in MySQL.

## Viewing the data

* **In a browser (Docker set-up):** phpMyAdmin at http://localhost:8081. See [10 Running with Docker → Viewing the database](10-docker.md#viewing-the-database-in-a-browser).
* **From a desktop tool** (MySQL Workbench, DBeaver, ...): host `localhost`, port **3307** for the Docker database (3306 for a locally installed MySQL), database `grsms`.

Table names keep their capitals (`Users`, not `users`). On Linux, which includes MySQL in Docker, table names are case-sensitive.

## Backups

For the Docker set-up, see [10 Running with Docker → Backups](10-docker.md#backups).

Back up at least daily, and keep `AuditLogs` with everything else:

```powershell
mysqldump -u root -p --single-transaction --routines grsms > grsms-backup-YYYYMMDD.sql
```

Also back up the **uploaded files folder** (`Storage:RootFolder`) at the same time. The database only holds descriptions of the files.

## Planned tables (later phases)

Following the same conventions (standard columns, soft delete, FKs, indexes), later phases will add: CandidateMessages, AppointmentLetters, Onboardings, Surveys and Notifications. Each phase adds its tables in its own numbered script.
