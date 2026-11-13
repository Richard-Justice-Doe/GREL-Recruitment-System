# Change log

Work done outside the numbered phases, newest first. Each entry says what changed, which files, how it was checked, and anything still open. For the phases themselves, see the [phase log](phases.md).

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
