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

### Departments
| Column | Type | Rules |
|---|---|---|
| Name | VARCHAR(150) | required, **unique** |
| Code | VARCHAR(20) | required, **unique**, stored upper-case |
| Description | VARCHAR(1000) | optional |
| HeadOfDepartmentUserId | VARCHAR(255) | optional, **FK → Users.Id** (ON DELETE SET NULL) |
| IsActive | TINYINT(1) | inactive = cannot be used for new work |
| + standard columns | | |

Seeded with the 24 departments from the specification: HR, IT, FIN, PROC, SCM, SALES, PROD, ENG, MAINT, HSE, QA, RND, LEGAL, SEC, ADMIN, CA, TRANS, WH, PROJ, IA, MED, CR, SUS, BD.

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
| Location | VARCHAR(300) | optional (town / region / address) |
| IsActive | TINYINT(1) | |
| + standard columns | | |

Seeded with **Head Office (HQ)**.

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
`Roles` holds the 8 roles, with readable ids such as `hr-manager` and `head-of-department`. `UserRoles` links users to roles (many-to-many). The other Identity tables are `UserClaims`, `UserLogins` (Microsoft 365 links), `UserTokens` and `RoleClaims`.

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

## Relationship diagram (Phase 1)

```
 Sites 1 ─────────< Users >───────── 1 Departments 1 ─────< Units
                     │  ▲                 │
                     │  └── HOD ──────────┘   (Departments.HeadOfDepartmentUserId)
                     │
                     └──< UserRoles >── Roles
```

## The SQL scripts (`database/` folder)

| Script | Phase | Creates |
|---|---|---|
| `01_phase01_foundation.sql` | 1 | All tables above, indexes, foreign keys, seed rows (roles, 24 departments, Head Office) |

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

## Backups

Back up at least daily, and keep `AuditLogs` with everything else:

```powershell
mysqldump -u root -p --single-transaction --routines grsms > grsms-backup-YYYYMMDD.sql
```

## Planned tables (later phases)

Following the same conventions (standard columns, soft delete, FKs, indexes), later phases will add: RecruitmentRequests, Vacancies, JobAdvertisements, Candidates (profiles linked to Users), Applications, CandidateMessages, AIResults (CV screening), Shortlists, Assessments, Interviews, InterviewPanels, InterviewScores, BackgroundChecks, OfferLetters, AppointmentLetters, Onboardings, Surveys, Notifications and Attachments. Each phase adds its tables in its own numbered script.
