# 23. System settings, email templates, backups and the audit monitor

Phase 16 adds the **Management** screens that let GREL run GRSMS without asking IT to change server files. This guide is for **Super Admins**, who change these, and **Admins**, who can see them read-only. The **audit monitor** is also for HR Administrators and HR Managers.

| Screen | Super Admin | Admin | HR Administrator / HR Manager |
|---|---|---|---|
| System settings | change | read-only (secrets never shown) | |
| Email templates | change | read-only | |
| Backups | see, back up now, download a database dump | | |
| Audit monitor | ✅ | ✅ | ✅ |

Every change on these screens is recorded in the audit trail.

---

## 1. System settings

**Menu → Management → System settings.** Four cards, each with its own **Save** button:

| Card | Settings |
|---|---|
| **Organisation and email wording** | Organisation name; the greeting at the top of every email (`{{Name}}` = the person's name); the signature at the bottom |
| **Email server (SMTP)** | Sender address and name, SMTP server, port, encrypted connection, user name, password. Below it: **Send test** |
| **Microsoft 365 sign-in** | Directory (tenant) ID, application (client) ID, client secret. Shows the redirect URI to register in Microsoft Entra |
| **Recruitment workflow** | Advertising window suggested for a new vacancy (14 days); the longest time a candidate may take to answer an offer (30); after how many days approvers are reminded (3); the knockout questions suggested for every new vacancy |

**Where each value comes from.** Each field shows a label:

* **screen**: saved on this page. It wins.
* **server configuration**: set by IT in the server settings (`appsettings.json`, environment variables or `deploy/.env`). It applies while the field here is empty.
* **default**: GRSMS's built-in value.

So IT can keep configuring the server as before ([06 Configuration](06-configuration.md)). To hand a value back to IT, empty the field and save.

**Secrets** (the SMTP password and the Microsoft 365 client secret) are stored **encrypted** with the server's data-protection keys. They are never shown again: the field only says *set* or *empty*. Leave the field empty to keep the current value, type a new one to replace it, or use **Remove the saved ...** to fall back to the server configuration. In the audit trail a secret appears only as *(secret, changed)*.

> If the server's data-protection keys are ever lost or replaced, saved secrets can no longer be read. GRSMS then writes a warning in its log, uses the server configuration, and the secret must be typed in again.

### Email

* With **no SMTP server** anywhere (screen or server configuration), GRSMS saves every email as a file on the server instead of sending it. The card says so.
* After changing the server, use **Send test** with your own address.
* **Port 465 (implicit SSL) is not supported** by the mail component GRSMS uses. Use **587 with the encrypted connection on (STARTTLS)**, which every common provider offers. For GREL's mailboxes at Hostinger, that is `smtp.hostinger.com`, port 587, user = the full mailbox address.

### Microsoft 365

"Sign in with Microsoft 365" appears on the sign-in page as soon as all three values are set, **without restarting GRSMS**. Register the redirect URI shown on the card in the Entra app registration first. Client secrets expire, so note the date in Entra and replace the secret here before it does.

### Recruitment workflow

* **Advertising window:** the closing date pre-filled on a new vacancy. The Recruitment Officer can still change it.
* **Longest time to answer an offer:** an offer can't be sent with a later reply-by date.
* **Remind approvers after:** a recruitment request or vacancy waiting this many days for a decision triggers a reminder, then one a week.
* **Suggested knockout questions:** pre-filled on every new vacancy (one per line, worded so that **Yes** is the answer needed). See [22 ATS screening](22-ats-screening.md#4-knockout-questions).

Changes apply to what happens next; existing vacancies and offers are not changed.

---

## 2. Email templates

**Menu → Management → Email templates.** These are the 11 emails **candidates and internal applicants** receive:

| Email | Sent |
|---|---|
| Application received | when someone applies |
| Not successful | when HR marks an application not successful, including confirming an ATS rejection (the reason is never sent) |
| Shortlisted | when an application is shortlisted |
| Interview or assessment invitation / rescheduled / cancelled | when an interview is scheduled, moved or cancelled |
| Offer of employment / Offer withdrawn / Welcome (offer accepted) | at each step of the offer |
| Reminder: reply to the offer / Reminder: interview tomorrow | the day before |

Staff notifications ("a request needs your approval", ...) keep their built-in wording.

**Editing:**
1. Click **Edit**, change the subject and message, and click **Preview**. The right-hand side shows the email with example values.
2. Click **Save**. Every email of that kind uses the new wording from then on.
3. **Reset** goes back to the built-in wording, which is also shown under *Built-in wording*.

**The rules:**
* Write **plain text**. A blank line starts a new paragraph; a single line break stays a line break.
* Use the **placeholders** listed on the page, e.g. `{{JobTitle}}`, `{{When}}`, `{{ReplyBy}}`. `{{Organisation}}` works everywhere. An unknown placeholder is refused when you save. A paragraph whose placeholders are all empty (e.g. `{{Instructions}}` when there are none) is left out.
* **Nothing typed becomes HTML.** `<b>`, links or scripts appear as typed. This keeps every email safe: nobody can make an email that runs code or links somewhere unexpected.
* The **greeting**, the button-style **link** and the **signature** are added to every email automatically (greeting and signature: System settings).

---

## 3. Backups

**Menu → Management → Backups** (Super Admin only). On GREL's production server, the backup service makes a backup every night and keeps 30 days ([21 Production deployment](21-production-deployment.md#5-backups)). This screen shows:

* the **latest database backup**, with a warning if it is more than a day old;
* every backup kept: the **database** dump and the **files and keys** archive of each night;
* **Back up now**: asks the backup service for an extra backup. It starts within a minute; reload the page to see it. If a request is not picked up within 10 minutes, the screen says so: check that the backup service is running.
* **Download** for database dumps. The *files and keys* archive can't be downloaded: it holds the server's encryption keys, so IT copies it on the server.

Every **Back up now** and every **download** is recorded in the audit trail (actions `BackupRequested`, `BackupDownloaded`).

**Restoring is not on this screen**, on purpose: it replaces the live data and must be done by IT on the server, following *Production deployment → Restore*.

> A database dump holds **all** recruitment data, including candidates' personal data. Keep downloads somewhere secure and delete them when they are no longer needed.

On a development PC there is no backup service, so the screen says *No backup folder on this server*.

---

## 4. Audit monitor

**Menu → Management → Audit monitor** (everyone who can read the audit trail). A daily check, worked out from the audit trail:

* **Worth a look** (alerts for the last 24 hours):
  * an account with **5 or more failed sign-ins** (password guessing, or someone who has forgotten their password);
  * a network address with **10 or more failed sign-ins** (one source trying many accounts);
  * any **account lockout**;
  * any change to **permissions, roles or settings**: check it was expected.
* **Figures for 24 hours and 7 days:** sign-ins, failed sign-ins, lockouts, record changes, permission and settings changes. Each tile opens the audit trail, filtered.
* **Failed sign-ins by account and by address**, the **most active users** and **changes by record type** (7 days), as bars. Names link to the audit trail.
* The latest **permission and settings changes** and **security events** (failed sign-ins, lockouts, password resets and changes).

Everyone can also see their own activity on **My audit trail** ([04 Security](04-security-and-roles.md#my-audit-trail-everyone)).
