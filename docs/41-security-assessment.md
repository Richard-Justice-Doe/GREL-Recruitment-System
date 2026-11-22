# 41. Security assessment

**Date:** 22 November 2026.
**Scope:** the whole GRSMS codebase on branch `feature/ats-and-super-admin`:
* the projects `Grsms.Domain`, `Grsms.Application`, `Grsms.Infrastructure` and `Grsms.Web`;
* the deployment files in `deploy/` and the `Dockerfile`.

**Method:** source review, dependency scan (`dotnet list package --vulnerable`) and configuration review. Attacks were checked against the code paths and the existing automated tests; no live penetration test was run against a deployed server.

**Status (updated 22 Nov 2026):** the four **High** findings are **fixed**, as below. The Medium and Low items remain in the plan (§12).

| # | Finding | Fixed by |
|---|---|---|
| H1 | Audit trail exposed personal data | The audit trail records *which* personal field changed, never its value (`(personal data)` / `(changed)`). Covers the personal record and everything in it (bank accounts, family, next of kin, emergency contacts, beneficiaries, the signed declaration's snapshot) and an employee's date of birth. Existing audit rows were cleaned by migration `AuditPersonalDataRedaction` (script `database/41_audit_personal_data_redaction.sql`). |
| H2 | No MFA | **Two-step sign-in** with an authenticator app (QR code set-up, 10 single-use recovery codes). It is **required** for Super Admin, Admin, HR Administrator, HR Manager and Recruitment Officer password sign-ins (System settings → Sign-in security, on by default) and optional for everyone else. Wrong codes count towards lockout and are audited. Administrators can reset a lost phone (privileged accounts: Super Admin only; never one's own). Microsoft 365 sign-in relies on Microsoft's MFA. |
| H3 | No virus scanning | **ClamAV** container; every upload is scanned before it is stored. **Fails closed**: if the scanner is down, the upload is refused with "try again in a few minutes". |
| H4 | Unencrypted backups with keys, downloadable | Backups are **encrypted with GREL's backup public key** (GnuPG; the private key stays offline). The data-protection keys are in a separate archive. Only *encrypted* database dumps can be downloaded through the app; the Backups screen warns in red while no key is configured. |

**Score after these fixes: about 88 / 100.** The data-protection score rises from 62 to about 74. The remaining gaps are personal-data encryption in the database (M1) and retention and erasure (M2).

Server steps for GREL IT: [docs/21 §5](21-production-deployment.md#encrypted-backups-22-nov-2026-docs41-h4).

> **Modules named in the brief that GRSMS does not have.** Leave management, performance management (appraisals) and payroll are planned but not built (docs/30, Phases 29–32). There is therefore no "payroll configuration" to protect today. The controls below must be applied to them when they are built.

---

## 1. Executive summary

GRSMS starts from a **strong security baseline**. Every request needs a signed-in user unless an action is explicitly public. The protections in place:
* **Forms:** anti-forgery checks on every form post; no raw SQL anywhere (all queries go through Entity Framework).
* **Browser protections:** a strict Content-Security-Policy with no inline scripts, and secure, HttpOnly, SameSite cookies.
* **Sign-in:** account lockout, rate-limited sign-in and password pages, and enumeration-resistant sign-in messages.
* **File uploads:** checked by extension, size and real file content ("magic bytes"), stored under random names outside the website, and served only after an ownership check.
* **Dependencies:** no package has a known vulnerability.

No **critical** vulnerability was found: no code path lets an outsider run code, read the database or bypass sign-in.

The material risks concern **personal data protection**:
1. **The audit trail exposes personal data.** It stores changed personal fields word for word: bank account numbers, SSNIT numbers, religion, dates of birth, next-of-kin details. The Admin and HR Administrator roles can read the audit trail, although they cannot open personal records.
2. **No second sign-in factor (MFA)** protects the accounts that can see every employee's personal data: Super Admin, Admin, HR Manager and Recruitment Officer.
3. **Uploaded files are not virus-scanned.** The scanner is a placeholder that passes every file, yet candidates from the public upload CVs and certificates that staff then open.
4. **Personal data and backups are not encrypted at rest.** Bank, SSNIT and identity numbers are plain columns. Backups are unencrypted archives that include the encryption keys, and they can be downloaded through the app.
5. **No retention or erasure policy for candidates.** Applicants' CVs and personal details are kept indefinitely. This is a gap against GDPR principles and Ghana's Data Protection Act, 2012 (Act 843).

| Score | |
|---|---|
| **Overall security score** | **78 / 100** |
| OWASP Top 10 (2021) | 84 / 100 |
| OWASP ASVS 4.0 Level 2 (applicable sections) | 74 / 100 |
| ISO 27001 Annex A (application-relevant controls) | 70 / 100 |
| Data protection (GDPR principles / Act 843) | 62 / 100 |

**Fixing the four "High" items (§3) would raise the overall score to about 88.** Most of them are a few days' work each.

---

## 2. Findings by severity

| # | Severity | Finding | Where |
|---|---|---|---|
| H1 | **High** | Audit trail stores personal data in clear and shows it to audit readers who can't otherwise see it | `Infrastructure/Persistence/AuditSaveChangesInterceptor.cs`; `Domain/Security/Permissions.cs` (Admin, HR Administrator hold `audit.view`) |
| H2 | **High** | No multi-factor sign-in for privileged accounts | `Web/Controllers/AccountController.cs`, `Program.cs` (Identity options) |
| H3 | **High** | Uploads are not virus-scanned (placeholder scanner) | `Infrastructure/Storage/NoVirusScanner.cs`, `Infrastructure/DependencyInjection.cs` |
| H4 | **High** | Backups unencrypted, include the data-protection keys, downloadable through the app | `deploy/backup.sh`, `docker-compose.prod.yml` (`app-data` volume in the backup), `Web/Controllers/BackupsController.cs` |
| M1 | Medium | Sensitive personal fields stored in plain text (bank account, SSNIT, Ghana Card / passport numbers, religion, medical-type declarations) | `Domain/Onboarding/PersonalRecord.cs`, `Infrastructure/Persistence/Configurations/PersonalRecordConfigurations.cs` |
| M2 | Medium | No retention / erasure for candidates and unsuccessful applications | (no feature) |
| M3 | Medium | Rate limiting only on account pages; none on uploads, applications, messages, knowledge questions | `Program.cs`, `Web/Security/RateLimits.cs` |
| M4 | Medium | The application's database account has full rights (DDL included) because migrations run at start-up | `deploy/docker-compose.prod.yml` (`MYSQL_USER`, `Database__ApplyMigrationsOnStartup`) |
| M5 | Medium | Data-protection key ring stored unencrypted on disk | `Program.cs` (`PersistKeysToFileSystem` with no `ProtectKeysWith…`) |
| M6 | Medium (governance) | One Recruitment Officer can review a request, publish the vacancy and advert, validate scores and approve a hire alone | business rules of 9 and 17 Nov 2026 (docs/41 §4) |
| L1 | Low | HSTS left at the 30-day default, no `includeSubDomains` | `Program.cs` (`app.UseHsts()`) |
| L2 | Low | PDF rendering runs Chromium with `--no-sandbox` and network access | `Infrastructure/Documents/ChromiumPdfRenderer.cs` |
| L3 | Low | Notification "open" changes state on a GET request | `Web/Controllers/NotificationsController.cs` (`Open`) |
| L4 | Low | The lockout message reveals that an email has an account (after 5 wrong passwords) | `AccountController.Login` |
| L5 | Low | Logs contain recipients' email addresses | `Infrastructure/Email/EmailService.cs`, `EmailQueue.cs`, `Application/Common/WorkflowMailer.cs` |
| L6 | Low | Database connection without TLS (inside the private Docker network) | `docker-compose.prod.yml` connection string |
| L7 | Low | Word-preview zip limit trusts each entry's declared size | `Infrastructure/Documents/DocxHtmlPreview.cs` (line 49) |
| L8 | Low | The rich-text editor writes `source.value` into `innerHTML` (mitigated by the server sanitizer and the CSP) | `wwwroot/js/rich-text-editor.js` (line 109) |
| L9 | Low | `AllowedHosts: "*"` in the base settings (production sets the domain) | `Web/appsettings.json` |
| L10 | Low | The seed administrator's password stays in `deploy/.env` unless removed by hand | `docker-compose.prod.yml` (`SEED_ADMIN_PASSWORD`) |

There are **no Critical findings**.

---

## 3. Phase 1: code security review (the 25 checks)

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | SQL injection | ✅ **None** | No `FromSqlRaw`, `ExecuteSqlRaw` or hand-built SQL anywhere; every query is LINQ through EF Core (parameterised). The only `CommandText` use is the slow-query logger, which reads it. |
| 2–4 | XSS: stored, reflected, DOM | ✅ **Low** | Razor encodes by default. `Html.Raw` appears 5 times, each on server-built or sanitised HTML: letters (built with `WebUtility.HtmlEncode`), email preview (`EmailTemplateCatalogue.RenderBody` encodes), the post description (`IRichTextSanitizer`: tag allow-list, no attributes, CSS or URL schemes) and the PDF stylesheet. The CSP (`script-src 'self'`) blocks inline scripts and event handlers even if markup slipped through. DOM: see L8. |
| 5 | CSRF | ✅ | `AutoValidateAntiforgeryTokenAttribute` is a global filter; no `[IgnoreAntiforgeryToken]` anywhere. The anti-forgery cookie is SameSite=Strict, the auth cookie SameSite=Lax. Only exception: L3. |
| 6 | Remote code execution | ✅ | Nothing evaluates user input. Chromium is started with `ArgumentList` (no shell); see L2. |
| 7 | Path traversal | ✅ | Files are stored as `yyyy/MM/{guid}.ext` and the full path is checked to stay under the storage root (`LocalFileStorage.cs`, lines 52–53). Backup downloads accept only `^grsms-(db\|files)-\d{8}-\d{4}\.(sql\|tar)\.gz$` (`BackupStore.cs`). |
| 8 | File upload | ⚠️ **H3** | Extension allow-list, size limit (10 MB; photos 3 MB), magic-byte check, random names, storage outside `wwwroot`, ownership-checked downloads. Word and PDF parsing are hardened (XXE off, zip-bomb and page limits). **Missing: real virus scanning.** |
| 9 | Open redirect | ✅ | Every `returnUrl` and notification link passes `Url.IsLocalUrl` before `LocalRedirect`. |
| 10 | Insecure deserialization | ✅ | Only `System.Text.Json` into fixed DTOs; no `BinaryFormatter` or `TypeNameHandling`. XML readers prohibit DTDs. |
| 11 | Dependencies | ✅ | `dotnet list package --vulnerable --include-transitive`: none. Client libraries: Bootstrap 5.3.3, jQuery 3.7.1, DataTables 2.1.8, PDF.js 4.10.38 (past CVE-2024-4367), with `isEvalSupported: false`. |
| 12 | Insecure API endpoints | ✅ / ⚠️ M3 | The JSON endpoints (`AiController`, units/sites lookups) need sign-in, the anti-forgery token and the matching permission; AI has per-user per-minute and monthly budget limits. Others lack rate limits (M3). |
| 13–16 | Hard-coded credentials, API keys, connection strings, secrets | ✅ | `appsettings.json` holds only empty placeholders. Production values come from `deploy/.env` (git-ignored; only `.env.production.example` is committed). Secrets saved in System settings (SMTP, Microsoft 365, AI keys) are encrypted with ASP.NET Data Protection and never shown again; the audit trail records only "(secret, changed)". |
| 17 | Weak cryptography | ✅ | Passwords: ASP.NET Identity (PBKDF2-HMAC-SHA512, 100,000 iterations). Data Protection: AES-256-CBC + HMAC-SHA256. No MD5 or SHA1 for security purposes. See M5 for key storage. |
| 18 | Password policy | ✅ | 10 characters minimum, upper, lower, digit, symbol, 4 unique characters. Recommended addition: block breached passwords (§10). |
| 19 | Cookies | ✅ | `grsms.auth`: HttpOnly, Secure (production), SameSite=Lax, 20-minute sliding expiry; `grsms.af`: HttpOnly, Secure, Strict. |
| 20 | Security headers | ✅ / L1 | CSP, `X-Content-Type-Options`, `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `Referrer-Policy`, `Permissions-Policy`, COOP; no `Server` header. HSTS at its default (L1). |
| 21 | Rate limiting | ⚠️ M3 | Sign-in, register, resend confirmation, forgot password and reset password: 10 per minute per address. |
| 22 | Session hijacking | ✅ | HttpOnly + Secure cookies, a 20-minute idle timeout, the security stamp re-checked every 5 minutes (a password change or deactivation signs out other sessions), and HTTPS only. |
| 23 | Broken authentication | ✅ / H2 | Lockout after 5 failures for 15 minutes; a confirmed email is required; deactivated accounts are blocked; no username enumeration on sign-in or password reset. **No MFA** (H2). |
| 24 | Broken authorization | ✅ | Global fallback policy "authenticated user". Permission policies on controllers and actions; object checks in the services (see §4). |
| 25 | Privilege escalation | ✅ | Only the Super Admin gives privileged roles; an Admin cannot create or edit Admins or Super Admins. Nobody approves their own request, recommendation or vacancy. Role-gated workflow steps cannot be granted through permissions. |

### Sample fixes for the High findings

**H1: redact personal fields in the audit trail.** Record *that* a sensitive field changed, never its value.

```csharp
// Infrastructure/Persistence/AuditSaveChangesInterceptor.cs
private static readonly HashSet<string> SensitiveProperties = new(StringComparer.Ordinal)
{
    "AccountNumber", "BankName", "SsnitNumber", "NationalIdNumber", "PassportNumber", "GhanaCardNumber",
    "DateOfBirth", "Religion", "ResidentialAddress", "NextOfKinTelephone", "NextOfKinAddress", "NextOfKinDateOfBirth",
    "Telephone", "Telephone1", "Telephone2", "MedicalCondition", "Disability"
};

// inside the property loop, after the ExcludedProperties check:
if (SensitiveProperties.Contains(name))
{
    if (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)) { oldValues[name] = "(personal data)"; newValues[name] = "(changed)"; }
    else if (action == AuditActions.Created && p.CurrentValue is not null) newValues[name] = "(personal data)";
    continue;
}
```

Also run a one-off script that replaces those keys in existing `AuditLogs.OldValues` / `NewValues` rows. Keep the audit row itself (who, when, what entity), which the trail needs.

**H2: MFA for privileged roles.** Identity's built-in authenticator-app support (TOTP) needs no new package:

```csharp
// AccountController.Login, after PasswordSignInAsync:
if (result.RequiresTwoFactor) return RedirectToAction(nameof(LoginWith2fa), new { model.ReturnUrl, model.RememberMe });

// A filter (or the sign-in flow) that sends privileged users without 2FA to the set-up page:
if (user.TwoFactorEnabled == false && (await userManager.GetRolesAsync(user)).Intersect(Roles.Privileged).Any())
    return RedirectToAction("EnableAuthenticator", "Manage");
```

Pages to add:
* **Enable authenticator:** a QR code from `userManager.GetAuthenticatorKeyAsync`, then `VerifyTwoFactorTokenAsync`;
* **Recovery codes:** from `GenerateNewTwoFactorRecoveryCodesAsync`;
* **Sign in with code:** using `TwoFactorAuthenticatorSignInAsync`.

Make MFA mandatory for Super Admin, Admin, HR Manager, HR Administrator and Recruitment Officer. Microsoft 365 sign-in can rely on Entra ID's own MFA, which is set by GREL IT through Conditional Access.

**H3: virus scanning.** Run ClamAV next to the app and implement `IVirusScanner`. Fail closed: if the scanner can't be reached, refuse the upload.

```yaml
# deploy/docker-compose.prod.yml
  clamav:
    image: clamav/clamav:1.4
    restart: unless-stopped
    networks: [grsms]
```

```csharp
// Infrastructure/Storage/ClamAvScanner.cs (INSTREAM over TCP 3310)
public async Task<ScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct)
{
    using var tcp = new TcpClient(); await tcp.ConnectAsync(host, 3310, ct);
    await using var s = tcp.GetStream();
    await s.WriteAsync("zINSTREAM\0"u8.ToArray(), ct);
    var buf = new byte[8192]; int n; content.Position = 0;
    while ((n = await content.ReadAsync(buf, ct)) > 0)
    { await s.WriteAsync(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(n)), ct); await s.WriteAsync(buf.AsMemory(0, n), ct); }
    await s.WriteAsync(new byte[4], ct);
    var reply = await new StreamReader(s).ReadToEndAsync(ct);   // "stream: OK" or "stream: Eicar-Test-Signature FOUND"
    content.Position = 0;
    return reply.Contains("OK") && !reply.Contains("FOUND") ? ScanResult.Ok : ScanResult.Infected(reply);
}
```

Then register it in place of `NoVirusScanner` in `Infrastructure/DependencyInjection.cs`.

**H4: backups.**
1. **Encrypt each archive** before it leaves the container. In `deploy/backup.sh`, pipe each dump through `gpg --batch --encrypt --recipient grel-backup@…`; GREL IT holds the private key off the server.
2. **Exclude `App_Data/keys`** from the files archive, and back the keys up separately (encrypted).
3. **Restrict downloads.** Keep the Backups screen to the Super Admin only, and let it download only the encrypted files.

---

## 4. Phase 2: role-based access control

### 4.1 How access is enforced
1. **Sign-in everywhere:** a fallback policy requires an authenticated user. Only the account pages, the public careers page (`CareersController`) and the error page are anonymous.
2. **Screens:** each controller or action names a permission policy (`Policies.cs` → `Permissions.cs`). Roles carry permissions, and the Super Admin can adjust them (Roles & permissions).
3. **Records (BOLA / IDOR):** every service re-checks the record against the user. Examples:
   * `ApplicationService`: `a.ApplicantUserId != MyId` → "not found";
   * `AccessScope.CanSee`: department scope for HODs, "See all departments" for HR;
   * `DocumentVerificationService`: own onboarding only;
   * `NotificationCenter.OpenAsync(userId, id)`;
   * `EmployeePhotoService.MaySeeAsync`;
   * `ProbationPlanDrafter`: only the HOD recording that meeting.

   Ids that belong to someone else return *not found*, not *forbidden*, so existence isn't revealed. The test suite covers this, e.g. `Other_departments_cannot_see_the_request_or_its_files` and `LinkCrawlWebTests` (every link on every page for each role).
4. **Workflow steps** (who approves at each stage) are tied to roles in the workflow classes and cannot be granted through permissions. Nobody acts on their own request, application, recommendation or vacancy.

### 4.2 Matrix

| Role | Module | Allowed actions | Security issues |
|---|---|---|---|
| **Super Admin** | All | Everything, including privileged accounts, permissions, system settings, backups | No MFA (H2); backup download (H4) |
| **Admin** (system administrator) | Users, master data, audit trail, settings (read-only) | Manage non-privileged accounts, organisation data; read the audit trail | **Sees personal data through the audit trail (H1)**; no MFA |
| **HR Administrator** | Organisation, staff accounts, audit trail | Divisions, departments, units, sites; everyday accounts | **Audit trail personal data (H1)** |
| **HR Manager** | Recruitment (view), shortlisting overrides, onboarding (view), employees, probation outcome, celebrations moderation, knowledge base | No approvals of requests, vacancies, adverts or hires since 9/17 Nov 2026 | No MFA; sees all personal records (by design) |
| **Recruitment Officer** | Requests (stage 2), vacancies, adverts, applications, interviews, hiring, offers, onboarding, document verification | Owns the process after the MD | **Governance M6**: one RO can take a vacancy from review to hire; no MFA. Cannot change security settings, roles or system settings (verified: no `ManageSystemSettings`, `ManageUsers` or `ManageRoles` permission). |
| **Managing Director** | Requests (stage 3), executive dashboard | Last approval of requests | – |
| **Head of Department** | Own department's requests, vacancies, adverts, shortlist views, interviews, probation assessments | Stage 1 approval; scoped by `HodDepartmentIds` | – |
| **IT Manager** | Job adverts | Take offline, record postings | – |
| **Interview Panel Member** | Interviews they sit on | Accept/decline, scorecards | – |
| **Employee** | Own requests, celebrations, knowledge base, my probation, my photo, internal jobs | Own records only | – |
| **Candidate** | Careers, my applications, my onboarding, my personal record | Own applications only (checked by `ApplicantUserId`) | Data kept indefinitely (M2) |

**Direct URL access:** pages without the permission redirect to *Access denied*. Records outside the user's scope return *Not found*. Downloads always go through the owning service. No unauthorised access path was found.

---

## 5. Phase 3: database security

| Area | Finding | Recommendation |
|---|---|---|
| SQL injection | None (EF Core parameterised; no stored procedures or views taking input) | Keep the "no raw SQL" rule in code review |
| Passwords | Identity hashes (PBKDF2, salted); never logged or audited (`ExcludedProperties`) | Add a breached-password check (HaveIBeenPwned k-anonymity API or a local list) |
| Secrets in settings | Encrypted with Data Protection | Protect the key ring itself (M5) |
| PII in clear (M1) | `EmployeePersonalRecords` and related tables: `SsnitNumber`, `AccountNumber`, ID numbers, `Religion`, addresses, next of kin, dates of birth | **Column encryption** of the most sensitive fields (bank account, SSNIT, ID numbers, religion, medical) with an EF value converter backed by Data Protection (sample below). Keep searchable fields (names, staff numbers) in clear. **Plus encryption at rest** of the MySQL data volume (LUKS / BitLocker on the host disk). |
| Audit trail (H1) | Personal values copied into `AuditLogs` | Redact (§3 sample); restrict `audit.view` to Super Admin and an "Auditor" role |
| Database account (M4) | The app's account `grsms` has every right on the `grsms` schema (DDL), because migrations run at start-up | Two accounts: `grsms_migrate` (DDL, used only by a one-off migration container) and `grsms_app` (`SELECT, INSERT, UPDATE, DELETE` only). Set `APPLY_MIGRATIONS=false` for the running app. |
| Soft delete | Users are deactivated, not deleted; attachments and photos kept as history; records cancelled, not deleted | ✅ Good. Add **retention-driven erasure** (M2) so "keep as history" doesn't mean "keep forever". |
| Audit gaps | Data changes, sign-ins, lockouts, backup downloads and file views of new-hire documents are audited | Add: **downloads of CVs and personnel files** (who opened which file), and **exports** (reports to Excel) |
| Data leakage | Reports and exports carry no salaries (docs/37 CR1); PII only in HR views | Watermark exported reports with the user's name and time |
| Backups (H4) | Plain `.sql.gz`, files archive with the key ring | Encrypt, separate keys, keep an off-site copy, test a restore every quarter |

**Encryption sample (M1):**

```csharp
// Infrastructure/Persistence/Converters/ProtectedStringConverter.cs
public sealed class ProtectedStringConverter(IDataProtector p) : ValueConverter<string?, string?>(
    v => v == null ? null : p.Protect(v),
    v => v == null ? null : p.Unprotect(v));

// PersonalRecordConfigurations.cs
var protector = dataProtection.CreateProtector("GRSMS.PII.v1");
b.Property(r => r.SsnitNumber).HasConversion(new ProtectedStringConverter(protector)).HasMaxLength(512);
b.Property(r => r.AccountNumber).HasConversion(new ProtectedStringConverter(protector)).HasMaxLength(512);
```

This needs a migration to widen the columns and a one-off job to encrypt the existing rows. Encrypted columns can't be searched with SQL `LIKE`; none of these fields is searched today.

**Database permission model (MySQL 8.4):**

```sql
CREATE USER 'grsms_app'@'%' IDENTIFIED BY '<long random>' REQUIRE SSL;
GRANT SELECT, INSERT, UPDATE, DELETE ON grsms.* TO 'grsms_app'@'%';
CREATE USER 'grsms_migrate'@'%' IDENTIFIED BY '<long random>' REQUIRE SSL;
GRANT ALL PRIVILEGES ON grsms.* TO 'grsms_migrate'@'%';
CREATE USER 'grsms_backup'@'%' IDENTIFIED BY '<long random>';
GRANT SELECT, LOCK TABLES, SHOW VIEW, EVENT, TRIGGER, PROCESS ON *.* TO 'grsms_backup'@'%';
```

---

## 6. Phase 4: file management

| Check | Status | Detail |
|---|---|---|
| Type validation | ✅ | Per-purpose allow-lists, for example: CVs PDF/DOCX; documents PDF/DOCX/XLSX/JPG/PNG; photos JPG/PNG. Each file's first bytes must match its type, so a renamed executable is refused. No SVG or HTML is accepted. |
| Malware protection | ❌ **H3** | Placeholder scanner |
| Size limits | ✅ | `Uploads:MaxFileSizeMb` (10), photos 3 MB, the proxy allows 25 MB per request, `RequestSizeLimit` on upload actions |
| Storage location | ✅ | `App_Data/uploads` (outside `wwwroot`), random GUID names, the original name kept only in the database |
| Public exposure | ✅ | No static file mapping to uploads; every file goes through a controller and a service check |
| Download authorization | ✅ | Owner, scope or permission checked per file; downloads use `Content-Disposition: attachment` unless previewed; previews of Word files are converted and sanitised on the server; PDFs render through PDF.js with eval off |
| Parsers | ✅ / L7 | Word: DTD prohibited, 50 MB expanded limit (declared sizes: L7); PDF text: 60-page limit; image headers only read for pixel size |

**Recommended architecture:**

```
upload → size + type + magic bytes → ClamAV INSTREAM (fail closed) → random name in a quarantine folder
       → (optional) Word/PDF re-render to a "clean" PDF for previews → move to storage → database row
download → service ownership check → stream with nosniff + attachment (or the sanitised preview)
```

Storage hardening:
* Mount the uploads volume `noexec`.
* Encrypt the host disk.
* Keep uploads out of the web root (already done).
* Include uploads in the encrypted backup (H4).

---

## 7. Phase 5: endpoint (API) review

GRSMS has no public REST API. Its JSON endpoints serve its own pages.

| Endpoint | Risk | Impact | Fix |
|---|---|---|---|
| `POST /Ai/*` (JobDescription, RequestDescription, AdvertVersions, ProbationPlan, Onboarding) | Low | Cost abuse is capped by per-user calls per minute and the monthly budget; prompts carry no personal data unless the switch allows it; output is shown as text or encoded HTML | – (keep the limits) |
| `GET /RecruitmentRequests/Units`, vacancy and site lookups | Low | Lists of department units (not sensitive) | – |
| `POST /Applications/*`, `/MyApplications/Apply` | Medium (M3) | A signed-in candidate could submit many applications or uploads quickly | Add a per-user "uploads" and "applications" limiter (sample below) |
| `POST /Celebrations/Post`, `/Knowledge/Ask` | Low (M3) | Message spam; AI cost for Ask (already budget-limited) | Per-user limiter |
| `GET /Notifications/Open/{id}` | Low (L3) | A cross-site link could mark one's own notification read | Make it a POST, or accept it as harmless |
| `GET /health` | ✅ | Reports only Healthy/Unhealthy, no details | – |
| Mass assignment | ✅ | Actions bind to form DTOs, never to entities; server-side fields (owner, status, numbers) are set by the services | – |

```csharp
// Program.cs: per-user limiter for uploads and submissions
options.AddPolicy(RateLimits.Uploads, http => RateLimitPartition.GetSlidingWindowLimiter(
    http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.Connection.RemoteIpAddress?.ToString() ?? "anon",
    _ => new SlidingWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(5), SegmentsPerWindow = 5, QueueLimit = 0 }));
// [EnableRateLimiting(RateLimits.Uploads)] on every Upload* / Apply / Post action
```

---

## 8. Phase 6: infrastructure

**As deployed today:**
* Docker Compose on a Linux host: Caddy 2.8 → GRSMS (ASP.NET Core 10, non-root user `app`) → MySQL 8.4.
* Only Caddy publishes ports (80/443).
* The app trusts forwarded headers only from the Docker subnet.

GRSMS does not run on IIS today. The IIS and Windows guidance below applies only if GREL moves it there.

| Item | Status | Recommendation |
|---|---|---|
| HTTPS | ✅ | Caddy redirects HTTP to HTTPS; the app also redirects |
| TLS | ✅ | Caddy defaults: TLS 1.2/1.3, modern ciphers, automatic renewal |
| HSTS | L1 | `builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });` once every GREL subdomain is HTTPS |
| Security headers | ✅ | Set by the app on every response |
| CORS | ✅ | Not enabled (same-origin only), which is correct |
| Reverse proxy | ✅ | `ForwardLimit = 1`, known networks only; Caddy hides `Server` |
| Container | ✅ / L2 | Non-root; add `read_only: true` with tmpfs for `/tmp`, `cap_drop: [ALL]`, `security_opt: [no-new-privileges:true]`. For Chromium, add `--proxy-server=127.0.0.1:9 --proxy-bypass-list=<-loopback>` so PDF rendering can't reach the network. |
| Database | M4 / L6 | Separate accounts; `REQUIRE SSL`; `local_infile=0`; no published port (already) |
| Logging | ✅ / L5 | Serilog to console and files; slow-query log. Mask email addresses in log messages; ship logs to a central store kept 1 year (ISO 27001 A.8.15) |
| Firewall | – | Host: allow 443 (and 80 for the redirect / ACME) inbound only; SSH from GREL admin IPs only; outbound only to SMTP, the AI provider, Let's Encrypt and OS updates |
| Server hardening | – | Automatic security updates; Docker from the official repository; `deploy/.env` mode 600 owned by root; full-disk encryption; fail2ban on SSH |

**If GREL moves GRSMS to IIS / Windows Server:**
* **IIS and the app pool:**
  * Install the ASP.NET Core Hosting Bundle (the version matching .NET 10).
  * Run the app pool as `ApplicationPoolIdentity` with *No Managed Code*.
  * Turn off directory browsing; remove the `X-Powered-By` header and the `Server` header (`removeServerHeader="true"`).
  * Request filtering: max content length 26 214 400 bytes; deny `.config` and `App_Data`.
  * TLS: disable TLS 1.0/1.1 and weak ciphers (IIS Crypto "Best Practices" template).
  * Bind HTTPS only, with a GREL or public certificate.
* **Windows Server:**
  * BitLocker on the data volume.
  * Windows Firewall inbound 443 only; RDP only through a VPN or a jump host.
  * Monthly patching; Defender with real-time scanning of the uploads folder (which covers H3 too).
* **ASP.NET Core settings:** `ASPNETCORE_ENVIRONMENT=Production`; `ReverseProxy:Enabled=false` (IIS in-process); keep `DataProtection:KeysFolder` on an encrypted volume, protected with `ProtectKeysWithDpapi()`.
* **MySQL on Windows:**
  * Bind to localhost or the private interface only.
  * `require_secure_transport=ON`.
  * The accounts in §5.
  * Encrypted backups (`mysqldump | gpg`, or MySQL Enterprise Backup with encryption).

---

## 9. Phase 7: attack simulation

| Attack | Attack vector | Affected area | Severity | Exploitability | Result / remediation |
|---|---|---|---|---|---|
| SQL injection | `' OR 1=1 --` in search boxes, filters, form fields | All lists and forms | – | None | Blocked: EF Core parameters; no raw SQL |
| XSS | `<script>`, `<img onerror>`, `javascript:` links in the post description, names, messages, AI output | Requests, celebrations, letters, email templates | Low | Very low | Encoded or sanitised; CSP blocks execution. Keep L8 in mind |
| CSRF | A cross-site form posting to `/Vacancies/Publish/5` | Every POST | – | None | Blocked: global anti-forgery token. L3 (GET marks a notification read) is harmless |
| Session hijacking | Stealing `grsms.auth` through XSS or plain HTTP | All | Low | Very low | HttpOnly, Secure, HTTPS-only, HSTS, 20-minute idle timeout, 5-minute stamp re-check |
| Brute-force sign-in | Password spraying across accounts; guessing one account | `/Account/Login` | Medium → Low | Low | 10 posts per minute per address and lockout after 5. **Add MFA (H2)** so a guessed or phished password is not enough |
| Privilege escalation | Changing a role or permission form field; an Admin giving Super Admin; acting at another stage | Users, roles, workflows | – | None found | Server-side checks; role-bound workflow steps |
| File upload attack | `shell.aspx` renamed `cv.pdf`; a polyglot PDF; an EICAR test file | Candidate and onboarding uploads | **High (H3)** | Medium | The renamed file is refused (magic bytes); **EICAR or real malware is accepted** (no scanner). Fix H3 |
| Directory traversal | `../../appsettings.json` in a file id or backup name | Downloads, backups | – | None | GUID storage names, root containment check, strict backup-name regex |
| Broken authentication | Reusing a reset link; signing in while deactivated or unconfirmed; account enumeration | Account pages | Low (L4) | Low | Single-use tokens, stamp checks, uniform messages; the lockout message is the only signal |
| IDOR / BOLA | Changing `/MyApplications/Details/12` to `/13`; `/Employees/Photo/7`; `DownloadDocument` with someone else's ids | Candidate portal, employees, onboarding | – | None found | Ownership and scope checks return *not found* |

---

## 10. Phase 8: compliance

| Framework | Score | Main gaps |
|---|---|---|
| **OWASP Top 10 (2021)** | **84** | **A01** broken access control ✅. **A02** cryptographic failures ⚠️ (M1, H4, M5). **A03** injection ✅. **A04** insecure design ⚠️ (M6). **A05** misconfiguration ✅ (L1, L9). **A06** vulnerable components ✅. **A07** identification and authentication failures ⚠️ (H2). **A08** software and data integrity ⚠️ (H3, unsigned images). **A09** logging and monitoring ⚠️ (H1, L5, no central log store). **A10** SSRF ✅ (L2). |
| **OWASP ASVS 4.0 L2** | **74** | V2.8 (MFA for privileged users), V6.1 (data classification and encryption of regulated data), V8.3 (sensitive data in logs and audit), V12.4 (malware scan), V7.1 (no secrets or PII in logs) |
| **ISO 27001:2022 Annex A** | **70** | A.5.15 and A.8.2 (access to audit data), A.8.5 (MFA), A.8.7 (malware), A.8.10 (deletion), A.8.13 (backup encryption and restore tests), A.8.24 (cryptography for PII), A.8.15 (central logging) |
| **GDPR principles / Ghana Act 843** | **62** | Storage limitation (M2), integrity and confidentiality (H1, M1, H4), data-subject rights (no self-service export or erasure request), a privacy notice on the careers page and at registration, a record of processing activities |
| Employee data protection practice | 65 | Purpose limitation for religion and medical fields (collect only if required); restrict audit readers; retention per category (candidates 12 months, unsuccessful applications 6–12 months, employees per HR policy plus statutory years) |

---

## 11. Phase 9: security-related performance

| Item | Finding | Recommendation |
|---|---|---|
| Authorization checks | Permission policies read role permissions from a cache; visibility (`AccessScope`) is computed per request | Cache `VisibilityAsync` per request (scoped). It is already called once per page in most services. |
| Navigation counts | Cached for `Navigation:CountsCacheSeconds` (30 s) | ✅ |
| N+1 queries | Lists use projections; celebrations, directory and photos load in batches (`WithPhotoAsync`: at most 2 queries per page) | ✅. Keep `SlowQueryInterceptor` (500 ms) on and review its log monthly |
| Session management | Cookie-based, no server session store; the 5-minute stamp check is one indexed query | ✅ |
| Logging | Serilog with async console and file sinks | Use `Serilog.Sinks.Async` for the file sink if log volume grows; never log request bodies |
| Rate limiter | In-memory per instance | If GRSMS ever runs more than one instance, move limits to the proxy or a shared store |
| AI calls | Cached results for identical inputs; budget checks are a single query | ✅ |

---

## 12. Recommendations and remediation plan

### 12.1 Immediate (within 24 hours)
1. **Remove `SEED_ADMIN_PASSWORD`** from `deploy/.env` on the server; set the file to mode 600 (L10).
2. **Take `audit.view` away from Admin and HR Administrator** in Roles & permissions (a temporary H1 mitigation; no code change). Give it back after the redaction fix.
3. **Restrict the Backups permission** to the Super Admin only (H4, interim).
4. **Check the host:** firewall (443/80 only), SSH limited to admin addresses, automatic security updates on.

### 12.2 Short term (within 30 days)

| Item | Finding | Effort | Files |
|---|---|---|---|
| Audit redaction + clean-up script | H1 | 1 day | `AuditSaveChangesInterceptor.cs`, `database/41_audit_redaction.sql` |
| MFA (authenticator app) for privileged roles | H2 | 3–4 days | `AccountController.cs`, new `ManageController` + views, `Program.cs` |
| ClamAV scanner, fail closed | H3 | 1–2 days | `Infrastructure/Storage/ClamAvScanner.cs`, `DependencyInjection.cs`, `docker-compose.prod.yml` |
| Encrypted backups; keys excluded; restore test | H4 | 1 day | `deploy/backup.sh`, `docker-compose.prod.yml`, docs/21 |
| Per-user rate limits on uploads, applications, messages | M3 | 0.5 day | `Program.cs`, `RateLimits.cs`, upload / apply / post actions |
| HSTS one year, Chromium network block, container hardening | L1, L2 | 0.5 day | `Program.cs`, `ChromiumPdfRenderer.cs`, `docker-compose.prod.yml` |
| Mask emails in logs; POST for notification open | L5, L3 | 0.5 day | `EmailService.cs`, `EmailQueue.cs`, `WorkflowMailer.cs`, `NotificationsController.cs`, notification views |

### 12.3 Long term (1–3 months)
* **Column encryption** of bank account, SSNIT, ID numbers, religion and medical fields, plus host disk encryption (M1).
* **Retention and erasure:**
  * a scheduled job that anonymises candidates and unsuccessful applications after the retention period;
  * "download my data" and "delete my account" for candidates;
  * a privacy notice (M2).
* **Separate database accounts** and a one-off migration step (M4).
* **Protect the key ring** with a certificate (`ProtectKeysWithCertificate`) or a key vault (M5).
* **Governance (M6):** a second-person check on hire approval above a threshold, or a weekly HR Manager review report of RO-only hires. This is a business decision.
* **Central log store** with one-year retention and alerts (lockouts, backup downloads, permission changes).
* **Yearly external penetration test**, plus a check of the Docker image (`docker scout` / Trivy) on every build.
* **Breached-password check** on registration and password change.

### 12.4 Secure coding rules (keep doing)
* No raw SQL; LINQ only.
* No `Html.Raw` except on server-built, encoded or sanitised HTML; new rich text always goes through `IRichTextSanitizer`.
* No inline scripts (the CSP depends on it); behaviour lives in `wwwroot/js`, wired with `data-*` attributes.
* Every action names a permission policy; every service method that takes an id checks ownership or scope and returns *not found* otherwise.
* Every upload goes through `AttachmentService` (type, size, magic bytes, scanner).
* Every redirect target passes `Url.IsLocalUrl`.
* New personal-data fields: decide classification, encryption and audit redaction when the field is added.
* Secrets only in `deploy/.env` or encrypted System settings, never in the repository.

### 12.5 Security checklist (release gate)
- [ ] `dotnet list package --vulnerable` clean
- [ ] Full test suite passes (includes access-control and link-crawl tests)
- [ ] No new `Html.Raw`, `FromSqlRaw` or inline `<script>` (code review)
- [ ] New actions have a policy; new ids are ownership-checked
- [ ] New uploads use `AttachmentService`
- [ ] New personal fields: encrypted? redacted in the audit trail? in the retention job?
- [ ] Backup taken before deployment; restore tested this quarter
- [ ] `deploy/.env` not in git; seed password empty

### 12.6 Recommended architecture

```
                 Internet / GREL network
                          │ 443 (TLS 1.2+, HSTS 1y)
                  ┌───────▼────────┐   WAF rules optional (Caddy / Cloudflare)
                  │  Caddy proxy   │   rate limits for anonymous pages
                  └───────┬────────┘
       private network    │ 8080
                  ┌───────▼────────┐  non-root, read-only FS, no-new-privileges
                  │   GRSMS app    │──► ClamAV (INSTREAM, fail closed)
                  │  (MFA, RBAC,   │──► SMTP / Microsoft 365 / AI provider (egress allow-list)
                  │  audit redact) │
                  └──┬─────────┬───┘
         TLS, app acct│         │ uploads (encrypted disk, noexec)
                  ┌───▼───┐  ┌──▼──────────────┐
                  │ MySQL │  │ key ring        │  protected by certificate / vault
                  └───┬───┘  └─────────────────┘
                      │ backup account (read-only)
                  ┌───▼──────────────┐   gpg-encrypted, off-site copy, quarterly restore test
                  │ Backup container │
                  └──────────────────┘
```

---

*This assessment reflects the code and configuration at the date above. Re-run the checks in §12.5 before each release and repeat the full review yearly or after major changes (leave, performance, payroll modules).*
