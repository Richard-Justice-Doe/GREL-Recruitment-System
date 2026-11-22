# Change log

Work done outside the numbered phases, newest first. Each entry says what changed, which files, how it was checked, and anything still open. For the phases themselves, see the [phase log](phases.md).

---

## 22 November 2026: two-step sign-in in Administration, and required per person by the Super Admin

Requested by the user: "the two-step should be part of the administration, so that each user can enable it personally". The user chose both of the following.

* **Every member of staff** finds **Two-step sign-in** on the Administration page, under the new section **Your account**, and can turn it on (and, unless it is required, off) for themselves. Staff with nothing else to administer now see Administration with just this item. It is still in the account menu too.
* **The Super Admin requires it for chosen people:** on **Users**, the shield button next to a staff account. Green means required.
  * That person is sent to set it up at their next sign-in (or within 5 minutes if already signed in), and cannot turn it off.
  * Only the Super Admin sees the button; candidates are excluded.
  * The change is recorded in the audit trail.
* **Status badge on Users:** *2-step* (set up) or *2-step to set up* (required, not yet done).
* **The company-wide rule** for privileged roles (System settings → Sign-in security) is unchanged and still off by default.
* **Correction:** the emergency command in docs/21 now names the right table (`Users`, not `AspNetUsers`) and also clears the per-person requirement.

**Files:**
* `ApplicationUser.RequireTwoStep`: migration `UserRequireTwoStep`, **script `database/42_user_require_two_step.sql`**.
* The "two_step_required" sign-in claim (`AppSignInManager.cs`).
* `TwoStepRequiredFilter.RequiredFor`.
* `AccountController` (reads the flag fresh) and the TwoStep view wording.
* `UserAdminService.SetTwoStepRequiredAsync`, `UsersController.RequireTwoStep`, Users/Index (button and badges).
* `AdministrationController` ("Your account").
* Docs [21](21-production-deployment.md) and [04](04-security-and-roles.md).

**Checked:** new tests cover:
* staff turn it on and off themselves from Administration;
* the Super Admin requires it for one person through the Users page, who is then forced to set it up and can't turn it off;
* only a Super Admin may set the requirement.

The menu test now expects a panel member's Administration page to show only "Two-step sign-in". The full suite passes (328).

---

## 22 November 2026: two-step sign-in is off until the Super Admin switches it on

Requested by the user: "the authentication should be activated by the super admin only, because the software is in the test stage".

* **System settings → Sign-in security → Require two-step sign-in for privileged staff** now defaults to **off**. Nobody is sent to set up two-step sign-in or asked for a code until a Super Admin ticks it. Only the Super Admin can change System settings.
* People who have already set it up for themselves keep using their codes. While the rule is off they can also turn it off (account menu → *Two-step sign-in*).
* **Before go-live:** switch it on (go-live checklist in docs/21).

**Files:**
* `Domain/Settings/SettingCatalogue.cs` (default "false", help text);
* comments in `Roles.cs` and `TwoStepRequiredFilter.cs`;
* a test that the default is off;
* docs [21](21-production-deployment.md), [04](04-security-and-roles.md) and [41](41-security-assessment.md).

The full suite passes (326).

---

## 22 November 2026: the four High security findings fixed (docs/41 H1–H4)

Requested by the user after the security assessment: "fix them". The score goes from 78 to about 88 / 100.

**H1: the audit trail no longer exposes personal data.**
* For the personal record and everything in it, the audit trail records which field changed and who changed it, but writes `(personal data)` / `(changed)` instead of the value. "Everything in it" covers bank accounts, spouse, children, dependants, emergency contacts, beneficiaries and the signed declaration's snapshot, plus an employee's date of birth.
* Existing audit rows are cleaned by migration `AuditPersonalDataRedaction` (**script `database/41_audit_personal_data_redaction.sql`**; MySQL JSON functions, safe to re-run).

**H2: two-step sign-in (MFA).**
* **Required roles:** Super Admin, Admin, HR Administrator, HR Manager and Recruitment Officer must confirm every password sign-in with a 6-digit code from an authenticator app. The rule is in System settings → Sign-in security, on by default.
* **First use:** at their next sign-in they are sent to **Two-step sign-in**:
  1. scan the QR code (or type the key);
  2. confirm with a code;
  3. get 10 single-use recovery codes, shown once.
* **Other staff** can turn it on from the account menu.
* **Wrong codes** count towards the 5-attempt lockout and are audited.
* **Lost phone:** a recovery code, or an administrator resets it on **Users**. For privileged accounts only a Super Admin can, and never for their own account.
* **Microsoft 365** sign-ins rely on Microsoft's own MFA.
* **Sessions:** the 5-minute security re-check keeps the "two-step done" mark, so people aren't asked again.

**H3: virus scanning.**
* A **ClamAV** container scans every upload before it is stored: CVs, certificates, new-hire documents, photos and the letter signature.
* An infected file is refused with the virus name.
* **Fail closed:** if the scanner can't be reached, the upload is refused with "try again in a few minutes".
* Without `Antivirus:Host` (development, tests) the placeholder stays.

**H4: encrypted backups.**
* The backup service has its own image (MySQL tools + GnuPG) and encrypts each archive with GREL's **backup public key**, placed in `deploy/backup-key/`. The private key stays offline.
* The data-protection keys now have their own archive.
* The Backups screen downloads **only encrypted** database dumps, and shows a red warning while backups are unencrypted.
* Without a key, backups are still made (unencrypted) with a warning, rather than stopping.

**Files**
* *H1:* `Infrastructure/Persistence/AuditSaveChangesInterceptor.cs` and the new migration.
* *H2:*
  * `Domain/Security/Roles.cs` (`TwoStepRequired`), `Domain/Settings/SettingCatalogue.cs`, `Domain/Auditing/AuditLog.cs` (actions);
  * `Web/Security/TwoStepRequiredFilter.cs`, `Program.cs`;
  * `Web/Controllers/AccountController.cs`, `Web/Models/AccountViewModels.cs`, views Account/LoginWith2fa and TwoStep;
  * `UsersController` / `UserAdminService.ResetTwoStepAsync` and Users/Index;
  * the account menu; QRCoder 1.6.0 (MIT).
* *H3:* `Infrastructure/Storage/ClamAvScanner.cs`, `DependencyInjection.cs`, `VirusScanResult.Unavailable`, `AttachmentService`, `LetterSettingsService`.
* *H4:* `deploy/backup.sh`, the new `deploy/backup.Dockerfile`, `deploy/backup-key/`, `docker-compose.prod.yml` (clamav, backup build, key mount), `BackupStore`, Backups/Index.
* *Guides:* [docs/21](21-production-deployment.md) (encrypted backups, decrypt and restore, virus scanning, two-step sign-in, go-live checklist), [docs/04](04-security-and-roles.md), [docs/41](41-security-assessment.md).

**Checked:** 325 tests pass, 9 of them new:
* audit rows contain no personal values;
* two-step sign-in through the real pages: forced set-up, wrong and right codes, single-use recovery codes, audit rows;
* the admin reset rules;
* ClamAV against a fake scanner: clean, infected, unreachable, error;
* an upload refused while the scanner is down;
* backups list and download only encrypted dumps.

Outside the tests:
* the GnuPG commands in `backup.sh` were run with a test key and decrypted back;
* the compose file passes `docker compose config`;
* the ClamAV and backup images were **not** run here (Docker isn't running on this PC). Check them on the server with the go-live checklist items in docs/21.

---

## 22 November 2026: security assessment (docs/41)

Requested by the user: a complete security assessment of the application and codebase.

**Result:** overall score 78/100, with no critical findings and four high findings:
1. the audit trail exposes personal data to Admin and HR Administrator;
2. no MFA for privileged accounts;
3. no real virus scanning of uploads;
4. backups are unencrypted, include the key ring and can be downloaded through the app.

The report also has the role matrix, the database, file, endpoint and infrastructure reviews, attack simulations, compliance scores and a remediation plan with sample code: [docs/41](41-security-assessment.md).

No code was changed; fixes wait for the user's go-ahead.

---

## 22 November 2026: clean printouts (employee report starts at the GREL logo)

Reported by the user with a screenshot of the printed employee report. The printout had several problems:
* the app's top bar (page title, notification bell, settings, the HR Manager's name);
* a strip of the sidebar;
* the browser's date and title line;
* faint grey text, because it was printed from dark mode.

**What changed**
* **Every page, when printed or saved as PDF:** no sidebar, top bar, footer or messages, on a white background.
* **Always light colours when printing:** the theme switches to light for the print and back afterwards. The saved choice is unchanged.
* **The employee report has no page margin.** The browser therefore prints no date, title or address line, and the page starts at the GREL letterhead. The report keeps its own 12–14 mm padding.

**Files:** `wwwroot/css/site.css` (print rules, `@page emp-report`), `wwwroot/js/site.js` (light colours for printing), [docs/31](31-employees.md).

**Checked:** the report was printed to PDF with headless Chrome. It starts at the logo, with no app bar and no browser header. The full suite passes (318).

---

## 22 November 2026: each employee uploads their own profile picture

Requested by the user: "each employee should be able to upload their own profile picture". Until now only HR could.

* **My photo** is a new page, reached from the account menu (and a button on the Celebrations page). The employee can:
  * see their current picture (uploaded, or the verified onboarding photograph) or their initials;
  * **Upload photo** to add or replace it;
  * **Remove my photo** to go back to initials.
* **Same rules as HR:** JPEG or PNG, at most 3 MB, the usual file checks, and earlier photos kept as history.
* **Who can change what:** an employee can only ever change their own photo. HR can still change anyone's. Staff without an employee record are told to ask HR to register them.
* **Where it shows:** the directory, the profile, the organisation chart and birthday and anniversary cards. Visibility rules are unchanged.

**Files:**
* `Application/Employees/EmployeePhotoService.cs`: the employee may change their own photo; `MineAsync`, `HasPhotoAsync`.
* `Web/Controllers/MyPhotoController.cs`: Index, Photo, Upload, Remove.
* Views: `Views/MyPhoto/Index.cshtml`; the account menu (`_Layout`) and the Celebrations button.
* Guides: [docs/31](31-employees.md) and [docs/40](40-celebrations.md).

No database change.

**Checked:**
* a new service test: an employee changes their own photo and can't change a colleague's;
* the celebrations web test: an employee uploads through the page, sees it, and colleagues see it on the celebration card.

The full suite passes (318).

---

## 22 November 2026: every employee's birthday can be in celebrations

Requested by the user: "every employee should be able to see the employees' birthday".

**What was already true:** every member of staff sees everyone's birthday, company-wide, unless HR switches on **Department only**.

**The gap:** a birthday came only from the onboarding personal record. Staff imported or registered by hand had no date of birth, so they never appeared.

**Who can add it** (the user chose HR and the employee):
* **The employee:** the Celebrations page shows **Add your birthday** when GRSMS has none for them. They can **Change** or **Remove** it later. If it comes from their personal record, it is shown and can't be changed there.
* **HR:**
  * **Date of birth (for birthdays)** on the employee profile;
  * an optional **Date of birth** on **Register existing employee**;
  * an optional **DateOfBirth** column in the CSV import (the template has an example).

**Rules:**
* The date must be 15 to 100 years ago.
* The personal record's date of birth always wins.
* Colleagues still see only the day and month.

**Files:**
* Domain: `Employee.DateOfBirth`.
* Application:
  * `CelebrationService`: personal record, then the entered date; `MineAsync`, `SetMineAsync`, `SetForAsync`, `CheckDateOfBirth`; `ProfileCelebrations` and `MyBirthday`;
  * `RegisterEmployeeForm.DateOfBirth`;
  * `EmployeeService` (registration, validation, import column and template).
* Web:
  * `CelebrationsController.MyBirthday` and `.Birthday`;
  * views Celebrations/Index and Employees/Details, Register and Import.
* Database: migration `EmployeeDateOfBirth`, **script `database/40_employee_date_of_birth.sql`**.
* Guides: [docs/40](40-celebrations.md) and [docs/31](31-employees.md).

**Checked:** new service tests cover:
* the employee adds their own birthday and colleagues see it;
* the date checks;
* HR sets and clears it;
* a personal-record date can't be changed.

There is also an import test for the column and a bad date, and the web test covers the "Add your birthday" card and HR's field. The full suite passes (317).

---

## 22 November 2026: role descriptions match the approvals

Requested by the user after reviewing the list of approval processes. Some role descriptions still claimed approvals that no longer exist.
* **HR Manager:** no longer "authorises hiring decisions". The RO has approved hires alone since 9 Nov 2026. It now says what the role does decide: shortlisting against the HOD's view, and the probation outcome.
* **Managing Director:** the last approval of recruitment requests (no longer "final vacancy and hiring approval").
* **Recruitment Officer:** lists the request review, publishing vacancies and adverts, score validation, approving hires, offers and document verification.
* **IT Manager:** no advert approval since 17 Nov 2026.

Where:
* the Roles & permissions screen (`Roles.Describe`);
* code comments in `Roles.cs` and `HiringService.cs`;
* [docs/04](04-security-and-roles.md): role table, workflow rules, advert controls.

Text only; nothing about what people may do changed. The full suite passes (316).

---

## 17 November 2026: job request approval simplified; nothing is approved after the Managing Director

Requested by the user: "revise job request approval workflow". There were approvals after the MD's: the HR Manager approved the vacancy, and the HR Manager reviewed and the IT Manager approved each advert. They caused delays and duplicated responsibility.

**The new flow**

Department request → Department Head approval → **Recruitment Officer review** → **Managing Director approval (the last)** → Recruitment Officer creates the vacancy → Recruitment Officer creates and publishes the advert → applications → screening → interview → selection → offer → onboarding.

**What changed**

*The request (QF-81-11)*
* **Stage 2 is now the Recruitment Officer review** (it was the HR Manager review). This reuses the RO stage the system already had.
  * To approve, the RO ticks three checks: **Request verified**, **Vacancy details validated** and **Requirements confirmed**. The checks are recorded in the history.
  * The RO who approves becomes the request's **Recruitment Officer**, the process owner after the MD.
  * A request raised by the HOD goes straight to this review.
* **After the MD approves:**
  * the status is **Approved for recruitment**;
  * the RO gets **"Job request approved"**: *The Managing Director has approved the requisition. Your next action is to create the vacancy and publish the job advertisement.*;
  * the HR Manager is no longer asked or emailed.
* **The request page shows the hand-over.** It says *No further approvals are required*, the **process owner** (Recruitment Officer, by name) and the next action. The process guide now runs:
  * ✅ Department Head approval → ✅ Recruitment Officer review → ✅ Managing Director approval → 🔵 Vacancy creation → ⚪ Advertisement → ⚪ Candidate applications → Selection and hiring → Positions filled;
  * owner, next action and *Once done, the next step is*;
  * no approver after the MD.
* **RO dashboard and Action Center:** **Create the vacancy**, *approved by the Managing Director*, always **High** priority.

*Vacancies and adverts*
* **The RO publishes the vacancy** (**Publish the vacancy**) with no HR Manager approval. The history records *Published by the Recruitment Officer*. The HOD is emailed, with *Next: create and publish the job advertisement*.
* **The RO publishes the advert** (**Publish now** / **Schedule the advert**) with no HR review or IT approval.
  * Editing a published advert keeps it published; the history records *Edited while published*.
  * The RO can record external channels as posted or taken down.
  * The IT Manager can still take an advert offline.

*Supporting changes*
* Role and permission descriptions, the request stepper, the settings labels for the old targets and the vacancy reminder (now sent to the RO: "publish it") are updated.

**Records already in progress (nothing historical changes)**
* **Requests at the HR Manager review** finish there and then go to the MD. The status is shown as *HR Manager review (earlier workflow)*.
* **Vacancies pending HR approval** can be published by the RO. The HR Manager can still approve or return them.
* **Adverts awaiting HR review or IT approval** are listed under **Earlier review**. The RO can publish them now, and their reviewer can still return or reject them.
* All history entries are kept. Statuses are stored as text, so nothing is migrated: no database script.

**Files**
* Domain:
  * `RecruitmentWorkflow`, `RecruitmentEnums` (labels);
  * `VacancyWorkflow` (`CanPublish`), `VacancyEnums` (`PublishedByRecruiter`);
  * `AdvertWorkflow` (`CanPublish`), `AdvertEnums` (`EditedWhilePublished`);
  * `Roles`, `Permissions`, `SettingCatalogue`.
* Application:
  * `RecruitmentRequestService` (routing, checks, owner, notifications) and `ReviewDecisionDto` (the three checks);
  * `VacancyService.PublishAsync` (replaces `SubmitAsync`);
  * `AdvertService.PublishAsync` (`SubmitAsync` and `HrApproveAsync` removed; edits stay live; RO records channels; `MyWorkAsync`);
  * `ProcessGuideService` (new journey), `ActionCenterService`, `DashboardService`, `ReminderService`.
* Web:
  * `VacanciesController.Publish`, `AdvertsController` (`Submit` and `HrApprove` removed; `Publish` and channels open to the RO);
  * views: RecruitmentRequests/Details and _WorkflowStepper, Vacancies/Details and Form, Adverts/Details, Form and Index, Roles/Index.
* Guides: [08](08-recruitment-requests.md), [09](09-vacancies.md), [13](13-job-adverts.md) and [39](39-guided-workflow.md).

**Checked:** request, vacancy, advert, process guide and Action Center tests are rewritten for the new rules, and the earlier-workflow cases (HR review, pending vacancy, adverts in review) are still tested. The full suite passes (316).

**This replaces earlier decisions:** the HR Manager stage of 5 Nov 2026 (Phase 17), the HR Manager vacancy approval (Phase 3), and the HR review / IT approval of adverts (11 Nov 2026, docs/29 change 2).

---

## 17 November 2026: AI drafts the "Description of the post" on the recruitment request

Requested by the user: "AI is supposed to generate the job description here" (QF-81-11, section 3). This was planned in [docs/36](36-ai-hr-platform-plan.md), phase 3, but until now only the vacancy form had the button.

**What changed**
* **Draft the description with AI** panel above the formatted editor in section 3. It appears only when the AI feature *Job description drafts* is switched on.
* **What it uses:**
  * the position title, department and engagement type already on the form;
  * an optional grade, level and main responsibilities typed in the panel.
* **What it fills:** the formatted editor, under headings: Job purpose, Main duties and responsibilities, Requirements, Competencies and Key performance indicators. The requester edits the text and saves as usual; nothing is saved by the AI.
* **Another version** asks for a fresh draft.
* **Missing details:** if the department or position title is missing, the panel says which section to fill first.
* **Who can use it:** the same people who can raise a request. No personal data is sent.

**Files**
* `Application/Ai/AiWriters.cs`: `JobDescriptionDraft.DescriptionHtml()`. The AI's text is HTML-encoded.
* `Web/Controllers/AiController.cs`: `RequestDescription`.
* `Views/RecruitmentRequests/Form.cshtml`: the panel.
* `wwwroot/js/rich-text-editor.js`: the editor reloads when its hidden field changes.

**Checked:** a new web test in `AiWritersWebTests` covers:
* the panel, when the feature is on;
* the department and title messages;
* the formatted HTML, with the AI text encoded;
* no personal data in the prompt.

The full suite passes (315).

---

## 17 November 2026: reopening a cancelled onboarding or induction

Requested by the user: "if an induction is being cancelled it needs to be reactivated".
* **Reopen onboarding** on a cancelled onboarding (RO, `onboarding.manage`, a reason required).
* It returns to the stage it was in when cancelled: *Induction in progress* for an induction cancelled after activation. Before activation, the stage is worked out again from the facts.
* Nothing is lost: cancelling only set the status, so the letter, documents, tasks and induction carry on.
* **History:** *Reopened after cancellation* on the onboarding, and *Onboarding reopened* on the application.
* **Emails:** the new employee and the Heads of Department.
* **Why this is the only way back:** a new onboarding can never be started for the same application.
* `OnboardingService.ReopenAsync`, `OnboardingController.Reopen`, the form on the onboarding page. No database change.
* 314 tests pass.

---

## 17 November 2026: celebrations, birthdays and work anniversaries

Requested by the user: birthdays and service anniversaries visible across GREL, with automatic announcements. Guide: [docs/40](40-celebrations.md).

* **No manual entry:**
  * birthdays come from the date of birth in the onboarding personal record (day and month only, never the year or age; 29 February → 28 February);
  * anniversaries come from the date joined (1, 5, 10, 15 … highlighted).
* **Home widget:** today's celebrations and the next days, with photos or initials.
* **Action Center line:** "🎂 Birthdays today: 3 · 🎉 Work anniversaries today: 2 · View celebrations".
* **Celebrations page:** a banner (light confetti, off for reduced motion), a card per person, and colleagues' messages (one-click wishes or their own, up to 280 characters, on the day and 3 days after) that HR can hide or show again. Then the next 7 days.
* **Calendar** by month, with department and site filters.
* **Profile:** "Birthday: 15 November · in 45 days", the next work anniversary, and HR's *Don't celebrate (at their request)*.
* **Morning announcement** (scheduled job, once a day from the configured hour):
  * one notification to every member of staff (or only the person's department) listing the day's celebrations, and one email if switched on;
  * a personal greeting to each person celebrated.
* **HR's settings** (*Manage employees*): birthdays, anniversaries, notifications, emails, department only, messages, the hour.
* **Not included:**
  * photos in emails (they need a sign-in, so the page shows them);
  * birthdays of staff hired before GRSMS, who have no date of birth in GRSMS until self-service (Phase 29); their anniversaries are celebrated.
* **Database:** `database/39_celebrations.sql`.
* **Tests:**
  * dates and 29 February;
  * visibility, opt-out and department-only;
  * the once-a-day announcement with greetings and email;
  * messages and moderation;
  * the pages, widget, Action Center line and settings through the site.
  312 tests pass.

---

## 17 November 2026: probation initial meeting, date help and AI suggestions

Requested by the user: the meeting form said "Enter the date of the meeting (not in the future)" without saying which dates work, and AI should suggest dates.

* **Meeting date:** the picker only offers the allowed dates, from two weeks before the start up to today. It is pre-filled with a valid date. A hint states the window. When the employee hasn't started yet, the hint explains when the meeting can be recorded. The errors name the dates: "Enter the date the meeting was held: between 29 Nov 2026 and today (…)", or "The meeting can be recorded from …".
* **Objective due dates:** the picker is limited to the start up to the end of the probation plus the 3-month maximum extension, and the error names both dates.
* **Suggest objectives and dates with AI** on the form (new switch *Probation objectives* in AI settings; no personal data):
  * drafts 3–5 objectives with success indicators and 2–4 development plan lines from the job (title, department, description, requirements);
  * the model picks a month for each, and GRSMS turns it into a date inside the probation (not before the first week, not after the end), so every suggestion passes the form's checks;
  * development lines get "By month 2 (13 Feb 2027)";
  * only the Head of Department recording the meeting may ask, and the suggestions only fill the form (edit, then sign).
* **Files:** `ProbationWorkflow.MeetingDateWindow` and `LatestObjectiveDue`, `Application/Ai/AiWriters.cs` (`ProbationPlanDrafter`), `AiController.ProbationPlan`, `wwwroot/js/probation-ai.js`. No database change.
* 307 tests pass.

---

## 17 November 2026: employee directory and profile workspace

Requested by the user: a modern employee directory and profile, as in Workday, BambooHR and SuccessFactors. It builds on the Phase 28 employee module and reuses its data. Guide: [docs/31](31-employees.md).

* **Directory** (*Menu → Employees*): **Grid** cards (default) or a **List**.
  * Each card shows the photo or initials, name, number, status, employment type, position, department, grade, site, manager and **profile completion** (% and bar).
  * Each card has *View profile* and a menu: edit, documents, leave, performance, training, report.
  * Tabs, search and filters work in both views.
* **Profile workspace:**
  * a header with a large photo and the key facts (joined, years of service, reporting to, email, phone);
  * the **profile completion** (seven parts) with what is missing and *Complete profile*;
  * **pending actions** (missing parts; contract ended or ending in 30 or 90 days; probation decision overdue, ending in 30 days or initial meeting overdue; overdue onboarding tasks);
  * tabs: **Personal, Employment, Documents** (preview in the page), **Onboarding** (the 9-stage journey), **Performance** (probation), **Training, Leave, Organisation** (manager → employee → direct reports), **Timeline, Audit trail**;
  * a printable **Employee report**.
* **Photos:**
  * HR uploads, replaces or removes them (JPEG/PNG, up to 3 MB, the usual upload checks; earlier photos kept);
  * the verified onboarding passport photograph is used when none was uploaded;
  * same visibility as the record;
  * served with a private one-hour cache.
* **Not invented:**
  * Leave (Phase 30), appraisals (Phase 32) and training records (Phase 29) are planned modules not built yet, so those tabs say which phase brings them; Performance shows the probation.
  * Nationality isn't recorded in GRSMS, so the profile says so. Marital status shows *Married* when a spouse is given, otherwise *Not recorded*.
* **Who sees what:** personal details and documents for HR only (onboarding.view); the audit trail for people who may read it (audit.view).
* **Files:**
  * `Application/Employees/EmployeePhotoService.cs`, `ProfileCompletionService.cs`;
  * `EmployeeService.ProfileAsync` (team, probation);
  * `IPersonalRecordRepository.SummariesAsync` (what a record contains, without the data);
  * `Web/Models/EmployeeProfileViewModel.cs`;
  * the views `Employees/Index`, `Details`, `Report`, `_EmployeeAvatar`;
  * styles in `site.css`.
* **Database:** `database/38_employee_photo.sql` (`Employees.PhotoAttachmentId`).
* **Tests:**
  * photos: HR only, pictures only, visibility, removal, the onboarding fallback;
  * completion and alerts after a full onboarding, and for staff hired before GRSMS;
  * the directory, list, profile, report and photo upload through the pages.
  306 tests pass.

---

## 15 November 2026: guided workflow (G1–G4)

Requested by the user: a new user should see at once where a process is, what is done and pending, who acts, who approves next, how long it has waited and what comes next. Much of this was already built by docs/37 CR5; these steps fill the gaps. Guide: [docs/39](39-guided-workflow.md).

* **G1, the process guide on every record page:**
  * the current step is blue, done steps have green ticks, steps to come are grey;
  * a **workflow completion** bar and percentage, with the health badge (🟢 On track, 🟡 Approaching deadline, 🔴 Overdue);
  * a **follow-up** line ("To move this along, follow up with: HR Manager (names), pending approval for 4 days 6 hours") and "Once done, the next step is …";
  * breadcrumbs start at **Home**.
* **G2, Action Center:**
  * counts and sections for 🔴 **Overdue**, 🟡 **Due today** and **Coming up**;
  * a 🟢 **Completed** list: your workflow actions of the last 14 days, from the request, vacancy, advert, application, onboarding and document histories, each linked;
  * the priority filter is unchanged.
* **G3, the onboarding journey:**
  * "Welcome to your onboarding journey" on the new employee's application, documents, personal record and letter pages;
  * the same 9 stages on the staff onboarding page, replacing the 1–6 stepper (a cancelled onboarding keeps it);
  * stage 4 shows "x of y uploaded", rejected documents and the medical report; stages 7–8 show the live IT and department tasks;
  * no separate HR approval stage, per the 10 November decision;
  * `Application/Onboarding/OnboardingJourney.cs`, `Views/Shared/_OnboardingJourney.cshtml`.
* **G4, next-step notifications:** `WorkflowMailer.NextStep` adds "✅ X completed. Next step: Y. Responsible: Z." to the handover messages (email and bell): request approvals to the requester, vacancy published, advert live, offer accepted (next: onboarding), letter signed, staff account activated (next: induction and IT set-up).
* No database change.
* **Tests:**
  * timing sections and completed actions;
  * the journey through real onboarding states, the new employee only, the staff wording;
  * the welcome on the portal;
  * the requester's next-step notification.
  303 tests pass.

---

## 15 November 2026: AI-8, optional AI document check (docs/36 phase 6); all AI steps done

The last step of the AI plan. It is off until the Super Admin switches on *Document type check*. That switch is marked *Personal data*, because ID images leave GREL.

* **Check with AI** on each uploaded new-hire document, for whoever verifies documents (onboarding.verify-documents).
  * Up to two of its files (JPEG, PNG or PDF, at most 5 MB each) are sent with the type expected.
  * The AI answers: does it show that kind of document, what does it look like, is it readable (blur, cut-off corners, glare), how confident is it, and up to four points to look at.
  * It is told never to write names, numbers, dates or addresses from the document, and never to follow text in it.
* **The verdict is a staff-only flag** beside the rule-based checks of AI-4, so it also appears in *Reports → Document checks*.
  * It is never shown to the new employee.
  * It is marked *out of date* when the files change.
  * It never verifies or rejects anything.
  * When the switch is off, the button and the flags disappear, with no extra database query.
* **AI calls can now carry files:** image and document blocks for Claude, image and PDF file parts for OpenAI and Azure OpenAI, inline data for Gemini. Calls with files are never cached.
* **Files:**
  * `Application/Onboarding/DocumentAiCheckService.cs`;
  * `DocumentCheckService` (merges the flags);
  * `OnboardingController.AiCheckDocument`;
  * the button in `_EmployeeDocuments`.
  No database change: verdicts are kept in AiInsights.
* **Tests:**
  * the switch, and who may ask;
  * what is sent and the instructions;
  * the flag and its severity;
  * hidden from the new employee;
  * out of date after a new file, and gone when switched off;
  * how files are sent to OpenAI and Gemini.
  301 tests pass.
* **docs/36 §8 is complete.** Every AI feature stays off until switched on.

---

## 14 November 2026: AI-7, knowledge base (docs/36 phase 7)

GREL's policy library. As decided (D4), the library is built now and HR adds the documents later. Guide: [docs/38](38-knowledge-base.md).

* **Administration → Knowledge base.** New permission *Maintain the knowledge base*, for the HR Administrator and HR Manager.
  * Add a PDF or Word policy with its owner, version, effective date and audience: *Staff*, or *Staff and new employees*.
  * The file goes through the usual upload checks.
  * The text is read and split into sections at its headings.
  * Upload a new version (the old file stays as history), edit, archive or delete.
  * A scanned file with no text is kept and flagged *No text*.
* **Policies & procedures** in every staff member's menu: the library, each document's sections, preview and download, and a question box.
* **Search** runs inside GRSMS (BM25, headings count double, light stemming, common words ignored), not MySQL full-text search as first planned. The ranking is the same on MySQL and in the tests, and short words aren't lost. The index is rebuilt only when a document changes.
* **AI answers** (switch *Knowledge base answers*, no personal data):
  * built only from the 6 best sections, with sources cited as [1], [2] (document, version, section);
  * invented source numbers are dropped, and "found" is only shown when a real source is cited;
  * nothing is sent when no section matches;
  * with the switch off, the matching sections are shown instead.
* **Onboarding assistant:** with both switches on, it answers new employees' policy questions from the documents marked for them, with an **Open** button. Staff-only documents are never sent for a new employee. Staff asking on the onboarding page can draw on all documents.
* **Database:** script `database/37_knowledge_base.sql` (KnowledgeDocuments, KnowledgeSections). The app applies the migration at start-up.
* **Tests:**
  * splitting and headings;
  * HR-only changes and file types;
  * search ranking and audiences;
  * new versions and history, scanned files and delete;
  * new employees' access;
  * answers with checked citations, and no call when nothing matches;
  * the assistant using only new-employee documents;
  * the full page journey.
  297 tests pass.

---

## 14 November 2026: Google Gemini added to the AI providers

Requested by the user. *AI settings & usage → Provider* now offers **Google Gemini** next to Anthropic (Claude), OpenAI and Azure OpenAI. Every AI feature works with it unchanged, through the same gateway: switches, budget, log and personal-data rules.

* **Setting it up:** choose *Google Gemini*, paste the API key from Google AI Studio, and enter the model name (e.g. `gemini-2.5-flash`; check AI Studio for current names). The model name is required. *Test the connection* checks the key.
* **How it calls Gemini** (`Infrastructure/Ai/AiClient.cs`):
  * the Gemini API `generateContent` call;
  * the key goes in the `x-goog-api-key` header, never in the URL, so it can't end up in logs;
  * the instructions go as the system instruction;
  * structured answers use `responseJsonSchema`, the same schema as for the other providers.
* **Thinking models:** Gemini counts its "thinking" against the output limit, so 8,192 tokens are added on top of each feature's limit so short answers aren't cut off. Only tokens actually used are billed, and thinking tokens are counted as output in the cost log. Thought summaries are never shown as the answer.
* **Errors** are reported in plain words without the key:
  * a wrong key (Gemini answers 400 "API key not valid") and permission refusals;
  * quota limits and a model that isn't found;
  * answers cut off, or stopped by Google's safety filters.
* **Also fixed:** an empty answer list from the OpenAI or Gemini APIs now gives "The provider gave no answer" instead of an internal error.
* **Tests:** request shape, key header, thought parts left out, usage counts, eight error cases, and the model name required. 290 tests pass. No database change.

---

## 14 November 2026: AI-6, onboarding assistant (docs/36 phase 5)

A chat box that answers questions about one onboarding. It is off until the Super Admin switches on *Onboarding assistant* in *AI settings & usage*. It is marked *Personal data* because it sends the person's onboarding status.

* **Where:**
  * the new employee's application page (open);
  * *My new-hire documents* (folded to one line, because on a phone it comes before the checklist);
  * the RO's onboarding page (folded).
* **Facts from GRSMS, wording from AI.** Each question sends the onboarding's own facts:
  * stage, letter and personal record;
  * every document, with its status and why it was rejected;
  * due date, first day and the things to bring;
  * open tasks, and the next step from the process guide.
* **Who sees what:** the facts are built by the same services and visibility rules as the pages. The new employee gets only their own onboarding, and staff only one they may open. The HOD gets no personal documents.
* **What is never sent:** pay, and the new employee's own name.
* **How it answers:**
  * only from the facts;
  * it says when it doesn't know, and who to ask;
  * policy questions go to HR until the knowledge base (AI-7) is ready.
* **Buttons** (*Upload my Ghana Card*, *Read and sign my appointment letter*, *Review the Passport*): the model picks them by key from a list GRSMS builds, so the URLs never come from the model. Unknown keys are dropped, and the page accepts only same-site links.
* **Privacy:** no cache, and the chat is not stored. The page sends the last 6 turns, the question is limited to 500 characters, and every call is logged and budgeted like the other AI features.
* **Files:**
  * `Application/Ai/OnboardingAssistant.cs`;
  * `AiController.Onboarding`;
  * `Views/Shared/_OnboardingAssistant.cshtml` and `Web/Helpers/AssistantBox.cs`;
  * `wwwroot/js/onboarding-assistant.js`, and chat styles in `site.css`.
  No database change.
* **Tests:**
  * switched off means nothing is sent;
  * own onboarding only;
  * no pay and no own name in what is sent;
  * buttons only from GRSMS keys;
  * staff and HOD visibility;
  * the endpoint, and the box appearing only when switched on.
  281 tests pass.

---

## 14 November 2026: change request C6, usability pass and mobile check (docs/37 CR5.4)

The last step of docs/37. Look and flow only: no database change, and no change to who can do what.

* **One record header** on the request, vacancy, advert, application, interview, hiring case, onboarding, probation, employee and personnel file pages:
  * the title with its status badge, the same size on every page (the employee's badge moved into the title);
  * the reference and context below;
  * the actions on the right, with *All …* (back to the list) first.
  * On a phone the back button hides, because the breadcrumb above already leads back (and returns to the list as you left it). The other header buttons get smaller. The candidate portal keeps its back button, since it has no breadcrumb.
* **One primary button per page.** Buttons that only jump to a form further down are now outline buttons with a ↓ arrow, so the form's own button is the one primary action:
  * *Go*, *Review the draft*, *Check and send*;
  * *Open*, *Correct* and *Validate the scores*;
  * *Add a document*.
  On phones the floating *Go* button stays solid.
* **Other secondary actions are outline buttons too:**
  * the vacancy's *Shortlisting*;
  * the request's attachment *Upload*;
  * the direct *Approve hire* next to *Proceed with this candidate*;
  * the second *Start onboarding* in the offer card;
  * the department's *Edit*;
  * the new employee's *Upload my new-hire documents*, while the appointment letter waits to be signed.
* **Checked automatically:** every record page that a web test opens, in every state the tests reach, may show at most one filled button. The per-document buttons inside the folded checklist rows and buttons in pop-up dialogs don't count. See `tests/Grsms.Tests/Infrastructure/PrimaryActionCheck.cs`, which runs through `GrsmsWebFactory.NewClient`, plus a unit test of the check.
* **Mobile check of every record page at 390 px:** no page scrolls sideways (wide tables and tab rows scroll inside their own box). Fixed:
  * **"Your next step"** no longer leaves an empty band under its button on phones. The space is kept only when the floating *Go* button is there.
  * **The process guide** keeps its one-row timeline on phones instead of 7–12 stacked rows. The steps are sized so a whole number fits, starting at the step before the current one, so no label is cut. The status boxes sit in two columns. The guide is about half as tall.
  * **Tab rows** (Documents / Checklist / … / Everything) fade at the right edge as a hint that they scroll, and keep the chosen tab in view.
* Breadcrumbs on every staff page already existed (docs/32). The header adds no database queries.
* 278 tests pass.

---

## 14 November 2026: change request C5, remove everything about money (docs/37 CR1)

Per decision D1: "remove anything about money". Budget codes on requests are removed too. The employee's bank account is kept, and so are the advert benefits text and the budget approval document.

* **Vacancies:** the salary range and "show on adverts" are gone from the form and the vacancy page. They are no longer validated or saved.
* **Adverts:** the default advert text has no salary line. The AI advert writer receives no salary and is told never to mention pay.
* **Hiring:**
  * no proposed salary on the draft or approval form;
  * no advertised-salary pill and no salary column in the hiring list;
  * offers have no salary field, and any salary that is posted is ignored.
* **Letters:** the offer and appointment letters, including the PDFs, show *Remuneration: In line with GREL's conditions of service* instead of an amount. The default notice term no longer mentions "salary in lieu".
* **Candidate portal and onboarding:** no salary on the offer line or the onboarding page. The personnel file's offer and appointment records have no amount.
* **Requests:** no budget code and no estimated annual cost on the request page.
* **Grades:** no pay band on the form or the list. "Salary grades" is now "job grades".
* **Reports and CSV exports:**
  * the requests report has no cost column, KPI or total;
  * the offers report has no salary column and no average-salary KPI.
* **Old values stay in the database, hidden.** No migration and no script.
* **Checks:** tests updated (the salary is ignored and never shown). 277 tests pass.

---

## 14 November 2026: a friendlier onboarding documents checklist

Requested by the user: "make it nice and lively; the user has to scroll down". It affects both the RO's onboarding page and the new employee's portal. Only the look changed: every action works as before.

* **Progress bar** ("14 of 18 complete") and **filter chips**:
  * All;
  * **Needs your action**;
  * Waiting for review / Being checked;
  * Rejected;
  * Missing;
  * Verified / Accepted.
* **Groups fold** (Personal, Education, …), each showing "4/6 done", or a green tick when complete. A group opens by itself only when something in it needs attention.
* **One compact row per document:**
  * a coloured status icon: ✔ verified, ⌛ waiting, ✖ rejected, ⬆ missing;
  * the name, number of files, any checks to look at, and "Your action" when it waits for you;
  * the status badge.
  * Click the row to see the description, files (with Preview), actions and history.
* **Only the first document waiting for you opens by itself.** New employees also see their rejected documents open. After an upload the page reopens that document and highlights it.
* "Same file as …" remarks are merged into one line, and the duplicated "valid until not given" line is gone.
* No database change. 277 tests pass.

---

## 14 November 2026: change request C4, document preview (docs/37 CR4)

* **Preview** next to every PDF, JPEG, PNG and Word (.docx) file, in:
  * the application (CV and documents), for staff and the candidate;
  * the recruitment request's attachments;
  * the candidate profile;
  * onboarding documents (new employee and RO verification);
  * personnel files.
* It opens a full-screen window without downloading:
  * **PDF** (including scans): page by page, previous / next, zoom in / out, fit to width. It uses PDF.js (Mozilla, Apache-2.0, version 4.10.38) served by GRSMS itself from `wwwroot/lib/pdfjs`, so nothing is loaded from the internet, and it works under the site's content security policy.
  * **Pictures:** zoom in / out, rotate, actual size.
  * **Word:** converted on the server to simple HTML (headings, text, bold / italic, lists, tables, pictures; layout approximate). Text is encoded and sanitised, and files with DTDs or that expand too much are refused.
  * **Download** stays one click away; Esc or ✕ closes.
* **Security:** the preview uses the same download address with `?view=inline` or `?view=html`, so every permission check and the "opened" log apply exactly as for downloads. Previews are never cached. Other file types are still only downloaded.
* No database change. 277 tests pass. The PDF viewer was also checked in Chrome under the site's security policy.

---

## 14 November 2026: change request C3, Action Center and the guide on every candidate page (docs/37 CR5)

* **Menu → Action Center** (every user):
  * **Staff:** everything waiting for you, across requests, vacancies, adverts, screening, shortlisting, panel invitations, scorecards, score validation, hiring, onboarding (letters, documents, activation, induction) and probation. All in one list, **most urgent first**:
    * **High:** overdue against its workflow target, or due today;
    * **Medium:** past 75 % of its target, or due within 2 working days;
    * **Low:** the rest.
    * Steps without a target become Medium after 3 working days and High after 7.
  * Each row shows what to do, the record, since when, why it has that priority (e.g. "Overdue: target 2 working days, waiting 5") and the expected date, with a button to the page. Filter by priority.
  * **Candidates and new employees:** their next steps (offer, documents, letter, personal record).
  * Built from the same queries as Home's *Your to-dos*, which now links to it ("See them all, by priority").
* The **process guide** (C2) is now also on the **interview**, **hiring case** and **onboarding** pages: the candidate's whole journey, next action, owner, waiting time against the target, expected date and last action. On the onboarding page it sits above the detailed onboarding steps.
* No database change. 275 tests pass.

---

## 13 November 2026: change request C2, the process guide (docs/37 CR5)

* At the top of every **recruitment request**, **vacancy** and **application** page, a guide that answers *where is this, what's next, who must act, since when, is it late*:
  * **Timeline:** ✔ done (with the date), ● current, ○ to come, ✖ stopped.
    * Requests and vacancies: request submitted → HOD review → HR Manager review → MD approval → vacancy prepared → vacancy approved → advertised → applications → selection and hiring → positions filled.
    * Applications: applied → screening → interview → hire approval → offer → onboarding → employee.
  * **Next action**, e.g. "Waiting for the HR Manager's approval", "Panel members to submit their scorecards", "Waiting for the candidate to accept or decline the offer (reply by …)".
  * **Current owner**, with the people's names, and the **next approver**.
  * **Pending for**, e.g. "5 days 9 hours", with **On track** (green), **Approaching the target** (amber, from 75 % of it) or **Overdue** (red). The targets are the workflow targets in *System settings*; steps without a target show no colour.
  * **Expected by** (start plus the target in working days, or the reply-by / closing / document deadline) and the **last action** (what, who, when) for following up.
* Worked out from the existing statuses and histories: no database change. It replaces the old progress steppers on these pages; the action banner (*Your next step*) stays. On narrow screens the timeline scrolls inside its card, and on phones it stacks.
* Next: the interview, hiring case and onboarding pages and the **Action Center** (C3).

---

## 13 November 2026: change requests C1, scorecard reasons and panel replacement (docs/37)

* **Docs/37:** impact analysis and plan for the five change requests of 13 November:
  1. remove anything about money;
  2. scorecard reasons;
  3. panel replacement;
  4. document preview;
  5. guided workflow and Action Center.
  Decisions in §8.
* **A reason for every score** (optional, up to 1,000 characters): a box under each criterion on the scorecard. Submitting without reasons still works. The reasons show:
  * on the session page with each panel member's scorecard;
  * on the hiring case with the validated scores;
  * in the justification drafted when the RO proceeds with a candidate;
  * in the AI interview summary's input.
  The scale stays 1–5.
* **Panel management** (interview page → *Manage the panel*, RO or HR Manager, before the session):
  * replace **any** member, not only one who declined. A reason is required unless they declined; the replacement is invited, and the member replaced is told they are no longer needed;
  * **add a substitute**;
  * **send the invitation again** to someone who hasn't replied;
  * every change is kept in **Panel changes** (original, replacement, reason, who, when).
* Database: `database/36_scorecard_comments_panel_changes.sql`. 269 tests pass.

---

## 13 November 2026: AI-5, interview and CV summaries; CV reading through the AI settings (docs/36)

These features send **personal data** to the AI provider. Each has its own switch in *AI settings & usage*, off by default.

* **Hiring case → AI interview summary** (*Interview summaries*):
  * contents: overview, strengths, weaknesses, a recommendation (hire / with reservations / do not hire / needs more evidence) with its reason, and what to check before an offer;
  * built only from **validated** scorecards (scores per criterion, recommendations, comments); panel members are called "Panel member 1, 2…"; protected characteristics are excluded in the instructions.
* **Application → AI CV summary** (*CV match summaries*): fit (strong / possible / weak), strengths, weaknesses, requirements the CV doesn't show, and soft skills, from the CV text and GRSMS's own screening results. The ATS score and rules are unchanged.
* Both summaries:
  * a Recruitment Officer or HR Manager clicks **Write the summary** (**Write again** later);
  * kept with the record; readable by whoever may open the case or application, including the HOD of the department;
  * never shown to the candidate;
  * advice only: nothing in GRSMS acts on them.
* **CV reading** (*CV reading* switch): when on, every CV is read by the provider chosen in AI settings, and now also lists **soft skills**; each read is logged and counted in the budget. When off, the earlier optional Claude reader (ATS settings) works as before, and GRSMS's own reader is still used when AI fails.
* Database: `database/35_ai_insights.sql` (new table `AiInsights`).

---

## 13 November 2026: AI-4, automatic document checks (docs/36; rules only, no AI)

* **Valid-until dates** for the Ghana Card, passport biodata page, non-citizen card, work permit and residence permit:
  * the new employee can enter the date when uploading, or later; the RO can set or correct it;
  * flagged *expired*, *expires soon* (within 90 days) or *not given*.
* **Same file twice:**
  * a file with the same fingerprint (SHA-256) uploaded for two documents is flagged;
  * a file also uploaded by **another new employee** is flagged *Act now* (staff only; the new employee never sees remarks about other people).
* **Low resolution:** photos and scans under 1,000 pixels on the longer side are flagged as possibly unreadable. The size is read from the file header; no image library and nothing sent out. True blur detection is not done.
* Remarks appear under each document (new employee and RO) and in the new **Reports → Document checks** (HR with *Open personnel files*). The RO still verifies or rejects every document.
* Database: `database/34_document_checks.sql` (`EmployeeDocuments.ExpiresOn`, `Attachments.ImageWidth` and `ImageHeight`; all nullable; files uploaded earlier have no size and are not checked for resolution).

---

## 13 November 2026: AI-3, executive dashboard (docs/36)

* **Menu → Executive dashboard** (permission *Open the executive dashboard*, `executive.view`: MD and HR Managers by default, added automatically at start-up; the Super Admin can give it to others in *Roles & permissions*).
  * **Recruitment:** open vacancies, positions still to fill, applications received, offers accepted, average time to hire, offer acceptance, approval steps on time.
  * **Onboarding and probation:** in progress, documents overdue, starting in 30 days, probations in progress, ending in 30 days, past their end date.
  * **Workforce:** headcount, joined this quarter, on probation, contracts ending in 90 days.
  * Counts are compared with **the same point of the previous quarter** (a fair comparison part-way through a quarter), with coloured arrows.
  * **Departments:** headcount, positions to fill and vacancy rate; the highest vacancy rate is highlighted.
* **AI summary** at the top, if *Executive dashboard summary* is switched on in AI settings:
  * written only from the totals on the page (no names), and told never to invent a number;
  * answered from the cache until the figures change; **Rewrite** asks for a new version.
* Every figure is computed by GRSMS from the live records. No database change.

---

## 13 November 2026: AI-2, interview guides (docs/36)

* **Vacancy page → Interview guide**: the questions the panel should ask, in five categories (technical, behavioural, situational, competency, practical assessment), each with *what a strong answer shows*.
  * The RO or HR Manager writes them, or clicks **Draft questions** (AI, from the vacancy's description and requirements; no personal data). The draft appears in the form, not saved; edit, add or remove, then **Save the guide** (replaces the previous one).
  * HODs and others who can see the vacancy read it. **Panel members see it on the interview page** (folded); applicants never do.
  * The prompt forbids questions about age, family, religion, ethnicity, health or politics.
* Database: `database/33_vacancy_interview_questions.sql` (new table; the Docker deployment applies it). Switch on in *AI settings & usage → Interview question sets*; writing by hand works without AI.

---

## 13 November 2026: AI-1, job descriptions and adverts drafted with AI (docs/36)

* **Vacancy form → "Draft the job description with AI"**:
  * Inputs: job title and department from the form, plus optional grade, level and main responsibilities.
  * The preview shows the job purpose, duties, competencies, KPIs and skills matrix, plus the requirements, with a confidence level based on how much was given.
  * **Use this draft** fills the job description, minimum education, field of study, years of experience, skills and certifications. The RO edits and saves as usual.
* **Advert form → "Write the advert with AI"**:
  * Four versions from the vacancy (requirements kept exactly as approved; benefits only if typed): careers portal (the advert text), job boards and newspapers, an internal notice for staff, and a LinkedIn post.
  * New optional advert fields hold the three extra versions (also editable by hand). Jobberman, Indeed and newspapers copy the job-board version; LinkedIn its post; the internal notice is copy-ready on the advert page.
  * HR review and IT approval unchanged.
* Prompts include GREL's context and fair-hiring rules (no age, gender, religion, ethnicity… requirements; nothing invented). No personal data is sent. Switch both on in *AI settings & usage → AI features*.
* Database: `database/32_advert_channel_versions.sql`, three nullable columns on JobAdverts; the Docker deployment applies it automatically.

---

## 13 November 2026: AI platform plan (docs/36) and step AI-0, the AI foundation

* **docs/36:**
  * Analysis of the eight-phase AI brief against GRSMS. Phase 1 (CV screening) is largely built already; phases 2-8 build on existing data.
  * Proposed architecture, database changes, security rules (AI advises, people decide), and a plan in steps AI-0 to AI-8.
  * Decisions: provider chosen in settings; personal data allowed per feature switch; the plan's order; knowledge-base library now, documents later.
* **AI-0:**
  * **Administration → AI settings & usage** (Super Admin edits, Admin views):
    * main switch;
    * provider: Anthropic (Claude), OpenAI or Azure OpenAI;
    * encrypted API key, model or deployment, Azure endpoint;
    * time-out, monthly budget and prices, requests per person per minute, "keep prompts" (off);
    * one switch per AI feature, saying what it sends and marked when it sends personal data;
    * **Test the connection**;
    * this month's usage and cost, plus the latest requests.
  * **One gateway for every AI call** (`Application/Ai/AiGateway.cs`):
    * checks the switches, rate limit and budget;
    * caches answers of features that send no personal data (7 days); never caches personal data;
    * asks for a strict JSON shape and checks the answer;
    * logs counts, cost, time and outcome (not the prompt, unless switched on for troubleshooting; cleared after 30 days);
    * turns every failure into a message, so pages keep working without AI.
  * Provider client: `Infrastructure/Ai/AiClient.cs`, using the Anthropic SDK already in GRSMS, and the REST API for OpenAI and Azure OpenAI.
  * New tables `AiRequestLogs`, `AiCacheEntries`: run `database/31_ai_foundation.sql` (the Docker deployment applies it automatically). They are not in the audit trail; changes to AI settings are.
* No existing feature changed; the existing optional Claude CV reader is untouched until AI-5. Tests: gateway (switches, cache, budget, rate limit, failures, logging), OpenAI and Azure requests (offline), the settings page.

---

## 13 November 2026: offer drafts and PDF letters (docs/35 Wave 1, docs/26 Phase 24)

* **Offer drafts:**
  * Approving a hire drafts the offer: reference number, approved salary and start date, reply-by date (System settings, 7 days, never after the start) and default terms (System settings).
  * The RO can **Save draft** while checks are pending, **Preview letter**, then **Send offer** with one click (same number).
  * Candidates, reports and the position count ignore drafts. Hires approved earlier: *Save draft* creates one.
* **PDF letters:** **Download PDF** on the offer letter (candidate and staff) and the appointment letter (new employee and staff). The PDF has the same layout and letterhead as on screen; drafts are marked *DRAFT*; the signed appointment letter includes the electronic signature line.
  * Printed by headless Chromium (`Infrastructure/Documents/ChromiumPdfRenderer.cs`) from a self-contained page (`Helpers/LetterPdfBuilder.cs`, `Views/Shared/_LetterPdf.cshtml`).
  * The **Docker image now installs `chromium` and fonts** (about 150 MB larger), so the next server rebuild takes a few minutes longer.
  * Without a browser the buttons are hidden and printing from the browser still works.
* Not done, as decided later or left for later: letters are still not frozen as HTML at sending (the offer's fields can't change after sending anyway); PDFs are not attached to emails (the email links to the letter).
* No database change. Tests: draft lifecycle, preview, PDF downloads and access, and a real Chromium render where a browser is installed.

---

## 12 November 2026: Letterhead & signature page

* **Administration → Letterhead & signature:**
  * address, telephone, email, website, signatory name and title, plus an optional scanned signature (PNG or JPEG, ≤ 1 MB, never public);
  * a preview of the letter's top and signature.
* The offer and appointment letters read them (`_LetterHead`, `_LetterSignature`); with nothing set they look as before.
* The settings are kept in the System settings table, but not shown on that screen. No database change. 242 tests pass. Guide: [23](23-system-settings.md).

---

## 12 November 2026: GREL logo

* The GREL mark replaces the "G" tile in the sidebar, on the sign-in pages and on the careers site, and is the browser-tab icon (`wwwroot/images/grel-mark.png`, `grel-icon-32.png`).
* **Letterhead** (`Views/Shared/_LetterHead.cshtml`): the full logo with "Ghana Rubber Estates Limited · Human Resources" and a green rule, at the top of the **offer letter** and the **appointment letter**, on screen and when printed (`grel-logo.png`).
* No database change. 241 tests pass.

---

## 12 November 2026: workflow targets report (docs/35 Wave 1)

* **Reports → Workflow targets:** every wait at every approval step, the candidate's offer answer and each document check, in working days against its target: on time, late, waiting, or waiting past target (listed first so they can be chased). Percentage on time per step, with averages.
* **System settings → Workflow targets (working days):** HOD 2, HR 2, MD 3, vacancy 2, advert 2, offer answer 5, documents 3 (the decision of 12 November), editable by the Super Admin.
* Worked out from the existing history: no database change. 241 tests pass. Guides: [20](20-reports.md), [23](23-system-settings.md).

---

## 12 November 2026: interview panel replies and quorum (docs/35 Wave 1)

* **Panel replies:** each member accepts or declines the invitation (on the session page and in *My tasks*); a decline needs a reason.
* **Quorum:** at least 3 members, including the HOD (or a same-department delegate) and an HR Manager. Scheduling checks it, and score validation waits until it is met on the day.
* **Declines:** the scheduler, the recruiter and the HR Managers are alerted. The RO or HR Manager replaces the member, who is invited and checked for clashes.
* Switch: *System settings → Workflow → Enforce interview quorum* (on). Sessions scheduled before this change are not held to quorum.
* Database: `database/30_interview_panel_replies_and_quorum.sql`, which the Docker deployment applies automatically. 239 tests pass. Guide: [16 §2a](16-interviews.md).

---

## 12 November 2026: onboarding document deadlines (Phase 27 stage C) and a site filter (docs/35 Waves 0-1)

* **Document deadlines:** reminders to the new employee every 3 working days while something is outstanding; a last reminder on the due date, copied to the RO; an overdue notice once the day after, with an *Overdue* badge on the onboarding page and list; a daily digest for each RO of the documents to verify; the RO may extend the deadline once (≤ 14 days, with a reason). Phase 27 is now complete.
* **Employees:** filter by site.
* No workflow or permission changed; no database change. 235 tests pass.

---

## 12 November 2026: six quick wins from the effectiveness audit (docs/34)

* **Interview clash check:** a warning when a panel member or the candidate is already booked; *Schedule anyway* to override.
* **Contract expiry alerts** at 90 / 60 / 30 / 7 days to HR, the HOD, the line manager and the employee.
* **Employee timeline** on the employee page: the whole story from application to the latest job change.
* **Talent pool** (Candidates tab): past applicants not hired, searchable; invite one to an open vacancy.
* **Organisation chart** (Employees): from line managers, by department.
* **Job alerts** (My profile, opt-in): an email when a matching vacancy is published. New table: run `database/29_job_alert_subscriptions.sql` (after 26-28) before deploying.
* No workflow, approval chain or permission changed. 232 tests pass. Details: [34 §10](34-effectiveness-audit.md#10-progress-quick-wins-12-november-2026).

---

## 11 November 2026: emails no longer slow pages down

* **Found** in the log of 10 November: each email through GREL's server (smtp.hostinger.com) took 4-5 seconds and the person who clicked waited for every one: applying for a job 18.5 s, approving a request 17.2 s, submitting a vacancy 7.0 s, publishing an advert 5.6 s. The server answers in about 0.5 s; the rest is encryption, sign-in and Hostinger's processing.
* **Fix:** with a mail server set, emails are queued and sent by a background sender (`Infrastructure/Email/EmailQueue.cs`): the action returns at once. 30-second limit per email (was .NET's 100 s), two retries, every outcome logged. **Send a test email** still waits and reports the real result. No `.eml` change (no server set = files, immediate).
* **Tests:** `EmailSendingTests`, with a small fake mail server that answers slowly: three emails are handed over in well under a second and then delivered by the background sender; the test-email button waits and reports a failure when the server is gone. 222 tests pass.
* **Note:** the queue is in memory; an email still waiting when GRSMS stops is sent during shutdown (30 s) or logged as not sent.

---

## 11 November 2026: clearer menu, breadcrumbs, action cards (docs/32, priority 2)

* **Menu** in groups named after what is in them: **Recruitment**, **Candidates**, **Hiring**, **Employees**, then Reports and Administration. Same pages and addresses.
* **Breadcrumbs** at the top of every staff page (*Recruitment › Vacancies › VAC-2026-0012*) and every administration screen (*← Administration › Divisions*).
* **Home page:** action cards (requests awaiting your approval, vacancies awaiting publication, applications received, interviews this week, offers awaiting the candidate) replace the KPI charts; 55 → 39 database queries.
* **Forms:** a red `*` on every required field. **Buttons:** approve green, return amber, reject / withdraw red, everywhere.
* No business rule, permission, URL or data changed. Details: [32 §13](32-ux-navigation-performance-audit.md#13-progress). 217 tests pass.

---

## 11 November 2026: keeping the user's place, faster pages (docs/32, priority 1)

* **Lists and pages keep your place:** back to a list returns to the same page, search, filters and tab; after Approve, Save, Return and the like the page comes back where you were, with the same sections open; tables keep their page and search.
* **Faster pages:** the menu numbers are kept for 30 seconds per user (your own actions refresh them at once; the bell is always live), about 8 fewer database queries on every page.
* **Employees and Users & roles** are searched and paged on the server (50 per page).
* **Slow database commands are logged** (`Database:SlowQueryMilliseconds`, 500 ms) to find what is slow on the real server.
* No business rule, permission, URL or data changed. Details and commits: [32](32-ux-navigation-performance-audit.md#13-progress). 214 tests pass.

---

## 11 November 2026: Phase 28, the employee master record

* **What:** every employee now has a record (staff number, job, grade, line manager, status) with a job history. See [31 Employees](31-employees.md) and the [phase log](phases.md).
* **Existing code touched (additive only):** one call at the end of `OnboardingService.ActivateAsync` and of the probation decision; three new constructor dependencies on `PersonnelFileService` (offer letter and interview results filed); four new methods on `IEmployeeAccounts`; the backfill in `DbInitializer`; menu group *People*; *Grades* on the Administration page; three permissions.
* **Database:** script `28_phase28_employee_master_record.sql` creates three new tables only.
* **Checked:** 209/209 tests (the 197 earlier ones unchanged).

---

## 11 November 2026: the appointment page opens before the letter is issued

* **Fix:** *My onboarding* links to **Appointment letter and first day** (`/MyApplications/Appointment/{id}`) from the start, but the page answered *Not found* until HR issued the letter. It now always opens for the new employee: before the letter it shows **Your letter is being prepared** and the first-day information; once issued, the letter, the signature box and the first day as before.
* **Files:** `MyApplicationsController.Appointment`, `Views/MyApplications/Appointment.cshtml`; `OnboardingWebTests` checks the page before the letter is issued.
* **Checked:** full suite, 197/197.

---

## 11 November 2026: the IT Manager approves job adverts (docs/29, change 2)

* **New built-in role IT Manager** (given by the Super Admin). Adverts now go: Recruitment Officer **Send for review** → HR Manager **Approve and send to IT** → IT Manager **Approve and publish** (live now, or scheduled for the start date). The HR and IT Managers can **return** (with what to correct) or **reject** (final; it can still be cloned).
* **Editing a published advert takes it offline** and sends it through both reviews again; the IT Manager can also **take it offline**. Only the IT Manager records external channels as posted or taken down.
* **Existing data:** live and scheduled adverts stay published (shown as *Published*); drafts go through the review.
* **Permissions** (group *Job adverts*): `adverts.review.hr` (HR Manager), `adverts.review.it`, `adverts.publish`, `adverts.unpublish` (IT Manager), `adverts.return` (both); applied once at start-up. Controller actions guarded by matching policies.
* **Emails:** HR Managers when an advert is sent; IT Managers when HR approves; the HR reviewer when IT approves; the RO (and the HR reviewer, when IT acts) on return, rejection or taking offline.
* **Home page:** to-dos for each step; the IT Manager's cards *awaiting approval, scheduled, active, expired or closed*. *Job adverts* list tabs: Published, Scheduled, In review, Drafts, Closed, All.
* **Files:** `AdvertWorkflow`, `AdvertEnums`, `JobAdvert`, `Roles`, `Permissions`, `AdvertService`, `AdvertDtos`, `AdvertRepository`, `DashboardService`, `AdvertsController`, `Policies`, `Views/Adverts/*`, `Views/Home/Index`; migration `ItManagerAdvertApproval`, script `27_it_manager_advert_approval.sql`. Docs 13, 04, 11, 03.
* **Checked:** 197/197 tests (three new review tests, web journey through all three roles). Script 27 not yet run on MySQL; not checked in a browser.

---

## 11 November 2026: workflow corrections (docs/29, changes 1, 3 and 4)

* **Change 1, no budget tick:** the HR Manager approves a recruitment request without ticking "Budget confirmed" (the budget document was already optional). Older requests still show the tick if it was given.
* **Change 3, candidate-only uploads:** only the new employee uploads onboarding documents or marks them "not applicable". Staff can no longer upload for them. The RO still verifies; HR still files documents in personnel files.
* **Change 4, bank details as data:** the personal record asks for exactly **one** bank account, with bank name and account number (no branch). The bank confirmation letter is no longer asked for. Old uploads stay in the document history and the personnel file but never hold up an onboarding (`OnboardingWorkflow.DocumentState.Counts`). An onboarding page left at an old stage corrects itself when opened. Database script `26_bank_details_as_data.sql`.
* **Checked:** 194/194 tests. Script 26 not yet run on MySQL; not checked in a browser.
* **Change 2** is in the entry above.

---

## 10 November 2026: shorter forms, helpful empty lists (usability U4)

* **Recruitment request** in 5 steps (one per section of QF-81-11), *Save draft* on every step, site pre-filled; **vacancy form** with *More options* for desirable skills, certifications and knockout questions.
* **Empty lists** say how things get there; **application status** explains itself on hover.
* **Checked:** 193/193 tests. Not checked in a browser.

---

## 10 November 2026: next step on every record page; busy pages in topics (usability U3)

* **"Your next step"** banner on the request, vacancy, application and probation pages (`Views/Shared/_NextStepBanner.cshtml`).
* **Onboarding page** and **hiring case** split into topics with tabs (`js/section-tabs.js`); everything still on the page without JavaScript.
* After acting on a document you come back to that document.
* **Checked:** 193/193 tests. Not checked in a browser.

---

## 10 November 2026: candidates see their next step; My onboarding (usability U2, Phase 27 stage B)

* **Candidate home** starts with "Your next step" (offer to answer, letter to sign, personal record, documents, interview, profile), one big button, sticky on phones (`MyNextStepService`, `Views/Shared/_MyNextSteps.cshtml`).
* **My onboarding**: a menu link with the progress % while onboarding is in progress; the documents page renamed and linked to the letter and personal record.
* **Personal record in 6 steps** (`js/form-steps.js`), still one form saved at the end: no rule changed.
* **Phones**: My applications as cards; uploads say "Take a photo or choose a file", bigger, with a preview.
* **Checked:** 193/193 tests. Not checked on a real phone.

---

## 10 November 2026: simpler menus and complete to-dos (usability U1)

**Why:** "make the whole project very simple and easy to use". Review, decisions and progress: [28 Usability review](28-usability-review.md). Look and flow only: no business rule or permission changed.

* **Menu:** Candidates (applications + screening), one Administration page instead of 13 links, survey results under Reports, staff's own job search in the menu under their name, a flat menu for people with few links, no Screening or Hiring & offers for Heads of Department.
* **Home to-dos** now include screening rejections to confirm, letters to issue, documents to verify, employees to activate, inductions to close, probation meetings, assessments and decisions.
* **Words:** "ATS" → "screening" where users read it; two outdated developer notes removed.
* **Files:** `Controllers/AdministrationController.cs`, `Views/Administration/Index.cshtml`, `Views/Shared/_CandidatesTabs.cshtml`, `Helpers/MenuRules.cs`, `_Sidebar.cshtml`, `_Layout.cshtml`; `OnboardingService.MyWorkAsync`, `ProbationService.MyWorkAsync`, `AtsService.AwaitingHrByVacancyAsync`, `DashboardService`.
* **Checked:** 192/192 tests. Not checked in a browser.

---

## 10 November 2026: codebase audit and cleanup

**Why:** a full audit of the codebase (dead code, duplicates, structure, database, security, performance) before Phase 27 stages B and C. Report, decisions and results: [27 Codebase audit](27-codebase-audit.md). No business rule changed except the two decisions below.

* **Removed (no callers):** 7 methods, 1 constructor parameter, 1 view helper, the unchecked permission `users.manage-privileged`, 11 CSS rules, ~9.3 MB of unused Bootstrap/jQuery variants.
* **Faster:** the onboarding page runs 16 queries instead of 71; several lists and reports filter in the database; reminders and dashboards prefetch lookups.
* **Simpler:** shared `Text.Clean`, `Signatures.SameName`, `TrySaveAsync`, `AppController.Fail`/`RunAsync` replace ~90 copies.
* **Clearer:** `ScheduledJobsWorker` (was `VacancyAutoCloseWorker`), four status-badge partials named by what they show, `Application/Workforce`, one controller per file.
* **Decisions:** **probation now uses permissions** (`probation.view`: RO and HR Manager; `probation.decide`: HR Manager), like onboarding; onboarding and probation actions are also guarded per action by policy. **Virus scanning:** the placeholder stays; [06 Configuration](06-configuration.md#virus-scanning-of-uploads-connect-before-go-live) explains how to connect ClamAV or Defender.
* **Database:** migration `AuditDropRedundantVacancyStatusIndex`, script `database/25_audit_drop_redundant_vacancy_status_index.sql` (drops one redundant index).
* **Checked:** 190/190 tests (new: onboarding page query count, per-action policies); full rebuild without warnings.
* **Open:** splitting `OnboardingService` (after Phase 27 B and C); scripts 24 and 25 not yet run on MySQL.

---

## 10 November 2026: Phase 27 stage A, onboarding documents reviewed one by one, no HR approval

**Why:** GREL asked for a candidate onboarding document portal with per-document verification. Stage A (rules and data) of three; details and every decision in [Phase 27](phases.md#-phase-27-candidate-onboarding-document-portal-10-november-2026-stage-a-of-3-delivered).

* **Domain:** `OnboardingDocumentType` (configurable, 24 default types + legacy "School certificates"), `EmployeeDocumentEvent` (history incl. every file opening), new statuses *Under review* and *Resubmitted*, rejection reasons, `IsApplicable`, `Version`; onboarding `IsExpatriate`, `DocumentsDueDate`, correction request; `OnboardingWorkflow.CheckReadiness / StageFor / ApplyRequirement / AddWorkingDays`; four permissions `onboarding.*`; setting *Onboarding:DocumentDeadlineWorkingDays*; three email templates (welcome, document rejected, requirements complete).
* **Application:** `OnboardingProgressService` (works the stage out after every change), `OnboardingDocumentTypeService`, `DocumentVerificationService` rewritten (replace with history, start review, reject with a reason, requirements, logging opens), `OnboardingService` (RO starts and issues the letter; activation creates the induction; closing the induction completes; HR approval, "Submit" and "Mark verified" removed), `AttachmentService` (`IVirusScanner`, `MoveAsync`), `PersonnelFileService` (provisional file at start), reports and dashboard adapted.
* **Web:** new *Onboarding documents* screen; start form with education certificates and the expatriate tick; onboarding page *Next step* panel (progress, outstanding, correction, requirements, activation with induction areas); shared document partial grouped by heading with *Start review*, reason list, *Replace*, history; candidate page shows progress and what is still needed (no *Submit* button).
* **Database:** migration `Phase27_OnboardingDocumentPortal`, script `database/24_phase27_onboarding_document_portal.sql`.
* **Checked:** 188/188 tests.
* **Open:** stage B (*My Onboarding* dashboard and menu), stage C (RO tracking dashboard with filters, daily digest, reminders, overdue flag and extension). The script has not been run on MySQL here.

---

## 9 November 2026: dark mode, text readable again

**Why:** after the redesign some text could not be seen in dark mode.

* **Cause:** `--bs-emphasis-color` and `--bs-border-color` were set only in the light block of `site.css`. The page's `<html>` matches both `:root` and `[data-bs-theme="dark"]`, and `site.css` loads after Bootstrap, so the light values (near-black text) won in dark mode. Bootstrap uses the emphasis colour for **table text**, emphasised text and some headings, which therefore turned near-black on the dark background.
* **Fix:** both are now set in the dark block too (white emphasis, a soft light border), with a comment saying every variable of the light block must also be set there.
* **Also:** in dark mode the brand and stage colours are light, so white text on them failed contrast. **Primary and danger buttons**, the logo mark, red menu counts, completed stepper dots, the "today" date chip and checked tick boxes now use dark text; the avatar keeps a strong purple.
* **Checked:** CSS only; the Content-Security-Policy already allows the `data:` image used for the tick. Not checked in a browser here.

---

## 9 November 2026: interview score validation by the Recruitment Officer

**Why:** GREL asked that, after a candidate has been scored, **all scores go to the Recruitment Officer for validation** before anything else happens. Until now a session could be "marked completed" with only one scorecard, and the HOD and MD saw the scores and ranking straight away.

**Decisions (user's choices):** the **vacancy's Recruitment Officer** validates (another may stand in); **every panel member who took part** must submit (Absent/Excused members exempt); the RO **validates or returns one scorecard** with a reason and never changes scores; scores stay **hidden until validated** from the HOD, MD, HR and the results. Added safeguard: the **HOD cannot be marked absent** (their scorecard is required). (A first version also stopped a Recruitment Officer on the panel from validating; the user asked for the validate button to be available, so **any Recruitment Officer validates**, only never returning their own scorecard. When the vacancy owner is not a Recruitment Officer, all of them are asked.)

* **Domain:** `InterviewStatus.AwaitingValidation` (set automatically when the last expected scorecard arrives); `InterviewPanelMember.Attendance` (+ note, who, when; ready for Phase 23's attendance); `Scorecard.ReturnedAt/ReturnReason/ReturnedByUserId`; `Interview.ValidatedAt/ValidatedByUserId/ValidationNote`; rules in `InterviewWorkflow`.
* **Application:** `InterviewService.ValidateAsync`, `ReturnScorecardAsync`, `SetAttendanceAsync`, `ListAwaitingMyValidationAsync` (replaces `CompleteAsync`); emails to the RO (ready), the panel member (returned) and panel + HOD (validated). Results, lists and the hiring case count **validated scores only**; a hiring recommendation needs a validated interview. Home to-do card *Validate the scores*; the *Interviews* menu count includes validations.
* **Web:** interview page with the redesign's **Your next step** panel, a *Validate the scores* card, panel statuses (Scored / Returned / Absent / Excused); *To validate* tab.
* **Database:** migration `InterviewScoreValidation`, script `database/23_interview_score_validation.sql` (existing panel members get Attendance = NotRecorded).
* **Docs:** [16 Interviews](16-interviews.md#3-score-validation-recruitment-officer).
* **Checked:** 179/179 tests (two new: the validate / return / resubmit journey with visibility, and absent / excused rules; the web journey now validates before the HOD sees results).
* **Hiring and offers (same day):** the hiring case shows an **Interview scores** panel (every round, each panel member's validated score, recommendation and comments, average, who validated; or what an unvalidated round is waiting for), and the Hiring list has an *Interview score* column. Scores never reach the candidate or the offer letter. Tests: 181/181.
* **Validation → hiring (same day):** validating takes the Recruitment Officer straight to the hiring case (*Your next step: write the hiring recommendation*, justification pre-filled from the validated scores); the vacancy's Recruitment Officer is emailed and gets a *Recommend for hire* to-do; new tab **Hiring and offers → Ready to recommend** (`HiringService.ListReadyToRecommendAsync`). 181/181 tests.
* **Proceed with chosen candidates (same day, user's choice):** validating now lands on *Ready to recommend* for the vacancy; the Recruitment Officer ticks who to proceed with, and GRSMS starts the hiring process for each: new `HiringStatus.Draft` recommendation (advertised minimum salary, start in 30 days, justification from the validated scores) and the five standard background checks as Pending (`HiringService.ProceedAsync`, `DiscardDraftAsync`; `RecommendAsync` submits a draft to the HOD). Nobody decides on a draft. No database change (status stored as text). 182/182 tests.
* **Recruitment Officer approval only (same day, user's decision):** after *Proceed*, the Recruitment Officer clicks **Approve hire** and the hire is approved at once: **no HOD, HR Manager or MD approval** (reverses the 4 Nov rule; recorded in phases.md). Only Recruitment Officers approve; selections are capped at the vacancy's positions (`SelectionProblemAsync`). The hiring case leads through *Send the offer* and, once accepted, **Click to proceed to onboarding**. Recommendations already waiting in the old chain still finish there. No database change. 183/183 tests.
* **Open:** sessions already fully scored but still *Scheduled* before this change can be validated from their page, but appear on *To validate* only after another scorecard change. The 3-person quorum is not enforced yet (Phase 23).

---

## 6 November 2026: Phase 21, probation, confirmation, dashboards and reports

**Why:** stage 4, the last, of GREL's onboarding → probation → confirmation request. Details in [Phase 21](phases.md#-phase-21-probation-confirmation-dashboards-and-reports-6-november-2026).

* **Domain:** `ProbationRecord` (objectives, development plan, assessments, history), `ConfirmationRecord`, `ProbationWorkflow`.
* **Application:** `ProbationService` (started by `OnboardingService` at activation), `WorkforceDashboardService`, `WorkforceReportService` (called by `ReportService`); `PersonnelFileService` files the meeting, assessments and confirmation letter; three new email templates.
* **Web:** *Probation* and *My probation* screens, the confirmation letter, the Home dashboard widgets, six new reports.
* **Database:** `database/22_phase21_probation_and_confirmation.sql`, checked on MySQL 8.4 after scripts 19, 20 and 21.
* **Checked:** 177/177 tests.
* **Open:** no automatic reminders for due reviews (shown on the dashboards instead).

---

## 6 November 2026: Phase 20, departmental induction and the electronic personnel file

**Why:** stage 3 of GREL's onboarding → probation → confirmation request. Details in [Phase 20](phases.md#-phase-20-departmental-induction-and-the-electronic-personnel-file-6-november-2026).

* **Domain:** `InductionArea` (12), `InductionAreaSetting`; `OnboardingTask` gets area, description, assigned date and verification; `PersonnelFile`, `PersonnelFileCategory` (7, seeded), `PersonnelFileDocument`; permission `personnel.view`.
* **Application:** `InductionAreaService`, `PersonnelFileService`; `OnboardingService` adds the chosen areas' tasks on approval, task verification, opens the personnel file at activation.
* **Web:** *Induction areas* screen, *Personnel files* (list, folders, letters, downloads, uploads), area checkboxes on approval, grouped and verifiable induction tasks.
* **Database:** `database/21_phase20_induction_and_personnel_file.sql`, checked on MySQL 8.4 after scripts 19 and 20.
* **Checked:** 168/168 tests.

---

## 6 November 2026: Phase 19, Employee Personal Record Declaration

**Why:** stage 2 of GREL's onboarding → probation → confirmation request. Details in [Phase 19](phases.md#-phase-19-employee-personal-record-declaration-6-november-2026).

* **Domain:** `EmployeePersonalRecord` and its seven lists, immutable `EmployeeDeclaration`.
* **Application:** `PersonalRecordService` (every rule, incl. beneficiaries = 100%); submitting needs the signed record; HR may send back with a message.
* **Infrastructure:** `PersonalRecordRepository`, EF configuration, the audit interceptor refuses changes to declarations, migration `Phase19_PersonalRecordDeclaration`.
* **Web:** *My personal record* (portal), HR read-only view, `_PersonalRecordView`, `js/repeater.js`.
* **Database:** `database/20_phase19_personal_record_declaration.sql` (new tables only), checked on MySQL 8.4 with script 19.
* **Checked:** 161/161 tests.

---

## 6 November 2026: Phase 18, onboarding workflow and new-hire documents

**Why:** stage 1 of GREL's onboarding → probation → confirmation request (decisions: extend the Phase 9 onboarding; 12 induction areas; HTML letters; four committed stages). Details in [Phase 18](phases.md#-phase-18-onboarding-workflow-and-new-hire-documents-6-november-2026) and the [user guide](24-onboarding-module.md).

* **Domain:** `OnboardingStatus` is now the eight stages; `OnboardingActionEntry` history; `EmployeeDocument` with 16 types; `OnboardingWorkflow` rules; three email templates.
* **Application:** `OnboardingService` stage moves (submit, return, verified, HR decision, induction complete, activate, notes); new `DocumentVerificationService`; `IEmployeeAccounts`; reminders and reports use "active" instead of "in progress".
* **Infrastructure:** `EmployeeAccounts` (Identity), EF configuration, migration `Phase18_OnboardingWorkflow` (moves existing onboardings to the new stages).
* **Web:** onboarding page (stepper, next step, documents, history, notes), portal page *My new-hire documents*, shared `_EmployeeDocuments` partial.
* **Database:** `database/19_phase18_onboarding_workflow.sql`, run on MySQL 8.4 against a throwaway copy of the development database.
* **Checked:** 156/156 tests.

---

## 5 November 2026 (19:40): Digital QF-81-11 Recruitment Request Form

**Why:** GREL asked for a digital version of form QF-81-11 with a three-stage approval (Department Head → HR Manager → MD) and conversion of the approved request into a job vacancy. Recorded as [Phase 17](phases.md#-phase-17-digital-qf-81-11-recruitment-request-form-5-november-2026), with the decisions taken (department staff raise and their HOD reviews; the Recruitment Officer stage removed).

* **Domain:** new `DepartmentHeadReview` status, `StaffCategory`, purpose `Other`, the QF-81-11 fields on `RecruitmentRequest`, `RequisitionNumber` on `Vacancy`, three new document categories; `RecruitmentWorkflow` rewritten for the three stages (old `RecruitmentReview` kept for requests already there).
* **Application:** `RecruitmentRequestService` (who raises, stage-1 rules, HOD sign-off, form checks, document types, emails), `VacancyService` (pre-fill, requisition number, owner), `RichText` helpers, `AttachmentService` type restriction, reminders and dashboard know the new stage.
* **Infrastructure:** `RichTextSanitizer` (HtmlSanitizer 10.0.1120), one-time permission grants in `PermissionSeeder`, EF configuration, migration `Qf8111RecruitmentRequestForm`.
* **Web:** the form, details page, stepper, vacancy form (Job requisition card), `rich-text-editor.js`, editor styles.
* **Database:** `database/18_qf8111_recruitment_request_form.sql` (columns only).
* **Driving licence optional (6 November):** Not needed (default) / Desirable / Mandatory instead of a yes/no "required" switch, class optional; only Mandatory becomes a knockout question on the vacancy. Migration `DrivingLicenceRequirement`, included in script `18_`.
* **One menu item for vacancies and adverts (6 November):** the sidebar shows "Vacancies & adverts"; a Vacancies / Job adverts tab bar (`Views/Shared/_VacanciesAndAdvertsTabs.cshtml`) sits at the top of both lists. People who may open only one of the two see just that one.
* **Checked:** 149/149 tests. **Open:** the editor not yet tried in a real browser; the migration not yet run on MySQL.

---

## 5 November 2026 (13:50): Public trial link, closed registration, candidate accounts by HR

**Why:** the user wants a public link for specific people to try GRSMS, with only accounts the user creates (choices: Cloudflare quick tunnel; self-registration off).

* **`docker-compose.share.yml`**: Production mode, forwarded headers trusted only from the Docker network, and a `cloudflared` 2024.12.2 quick tunnel giving a public `https://...trycloudflare.com` link to this PC. See [10 Docker → Sharing a trial link](10-docker.md#sharing-a-trial-link-5-november-2026).
* **Self-registration switch** (System settings → Recruitment workflow, default on). When off, the Register page and its post send people to sign-in, and the "create a candidate account" links are hidden.
* **Add candidate account** on Users & roles: no password is set; the person is emailed (template *New candidate account*) a 24-hour link to choose one.
* 1 new test (136 in total).
* **Done on this PC:** self-registration switched off, then the share stack started. Link at the time: `https://handhelds-levy-receives-accompanied.trycloudflare.com`, also set as `APP_BASE_URL` in `.env`. Checked through the public link: `/health`, sign-in page over HTTPS with secure cookies, HSTS and the security policy, Register refused, Super Admin sign-in, the app seeing its public HTTPS address (so emailed links use it), and the audit trail recording the visitor's real address.

---

## 5 November 2026 (13:30): Welcome email for every new account

**Why:** the user asked that every user receive an email when an account is created for them.

* **Staff accounts created on Users & roles** (Super Admin, Admin or HR Administrator) are emailed at once (template *New staff account*): who created it, the sign-in address, the roles, and a **link to choose their own password**. It's the same 24-hour link as "Forgot password"; choosing a password through it also clears the "must change password" flag. **The temporary password is never emailed** (email is not a safe place for passwords); it still works if HR hands it over.
* **Accounts created at a first Microsoft 365 sign-in** get a welcome email too (template *New account (Microsoft 365)*).
* **Candidates** already received an email when they registered (the confirmation link), so nothing changed for them.
* Both templates can be reworded on **Email templates** (now 13 templates).
* 1 new test (135 in total): through the real pages, the Super Admin creates an account, the email has the address and roles but not the password, and the link lets the person choose a password and sign in.

---

## 5 November 2026 (12:40): Phase 16 and the SMTP details

* Phase 16 (system settings, email templates, backups, audit monitor) is recorded in the [phase log](phases.md#-phase-16-system-settings-email-templates-backups-and-audit-monitor-5-november-2026).
* **SMTP:** the user's edit put the sender address and password for `jobmaster@grelghana.com` into `EmailService.cs`, which also stopped it compiling. At the user's choice the credentials were moved to **local user-secrets** (`Email:FromAddress`, `Email:FromName`, `Email:UserName`, `Email:Password`, `Email:SmtpHost` = `smtp.hostinger.com`, `Email:SmtpPort` = 587, `Email:EnableSsl` = true). These are plain text in the developer's Windows profile and never in the repository. `EmailService.cs` has no credentials and a comment saying where they belong. One test email to the jobmaster mailbox was sent successfully. The local `dotnet run` app now sends **real** email; the Docker app still saves emails as files (it reads `.env`). For production, put the same values in `deploy/.env` or on **System settings** (encrypted).
* New package `Microsoft.AspNetCore.DataProtection` 10.0.14 in Infrastructure (encrypting saved secrets).

---

## 5 November 2026 (11:10): Animation

**Why:** the user asked for "nice and well designed animation". The request did not say where, so it was applied across the whole interface, kept subtle and consistent for a business tool.

**Changed**
* New **`wwwroot/css/motion.css`**, loaded after `site.css` by all three layouts. Durations and easing are tokens at the top (`--motion-fast` 140 ms, `--motion-base` 240 ms, `--motion-slow` 520 ms), so the whole app can be sped up or slowed down in one place.
  * **Page enter:** the page header, then each block, then the cards in each row rise 8 px and fade in, with a short stagger (all of it under about 0.4 s). Rows don't move themselves, so cards never rise twice. The sign-in card settles in.
  * **Charts:** report bars, dashboard 7-day bars, ATS score bars, missing-skill bars and progress bars grow from their baseline, one after another.
  * **Feedback:** clickable cards and dashboard tiles lift 2 px under the pointer; buttons press in; arrow / plus / refresh icons nudge on hover; links, tabs, form fields and table rows change colour smoothly; menus and alerts slide in.
  * **Attention:** the current step of an applicant's tracker has a slow pulsing ring; the "needs you" counts in the menu pulse three times and then rest; the bell count pops in.
  * **Light / dark switch:** colours cross-fade for a third of a second.
* **`site.js` section 8:** numbers on dashboard and ATS tiles count up when they come into view. The text from the server is always restored exactly at the end, and a timer guarantees that even when the browser pauses animation (background tab, battery saving).
* **Off for "reduce motion" and printing:** with the operating system's *reduce motion* setting, every animation and transition is effectively instant and nothing counts up; printed pages have no animation.
* Only `transform` and `opacity` are animated, and every animation ends at `transform: none`, so nothing stays shifted. No inline scripts or styles were added, so the Content-Security-Policy is unchanged.

**Checked:** 128/128 tests (new: every layout loads `motion.css`, and the file contains the reduce-motion and print rules). In a real browser (headless Edge, against the Docker app): the sign-in card mid-animation and settled; the careers page settled; a check page using the real `site.js` and `motion.css` mid-way (numbers counting, bars growing) and at the end (exact values 1,234 / 86% / 12.5, full-length bars). The first version of the count-up stalled when the browser slowed animation frames; the timer above fixes that. Signed-in pages were not screenshotted (headless Edge cannot sign in); they use the same classes as the check page.

---

## 5 November 2026 (10:30): Admin role and "My audit trail"

**Why:** the user asked for "admin and super admin" and for each user to see their own audit trail. Choices made: a **new Admin role** between HR Administrator and Super Admin, and a personal trail showing **what I did and what was done to my account**.

**Changed**
* **Admin** (built-in role, id `admin`). Manages every user account except Admins and Super Admins: details, password resets, unlock, deactivate. Maintains organisation data and reads the whole audit trail. Sees Roles & permissions and ATS settings **read-only** (new permission `settings.view`). Like the HR Administrator, an Admin gives only the Employee and Interview Panel Member roles: the GREL requirement that **only a Super Admin creates HR Managers, Recruiters and HODs** still holds, and only a Super Admin creates Admins. When an Admin edits a HOD's account, its locked roles are kept automatically. Default permissions are given once at start-up (`Permissions.RolesAddedLater`, marker `role:Admin` in KnownPermissions); other roles keep the Super Admin's choices.
* **My audit trail** (`/MyAuditTrail`, in everyone's menu, candidates too). It shows the user's own record changes, sign-ins, failed sign-ins and lockouts, and password changes (with their own IP address). It also shows changes others made to their account (details, roles, password resets, deactivation), naming who did it but **without that person's IP address**.
* **System work is recorded as the system.** `SystemActor` marks work GRSMS does by itself inside someone's request. The ATS screening on submission now appears in the audit trail as *ATS (automatic screening)*, with no user and no IP, instead of under the applicant's name. This was needed so a rejection held for HR can never show in the applicant's own trail.
* Migration `AdminRole`, script `database/16_admin_role.sql`; 4 new tests (127 in total); security, admin, database and testing docs updated.

**Problem found and fixed:** the first version kept locked roles by quietly dropping privileged roles a non-Super-Admin tried to add, so such an attempt "succeeded" without doing anything. Now a missing locked role is kept, and adding a privileged role is refused with a message (found by the existing HR Administrator test).

**Checked:** 127/127 tests. On the Docker MySQL (after a backup): the migration applied, the Admin role received its 11 default permissions, every other role kept exactly its grants, and My audit trail, Roles & permissions, Users, ATS settings and the audit trail all opened. Builds and tests ran in a separate output folder, because the user's own debug session (VS Code, port 5014) had the normal build folder open; it was left running.

---

## 5 November 2026: Clean-up committed; Phase 14 started

* The 4 November clean-up (unused demo mode code and two methods nothing called) was committed on branch `feature/ats-and-super-admin`. The `_removed-for-review/` copies are still on disk (git-ignored) until they are deleted.
* Phase 14 (Super Admin and permissions) is recorded in the [phase log](phases.md#-phase-14-super-admin-and-granular-permissions-5-november-2026).
* Phase 15 (ATS screening engine) is recorded in the [phase log](phases.md#-phase-15-ats-screening-engine-5-november-2026). New packages: PdfPig 0.1.16 (PDF text) and Anthropic 13.5.0 (official SDK, used only by the optional AI reader); no known vulnerabilities.

---

## 4 November 2026 (22:00): SweetAlert2 for confirmations and messages

**Why:** the user asked to use SweetAlert instead of the browser's plain boxes.

**Changed**
* **Confirmations** (all 35 `data-confirm` buttons: approve, reject, withdraw, deactivate, send offer, ...) now open a SweetAlert2 dialog: *Are you sure?*, the message, **Cancel** and a button named like the one clicked (red for red buttons, with Cancel focused). On *yes* the form is sent with the same button, so `name="decision" value="Approve"` buttons keep working. Without the library (script blocked) the browser's own box is used.
* **Messages after an action** (TempData success / info / error) appear as SweetAlert2 pop-ups: success and info as a notice in the top corner that closes itself after 5 seconds; errors as a dialog with **OK**, so they are not missed. Without JavaScript the old banner shows instead.
* Styled for GRSMS in light and dark mode (Bootstrap buttons, page colours) in `site.css`.
* Files: SweetAlert2 **11.14.5** (MIT licence) in `wwwroot/lib/sweetalert2/`, **served by GRSMS itself**, not from a CDN, because the Content-Security-Policy (Phase 12) only allows the site's own scripts; added to the three layouts; logic in `js/site.js` (section 3); `Views/Shared/_Alerts.cshtml` marks each message with `data-flash`.

**Security:** SweetAlert2 treats `title` and button texts as HTML. Messages can contain names people typed, so they are passed as plain text (`text`, `titleText`) and button labels copied from the page are escaped. The library contains a `new Function` call, used only by a feature GRSMS does not use (dialogs declared in HTML `<template>`); the policy would block it anyway.

**Checked:** 111/111 tests (new: every layout loads SweetAlert2 from `/lib`, and the file is served). In a real browser (headless Edge): the error dialog (dark), the success notice (light), the confirmation for a red *Withdraw offer* button (light and dark), and that answering *yes* submits the form with the clicked button's value and the hidden fields.

---

## 4 November 2026 (12:20): Demo mode removed; local app uses the real database

**Why:** the user asked to remove the demo site and use the actual database and web app.

* Stopped the demo app (port 5014) and deleted its SQLite file `src/Grsms.Web/App_Data/grsms-demo.db` (made-up sample data only).
* Removed the `demo` profile from `launchSettings.json`. `DemoDataSeeder.cs` is still in the code but nothing starts it.
* Set the user-secret `ConnectionStrings:DefaultConnection` to the Docker MySQL database (`localhost:3307`), so `dotnet run` on http://localhost:5014 uses the same data as the Docker app on http://localhost:8080.
* Docs: [01 Getting started](01-getting-started.md) demo section replaced; `CREDENTIALS.local.md` updated.

**Checked:** http://localhost:5014 signs in as `admin@grel.local` and its Sites page shows the MySQL sites, with no demo sites.

**Found:** site 5 **Tsibu** is missing from the database. phpMyAdmin's log shows a change to the Sites table at 12:17 (UTC), made in the database viewer, which bypasses the app's audit trail. Not restored, pending the user's decision.

---

## 4 November 2026 (latest): GREL's 12 sites confirmed

**Why:** GREL confirmed the site list is exactly the 12 sites in its own system, that Tsibu is the TBU factory, and that the APM site is spelt *Apeminim*.

**Changed** (migration `GrelSites`, script `database/06_grel_sites.sql`)
* Site 3 renamed *Apimenim (APM)* → **Apeminim**.
* **Tsibu** gets the type *Rubber Processing Factory (1D1F Expansion Project)*.
* Sites 13 *TBU Factory* and 14 *Nucleus Operations Areas* set **inactive** (kept, not deleted, so ids never change).
* Seed test now checks 12 active sites, the Tsibu type and the Apeminim spelling.

**Checked:** 53/53 tests passed; Docker MySQL shows the 12 active sites and the two inactive ones; the Sites page in the Docker app lists them.

---

## 4 November 2026 (later): Divisions, GREL sites and report filters

**Why:** the GREL requirements call for a two-level structure (Division → Department), for every recruitment record to be linked to a division, department and requesting HOD, and for reports to filter by them. GREL's site list (with site types) arrived at the same time. Full guide: [12 Divisions, departments and sites](12-divisions-and-departments.md).

**Changed**
* **Divisions** (new table): Corporate and Operations. New *Divisions* screen for HR Administrators (`DivisionService`, `DivisionsController`).
* **Departments** now belong to a division (required). The list was set to the 17 departments in the requirements:
  * renamed: Transport & Logistics → **Transport** (reactivated), APM Factory → **Factory APM**, TBU Factory → **Factory TBU**, RUTAP → **RuTAP**;
  * added: **Nucleus Operations** (NUC, id 33);
  * deactivated (kept, not deleted): **Estates, Progress and Quality, Project**.
  * Department screens show and edit the division.
* **Sites** have a new *Site type* field. GREL's 14 sites are pre-loaded (Head Office updated; ids 2–14 added).
* **Recruitment requests** store `DivisionId`. **Vacancies** store `DivisionId` and `RequestingHodUserId`, copied from the request. Details pages show the division and the requesting HOD.
* **Filters:** Division and Period (from / to) added to the request and vacancy lists, next to the existing Department and Status filters, with a Division column. The dashboard has a Division / Department filter (`DashboardFilter`).
* Migration `DivisionsAndSites` and script `database/05_divisions_and_sites.sql`. The migration fills the new columns of existing requests and vacancies from their department and request before switching the foreign keys on.
* **Demo data:** the demo HOD moved from Estates to **Factory APM**; demo login `hod.estates@demo.grel` → **`hod.apm@demo.grel`**; request numbers `RR-EST-…` → `RR-APM-…`. The demo's two made-up sites were removed (GREL's real sites are used). **An existing demo database must be deleted** (`src/Grsms.Web/App_Data/grsms-demo.db`), because demo mode builds its tables once and does not apply migrations.
* Tests: the Estates-based tests now use Factory APM (id 31); 4 new tests (seed structure, department needs an active division, division can't be deactivated while in use, request and vacancy division links and filters).

**Checked**
* Build clean; **53/53 tests passed**.
* Docker MySQL: migration applied; 9 active Corporate and 8 active Operations departments; 14 sites with their types.
* Signed in to the Docker app as the administrator: the Home (with and without filters), Divisions, Departments, Sites, Recruitment requests and Vacancies pages all load, and the Department filter is grouped by division.
* Not checked in a browser screenshot: the layout of the new dashboard filter bar on a phone.

**Open:** see [Open points to confirm with GREL](12-divisions-and-departments.md#open-points-to-confirm-with-grel) (Apimenim spelling, Tsibu vs TBU Factory, unknown site types, the three deactivated departments). The HOD's part in shortlisting, interviews and the hiring recommendation is recorded as a rule for Phases 6–8 in the [phase log](phases.md#later-phases); those modules don't exist yet.

---

## 4 November 2026: Docker, home dashboard, database viewer, GREL departments

Four pieces of work, done in this order. None of it is committed to git yet.

### 1. Code check and Docker set-up

**Why:** to run GRSMS against a real MySQL server without installing MySQL or .NET on the PC.

**Code check:** the solution built cleanly and all 49 automated tests passed before any change. The review covered start-up (`Program.cs`), configuration, database initialisation and the project files: what was needed to containerise the app. It was not a line-by-line review of every module.

**Added**
* `Dockerfile`: multi-stage build (.NET 10 SDK → ASP.NET 10 runtime), non-root user, port 8080, writable `App_Data` and `logs` folders.
* `docker-compose.yml`: `db` (MySQL 8.4 with `utf8mb4_0900_ai_ci` and a health check) and `web` (waits for a healthy database). Data kept in the `mysql-data`, `app-data` and `app-logs` volumes.
* `.dockerignore`.
* `.env` (local only, git-ignored): generated MySQL and first-administrator passwords.

**Decisions**
* The container runs in **`Development`**. In `Production` the sign-in cookie requires HTTPS, so sign-in fails over the container's plain HTTP. `Development` also applies migrations automatically.
* App on host port **8080**, not 5014. A `dotnet run` copy of the app was already listening on `localhost:5014`, and requests to `localhost` were reaching *that* copy instead of the container (diagnosed when sign-in failed and the container logged no requests).
* MySQL exposed on host port **3307** to avoid clashing with a locally installed MySQL.

**Checked:** all three EF migrations applied to MySQL 8.4; the first HR Administrator was created; signing in and opening the Home and Departments pages worked through the container. This is the **first time GRSMS has run on a real MySQL server** (see the Phase 1 "known limits" in the [phase log](phases.md)).

**Docs:** [10 Running with Docker](10-docker.md).

### 2. New home dashboard and visual update

**Why:** the Home page was redesigned to follow a reference dashboard layout supplied by GREL: KPI cards with weekly bar charts, a task list, an interview-style calendar, progress bars and a latest-applicants list.

**Mapping to GRSMS data.** Tasks, interviews and applicants do not exist until later phases, so each panel uses the nearest real data:

| Reference panel | GRSMS panel |
|---|---|
| Total candidates / interviewed / active jobs / offers accepted | Recruitment requests / Approved requests / Open vacancies / Registered candidates, each with 7-day bars and the change vs the previous 7 days |
| Upcoming Task | **My tasks**: items waiting for the signed-in user |
| Upcoming Interview (calendar) | **Vacancy closing dates**, with month navigation |
| Compliance (progress bars) | **Requests in approval** (25 / 50 / 75%) |
| Latest Applicant | **Latest candidates** (HR roles only) |

**Visual update (all pages):** flat grey frame, light sidebar with section headings and count badges, white rounded content panel, white hairline-bordered cards (the `.glass` class kept its name, so every existing page picked up the new look), a search box and a notification bell in the top bar. Light and dark mode both updated. The brand stays **GREL green**. The reference used purple, which is a one-token change (`--grsms-brand`).

**Added**
* `src/Grsms.Application/Dashboard/DashboardService.cs`, `DashboardDtos.cs`
* `src/Grsms.Web/Helpers/NavCounts.cs` (sidebar badges and bell, worked out once per page)
* `docs/11-home-dashboard.md`

**Changed**
* `HomeController.cs`, `Models/HomeViewModel.cs`, `Views/Home/Index.cshtml` (rewritten)
* `Views/Shared/_Layout.cshtml` (search box, bell, icon buttons), `Views/Shared/_Sidebar.cshtml` (sections, counts, footer)
* `wwwroot/css/site.css`: sections 1–4 rewritten, section 8 (dashboard) added, phone rules extended. The previous version was backed up during the work, and `git diff` shows the change.
* `IUserDirectory` / `UserDirectory`: three candidate queries
* `Application/DependencyInjection.cs`, `Program.cs`: service registrations

**Checked**
* Build clean; 49/49 tests passed.
* Screenshots taken in headless Chrome against demo data: HR Manager (all panels), Recruitment Officer (dark mode), Employee (minimal page), a 390 px phone, and the Recruitment requests list (to confirm the restyle on other pages).
* Fixed from those screenshots: the phone task table overflowed (two columns now hidden on small screens); phone KPI cards were one per row with tall empty charts (now two per row, shorter); a lone KPI card stretched full width (grid now keeps a normal card width); 0% change showed a green "up" arrow (now neutral grey); the bell's red dot was too small.

**Open / not done**
* **No automated tests target the dashboard itself.** The existing web tests load `/` as staff and pass, which proves the page renders, but the numbers are not asserted. Worth adding `DashboardService` tests (visibility per role, week-on-week maths, task list per role).
* About 15 small queries per Home page load, plus 2–4 on every other page for the sidebar counts. Fine at GREL's volumes; see the performance note in [11 Home dashboard](11-home-dashboard.md#for-developers).

### 3. Database viewer (phpMyAdmin)

**Why:** to view the database in a web browser.

**Added** a `phpmyadmin` service (`phpmyadmin:5.2`) to `docker-compose.yml` at **http://localhost:8081**, bound to `127.0.0.1` so only this PC can open it. Sign in as `grsms` / `MYSQL_PASSWORD` from `.env`.

**Checked:** signing in as `grsms` showed the `grsms` database.

**Caution recorded:** the `grsms` account can change data, and edits made in phpMyAdmin bypass the app's soft delete and audit trail. See [10 Running with Docker → Viewing the database](10-docker.md#viewing-the-database-in-a-browser).

### 4. GREL's real department list

**Why:** the 24 seeded departments were placeholders from the specification. GREL supplied its real list (a screenshot of the department drop-down from another GREL system), confirmed as complete, with the instruction to **replace** the placeholders and deactivate the extra ones rather than delete them.

**Result:** 18 active departments.

| | |
|---|---|
| Unchanged | Finance, Administration, Human Resources, Corporate Affairs, Information Technology, Supply Chain, Internal Audit, Procurement |
| Renamed | Quality Assurance → **Progress and Quality** (code QA → **PQ**); Projects → **Project** |
| Added (ids 25–32) | Civil & Infrastructure (CIV), Stores (STR), Garage (GAR), Estates (EST), RUTAP, DTC, APM Factory (APM), TBU Factory (TBU) |
| Deactivated (kept) | Sales and Marketing, Production, Engineering, Maintenance, HSE, Research & Development, Legal & Compliance, Security, Transport & Logistics, Warehouse, Medical Services, Community Relations, Sustainability, Business Development (14) |

**Decisions**
* Ids never change: the placeholders stay as inactive rows (an HR Administrator can reactivate any of them), and new departments start at id 25.
* Codes for the new departments were chosen here (CIV, STR, GAR, EST, RUTAP, DTC, APM, TBU). They appear in request numbers (`RR-EST-2026-0001`). **Confirm them with GREL** and change any in the Departments screen before real requests are raised.
* "RUTAP" and "DTC" are kept as GREL writes them. Their full names can be added as the department description.

**Changed**
* `Persistence/Configurations/OrganizationConfigurations.cs`: the seed list, now with an active flag per row
* New migration `Persistence/Migrations/20261104095611_GrelDepartments.cs`, which updates and inserts department rows only
* New script `database/04_grel_departments.sql` for DBA-managed databases
* `DemoDataSeeder.cs`: demo HOD moved from HSE to **Estates**; demo login `hod.hse@demo.grel` → **`hod.estates@demo.grel`**; demo request numbers `RR-HSE-…` → `RR-EST-…`. The "already seeded" check now looks for `recruiter@demo.grel`, which exists in every version, so an old demo database is not seeded twice.
* Tests: the HSE department (now inactive) replaced by Estates (id 28) in the request and vacancy tests; seed and drop-down counts updated (32 rows, 18 active)
* Docs: README, 01, 02, 03, 07, 08 and the phase log

**Checked:** 49/49 tests passed; in the Docker MySQL database exactly the 18 GREL departments are active, 14 are inactive, and the migration is recorded in `__EFMigrationsHistory`.

**Follow-ups**
* The 8 new departments have **no Head of Department** yet. Set one per department before it can raise recruitment requests.
* An existing local demo database (`src/Grsms.Web/App_Data/grsms-demo.db`) still has the old list, because demo mode builds the file once and never migrates it. Delete it to get the new list.
