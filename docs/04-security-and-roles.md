# 4. Security and roles

## Super Admin, permissions and custom roles (Phase 14)

GRSMS uses **role-based access control with permissions**:

* A **permission** opens a screen or a function, for example *Open vacancies* or *Maintain organisation data*. The list of permissions is fixed in code (`src/Grsms.Domain/Security/Permissions.cs`), because the code checks each one.
* **Which roles hold which permission** is stored in the database (table `RoleClaims`). A **Super Admin** changes it on **Management → Roles & permissions** by ticking boxes. A change applies straight away: nobody has to sign out.
* The defaults reproduce exactly what each role could open before Phase 14, so nobody gained or lost access when permissions were introduced. Each new permission gets its defaults **once**, at the first start-up of the version that adds it. After that, only the Super Admin's choices count.

| Permission | Super Admin | HR Admin | HR Manager | MD | Recruitment Officer | HOD | Panel Member / Employee | Candidate |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Manage user accounts | 🔒 | ✅ | | | | | | |
| Maintain organisation data (divisions, departments, units, sites) | 🔒 | ✅ | | | | | | |
| Read the audit trail | 🔒 | ✅ | ✅ | | | | | |
| Open recruitment requests | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | | |
| Raise recruitment requests | 🔒 | | | | | ✅ | | |
| Open vacancies / job adverts / applications / reports | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | | |
| Read survey results | 🔒 | | ✅ | ✅ | ✅ | | | |
| See all departments | 🔒 | ✅ | ✅ | ✅ | ✅ | | | |
| Manage roles and permissions | 🔒 only | | | | | | | |
| Create privileged accounts | 🔒 only | | | | | | | |
| Configure the ATS (weights, bands, auto-rejection rules, AI reader; Phase 15) | 🔒 only | | | | | | | |

🔒 = always held, cannot be removed. ✅ = default, can be changed by the Super Admin. **Candidate** can never be given a staff permission.

### Rules that are not permissions (on purpose)

These are separation of duties, so no setting can switch them off:

* **Who acts at each workflow step stays with the role:** the HOD raises, the Recruitment Officer reviews, the HR Manager approves, the MD gives final approval, the HOD gives their view on shortlists. A permission only opens the screen; the workflow decides what each person may do there. A Super Admin who wants to approve as the MD must also hold the Managing Director role, and the "not your own request" rules still apply.
* **Only a Super Admin can give or remove any role other than Employee and Interview Panel Member.** That covers HR Manager, Recruitment Officer, Head of Department, HR Administrator, Managing Director, Super Admin and every custom role. Only a Super Admin can **edit, reset the password of, or deactivate** an account that holds one of them. Otherwise an HR Administrator could raise their own access, or take over an HR Manager's account by resetting its password. HR Administrators can still **unlock** any account, because that gives no new access.
* **Permissions marked "Super Admin only"** (manage roles and permissions, create privileged accounts) can never be ticked for another role.
* **Built-in roles** cannot be renamed or deleted, because the workflows check them by name.

### Custom roles

A Super Admin can add a role such as *Recruitment Auditor*, tick its permissions, and give it to people. A custom role opens only what is ticked for it, and has no part in any workflow step. Give its holders the **Employee** role too, so they can see internal vacancies. A custom role can be renamed, and deleted once nobody holds it.

### Everything is audited

Each permission ticked or unticked is a `RoleClaim` row created or deleted. Each new custom role is a `Role` row. Each role given to a user is a `UserRole` row. All of them are in the audit trail with who did it, when, and from which address. Grants made automatically at start-up appear as made by *system*.

## Who can do what (Phases 1-11, with the default permissions)

The **Super Admin** can open every screen below and sees every department. For the workflow actions they need the matching role, like anyone else.

| Screen / action | HR Admin | HR Manager | MD | Recruitment Officer | HOD | Panel Member | Employee | Candidate |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Sign in, change own password | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Home page | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (candidate home) |
| Divisions, Departments, Units, Sites | ✅ | | | | | | | |
| Users & roles | ✅ Employee and Panel Member accounts only | | | | | | | |
| Roles & permissions | | | | | | | | |
| Audit trail (read only) | ✅ | ✅ | | | | | | |
| See recruitment requests | all | all | all | all | own departments | | | |
| Raise / edit / submit / cancel a request | | | | | ✅ own departments, own requests | | | |
| Decide at Recruitment Review (and assign recruiter) | | | | ✅ | | | | |
| Decide at HR Approval (and validate budget) | | ✅ | | | | | | |
| Decide at MD Approval (final) | | | ✅ | | | | | |
| Download request documents | ✅ | ✅ | ✅ | ✅ | own departments | | | |
| See vacancies | all | all | all | all | own departments (read only) | | | |
| Create (from approved request), edit, submit a vacancy | | | | ✅ | | | | |
| Approve (publish) or return a vacancy | | ✅ not one they submitted | | | | | | |
| Close, reopen, archive a vacancy | | ✅ | | ✅ | | | | |
| See job adverts | all | all | all | all | own departments (read only) | | | |
| Create, edit, publish / schedule, close, clone an advert; record external postings | | ✅ | | ✅ | | | | |
| Public careers page (website adverts) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (and anyone, no sign-in) |
| Internal jobs on the careers page (internal-portal adverts) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | |
| My profile, apply, my applications, withdraw | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (external jobs only) |
| See applications (details and documents) | all | all | all | all | own departments (read only) | | | |
| Start review, mark not successful | | ✅ not their own | | ✅ not their own | | | | |
| Shortlisting board and scores | all | all | all | all | own departments | | | |
| Re-run screening, recommend, ask the HOD | | ✅ not their own | | ✅ not their own | | | | |
| ATS overview and dashboards (Phase 15) | all | all | all | all | own departments | | | |
| Confirm or override an ATS rejection (held for HR) | | ✅ not their own | | ✅ not their own | | | | |
| Agree / disagree with a shortlist recommendation | | | | | ✅ own departments | | | |
| Shortlist (HOD agreed) | | ✅ | | ✅ | | | | |
| Shortlist without the HOD's agreement (with reason) | | ✅ | | | | | | |
| Schedule, reschedule, complete, cancel interviews and assessments | | ✅ not their own | | ✅ not their own | | | | |
| Sit on a panel and submit a scorecard | if chosen | if chosen | if chosen | if chosen | ✅ always for own department | if chosen | if chosen | |
| See all scorecards of a session; interview results | ✅ | ✅ | ✅ | ✅ | ✅ own departments | after submitting their own | | |
| Hiring cases (checks, recommendation, offer) | all | all | all | all | own departments | | | |
| Record checks, recommend hire, send / withdraw offers | | ✅ not their own | | ✅ not their own | | | | |
| Endorse a hiring recommendation | | | | | ✅ own departments | | | |
| Approve at HR stage (reason needed against the HOD) | | ✅ not if they proposed it | | | | | | |
| Final hiring approval | | | ✅ not if they proposed it or approved at HR | | | | | |
| Read offer letter, accept / decline | | | | | | | ✅ own | ✅ own |
| See onboardings (checklist, first day, letter preview) | all | all | all | all | own departments | if given a task | if given a task | |
| Start onboarding; change first-day details, staff number and checklist; complete, cancel | | ✅ not their own | | ✅ not their own | | | | |
| Issue / revise the appointment letter (until signed) | | ✅ not their own | | | | | | |
| Tick off a checklist item | | ✅ any | | ✅ any | ✅ department items and their own | their own | their own | |
| Read and sign own appointment letter; see own first-day information | | | | | | | ✅ own | ✅ own |
| Own notifications (the bell) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Answer own feedback surveys | | | | | | | ✅ own (internal applicants) | ✅ own |
| Survey results (no names, from 3 responses) | | ✅ | ✅ | ✅ | | | | |
| Reports and the overview; CSV download | all | all | all | all | own departments | | | |

### Workflow controls (recruitment requests)
* **Segregation of duties:** nobody can decide on a request they raised, even if they also hold the approver role.
* **One role per stage:** the server refuses a decision from anyone outside that stage's role.
* **Comments are compulsory** when returning or rejecting, so the HOD always knows why.
* **Budget validation** must be ticked by the HR Manager before HR approval. It is cleared again if the request is resubmitted.
* **No double decisions:** each request carries a version stamp. If two approvers act at the same moment, only the first decision is saved and the second person is asked to reload. A decision posted from an out-of-date page (the stage has moved on) is also refused.
* **Everything is recorded:** each step is written to the request's history and to the audit trail.

### Workflow controls (vacancies)
* A vacancy can only be created from a **fully approved** recruitment request, and only **one** per request (also enforced by a unique database index).
* The department, number of positions and employment type are copied from the approved request and **can't be changed**, so a vacancy can't quietly grow beyond what the MD approved.
* The person who submitted a vacancy can't approve it, and stale or simultaneous decisions are refused (same version-stamp protection as requests).
* Automatic closing runs as **"System"**, and each closure is recorded in the history and audit trail.

A person can hold several roles. For example, a Head of Department is usually also an Employee. Each new phase adds its own rows to this table (raising requests, approvals, interviews, ...).

The screen rules are defined once in `src/Grsms.Web/Security/Policies.cs` (each policy is a permission since Phase 14) and **checked on the server for every request**. Hiding a menu link is only for tidiness: typing the address directly still gives *Access denied*.

### The roles

| Role | Purpose (from the specification) |
|---|---|
| **Super Admin** (Phase 14) | Full access to the whole system. The only role that creates HR Managers, Recruitment Officers, HODs and other privileged accounts, and sets permissions. The first administrator account (`SeedAdmin`) is a Super Admin. |
| **HR Administrator** | Maintains divisions, departments, units and sites, and everyday staff accounts (Employee, Interview Panel Member). Views the audit trail. |
| **HR Manager** | Approves requests, validates budgets, reviews candidates, authorises hiring. |
| **Managing Director** | Final vacancy and hiring approval, executive dashboard. |
| **Recruitment Officer** | Manages vacancies, candidates, adverts and interview schedules, and communicates with candidates. |
| **Head of Department** | Raises recruitment requests, shortlists, sits on interviews. |
| **Interview Panel Member** | Reviews interviews, completes scorecards. |
| **Employee** | Views internal vacancies, applies internally, tracks applications. |
| **Candidate** | External job seeker: registers on the portal and applies. Cannot be given to staff. |

### Workflow controls (job adverts)
* An advert can only be created or published while its vacancy is **Published** (approval complete) and before its closing date; it never runs past that date.
* Staff-only (internal) vacancies can only be advertised on the internal portal, so they never reach the public careers page.
* The public careers page shows only adverts that are live today, on the company-website channel, for a vacancy that is still open. Internal-portal adverts are shown only to signed-in staff (never to candidates or anonymous visitors).
* External channels (LinkedIn, Indeed, Jobberman, newspapers) are not connected to GRSMS: no passwords for those sites are stored. Recruiters post the copy-ready text themselves and record the link.
* Links recorded for a channel are only shown as clickable when they start with http:// or https://.

## Sign-in protection

| Protection | Setting |
|---|---|
| **Password policy** | At least 10 characters with upper case, lower case, a digit and a symbol, and at least 4 different characters |
| **Account lockout** | 5 wrong passwords lock the account for 15 minutes. A Super Admin or HR Administrator can unlock it early on the Users screen. |
| **Session timeout** | Signed out after 20 minutes without activity (`Security:SessionTimeoutMinutes`) |
| **Forced password change** | Accounts created or reset by an administrator must choose a new password at their next sign-in |
| **Deactivated accounts** | Cannot sign in by password or Microsoft 365. An existing session ends within 5 minutes. |
| **Role changes** | Take effect within 5 minutes (the session is re-checked against the database). **Permission changes** (Phase 14) take effect at once. |
| **Email confirmation** | Candidates must click the link in their confirmation email before they can sign in |
| **No account "fishing"** | Forgot-password and registration pages give the same answer whether or not the email has an account. On the sign-in page, *deactivated* or *please confirm your email* is only shown when the password is right; with a wrong password every account, and every non-existent email, gets the same *Incorrect email or password* (Phase 12) |
| **Rate limiting** (Phase 12) | The sign-in, registration, resend-confirmation, forgot-password and reset-password forms accept at most **10 posts per minute from one network address** (`Security:SignInAttemptsPerMinute`); more get *Too many attempts* (HTTP 429) and are logged. This slows password guessing spread over many accounts, which the per-account lockout cannot catch, and stops the email pages being used to flood inboxes. |

### Safety rules for administrators

To stop anyone being locked out of the system:
* you cannot deactivate your own account or remove your own Super Admin role;
* there must always be at least one active Super Admin (if none is left, the next start-up gives the role back to the `SeedAdmin` account);
* the Super Admin always holds every permission, so it can always repair a permission change;
* a user who is a department's HOD keeps the HOD role, and stays active, until another HOD is chosen for that department;
* only users with the Head of Department role can be chosen as a department's HOD.

## Web protections

| Threat | Protection |
|---|---|
| Cross-site request forgery (a malicious site submitting forms as you) | Every form carries an anti-forgery token, checked automatically on every POST |
| Cookie theft | Sign-in cookie is HttpOnly (JavaScript cannot read it), SameSite=Lax, and Secure (HTTPS only) outside development. The anti-forgery cookie is HttpOnly, SameSite=Strict and Secure outside development. |
| Injected scripts running in the browser | **Content-Security-Policy** (Phase 12): scripts, styles, fonts and images load only from GRSMS itself; no inline scripts or `onclick="..."` exist anywhere (behaviour lives in `js/site.js`), so an injected script would not run. Plug-ins are blocked, forms may only post to GRSMS (and Microsoft's sign-in page), and the `<base>` address cannot be changed. Inline `style="..."` attributes are allowed: they cannot run code. See `Security/SecurityHeaders.cs`. |
| Click-jacking | `frame-ancestors 'none'` in the policy, plus `X-Frame-Options: DENY` for older browsers |
| Other sites' windows reaching into GRSMS | `Cross-Origin-Opener-Policy: same-origin`; `X-Permitted-Cross-Domain-Policies: none` |
| Advertising the server software | No `Server` header |
| Spreadsheet formulas in report downloads | CSV values typed by users that start with `=`, `+`, `-` or `@` get a leading `'` (Phase 11) |
| Tokens in log files | Request logs record the path only, never the query string, so password-reset and confirmation links never reach the logs |
| Content-type sniffing | `X-Content-Type-Options: nosniff` |
| Leaking addresses to other sites | `Referrer-Policy: strict-origin-when-cross-origin` |
| Unencrypted traffic | HTTPS redirection, plus HSTS in production |
| Malicious redirects after sign-in | Only addresses inside GRSMS are followed |
| SQL injection | All database access goes through Entity Framework parameterised queries |
| Script injection (XSS) | Razor HTML-encodes all output, and email bodies encode user-entered text |
| Malicious uploads | Only PDF, Word, Excel, PowerPoint, PNG and JPG are accepted, up to 10 MB. The first bytes must match the file type, so a renamed program is refused. Files are stored **outside** the website folder under random names, are only reachable through a permission-checked download page, and are always sent as downloads (never opened inside the site). Each file's SHA-256 fingerprint is recorded. |
| Path tricks in file names | Folder parts of an uploaded name (`C:\fakepath\..\`) are stripped. Stored names are generated by the system and checked to stay inside the storage folder. |

### Security review (Phase 12, 4 November 2026)

Everything above was reviewed, and the code was checked for the usual web weaknesses. Results:

| Checked | Result |
|---|---|
| Sign-in messages | **Fixed:** a wrong password on a deactivated or unconfirmed account showed *deactivated* / *confirm your email*, revealing that the account existed (ASP.NET Identity checks those rules before the password), and did not count towards the lockout. Now answered and counted like any wrong password. |
| Pages open without sign-in | Only the sign-in, registration, password and email-confirmation pages, the public careers pages and the error page. Everything else needs sign-in (fallback policy), and every service checks the role and department again. |
| Inline scripts (needed for a strict CSP) | Three `onchange="..."` handlers moved to `site.js`. An automated test now fails if any main page gains an inline script or handler. |
| Redirects | After sign-in and from notifications, only addresses inside GRSMS are followed (`LocalRedirect` / `Url.IsLocalUrl`). |
| Downloads | Permission-checked, sent as attachments with `nosniff`; CSV export protected against formulas. |
| Packages | `dotnet list package --vulnerable --include-transitive`: no known vulnerable packages. |
| Secrets | Passwords and keys live in `.env`, user-secrets or environment variables (all git-ignored); none in source. Password hashes and tokens never reach the audit trail or logs. |
| Uploads, SQL injection, XSS, CSRF | As in the table above; no change needed. |

**Done in Phase 13 (production deployment):** the production set-up (`deploy/`) runs in **Production** mode (no detailed error pages) behind an HTTPS proxy, with only ports 80/443 open. The visitor's real address is read from the proxy's forwarded headers, **trusted only from the proxy's own network**, so the audit trail and the sign-in rate limit see real addresses and nobody can fake one. The developer Docker set-up stays in Development mode and must never be exposed to the network. See [21 Production deployment](21-production-deployment.md).

## Audit trail

Recorded automatically, with no developer action needed:
* **every** create, update and (soft) delete of any record, with the changed fields' old and new values;
* user account changes and role assignments;
* sign-in success (and method), failure, lockout, sign-out, password reset and password change;
* the user and IP address for each of these.

Passwords, password hashes and security stamps are never written to the trail.

**ATS decisions (Phase 15)** are in the trail like any other change. The score, band and rejection are stored on the application, and its history names *ATS (automatic screening)* as the actor; the trail row shows the applicant's own request, during which the screening ran. HR's confirm or override is recorded with the reviewer's name and note, and every ATS settings change is an `AtsSettings` row. The **full text of a CV is not copied into the trail**: it is already in the CV file, and copying it would spread personal data further. The trail records what the ATS read from it (`CvParsed`) and what it decided.

**Data protection (Phase 15).** Automatic rejections never reach the applicant without a person's decision, so no one is turned down by a machine alone. CV text stays on GREL's server unless the Super Admin switches on the optional Claude AI reader, which sends CV text to Anthropic and should be switched on only after GREL has approved that.

## Turning on "Sign in with Microsoft 365"

GREL staff can sign in with their work Microsoft account once IT completes these steps.

1. In the **Microsoft Entra admin center** → *App registrations* → **New registration**:
   * Name: `GREL Recruitment (GRSMS)`
   * Supported account types: **Accounts in this organizational directory only** (single tenant)
   * Redirect URI (Web): `https://<your GRSMS address>/signin-oidc`
2. Under **Certificates & secrets**, create a **client secret** and copy its *Value*.
3. Copy the **Directory (tenant) ID** and **Application (client) ID** from the *Overview* page.
4. Give the three values to GRSMS, for example on the server as environment variables:
   ```
   AzureAd__TenantId     = <Directory (tenant) ID>
   AzureAd__ClientId     = <Application (client) ID>
   AzureAd__ClientSecret = <client secret value>
   ```
5. Restart GRSMS. A **Sign in with Microsoft 365** button now appears on the login page.

**What happens at a staff member's first Microsoft sign-in:**
* if an account with the same email already exists (created by HR), the Microsoft account is linked to it;
* otherwise a new staff account is created with the **Employee** role, and an HR Administrator then adds any other roles;
* candidate accounts can never sign in this way.

Because the app registration is *single tenant*, only people in GREL's own Microsoft 365 can use this button.

Client secrets expire (12 or 24 months). Put a reminder in IT's calendar to renew it.
