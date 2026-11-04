# 4. Security and roles

## Who can do what (Phase 1)

| Screen / action | HR Admin | HR Manager | MD | Recruitment Officer | HOD | Panel Member | Employee | Candidate |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Sign in, change own password | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Home page | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (candidate home) |
| Departments, Units, Sites | ✅ | | | | | | | |
| Users & roles | ✅ | | | | | | | |
| Audit trail (read only) | ✅ | ✅ | | | | | | |

A person can hold several roles. For example, a Head of Department is usually also an Employee. Each new phase adds its own rows to this table (raising requests, approvals, interviews, ...).

The rules are defined once in `src/Grsms.Web/Security/Policies.cs` and **checked on the server for every request**. Hiding a menu link is only for tidiness: typing the address directly still gives *Access denied*.

### The roles

| Role | Purpose (from the specification) |
|---|---|
| **HR Administrator** | Maintains departments, units, sites and user accounts. Views the audit trail. |
| **HR Manager** | Approves requests, validates budgets, reviews candidates, authorises hiring. |
| **Managing Director** | Final vacancy and hiring approval, executive dashboard. |
| **Recruitment Officer** | Manages vacancies, candidates, adverts and interview schedules, and communicates with candidates. |
| **Head of Department** | Raises recruitment requests, shortlists, sits on interviews. |
| **Interview Panel Member** | Reviews interviews, completes scorecards. |
| **Employee** | Views internal vacancies, applies internally, tracks applications. |
| **Candidate** | External job seeker: registers on the portal and applies. Cannot be given to staff. |

## Sign-in protection

| Protection | Setting |
|---|---|
| **Password policy** | At least 10 characters with upper case, lower case, a digit and a symbol, and at least 4 different characters |
| **Account lockout** | 5 wrong passwords lock the account for 15 minutes. An HR Administrator can unlock it early on the Users screen. |
| **Session timeout** | Signed out after 20 minutes without activity (`Security:SessionTimeoutMinutes`) |
| **Forced password change** | Accounts created or reset by an administrator must choose a new password at their next sign-in |
| **Deactivated accounts** | Cannot sign in by password or Microsoft 365. An existing session ends within 5 minutes. |
| **Role changes** | Take effect within 5 minutes (the session is re-checked against the database) |
| **Email confirmation** | Candidates must click the link in their confirmation email before they can sign in |
| **No account "fishing"** | Forgot-password and registration pages give the same answer whether or not the email has an account |

### Safety rules for administrators

To stop anyone being locked out of the system:
* you cannot deactivate your own account or remove your own HR Administrator role;
* there must always be at least one active HR Administrator;
* a user who is a department's HOD keeps the HOD role, and stays active, until another HOD is chosen for that department;
* only users with the Head of Department role can be chosen as a department's HOD.

## Web protections

| Threat | Protection |
|---|---|
| Cross-site request forgery (a malicious site submitting forms as you) | Every form carries an anti-forgery token, checked automatically on every POST |
| Cookie theft | Sign-in cookie is HttpOnly (JavaScript cannot read it), SameSite=Lax, and Secure (HTTPS only) outside development |
| Click-jacking | `X-Frame-Options: DENY` |
| Content-type sniffing | `X-Content-Type-Options: nosniff` |
| Leaking addresses to other sites | `Referrer-Policy: strict-origin-when-cross-origin` |
| Unencrypted traffic | HTTPS redirection, plus HSTS in production |
| Malicious redirects after sign-in | Only addresses inside GRSMS are followed |
| SQL injection | All database access goes through Entity Framework parameterised queries |
| Script injection (XSS) | Razor HTML-encodes all output, and email bodies encode user-entered names |

Phase 12 (Security hardening) adds a full Content-Security-Policy, rate limiting on the sign-in page, and a review of all of the above.

## Audit trail

Recorded automatically, with no developer action needed:
* **every** create, update and (soft) delete of any record, with the changed fields' old and new values;
* user account changes and role assignments;
* sign-in success (and method), failure, lockout, sign-out, password reset and password change;
* the user and IP address for each of these.

Passwords, password hashes and security stamps are never written to the trail.

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
