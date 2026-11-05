# Phase log

## ✅ Phase 1: Foundation (November 2026)

**Delivered**
* Clean Architecture solution (Domain / Application / Infrastructure / Web) with dependency injection and the repository + unit-of-work pattern
* MySQL schema and script `database/01_phase01_foundation.sql`: Departments, Units, Sites, Users, Roles, the Identity tables and AuditLogs, with primary keys, foreign keys, unique indexes, soft delete and audit columns
* Seed data: the departments, 8 roles and Head Office (the department list was later replaced with GREL's 18 real departments: `database/04_grel_departments.sql`)
* Sign-in: password policy, lockout, 20-minute idle timeout, forgot/reset/change password, forced change of temporary passwords, candidate self-registration with email confirmation, and optional Microsoft 365 sign-in (off until IT provides the IDs)
* Role-based access via named policies, anti-forgery on every form, security headers
* Screens: Departments (add, edit, activate/deactivate, HOD), Units, Sites, Users & roles, Audit trail
* Automatic audit trail of every record change and security event
* Email: saved as `.eml` files until SMTP is configured
* Serilog daily log files
* UI: sidebar, glass cards, light/dark mode, mobile layout, DataTables
* 20 automated tests (services and full web flows)
* Documentation in `docs/`

**Decisions recorded**
* .NET 10 instead of 8; Oracle MySQL provider; no MediatR, Tailwind or AutoMapper (see [Architecture → Technology choices](02-architecture.md#technology-choices))
* A Unit is a sub-section of a department. A Site is a separate physical location, and requests record both.
* Staff sign in with Microsoft 365 (when configured) or a local account. Candidates use local accounts only.

**Known limits / to do later**
* ~~Not yet run against a live MySQL server.~~ **Done 4 November 2026:** all migrations applied cleanly to MySQL 8.4 in Docker, and sign-in and the screens worked (see the [change log](changelog.md)). Case-insensitive duplicate checks on MySQL are still worth a manual check. See [07 Testing](07-testing.md#what-sqlite-cant-prove).
* IP addresses behind a reverse proxy need forwarded-headers set-up (Phase 13).
* Content-Security-Policy and sign-in rate limiting (Phase 12).

## ✅ Phase 2: Recruitment requests (November 2026)

**Delivered**
* Recruitment request form: automatic number (`RR-<DEPT>-<YEAR>-0001`), department, unit, site, position, number of positions, employment type, vacancy reason (and who is being replaced), justification, budget code, estimated annual cost (GHS), HOD and date requested
* Attachments: job description (required to submit), organisational chart, induction programme and other documents, with type/content/size checks, storage outside the website, SHA-256 fingerprints and permission-checked downloads
* Workflow **Draft → Recruitment Review → HR Approval → MD Approval → Approved**. At each stage the reviewer chooses *Approve*, *Return for changes* (comments required; the HOD edits and resubmits) or *Reject permanently* (comments required). The requester can cancel.
* Participants found automatically: the department's HOD, every Recruitment Officer, the HR Manager and the MD. The Recruitment Officer assigns the recruiter; the HR Manager validates the budget.
* Controls: no approving your own request; only the stage's role may decide; out-of-date pages and simultaneous decisions refused
* Email notifications to the next approvers and to the requester at every decision
* Screens: list with *Awaiting my action* (oldest first, "N days" warning) and *All requests* tabs, search and filters, server-side paging; form with a unit list that follows the department; details page with progress stepper, history timeline, decision panel and documents; home-page tile
* MySQL script `database/02_phase02_recruitment_requests.sql`: RecruitmentRequests, RecruitmentRequestActions, Attachments, NumberSequences
* 14 new automated tests (34 in total), including the full approval journey through the web pages
* New [Recruitment requests user guide](08-recruitment-requests.md); database, security, configuration, architecture and testing docs updated

**Notes**
* "Vacancy creation" after approval is Phase 3. Approved requests show that the vacancy step comes next.
* In-app notifications (the bell icon) arrive with the Notifications module. Phase 2 uses email.
* Still not run against a live MySQL server on the development PC; see Phase 1 notes.

## ✅ Phase 3: Vacancy management (November 2026)

**Delivered**
* Vacancies created **only from fully approved recruitment requests**, one per request (also enforced in the database), pre-filled from the request. The department, positions and employment type are locked to what was approved.
* Fields: vacancy number (`VAC-<YEAR>-0001`), job title, department, unit, site, reporting manager (HOD by default), who may apply (internal / external / both), job description, minimum education and field of study, qualification notes, minimum years and type of experience, **structured essential/desirable skill and certification lists** (ready for CV screening in Phase 6), monthly salary range with a "show on adverts" option, and closing date
* Life cycle **Draft → Pending approval → Published → Closed → Archived**: the HR Manager approves (publishing it) or returns it with comments; it closes **automatically** once the closing date passes (hourly background job) or by hand; it can be reopened with a new date, and archived when finished or abandoned
* The same controls as requests: the submitter can't approve, the role is checked at each step, stale and simultaneous decisions are refused, and there's a full history plus audit trail
* Emails to HR Managers (for approval), the recruiter, submitter and HOD (when published or returned), and the recruiter (when it closes automatically)
* Screens: Vacancies list with *Open*, *Pending approval*, *Approved requests awaiting a vacancy* and *All* tabs; form; details page with stepper, requirement chips, manage panel and history. A "Create the vacancy" button and a link appear on approved requests. Home-page tiles for open vacancies and to-do counts.
* Shared building blocks: `AccessScope` (department visibility) and `WorkflowMailer` (notification emails), now used by requests too
* MySQL script `database/03_phase03_vacancies.sql`: Vacancies, VacancyRequirements, VacancyActions
* 14 new automated tests (48 in total), including the full journey through the web pages
* New [Vacancies user guide](09-vacancies.md); the other docs updated

**Decisions recorded**
* **The HR Manager approves vacancies.** The MD's approval is given on the recruitment request, so the MD isn't asked twice. (The specification lists "final vacancy approval" for the MD; this can be changed by adding an MD stage to `VacancyWorkflow`.)
* Approval publishes the vacancy immediately. There is no separate "approved but not yet published" state, matching the specification's status list.
* Applications are accepted for the whole closing day. The vacancy closes automatically the following day.

## ✅ Between Phases 3 and 4 (4 November 2026)

Not a numbered phase. Full details are in the [change log](changelog.md).
* **Docker:** app + MySQL 8.4 + phpMyAdmin with one command ([10 Running with Docker](10-docker.md))
* **Home dashboard** replacing the Phase 1–3 home page, plus a visual update of every page ([11 Home dashboard](11-home-dashboard.md))
* **GREL's real departments** replace the placeholder list (`database/04_grel_departments.sql`)
* **Divisions:** Corporate and Operations over GREL's 17 departments; GREL's 12 sites with site types; requests and vacancies record their division and requesting HOD; Division / Department / Status / Period filters on the lists and the dashboard (`database/05_divisions_and_sites.sql`, [12 Divisions and departments](12-divisions-and-departments.md))

## ✅ Phase 4: Job advertisements (November 2026)

**Delivered**
* Job adverts for **published vacancies only** (approval complete, before the closing date), numbered `ADV-<YEAR>-0001`. A vacancy can have several adverts over time. Each stores the division, department and requesting HOD of its vacancy.
* Advert text, *How to apply*, start and end dates (never after the vacancy's closing date), pre-filled from the vacancy (description, requirements, salary if allowed, location)
* Channels: **Internal portal** and **Company website** are published automatically; **LinkedIn, Indeed, Jobberman and Newspapers** get copy-ready text and are tracked per channel (to post → posted with link or edition → taken down). Internal (staff-only) vacancies can only use the internal portal.
* Life cycle **Draft → Scheduled → Live → Closed**: publish now, or schedule for a later start date (published automatically that day); cancel a schedule; close by hand, or automatically after the end date or when the vacancy closes (the hourly background job); **clone** any advert into a new draft
* **Public careers page** `/Careers` (no sign-in) with a page per job; signed-in staff also see internal-portal jobs. Linked from the sign-in page and the candidate home page.
* Emails to the recruiter and HOD when an advert goes live, listing the external channels still to post
* Screens: Job adverts list (*Live*, *Scheduled*, *Drafts*, *All*) with Division, Department, Channel, Period and Status filters; form; details page with channels, copy buttons and history; a *Job adverts* box with *Create advert* on each vacancy page
* MySQL script `database/07_phase04_job_adverts.sql`: JobAdverts, AdvertChannels, JobAdvertActions
* 11 new automated tests (64 in total), including the journey through the web pages and the public careers page
* New [Job adverts user guide](13-job-adverts.md); the other docs updated

**Decisions recorded**
* **No automatic posting to LinkedIn, Indeed, Jobberman or newspapers.** Those need paid business accounts and API agreements (LinkedIn's job-posting API is for approved partners only), and newspapers take adverts by email. GRSMS prepares the text and tracks each posting instead. A real connection can be added per site later.
* **"Company website" = the GRSMS careers page.** GREL's own website links to `/Careers`, so nothing needs installing on that website.
* Recruitment Officers **and HR Managers** manage adverts (the same people who manage vacancies). There is no separate approval step: the content was approved with the vacancy.
* Closing an advert removes it from the automatic channels at once; external postings must be taken down by hand and recorded.

## ✅ Phase 5: Candidate portal and application tracking (November 2026)

**Delivered**
* **My profile** for every account (candidates and staff): phone, location, highest education, field of study, years of experience, current job, skills and certifications (cleaned lists, ready for CV screening in Phase 6), LinkedIn link, summary, **CV** (one current CV) and up to 10 certificates
* **Apply online** from the careers page through a live advert: one application per person per vacancy (also enforced in the database), numbered `APP-<YEAR>-00001`; profile must have a phone number, education level and CV; source ("where did you see this job?"), optional covering message and a confirmation. Staff-only vacancies accept staff only; nothing can be applied for once the advert or vacancy closes.
* Each application **freezes a copy** of the applicant's details and documents, so later profile edits never change what was sent, and stores the division, department and requesting HOD of its vacancy
* **Tracker** for applicants (Received → Under review → Shortlisted → Interview → Offer → Hired, with a cross where it stopped), "My applications", withdrawal, and a candidate home page showing profile readiness and recent applications
* **Applications** screens for staff: *New*, *Under review* and *All* tabs; filters for division, department, vacancy, period, staff/external, source and status; the applicant's details, covering message and documents; **Start review** and **Mark not successful** (internal reason, never shown to the applicant)
* Emails: confirmation to the applicant, new application and withdrawal to the recruiter, a polite outcome email to the applicant
* An **Applications** KPI card on the dashboard (follows the Division / Department filter); application counts on each vacancy page; sidebar section *My career* / *My job search* for everyone
* MySQL script `database/08_phase05_applications.sql`: CandidateProfiles, JobApplications, JobApplicationActions
* 8 new automated tests (72 in total), including the journey through the web pages (sign in to apply, profile, CV upload, apply, review, privacy)
* New [Applications user guide](14-applications.md); the other docs updated

**Decisions recorded**
* **Staff use the same portal** to apply for internal vacancies (the specification's "Employee: views internal vacancies, applies internally, tracks applications"), so there is one way to apply and one tracker.
* Applicants never see staff comments or the internal reason for an outcome; they see a fixed, polite wording.
* An application keeps working after its vacancy closes (selection continues); only new applications stop.
* Applications can be withdrawn until an offer is made (from Phase 8, offers are handled in their own step).

## ✅ Phase 6: CV screening and shortlisting (November 2026)

**Delivered**
* **Automatic screening** of every application when it is submitted: a transparent score out of 100 (education 20, experience 20, essential skills/certifications 45, desirable 15) and a **meets-the-minimum** check, with the matched and missing items stored on the application. Reviewers can re-run it per vacancy.
* **Shortlisting board** per vacancy: requirements, applications ranked (meets minimum first, then score, then first to apply), missing essentials, recommendations and status
* **HOD participation** (from the GREL requirements): the recruiter recommends shortlist / do not shortlist (note required for "do not"), asks the Head of Department (email + a task on their Home page), the HOD agrees or disagrees (comment required to disagree), then the recruiter or HR Manager shortlists. Without the HOD's agreement only an **HR Manager** can shortlist, with a recorded reason (*override*). A changed recommendation asks the HOD again.
* Shortlisted applicants are emailed and their tracker moves to **Shortlisted**
* Screening breakdown and the shortlisting actions on each application page; a *Shortlisting* button on each vacancy
* MySQL script `database/09_phase06_shortlisting.sql`: screening and recommendation columns on JobApplications; its long free-text columns become `TEXT`
* 6 new automated tests (78 in total), including the scoring rules and the recruiter → HOD → shortlist journey through the web pages
* New [Shortlisting user guide](15-shortlisting.md); the other docs updated

**Decisions recorded**
* **Rule-based, not AI.** The specification mentions AI CV screening. GRSMS scores the structured profile data with published rules, so every point can be explained to a panel or a rejected applicant, and nothing personal leaves GREL. The text inside CV files is not read. An AI assistant that reads CVs and suggests matches could be added later as an extra, with its suggestions shown beside (not instead of) this score.
* The score ranks and highlights; **people decide**. No one is rejected automatically.
* The HOD gives a **view**; the recruiter or HR Manager takes the action, so HR stays accountable for the shortlist. Disagreements need an HR Manager and a written reason.

**Problem found and fixed while deploying:** the first version of the Phase 6 migration failed on MySQL with *"Row size too large"*. MySQL limits a row to 64 KB and counts each `VARCHAR` at 4 bytes per character (utf8mb4), and JobApplications had grown past it. The SQLite test database has no such limit, so the tests did not catch it. Long free-text columns on JobApplications (skills, certifications, covering message, outcome reason, notes, screening details) are now `TEXT`, which is stored outside the row; the forms and services still limit their length. The failed attempt changed nothing in the database. **Lesson:** each phase's migration is now applied to the Docker MySQL before it is reported as done.

## ✅ Phase 7: Assessments, interviews and scorecards (November 2026)

**Delivered**
* **Sessions** for shortlisted applicants: interview, written test or practical assessment; in person, video or phone; date and time (GMT), duration, place or link, instructions; several **rounds** per application. The application moves to **Interview**.
* **Panels** of 2-8 GREL staff with a chair. The department's **Head of Department must be on the panel** (GREL requirement); the applicant can never be on their own panel.
* **Weighted scorecard criteria** per session (weights add up to 100; five defaults). Each panel member rates every criterion 1-5, gives strengths, concerns (required to not recommend) and an overall recommendation; GRSMS computes the **weighted score out of 100**.
* **Independent scoring:** panel members see the others' scorecards only after submitting their own. Scorecards open at the start time and stay editable until the session is marked completed.
* Mark completed (needs a scorecard), reschedule (before any scorecard), cancel (with reason) and did-not-attend, all recorded on the application's history
* **Interview results** per vacancy: applicants ranked by average interview score, with screening score, rounds and the panel's recommendations; the HOD reviews all scorecards of their department
* Emails: invitation to the applicant and panel, reschedule and cancellation notices. The applicant's tracker shows the date, place and instructions, never scores.
* **Interviews** menu for every staff member (*My panels*, *Scorecards to do* with a count, *Upcoming*, *All* with division, department and period filters); scorecards to do appear in *My tasks* and the notification bell
* MySQL script `database/10_phase07_interviews.sql`: Interviews, InterviewPanelMembers, InterviewCriteria, Scorecards, ScorecardRatings (long text as `TEXT`)
* 6 new automated tests (84 in total), including scheduling, scoring and reviewing through the web pages
* New [Interviews user guide](16-interviews.md); the other docs updated

**Decisions recorded**
* **One model for interviews and assessments.** A written or practical test is a session with its own criteria (e.g. *Test score | 100*), so results compare the same way.
* **Any staff member can sit on a panel**, without needing the *Interview Panel Member* role; access to each session comes from being on its panel.
* The department's HOD is required on every panel and always sees every scorecard, which covers both "interview evaluation" and "scorecard reviews" in the GREL requirements.
* Times are stored and shown in GMT, which is Ghana's time all year.
* The migration was applied to the Docker MySQL before this phase was reported done.

## ✅ Phase 8: Background checks, hiring recommendation and offers (November 2026)

**Delivered**
* **Background checks** from the interview stage: references, education, previous employment, criminal record, medical, other; pending → cleared / cleared with concerns / failed (notes required for concerns and failures)
* **Hiring recommendation** (the *hiring report*, `HIR-<YEAR>-0001`): proposed salary, start date and justification; needs a completed interview. **The department's HOD endorses or not** (GREL requirement), then the **HR Manager** approves (a written reason is required to approve against the HOD), then the **MD** gives final approval. Proposer, HR approver and MD must be different people; nobody acts on their own application. Withdraw, reject and re-recommend are supported. Stores the division, department and requesting HOD.
* **Offers** (`OFF-<YEAR>-0001`) once approved, with every check complete and none failed, and a free position (sent + accepted offers ≤ positions): salary up to the approved amount, start date, reply-by date (≤ 30 days, ≤ start), terms. Withdraw with a reason; unanswered offers **expire automatically** (hourly job).
* **Offer letter** for the candidate (printable) with **Accept** (with a confirmation tick; application → **Hired**) or **Decline** (application → **Offer declined**)
* **Hiring and offers** menu (*Awaiting my decision*, *Approved*, *With offers*, *All*, with division, department, period and status filters); hiring decisions in *My tasks*; links from applications and interview results
* Emails at every step: HOD (endorse), HR Managers, MD, recruiter (approved / rejected, accepted / declined, expired), candidate (offer, withdrawal, welcome)
* MySQL script `database/11_phase08_hiring.sql`: BackgroundChecks, HiringRecommendations, Offers
* 6 new automated tests (90 in total), including the MD approval, offer and acceptance through the web pages
* New [Hiring user guide](17-hiring.md); the other docs updated

**Decisions recorded**
* **The hiring recommendation is the "hiring report"** in the GREL requirements: one record per recommended candidate, with the evidence, the HOD's endorsement and the approvals, linked to division, department and HOD.
* The HOD **endorses**; HR and the MD **approve**. As in shortlisting, HR can override a HOD who did not endorse, but only with a written reason, so the disagreement is on record.
* A candidate who **accepts** is marked **Hired**. The appointment letter and onboarding follow (delivered in Phase 9).
* The offer letter is a printable web page rather than a PDF file, so no PDF library is needed. A PDF and an electronic signature can be added in Phase 9 with the appointment letter.
* The migration was applied to the Docker MySQL before this phase was reported done.

## ✅ Phase 9: Appointment letters and onboarding (November 2026)

**Delivered**
* **Onboarding** (`ONB-<YEAR>-0001`) started by a Recruitment Officer or HR Manager for each **accepted offer** (one per application, also enforced in the database), from *Waiting to start*, the hiring case or *My tasks*. Stores the division, department, requesting HOD and site of its vacancy, and the salary from the offer.
* **First-day information:** start date (from the offer), time, place, who meets the new employee, notes; the staff number once payroll has one. Moving the start date moves the open checklist items with it.
* **Checklist** of 15 standard items for **HR** (personal file, induction, payroll set-up, benefits, staff account), the **department** (workspace and PPE, department induction, probation objectives), **IT** (email and system access, equipment) and the **new employee** (documents to bring, ticked by HR when received). Default owners: the vacancy's recruiter, the department's Head and the Head of IT, each emailed. Items are done or not needed (with a reason), can be reassigned to any staff member, given a due date, added or removed; overdue items are highlighted.
* **Appointment letter** (`APT-<YEAR>-0001`) issued by an **HR Manager** with the conditions of appointment, revisable until signed, printable, with a staff preview. The new employee **signs online** by typing their name as it appears on the letter and ticking a confirmation; the time, name and network address are recorded. A signed letter is locked, and so is its start date.
* **The new employee's portal:** a *Your first day* card on the application (when, where, who meets them, what to bring and what HR has received) and the letter with its signature box
* **Complete** (letter signed, start date reached, nothing left to do) and **cancel** (with a reason), both by HR, with the department's Head emailed
* **Onboarding** menu (*My tasks*, *In progress*, *Waiting to start*, *All*, with division, department, start-date and status filters) for HR and HODs, and for any staff member while they have items to do, with a count; onboarding items and onboardings to start in *My tasks*; links between the hiring case and the onboarding
* Application history: started, letter issued, letter signed, completed, cancelled
* MySQL script `database/12_phase09_onboarding.sql`: Onboardings, OnboardingTasks (long text as `TEXT`)
* 5 new automated tests (95 in total), including the journey through the web pages
* New [Onboarding user guide](18-onboarding.md); the other docs updated

**Decisions recorded**
* **HR starts onboarding by hand** rather than automatically when an offer is accepted, so the first-day details are checked before anyone is emailed. Waiting cases are listed in *My tasks* and *Waiting to start*.
* **The HR Manager issues the appointment letter.** The Recruitment Officer prepares the onboarding; the letter is a formal document from GREL, so it goes out under HR Manager authority. The MD already approved the hire in Phase 8 and is not asked again.
* **No IT role.** IT items go to the Head of the department with code `IT` by default and can be given to any staff member. Access to an onboarding comes from having an item on it, like interview panels in Phase 7.
* **A typed-name electronic signature**, not a PDF with a drawn signature, so no PDF or e-signature service is needed. The letter is a printable page, like the offer letter. A PDF copy can be added later if GREL's files need one.
* **The candidate account is not converted automatically** to a staff account; that stays an HR item on the checklist, done in *Users & roles*, so roles are only ever given by an HR Administrator.
* The application stays **Hired**; onboarding has its own status.
* The migration was applied to the Docker MySQL before this phase was reported done.

## ✅ Phase 10: Notifications, reminders and surveys (November 2026)

**Scope agreed on 4 November 2026:** in-app notifications, automatic reminders, feedback surveys, and SMS prepared but switched off. Message threads between HR and candidates were **not** included.

**Delivered**
* **Notification bell for everyone** (staff and candidates) with an unread count, a list page, click-through and *Mark all as read*. Every workflow email from Phases 2-9 now also creates a notification with the same title and link, because the hook is in the one shared mailer. Links are stored as paths inside GRSMS only; read notifications are removed after 6 months.
* **Automatic reminders**, each sent once (recorded by key), by email and the bell, from 07:00 GMT: offer reply-by date tomorrow; interview or assessment tomorrow (applicant and panel); first day tomorrow (new employee and the person meeting them); onboarding items due tomorrow or overdue (one daily summary per owner); requests, vacancies and hiring recommendations waiting 3+ days at one stage (then weekly)
* **Text messages (SMS)** to applicants for interview invitations, reschedules and cancellations, offers, appointment letters and the reminders above. Ghanaian numbers are converted to international form. **Switched off**: messages are written to `App_Data/sms` until `Sms:Enabled` is set; a Hubtel connection is included but untested
* **Feedback surveys**: *candidate experience* (the day after an application ends not successful, offer declined or hired) and *new employee onboarding* (the day after onboarding is completed); 1-5 ratings with *not applicable* and a comment, 30 days to answer, on the bell, by email and on the application page. Invitations store the division, department and requesting HOD.
* **Survey results** for the HR Manager, Recruitment Officers and MD: response rate, averages, spread and comments, filtered by division, department and period, without names and only from 3 responses
* Notifications, reminder keys and survey answers are **not copied into the audit trail** (new `INotAudited` marker)
* MySQL script `database/13_phase10_notifications_surveys.sql`: Notifications, SentReminders, SurveyInvitations, SurveyAnswers
* 5 new automated tests (100 in total), including the bell and a survey through the web pages
* New [Notifications, reminders and surveys guide](19-notifications-and-surveys.md); configuration and the other docs updated

**Decisions recorded**
* **The bell shows notifications, not tasks.** Before Phase 10 it counted pending approvals for some roles; those stay in *My tasks* on the Home page and the sidebar counts.
* **One hook, not one per module:** notifications are created by `WorkflowMailer`, so every past and future workflow email gets one automatically. They are saved in their own database context, so they never save a service's unfinished changes.
* **Reminders are sent once per key**, not on a fixed schedule, so the hourly job can safely run any number of times.
* **Surveys use fixed questions** defined in code (no survey designer) so results stay comparable over time. New questions get new keys; old keys are never reused.
* **Survey confidentiality:** no names on the results page, nothing shown under 3 responses, answers kept out of the audit trail, and results not shown to HODs.
* **SMS is ready but off.** Turning it on needs a GREL account with an SMS provider (Hubtel is built in; mNotify or others would need a small addition), a registered sender ID, and one live test. Each text costs money.
* The migration was applied to the Docker MySQL, and the reminder and survey job was run against it, before this phase was reported done.

## ✅ Phase 11: Reports and dashboards (November 2026)

**Delivered**
* **Reports page** (menu *Reports*) with eight reports sharing one filter bar: **division, department, vacancy, recruitment status and recruitment period** (GREL requirement), the status list being that report's own statuses and the period applying to its main date
* **Overview (executive dashboard):** requests raised, positions approved, vacancies published, applications, hires, offer acceptance rate, average time to hire, positions still open; the recruitment funnel; applications and hires per month (12 months); divisions compared
* **Recruitment requests**, **Vacancies** (applications at each stage, filled, open), **Applications** (source, score, furthest stage), **Application sources**, **Offers** (acceptance rate, salaries, reply times), **Time to hire** (request → approval → published → accepted, by department), **Onboarding** (letters, checklists, overdue items); a link to the Phase 10 survey results
* **CSV download** of every report with the same filters, protected against spreadsheet formulas
* Visibility as everywhere else: HR, the MD and the HR Administrator see all departments; a HOD sees only theirs
* One-colour bar and column charts with value labels, hover details and a table view
* 6 new automated tests (106 in total), including every report through the web pages and the CSV download
* New [Reports guide](20-reports.md); the other docs updated
* No database change (no new script)

**Decisions recorded**
* **Live figures, no stored statistics.** Every report reads the current records, so it is never out of date and needs no overnight job. Fine for GREL's volumes; the slowest reports could be pre-calculated later without changing the pages.
* **"Reached a stage" comes from the history**, so an applicant turned down after an interview still counts as interviewed. This makes the funnel honest.
* **CSV rather than an Excel file**, so no extra library is needed; Excel opens it directly.
* **The Home dashboard stays as it is** (personal tasks and short trends); the executive view is the *Overview* report.
* The HR Administrator can read reports (but, as before, not survey results).

**Problem found and fixed while running it:** signing in to the Docker app and opening the reports gave *"Unable to cast DateTime to DateOnly"* on every report, and the same error was found on **Vacancies → All** (there since Phase 3, unseen until the Docker database held a vacancy). The Oracle MySQL provider returns `DATE`/`TIME` columns as `DateTime`/`TimeSpan` and cannot turn them into `DateOnly`/`TimeOnly`; SQLite (the tests) has no such problem. `ApplicationDbContext.ConfigureConventions` now converts every `DateOnly`/`TimeOnly` through `DateTime`/`TimeSpan` with the same column types (no database change; EF confirms no pending model changes). All reports, the vacancy list and the CSV download were then checked against MySQL, and the charts were screenshotted. **Lesson:** besides applying each migration, open the new pages against the Docker MySQL with real rows before calling a phase done.

## ✅ Phase 12: Security hardening (November 2026)

**Delivered**
* **Content-Security-Policy** on every response: scripts, styles, fonts and images only from GRSMS itself, no plug-ins, no framing, forms only to GRSMS and Microsoft's sign-in page. The three inline `onchange` handlers were moved to `site.js`, so the policy breaks nothing (checked in a real browser against the Docker app: no violations).
* Extra headers: `Cross-Origin-Opener-Policy`, `X-Permitted-Cross-Domain-Policies`, a wider `Permissions-Policy`; the `Server` header removed; the anti-forgery cookie hardened (HttpOnly, SameSite=Strict, Secure outside development)
* **Rate limiting** on the sign-in, registration, resend-confirmation, forgot-password and reset-password forms: 10 posts a minute per network address (`Security:SignInAttemptsPerMinute`), then a *Too many attempts* page (HTTP 429) and a log warning
* **Security review** of the whole system ([04 Security → Security review](04-security-and-roles.md#security-review-phase-12-4-november-2026)). **Fixed:** the sign-in page revealed with a *wrong* password that an account existed and was deactivated or unconfirmed, and such attempts did not count towards the lockout. No vulnerable packages; no secrets in source; tokens never logged.
* 3 new automated tests (109 in total): the headers, **no inline script on any main page** (a guard for the policy), sign-in that reveals nothing with a wrong password, and the rate limit
* Security, configuration and testing docs updated; no database change

**Decisions recorded**
* **Inline `style="..."` attributes stay allowed** (`style-src 'unsafe-inline'`). They cannot run code; the stricter attribute-only rule is not supported by every browser, and charts and progress bars rely on them.
* **The rate limit is per network address, on the forms that send emails or check passwords only**, not on every page; the per-account lockout (5 wrong passwords) stays as it was.
* Production settings (Production mode, HTTPS, forwarded headers for the real client address) belong to Phase 13 and are listed there.

## ✅ Phase 13: Production deployment (November 2026)

**Delivered**
* **Production set-up** in `deploy/`: `docker-compose.prod.yml` with MySQL, GRSMS in **Production** mode, a **Caddy** HTTPS front end (automatic Let's Encrypt, GREL's own certificate, or an internal one; HTTP → HTTPS; no `Server` header) and a **backup** container; only ports 80/443 open; all settings in `deploy/.env` (template `.env.production.example`; real file, backups and certificates git-ignored)
* **Forwarded headers**: behind the proxy GRSMS takes the visitor's address and HTTPS from `X-Forwarded-For` / `-Proto`, trusted **only from the proxy's network** (`ReverseProxy:*`), so the audit trail and the sign-in rate limit see real addresses
* **`/health`** endpoint for uptime monitoring (database reachable or not, no details)
* **Nightly backups**: database dump plus uploads and keys, 30 days kept, one-command backup on demand; restore procedure written and tested
* **Safe start-up**: with automatic database updates off, GRSMS refuses to start while an update is missing and names it, instead of failing later on a page
* `.gitattributes` keeps Unix line endings for the scripts run inside Linux containers
* New [Production deployment guide](21-production-deployment.md) for GREL IT, with updates, rollback, restore, monitoring and the **go-live checklist**; configuration, security and Docker docs updated
* 1 new automated test (110 in total). The production stack was also run end to end on a PC: Production mode, HTTPS redirect, secure cookies, CSP, sign-in, the visitor's address in the audit trail, `/health`, backup **and restore** (40 tables), and the refusal to start on a missing update.

**Decisions recorded**
* **Docker with Caddy** as the recommended production set-up: it works on Linux and Windows Server, matches the developer set-up, and Caddy handles certificates and renewal itself. IIS remains possible (guide, section 9).
* **Database updates are applied automatically at start-up** in the production set-up, after a backup taken as the first step of every update. GREL can switch to DBA-run scripts with `APPLY_MIGRATIONS=false`.
* **phpMyAdmin is not part of production.** Database access is for administrators on the server only.
* Backups stay on the server for 30 days; **copying them off the server is GREL IT's responsibility** (on the go-live checklist).

**Not tested here** (needs GREL's environment): a real domain and Let's Encrypt certificate, GREL's SMTP server, Microsoft 365 sign-in, an SMS account.

## ✅ Phase 14: Super Admin and granular permissions (5 November 2026)

**Asked for** (5 November 2026): a Super Admin role with full access; only Super Admins create HR Managers, Recruiters and HOD accounts and configure system-wide permissions; granular RBAC; every action in the audit trail. Part of a larger request that also covers the ATS screening engine (Phase 15) and system settings (Phase 16). The order was swapped so that the ATS settings can be protected by the new permissions from the start.

**Delivered**
* **Super Admin** role: holds every permission (it can't be taken away), sees every department, and is the only role that can create privileged accounts or change permissions. The `SeedAdmin` account is a Super Admin. On an existing installation, the first start-up gives that account the role (it keeps its other roles); a brand-new installation creates it as Super Admin + Employee.
* **Permissions** (13, in `Domain/Security/Permissions.cs`): every screen policy is now a permission held by roles, stored in `RoleClaims`. The defaults reproduce exactly what each role could open before, so nobody gained or lost access. New permissions get their defaults once (table `KnownPermissions`), so the Super Admin's later choices are never overwritten. "See all departments" replaces the fixed role list used by every module's visibility rule.
* **Roles & permissions** screen (Super Admin): a tick-box grid of roles × permissions grouped by area, with the Super Admin column and the two "Super Admin only" permissions locked. Changes apply **straight away** without signing anyone out (an in-memory copy refreshed on save, and at most a minute old otherwise).
* **Custom roles:** add (readable id `custom-...`), describe, rename, tick permissions, delete when nobody holds them. Built-in roles can't be renamed or deleted.
* **Privileged accounts:** only a Super Admin can give or remove any role other than Employee and Interview Panel Member, or edit, reset the password of, or deactivate an account holding one. The Users screen shows a 🔒 *Super Admin* label instead of the buttons, and the user form locks those roles. The "last active HR Administrator" rule became "last active Super Admin"; you can't remove your own Super Admin role.
* Audit trail: permission grants are `RoleClaim` rows (new friendly name), roles are `Role` rows, role assignments `UserRole` rows, so every change records who, when and from where.
* MySQL script `database/14_phase14_super_admin_permissions.sql`; migration `Phase14_SuperAdminAndPermissions`
* 6 new automated tests (117 in total), including taking a permission from a signed-in HOD through the real pages
* Security, admin, database, getting-started, configuration, deployment and testing docs updated

**Decisions recorded**
* **Permissions open screens; workflow steps stay with the role.** Who raises, reviews, approves and gives final approval is separation of duties written in each workflow class, so no setting can switch it off. A Super Admin who must approve as the MD also needs the Managing Director role, and the "not your own request" rules still apply.
* **Privileged = every role except Employee and Interview Panel Member**, including HR Administrator, the MD and custom roles. The request named HR Managers, Recruiters and HODs; HR Administrator and MD were added because giving them would also raise access, and custom roles because they can carry any permission.
* **Permission list in code, assignments in the database.** Each permission is checked by code, so new ones come with a new version; which roles hold them is GREL's choice.
* **Live permission check instead of copying permissions into the sign-in cookie**, so a removed permission stops working at once.
* Custom-role descriptions are stored as a `RoleClaims` row (`grsms:description`) rather than changing the Identity role table.

**Problem found and fixed while checking against MySQL:** an empty tick-box value in a posted grid arrived as *null* and gave an error page (500). Empty values are now ignored. While checking, a test script of mine that read the grid wrongly saved it with most boxes empty, which removed 37 default grants from the **local Docker database**. The defaults were re-applied by clearing the `grsms:permission` rows and `KnownPermissions` and restarting (now described in [03 Database](03-database.md#roleclaims-and-knownpermissions-phase-14)). The app had done exactly what was posted. **Lesson:** a "save the whole grid" form saves what it is sent, so check the posted fields before trusting a hand-made post.

## ⏳ Phase 15: ATS screening engine (planned)
CV text reading on GREL's server (PDF and DOCX) with an optional Claude AI reader that stays off until GREL approves it; extracted details (name, email, phone, education, experience, certifications, skills, employment history); configurable criteria per vacancy; mandatory knockout questions; education / experience / skills / certification scores and an overall match % (weights configurable, 25% each by default); classification bands (Highly Recommended ≥ 90, Recommended ≥ 80, Review Required ≥ 70, Low Match ≥ 50, Auto Reject < 50); **Automatically Rejected** status with the reason recorded, **held for HR**: the applicant is told only after HR confirms, and HR can override; recruiter dashboard (top ranked, rejected, missing skills, ranking table, score breakdown); ATS settings and auto-rejection rules for Super Admins only; every decision stored and audited.

## ⏳ Phase 16: System settings (planned)
Email templates, recruitment workflow settings, AI scoring thresholds, Microsoft 365 settings, backups (list, run now, download; restore stays a server procedure), audit-log monitoring.

## 🎉 All 13 original phases delivered
Next steps are GREL's: work through the [go-live checklist](21-production-deployment.md#8-go-live-checklist), then gather users' feedback for improvements.

**Rules every later phase must follow** (from the GREL requirements, 4 November 2026; see [12 Divisions and departments](12-divisions-and-departments.md#what-later-phases-must-do)):
* Every candidate application, interview panel, hiring report, dashboard KPI and onboarding record stores its **division, department and requesting HOD** (copied from the vacancy, the same way vacancies copy them from the request).
* Reports (Phase 11) filter by **division, department, vacancy, recruitment status and recruitment period**.
* The department's **HOD takes part** in shortlisting (Phase 6), interview evaluation and scorecard review (Phase 7), and gives the final hiring recommendation (Phase 8) for positions in their department.
