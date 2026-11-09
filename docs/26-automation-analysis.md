# 26. Recruitment-to-confirmation automation: analysis and proposed design

**Status:** APPROVED 9 November 2026. Every question is answered (see §12, *Decisions recorded*). Nothing is built yet; delivery runs Phases 22–26.
**Date:** 9 November 2026 · **Branch:** `feature/ats-and-super-admin` (after Phase 21, commit a20c1a6)

This is a review of GRSMS **as it is built today** (Phases 1–21), set against the automation brief. For every step it says what is already automatic, what is still manual, and what is proposed. Where the brief conflicts with a decision you made earlier, this document points out the conflict and asks again. It does not quietly reverse the decision.

Legend: ✅ built and automatic · 🟡 built but manual or partial · ❌ not built · 🔒 should stay a human decision

---

## 1. What already exists (baseline)

| Area | Already automatic today | Still manual today |
|---|---|---|
| Request → vacancy | Approval routing HOD → HR → MD (QF-81-11), emails + bell at each step, a reminder after 3 days waiting and then weekly | HR converts an approved request into a vacancy |
| Adverts | Publish on start date, close on end date (hourly job) | Writing the advert |
| Vacancy | Auto-close on closing date | — |
| ATS | CV text extraction, scoring, auto-reject held until HR confirms | HR confirms/overrides auto-rejects 🔒 |
| Interviews | Invitations to the applicant and panel, reminder **1 day** before, HOD forced onto the panel, weighted scorecards, ranking | Panel **cannot accept/decline**, no attendance per member, no quorum, no 7/3-day or 2-hour reminders, no calendar invite, no Teams link |
| Hiring | HOD → HR → MD approval routing and reminders | Recruiter types salary/start/reply-by and clicks **Send offer** |
| Offer | Candidate accepts/declines in the portal, offers expire automatically, reminder the day before expiry | No "request clarification", no frozen offer letter, no PDF, offer letter not filed |
| Onboarding | 8 stages, document rejection loop, induction tasks created on HR approval, daily overdue summary, personnel file opened **and filled at activation** | HR must click **Start onboarding** after acceptance, no completion %, tasks can't be accepted/returned |
| Probation | Opens automatically at activation, deadlines calculated, confirmation letter generated and filed | **No reminders** for due meetings/assessments (known gap), objectives have no status, training not tracked |

**Technical baseline:** one `BackgroundService` ([VacancyAutoCloseWorker.cs](../src/Grsms.Web/Workers/VacancyAutoCloseWorker.cs)) runs every 60 minutes and calls the expiry, advert, reminder and survey jobs. Emails are sent **inline** during the web request, with no outbox or retry. Reminders are de-duplicated by key. There is no anonymous token-link mechanism, no Microsoft Graph or Teams integration, and no PDF library (by decision: HTML letters, browser "Save as PDF").

---

## 2. End-to-end workflow (proposed)

```
 REQUEST & VACANCY
 Staff/HOD raises QF-81-11 ─► HOD ─► HR Mgr ─► MD ─► HR converts to vacancy ─► Advert published (auto on date)
      [3-day wait reminder → weekly; ❓ escalate to approver's superior?]

 APPLICATION & SCREENING
 Candidate applies ─► ATS scores (auto) ─► auto-reject HELD ─► HR confirms 🔒 ─► Shortlist 🔒
                                                     └─► ❓ auto-email "not successful" after confirmation

 INTERVIEW (NEW AUTOMATION)
 RO schedules + picks panel ─► invitations + .ics + [Teams link] to panel & candidate
      ├─ each panel member: Accept / Decline(reason) from email ──► decline ⇒ HR alerted, alternates suggested
      │                                                         └─► quorum lost ⇒ "Reschedule needed" task
      ├─ candidate: Confirm attendance / Request new date ❓
      ├─ reminders T-7d, T-3d, T-1d, T-2h (only to people who accepted or haven't answered)
      └─ on the day: chair/RO marks each person Present / Absent / Excused ─► quorum check ─► scorecards open
 Scorecards ─► session completed ─► ranked results

 HIRING & OFFER
 Checks cleared ─► Recommendation ─► HOD endorse ─► HR approve ─► MD approve (final)
      └─► AUTO: offer DRAFT created (ref no., letter from approved terms) ─► RO/HR "Send" 🔒 ❓ or fully auto-send
 Candidate email ─► Accept │ Decline(reason) │ Request clarification (question → RO task, deadline paused? ❓)
      ├─ Accept  ⇒ Offer Accepted, application Hired, AUTO: onboarding record + checklist + personnel file
      │           (provisional) + offer letter filed + welcome email + appointment letter DRAFT
      ├─ Decline ⇒ offer closed, reason stored, recruitment team notified, next-ranked candidate suggested
      └─ Expired ⇒ same as decline (already automatic)

 ONBOARDING (8 stages, already built; automation added)
 Draft ─► HR Mgr issues appointment letter 🔒 ─► employee signs + personal record + documents ─► submit
   ─► HR verifies documents 🔒 ─► HR Mgr approves (≠ verifier) 🔒 ─► departments get tasks AUTOMATICALLY
   ─► owners Accept / Complete / Return ─► HR verifies ─► all done + start date reached
   ─► HR enters staff no. & activates 🔒 ─► staff account + personnel file finalised + probation opened (auto)

 PROBATION (automation added)
 Day 0 activation ─► initial meeting due ≤7d (reminders, ❓ calendar invite)
   ─► HOD records objectives, both sign ─► objective status tracked
   ─► Month 3: assessment form auto-opened, notify employee/HOD/HR, reminders, escalate if overdue
   ─► Month 6: final assessment auto-opened, same notifications
   ─► Recommendation: Confirm / Extend / Do not confirm ─► HR Manager decision 🔒
        ├─ Confirm      ⇒ letter CNF-… generated + filed + emailed, probation closed (already automatic)
        ├─ Extend       ⇒ new end date, follow-up final assessment scheduled + reminders (reminders new)
        └─ Not confirm  ⇒ reasons required, assessment archived, HR Manager approval (already required)
```

---

## 3. Users, responsibilities, deadlines and escalation

| Stage | Acts | Deadline (current / proposed) | Escalation (proposed) |
|---|---|---|---|
| Request approval | HOD, HR Mgr, MD | none / reminder at 3 days, weekly after | ❓ to HR Manager after N days at HOD stage |
| Vacancy conversion | Recruitment Officer | none / **3 days** after MD approval | HR Manager |
| ATS auto-reject confirmation | RO | none / **2 days** after the vacancy closes | HR Manager |
| Shortlisting | RO, HOD | none / ❓ | HR Manager |
| Panel RSVP | Panel members | **48 h** before the interview, or T-3d ❓ | RO + HR Manager |
| Attendance marking | Chair or RO | same day | RO |
| Scorecards | Panel | none / **2 working days** after the session | Chair, then RO |
| Hiring approvals | HOD, HR Mgr, MD | 3-day reminder (built) | ❓ |
| Offer reply | Candidate | 7 days default, max 30 (built) | auto-expire (built) |
| Clarification reply | RO / HR Mgr | **2 working days** ❓ | HR Manager |
| Employee submission | New employee | none / ❓ before the start date | RO |
| Document verification | RO / HR | none / **3 working days** | HR Manager |
| Induction tasks | Area owners | 7 days after induction starts (built) | ❓ area owner's HOD, then HR Manager |
| Initial probation meeting | HOD | 7 days (built, no reminder) | HR Manager |
| 3- and 6-month assessments | HOD | due dates (built, no reminder) | HR Manager at due date + 7 days |
| HR decision on final assessment | HR Mgr | none / **5 working days** | MD ❓ |

---

## 4. Automation opportunities by brief task

### Task 2: interview automation
| # | Item | Status | Proposal |
|---|---|---|---|
| I1 | Panel roles (HR Mgr, RO, HOD, member, technical assessor, MD) | ❌ only `IsChair` | Add `PanelRole` to `InterviewPanelMember` |
| I2 | Invitation with candidate details, date, time, venue, type | 🟡 email exists | Add candidate summary (name, position, CV link inside GRSMS, never attached), `.ics` calendar file |
| I3 | Physical / Teams / Online | 🟡 InPerson/Video/Phone | Rename the labels. Teams link: pasted by the RO (simple) **or** created through Microsoft Graph ❓ |
| I4 | Accept / Decline from the email | ❌ | Signed single-use links per panel member (see §8, security). Decline requires a reason (form opens after the click). Records status, time, reason |
| I5 | Decline → notify HR, suggest alternates, reschedule if quorum lost | ❌ | Alternates = staff in the same department, or earlier panellists for the same vacancy, who have no clash at that time. Quorum rule ❓ |
| I6 | Reminders 7d / 3d / 1d / 2h | 🟡 1 day only | New reminder keys. The 2-hour reminder needs the job to run every **15 minutes** (today: 60) |
| I7 | Attendance Present / Absent / Excused | ❌ (only the candidate's no-show) | Per panel member, plus the candidate. Quorum check, HR alerted |
| I8 | Non-responders | ❌ | Chase the people who haven't replied at T-3d, alert the RO at T-2d |
| I9 | Candidate confirms attendance | ❌ | Same link mechanism ❓ |

### Task 3: offer automation
| # | Item | Status | Proposal |
|---|---|---|---|
| O1 | Offer generated when final approval is given | 🟡 RO clicks *Send offer* | Automatic **draft** from the approved salary, start date and default terms, with a reference number assigned. Send is one click ❓ (or fully automatic) |
| O2 | Reference number | ✅ `Offer.Number` | — |
| O3 | PDF | ❌ (HTML by decision) | ❓ keep HTML + print-to-PDF, or add a server-side PDF (see §9) |
| O4 | Store in the personnel file | ❌ the file exists only after activation | Open the personnel file as **provisional** at acceptance ❓ and freeze the offer letter HTML into *Recruitment Documents* |
| O5 | Accept / Decline / **Request clarification** | 🟡 accept/decline only | Add a clarification thread (candidate question → RO task → answer emailed). Pauses the expiry ❓ |
| O6 | On accept: employee record, onboarding, file, welcome, workflow | 🟡 Hired + welcome; onboarding manual | Auto-create `OnboardingRecord` (Draft), default first-day details, provisional personnel file, appointment-letter draft, HR Manager task "issue letter". **Note:** "Employee record" = the staff account, which is activated only at the end of onboarding (Phase 18 decision). Unchanged unless you say otherwise |
| O7 | On decline: close, reason, notify | 🟡 reason optional, only the recruiter emailed | Reason **required** (pick list + text) ❓; notify RO, HR Managers, HOD; propose the next-ranked candidate |

### Task 4: onboarding
| # | Item | Status | Proposal |
|---|---|---|---|
| N1 | Checklist, declaration, beneficiaries, next of kin, emergency contact | ✅ (sections of the Personal Record) | Created automatically with the onboarding (O6) |
| N2 | Departmental tasks created automatically | ✅ on HR approval | — |
| N3 | Probation record at acceptance | ❌ created at activation | Keep at activation (probation dates depend on the actual start) ❓ |
| N4 | Completion %, missing tasks, outstanding verifications | ❌ | Calculated: weighted (documents, personal record, signatures, tasks) shown on the onboarding page, dashboard and report |
| N5 | Employee chase before the start date | ❌ | Reminders at start-7d and start-3d for missing documents |

### Task 5: departmental induction
| # | Item | Status | Proposal |
|---|---|---|---|
| D1 | Departments get tasks automatically | ✅ 12 areas, owner per area | — |
| D2 | Email to department representatives | ✅ on approval | Add per-task links |
| D3 | **Accept** task | ❌ | New status *Accepted* (owner acknowledges, due date confirmed) |
| D4 | **Complete** | ✅ *Done* + HR verify | — |
| D5 | **Return** task (wrong owner, can't do, needs info) | ❌ | New status *Returned* + reason → back to HR to reassign |
| D6 | Examples (uniforms, tools, laptop, access card) | 🟡 | Seed the extra tasks in the areas. Role-specific tasks ❓ |
| D7 | Overdue escalation | 🟡 daily summary to the owner | Escalate to the area owner's HOD after N days ❓ |

### Task 6: probation
| # | Item | Status | Proposal |
|---|---|---|---|
| P1 | Day 1: schedule the initial meeting | 🟡 due date only | Reminders to the HOD at day 1, day 5, overdue; optional `.ics` |
| P2 | Month 3: generate the assessment + notify the employee, HOD, HR | 🟡 form available from -14 days, no notification | Auto-open at due-14d, notify all three, reminders at -7d, 0, +7d (escalate) |
| P3 | Month 6: final assessment + notify | 🟡 same | Same as P2, notify the HR Manager |
| P4 | Outstanding / completed objectives | ❌ no objective status | Add `ObjectiveStatus` (Not started / In progress / Achieved / Not achieved), updated by the HOD at each assessment |
| P5 | Training activities | 🟡 development plan, no tracking | Add completion status + date per development-plan line |

### Task 7: confirmation
| # | Item | Status | Proposal |
|---|---|---|---|
| C1 | Confirm ⇒ letter, PDF, notify, close | ✅ (HTML letter) | PDF per O3 decision |
| C2 | Extend ⇒ record, new end date, follow-up | ✅ record + date; ❌ follow-up reminders | Follow-up via P3 reminders. Extension = separate `ProbationExtension` rows (today: running total on the record) |
| C3 | Not confirmed ⇒ HR Mgr approval, reasons, archive | ✅ approval + comments | Structured reason; "archive" = lock the record + file the assessment (already filed) |

---

## 5. Manual activities: classification

| Activity | Today | Recommendation |
|---|---|---|
| Converting an approved request into a vacancy | Manual | **Semi-auto**: pre-filled vacancy draft, RO confirms (owner, closing date) |
| Confirming ATS auto-rejects | Manual | **Keep human** (your decision of 5 Nov) |
| Shortlisting | Manual | **Human judgement**, ATS ranking as the aid |
| Choosing the panel | Manual | **Human**, with auto-suggested members + clash check |
| Chasing the panel for availability | Manual (outside the system) | **Automate** (I4, I6, I8) |
| Rescheduling after a decline | Manual | **Semi-auto**: system flags it and proposes, RO picks the new time |
| Attendance | Not recorded | **Human input**, automatic quorum check |
| Hiring approvals | Workflow | **Keep approval-based** 🔒 |
| Drafting the offer | Manual typing | **Automate the draft** from the approved recommendation |
| Sending the offer | Manual click | ❓ one-click (recommended) vs automatic |
| Starting onboarding after acceptance | Manual | **Automate** |
| Issuing the appointment letter | HR Manager | **Keep approval** 🔒 (auto-drafted) |
| Document verification | HR | **Human judgement** 🔒 (compared against originals) |
| Onboarding approval | HR Manager | **Keep approval** 🔒 (segregation of duties) |
| Creating induction tasks | Automatic | ✅ |
| Verifying induction tasks done outside HR | HR | Keep, or ❓ auto-verify for trusted areas (IT, Security) |
| Staff number from payroll | Manual | Manual until a payroll integration exists (out of scope) |
| Chasing HODs for probation reviews | Manual | **Automate** (P1–P3) |
| Probation outcome | HOD + HR Manager | **Human judgement** 🔒 |
| Letters (confirmation) | Automatic | ✅ |

---

## 6. Notification matrix (new or changed rows **bold**)

| Event | Candidate / employee | Panel | RO | HR Mgr | HOD | MD | Area owner | Channel |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| **Interview scheduled** (with links + .ics) | ✉ | ✉ links | ✉ | ✉ if on panel | ✉ | ✉ if on panel | | email, bell, SMS (candidate) |
| **Panel member declined** | | | ✉ | ✉ | ✉ | | | email, bell |
| **Quorum at risk / lost** | | | ✉ | ✉ | ✉ | | | email, bell, task |
| **Interview reminder 7d/3d/1d/2h** | ✉ (1d, 2h) | ✉ | | | ✉ | | | email, bell, SMS 1d |
| **Panel not responded (T-2d)** | | ✉ chase | ✉ | | | | | email |
| **Attendance: quorum not met** | | | ✉ | ✉ | | | | email, bell |
| **Scorecard overdue** | | ✉ | ✉ | | | | | email |
| Final hiring approval | | | ✉ | | ✉ | | | (built) |
| **Offer draft ready** | | | ✉ task | ✉ | | | | bell, task |
| Offer sent | ✉ links | | | | | | | (built) + links |
| **Clarification requested / answered** | ✉ answer | | ✉ task | ✉ | | | | email, bell |
| Offer accepted | ✉ welcome | | ✉ | **✉** | ✉ | | | (built) + HR Mgr |
| **Offer declined / expired** | | | ✉ | ✉ | ✉ | | | email, bell |
| **Onboarding auto-started; issue letter** | | | ✉ | ✉ task | | | | bell, task |
| **Missing documents (start-7d, -3d)** | ✉ | | ✉ (-3d) | | | | | email, SMS |
| Induction tasks created | | | | | ✉ | | ✉ links | (built) + links |
| **Task returned** | | | ✉ | | | | | email, bell |
| **Task overdue escalation** | | | ✉ | | ✉ owner's HOD | | ✉ | email |
| Employee activated | ✉ | | | | ✉ | | | (built) |
| **Initial meeting due / overdue** | ✉ | | | ✉ overdue | ✉ | | | email, bell |
| **3-month assessment open / due / overdue** | ✉ | | | ✉ | ✉ | | | email, bell |
| **Final assessment open / due / overdue** | ✉ | | | ✉ | ✉ | | | email, bell |
| Final assessment submitted | | | | ✉ task | | | | (built) |
| Confirmed / extended / not confirmed | ✉ | | | | ✉ | | | (built) + HOD |

---

## 7. Approval matrix

| Decision | Approver(s) | Rules kept | Change proposed |
|---|---|---|---|
| Recruitment request | HOD → HR Mgr → MD | HOD's own request skips stage 1 | — |
| ATS auto-reject | RO / HR | Applicant told only after confirmation | — |
| Panel change after a decline | RO (or HR Mgr) | — | **New**: replacement ≠ candidate, no clash |
| Proceed below quorum | ❓ HR Manager | — | **New** |
| Hiring recommendation | HOD → HR Mgr → MD | proposer ≠ approver, HR ≠ MD | — |
| Offer send | RO / HR Mgr | salary ≤ approved | ❓ auto-send |
| Offer deadline extension | ❓ RO or HR Mgr | max 30 days | **New** |
| Appointment letter | HR Manager | — | — |
| Onboarding approval | HR Manager ≠ verifier | — | — |
| Probation extension | HOD recommends, HR Mgr approves | total ≤ 3 months | ❓ more than once / longer |
| Not confirmed | HR Mgr | — | ❓ MD too? |
| Confirmation | HR Mgr | — | ❓ HOD alone ever? |

---

## 8. Recommended architecture changes

1. **Scheduler**: replace the single hourly loop with a small job registry (`IScheduledJob` with its own interval: 15 min for interview reminders, hourly for the rest, daily for summaries). Still a hosted `BackgroundService` in the web app, no new infrastructure (Hangfire/Quartz not needed at GREL's volume). Keep the existing reminder-key de-duplication.
2. **Email outbox**: write emails to an `OutboundMessage` table in the same transaction as the change; a job sends them with retry and backoff. Today a Hostinger SMTP failure inside a request can lose the email or fail the action. This becomes essential once one event fans out to 8 panel members.
3. **Action links (email Accept/Decline)**: `ActionToken` table storing the *hash* of a random 256-bit token, purpose, target id, user id, expiry, used-at. Links open a confirmation page (one click, so link scanners that pre-fetch URLs don't trigger the action). Security tiers:
   * **Panel RSVP, task accept/return:** token alone is enough (low risk, internal).
   * **Offer accept/decline, clarification:** the link opens the page, but the **candidate must sign in** (legally significant decision, typed-name signature as today).
4. **Domain events**: `OfferAccepted`, `PanelMemberDeclined`, `QuorumLost`, `ProbationAssessmentDue`… raised by the services and handled in-process after save. This keeps HiringService from calling OnboardingService directly, and the handlers can be tested on their own.
5. **Calendar**: generate `.ics` (RFC 5545) attachments, which works with Outlook and needs no integration. Teams meetings through Microsoft Graph are optional and need an Azure app registration with `OnlineMeetings.ReadWrite.All` ❓.
6. **PDF** ❓: options in §10 Q-O3.
7. **Settings**: every new deadline and interval in System settings (Phase 16 catalogue), not hard-coded.

---

## 9. Database entities, services and jobs

### New / changed entities
| Entity | Change |
|---|---|
| `InterviewPanelMember` | + `Role`, `ResponseStatus` (Pending/Accepted/Declined/Replaced), `RespondedAt`, `DeclineReason`, `Attendance` (Present/Absent/Excused), `AttendanceNote`, `ReplacedByUserId` |
| `Interview` | + `MeetingUrl`, `CandidateResponse`, `CandidateRespondedAt`, `QuorumRequired`, `QuorumStatus` |
| `InterviewQuorumRule` (settings) | min panel, required roles (e.g. HOD + 1 technical) |
| `ActionToken` | new (see §8.3) |
| `OutboundMessage` | new: to, subject, body, attachments, status, attempts, next attempt, error |
| `Offer` | + `Status = Draft`, `LetterHtml` (frozen), `LetterHash`, `DeclineReasonCode`, `ExtendedFrom`, `ExtensionCount` |
| `OfferClarification` | new: question, asked at, answer, answered by/at |
| `PersonnelFile` | + `Status` (Provisional/Active/Closed) if Q-O4 = yes |
| `OnboardingTask` | + statuses *Accepted*, *Returned*, + `ReturnReason`, `AcceptedAt` |
| `InductionTaskTemplate` | + optional `JobGrade` / `Role` filter if Q-N2 = yes |
| `ProbationObjective` | + `Status`, `StatusUpdatedAt`, `Comment` |
| `ProbationDevelopmentPlan` | + `Status`, `CompletedOn` |
| `ProbationExtension` | new: months, reason, plan, approved by/at, old/new end date |

### Services
* `InterviewService`: + RSVP, replace panel member, attendance, quorum evaluation, alternate suggestion.
* `PanelSuggestionService` (new): ranks alternates by department, previous panels, calendar clash.
* `HiringService`: + draft offer on final approval, clarification, extend deadline, structured decline.
* `OfferAcceptedHandler` (new): onboarding, provisional file, letter freeze, notifications.
* `OnboardingProgressService` (new): completion %, missing items.
* `ActionLinkService` (new), `OutboxSender` (new), `CalendarInviteBuilder` (new), `IPdfRenderer` (new, if chosen).
* `ProbationService`: + objective/training status, extension rows.
* `ReminderService`: split into focused reminder jobs.

### Background jobs
| Job | Interval | Does |
|---|---|---|
| OutboxSender | 1 min | sends queued email/SMS, retries |
| InterviewReminders | 15 min | T-7d, T-3d, T-1d, T-2h; RSVP chasers; quorum check at T-2d |
| ScorecardChaser | hourly | scorecards overdue after N days |
| OfferJobs | hourly | expiry (built), reminder (built), clarification overdue |
| OnboardingJobs | daily 07:00 | missing documents before the start date, task overdue + escalation |
| ProbationJobs | daily 07:00 | initial meeting / 3-month / final due, overdue, escalation |
| ApprovalWaiting | hourly | built; + escalation if chosen |
| ActionTokenCleanup | daily | removes expired tokens |
| Existing: vacancy close, adverts, surveys, notification cleanup | hourly | unchanged |

---

## 10. Missing business rules: questions

Each question has a **recommended default** that will be used if you answer "use the defaults".

### Interviews
1. **Q-I1 Quorum.** What is the minimum panel for an interview to go ahead? *Default: at least 3 present, and the HOD (or a nominated delegate) must be one of them.*
2. **Q-I2 HOD delegate.** Can a HOD nominate an alternate interviewer in their place? Who approves it? *Default: yes, a staff member of the same department; RO approves.*
3. **Q-I3 Decline handling.** If one member declines and quorum still holds, does the interview go ahead without a replacement? *Default: yes, the RO is told and may add a replacement.*
4. **Q-I4 RSVP deadline.** By when must the panel answer, and what happens to non-responders? *Default: chased at T-3d, RO alerted at T-2d; no answer = treated as "not confirmed" for quorum.*
5. **Q-I5 Who sits on panels.** Is the HR Manager, the RO or the MD ever **required** (like the HOD)? For which grades is the MD on the panel? *Default: only the HOD is required; the MD is optional.*
6. **Q-I6 Teams.** Should GRSMS create Teams meetings itself (needs an Azure app registration with admin consent from GREL IT), or does the RO paste the link? *Default: paste the link + .ics file.*
7. **Q-I7 Candidate confirmation.** Should the candidate also confirm or ask for another date? Is one reschedule request allowed? *Default: confirm or request one new date.*
8. **Q-I8 Scorecard deadline.** How long after the session do panellists have to submit? *Default: 2 working days.*
9. **Q-I9 Attendance.** Who marks attendance: the chair or the RO? Does an "excused" panellist still have to score? *Default: either; excused = no scorecard.*

### Offers
10. **Q-O1 Auto-send.** After MD approval, should the offer **send itself**, or be drafted for the RO to check and send with one click? *Default: draft + one click (salary, terms and start date can change after approval).*
11. **Q-O2 Reply period and extensions.** Keep 7 days default / 30 max? Can the deadline be extended, how many times, by whom? *Default: yes, once, by RO, within the 30-day maximum.*
12. **Q-O3 PDF.** On 6 Nov you chose HTML letters printed to PDF from the browser. The brief now asks for generated PDFs. Options: (a) keep HTML; (b) **QuestPDF**: the free licence only covers companies under USD 1M annual revenue, so GREL would need a paid licence; (c) headless Chromium (PuppeteerSharp/Playwright): free, but adds about 150 MB to the Docker image. *Default: (c), so the PDF looks exactly like today's HTML letters.*
13. **Q-O4 Personnel file timing.** The brief says the offer letter goes into the personnel file, but the file is opened at activation today. Open a **provisional** file at offer acceptance? *Default: yes.*
14. **Q-O5 Clarification.** Does a clarification request pause the reply deadline? Can terms (salary) be changed in reply, and if so does that need HR Manager/MD approval again? *Default: pauses the deadline; any salary change above the approved amount goes back through approval.*
15. **Q-O6 Decline reason.** Mandatory? Fixed list (salary, counter-offer, location, role, personal, other)? *Default: mandatory, fixed list + optional text.*
16. **Q-O7 Next candidate.** After a decline or expiry, should the system only *suggest* the next-ranked candidate, or start the hiring recommendation for them? *Default: suggest only.*

### Onboarding and induction
17. **Q-N1 Auto-start.** Start onboarding automatically on acceptance, with the first-day details defaulted (start date from the offer, report to the HOD, 08:00 at the vacancy's site)? *Default: yes; HR can edit them before the letter.*
18. **Q-N2 Role-specific tasks.** Do tasks differ by role or grade (e.g. laptop only for office roles, uniform/PPE only for factory roles)? If yes, by job grade, by department, or by site? *Default: by department + site, set in Induction areas.*
19. **Q-N3 Every department for every employee?** Today the HR Manager ticks the areas at approval (4 ticked by default). Keep that, or make some areas compulsory? *Default: keep, with HR, IT, Security and H&S compulsory.*
20. **Q-N4 Task "Return".** When a task is returned, does it go to HR to reassign, or straight back to the previous owner? *Default: HR.*
21. **Q-N5 Escalation.** Overdue induction tasks escalate to whom, after how long? *Default: the owner's HOD after 3 days overdue, the HR Manager after 7.*
22. **Q-N6 No-show.** If the new employee hasn't submitted documents by the start date, or doesn't report, what happens? *Default: HR alerted at start-3d; cancel stays manual.*
23. **Q-N7 Probation record timing.** The brief says to create the probation record at acceptance; today it's created at activation (dates depend on the real start). Keep activation? *Default: keep.*

### Probation and confirmation
24. **Q-P1 Extensions.** Today: up to 3 months in total, spread over one or more extensions, HR Manager approves. Correct? Should the MD approve extensions beyond 3 months, if allowed at all? *Default: keep as is.*
25. **Q-P2 Escalation.** An overdue assessment escalates to the HR Manager after how many days? Does a long-overdue HR decision go to the MD? *Default: 7 days; no MD escalation.*
26. **Q-P3 HOD confirming alone.** Can a HOD ever confirm without the HR Manager? *Default: no.*
27. **Q-P4 Not confirmed.** Is the MD's approval also needed for non-confirmation? Is there an appeal step? *Default: HR Manager only; no appeal in GRSMS.*
28. **Q-P5 Objectives.** Who updates objective status: the HOD only, or the employee as well (HOD agrees)? *Default: HOD only, at each assessment.*
29. **Q-P6 Day-1 meeting.** Should GRSMS send a calendar invite for the initial meeting, or just remind the HOD? *Default: remind; HOD can download an .ics file.*
30. **Q-P7 Pre-Phase-21 employees.** Create probation records for employees activated before Phase 21? *Default: no.*

### Cross-cutting
31. **Q-X1 Working days.** Should deadlines count working days (skipping weekends and Ghana public holidays)? If yes, who maintains the holiday list? *Default: working days; HR Admin maintains holidays in settings.*
32. **Q-X2 Approval escalation.** Should waiting approvals (request, hiring) escalate beyond the weekly reminder, for example to the approver's superior or the HR Manager? *Default: no change.*
33. **Q-X3 Email links for staff.** Are one-click token links (no sign-in) acceptable to GREL IT for panel RSVP and induction tasks? *Default: yes, 14-day expiry, single use.*
34. **Q-X4 Delivery order.** Proposed: Phase 22 email outbox + scheduler + action links; 23 interview automation; 24 offer automation + auto-onboarding; 25 induction task actions + onboarding %; 26 probation automation + PDF. OK?

---

## 11. Assumptions

* Earlier decisions stand unless a question above reopens them: 3-stage request approval, ATS auto-reject held for HR, HTML letters, 8-stage onboarding, 12 induction areas, staff account activated only at the end of onboarding.
* "Employee record" in the brief = the GRSMS staff account + personnel file (not a payroll/HRIS record, which remains out of scope).
* All times are GMT (Ghana). Reminders are not sent between 20:00 and 07:00, except the T-2h interview reminder.
* Nothing that legally binds GREL or the candidate (offer acceptance, letters, outcomes) runs without a signed-in, named person.

---

## 12. Decisions recorded (9 November 2026)

Where these differ from §10's defaults, the decision wins.

| Question | Decision |
|---|---|
| Q-I1 Quorum | **3 present, including the HOD** (or the HOD's approved delegate) |
| Q-I2 HOD delegate | **Yes**, staff of the same department, **RO approves** |
| Q-I3 Decline, quorum still met | **Proceed**; RO and HR alerted with suggested alternates; RO may replace |
| Q-I4 RSVP deadline | **Chase at T-3d, RO alerted at T-2d**; no answer = not confirmed for quorum |
| Q-I5 Required panel roles | **HOD and an HR Manager** required on every panel (confirmed); others optional |
| Q-I6 Teams | **RO pastes the link, + .ics** calendar file on every invitation; no Graph integration |
| Q-I7 Candidate | **Confirm, or request one new date** with a reason; RO decides |
| Q-I8 Scorecards | **2 working days**; reminder after day 1, chair and RO alerted after day 2 |
| Q-I9 Attendance | **Chair or RO** marks it; excused = no scorecard |
| Q-O1 Offer | **Draft + one-click send** by the RO after MD approval |
| Q-O2 Deadline extension | **Once, by the RO**, within the 30-day maximum |
| Q-O3 PDF | **Headless Chromium** renders the existing HTML letters to PDF (replaces the 6 Nov "HTML only" decision; the HTML stays the frozen original) |
| Q-O4 / on acceptance | **Auto-start onboarding + provisional personnel file** (offer letter filed in it), appointment-letter draft, HR Manager task to issue it |
| Q-O5 Clarification | **Pauses the deadline**; a salary above the approved amount goes back through HR → MD approval |
| Q-O6 Decline reason | **Mandatory fixed list** (salary, counter-offer, location, role, personal, other) + optional text |
| Q-O7 Next candidate | **Suggest only** |
| Q-N1 Auto-start | **Yes** (as Q-O4); first-day details defaulted, editable by HR |
| Q-N2 Role-specific tasks | **By department + site** filters on task templates |
| Q-N3 Compulsory areas | **HR, Corporate IT, Security, Health & Safety** can't be unticked |
| Q-N4 Returned task | **To HR** (the vacancy's recruiter) to reassign |
| Q-N5 Overdue tasks | **Owner's HOD after 3 days overdue, HR Manager after 7** |
| Q-N6 Missing documents | Employee reminded at **start-7d and -3d**; HR alerted at -3d; cancelling stays manual |
| Q-N7 Probation record | **At activation** (unchanged) |
| Q-P1 Extensions | **Unchanged**: up to 3 months in total, one or more extensions, HR Manager approves |
| Q-P2 Overdue assessments | HOD reminded at -7d and on the due date; **HR Manager alerted 7 days after**; no MD escalation |
| Q-P3 HOD confirming alone | **No**, the HR Manager approves every outcome |
| Q-P4 Not confirmed | **HR Manager only**, no appeal step |
| Q-P5 Objectives | **HOD only**, at each assessment |
| Q-P6 Initial meeting | **Reminders** (day 1, day 5, overdue) **+ downloadable .ics** |
| Q-P7 Pre-Phase-21 employees | **No backfill** |
| Q-X1 Days | **Working days**, skipping weekends and Ghana public holidays (holiday list maintained by the HR Admin in settings) |
| Q-X2 Approval escalation | **No change** (3-day reminder, then weekly) |
| Q-X3 Email links | **One-click, single use, 14-day expiry** for panel RSVP and induction tasks; offer actions still need sign-in |
| Q-X4 Delivery | **22** outbox + scheduler + action links + working-day calendar → **23** interview automation → **24** offer automation + auto-onboarding + PDF renderer → **25** induction actions + onboarding % → **26** probation automation; commit after each phase |
