# 35. Enterprise readiness: consolidated assessment, gaps, roadmap and open questions

**Date:** 12 November 2026 · **Code:** `feature/ats-and-super-admin` at `5c2ab7d` (running on the server) · **No code changed for this report.**

**Why one more report.** The "Enterprise Readiness, Gap Analysis & Completion" brief repeats tasks already answered in earlier reviews. This report does **not** redo them; it brings their conclusions up to date in one place and adds what was missing: the **open business questions**, the **exact files** for each next step, and **one implementation order**.

| Brief task | Answered in | State today |
|---|---|---|
| 1 Full system audit (architecture, dependencies, dead and duplicate code) | [27](27-codebase-audit.md), [32](32-ux-navigation-performance-audit.md) §7 | Re-checked today: **0 build warnings, 0 errors**, database model in sync with the 30 migrations (29 scripts), 232 tests pass; clean layering (no EF or Infrastructure in Application or Web pages) |
| 2-3 HR effectiveness, missing features | [34](34-effectiveness-audit.md) | Six quick wins built since (§10 there); updated below |
| 4-5 UX and navigation | [28](28-usability-review.md), [32](32-ux-navigation-performance-audit.md), [33](33-presentation-readiness.md) | Menu by stage, breadcrumbs everywhere, place kept after actions, link checker for 9 roles |
| 6 Performance | [32](32-ux-navigation-performance-audit.md) §3 | Measured: no page grows queries with data; home 55 → 39 queries; emails in the background; slow-query log on |
| 7 Presentation readiness | [33](33-presentation-readiness.md) | Done; running on the server since 12 November |
| 8 HRMS roadmap | [30](30-hr-platform-impact-analysis.md) | Approved order, Phases 28-34; Phase 28 delivered |

---

## 1. Current system assessment

| Score | Value | Basis |
|---|:-:|---|
| **HR effectiveness** (recruitment to confirmation) | **76 / 100** | docs/34 §3, after the quick wins |
| **ATS maturity** | **80 / 100** | Requests, vacancies, adverts with HR/IT approval, portal, weighted screening, shortlisting, scorecards with validation, offers, talent pool, job alerts. Missing: interview RSVP / quorum, PDF letters, pipeline analytics |
| **HRMS maturity** (whole employee lifecycle) | **50 / 100** | Onboarding, induction, personnel file, probation, employee record, job history, org chart, timeline, contract alerts. Missing: self-service, leave, movements with approvals, performance, separation, payroll |
| **UX** | **78 / 100** | docs/28, 32, 33 delivered; open: one primary button per page, topic tabs on long record pages |
| **Performance** | **78 / 100** | Measured on test data; GREL's server not yet measured (slow-query log in place) |
| **Security** | **75 / 100** | Granular permissions, audit trail, lockout, CSP, encrypted secrets, backups; missing: two-factor sign-in, candidate data retention rule, real virus scanner |

**What the system is:** a mature recruitment-to-confirmation platform with the first layer of employee management. **What it is not yet:** a full HRMS (leave, performance, payroll, self-service, exit).

## 2. Missing features (business value, complexity, priority, phase)

Complexity: S = days, M = 1-2 weeks, L = 3+ weeks.

### Critical (needed for daily HR work or already approved)
| Feature | Business value | Complexity | Priority | Phase |
|---|---|:-:|:-:|---|
| **Onboarding document deadlines** (reminders every 3 working days, overdue flag, one extension, daily digest) | New hires' documents arrive on time without chasing | S | 1 | Phase 27 stage C (approved, open) |
| **Interview RSVP and quorum** (panel accept / decline with reason, quorum 3 incl. HOD and HR Manager, RO alerted) | No interview held without the right panel | M | 1 | docs/26 Phase 23 (approved 9 Nov) |
| **Employee self-service (My HR)**: own profile and change requests, personal record for staff registered directly | Employees update their own details; HR approves | M | 1 | docs/30 Phase 29 |
| **Leave management** | The most-used HR module in any organisation | L | 1 | docs/30 Phase 30 |

### Recommended
| Feature | Business value | Complexity | Priority | Phase |
|---|---|:-:|:-:|---|
| **Workflow SLAs**: a target per step (request approval, vacancy approval, offer answer, document verification) and an on-time / late view | Management sees where recruitment slows | S | 2 | New, with Reports |
| **PDF letters** (offer, appointment, confirmation) via headless Chromium | Letters attached as PDF; signed copies | M | 2 | docs/26 Phase 24 (approved) |
| **Offer drafts** after the hiring approval | One click to send a correct offer | S | 2 | docs/26 Phase 24 |
| **Movements** (promotion, transfer, acting, **demotion**, contract renewal) with approvals and letters | Career changes recorded and approved, never overwritten | M | 2 | docs/30 Phase 31 |
| **HR analytics page**: trends by month, time to hire by stage, onboarding completion time, confirmation / extension / failure rates, **cost per hire** | Management dashboard | M | 2 | New |
| **Manager dashboard**: a HOD's team, leave, probations, contracts ending | HODs manage without asking HR | S | 2 | With Phase 30 |
| **People directory for all staff** (name, department, site, position, phone) | Find colleagues | S | 3 | Phase 29, if approved (question E2) |
| **Site filter** on Employees | Plantation sites | S | 3 | Any time |

### Nice to have
| Feature | Business value | Complexity | Priority | Phase |
|---|---|:-:|:-:|---|
| **Performance management** (goals, mid-year, annual, ratings) | Builds on probation | L | 3 | docs/30 Phase 32 |
| **Separation**: exit interview, clearance workflow, **asset register** | Clean exits, assets returned | M | 3 | docs/30 Phase 33 |
| **Payroll** (or export to GREL's payroll) | Pay from the same data | L | 4 | docs/30 Phase 34 |
| **Candidate pipeline analytics** (conversion per stage, per source, per vacancy) | Recruiting effectiveness | S | 3 | With the analytics page |
| **Succession planning** | Continuity of key roles | M | 4 | After performance |
| **Panel availability with suggested slots** (needs Outlook / Microsoft 365 calendar access) | Faster scheduling | M | 4 | Only if GREL IT allows calendar access (question I3) |

## 3. Navigation (within 3 clicks)

The menu already follows the brief's structure (docs/33 §4), same pages and addresses:

```
Recruitment     Recruitment requests · Vacancies & adverts
Candidates      Candidates (applications | screening | shortlisting | talent pool) · Interviews
Hiring          Hiring & offers
Onboarding      Onboarding (documents, verification, induction) · Probation
Employees       Employees (records, organisation chart, timeline) · Personnel files
Administration  Reports, organisation, people, settings, audit
```

As modules arrive: **Employees** gains *Movements* (Phase 31) and *Directory* (Phase 29); a new group **HR operations** gets *Leave* (Phase 30), *Performance* (Phase 32), *Payroll* (Phase 34); **My HR** appears for every employee (Phase 29). Every page stays within: menu item → list → record.

## 4. Risks and technical debt

| # | Item | Risk | Action |
|---|---|---|---|
| T1 | **Edits made directly on the server** (7 files, set aside with `git stash` on 12 Nov) | The server and GitHub drift apart; updates fail | Review the saved `server-edits-12nov.patch`; from now on change code only on the PC and push |
| T2 | **The server runs `feature/ats-and-super-admin`; `main` is 8 commits behind** | Two "versions of the truth" | Merge to `main` by pull request after each tested batch; point the server at `main` |
| T3 | `OnboardingService` is 1,010 lines | Harder to change safely | Split by topic (docs/27 B6), after Phase 27 stage C |
| T4 | `AdvertService` 799, `HiringService` 756, `InterviewService` 740 lines | Same | Split when next changed (Phase 23 / 24) |
| T5 | Email queue in memory | An email can be lost if GRSMS stops at the wrong moment (logged) | An outbox table if volume grows |
| T6 | Reads are tracked by EF everywhere | Slower list pages at scale | Read-only queries (docs/32 PF3) |
| T7 | No two-factor sign-in; no candidate data retention rule; virus scan placeholder | Security / compliance (Ghana Data Protection Act, 2012, Act 843) | Microsoft 365 sign-in; retention rule (question R3); real scanner |
| T8 | Production performance not yet measured | Unknown slow pages | Read the slow-query log and request times after a week of use |
| T9 | Letters are HTML only | Recipients may expect PDF | Phase 24 |

## 5. Implementation order

Each step: its own commit(s), migration and SQL script when the database changes, tests, guide, changelog; the 232 existing tests unchanged; merged to `main` and deployed with `git pull` + `docker compose ... up -d --build`.

| Wave | Steps | Why this order |
|---|---|---|
| **0. Housekeeping (½ day)** | T1 review the server patch; T2 merge to `main`, server on `main`; site filter on Employees | Clean base, nothing lost |
| **1. Finish what is approved (2-3 weeks)** | Phase 27 stage C (document deadlines) → docs/26 Phase 23 (interview RSVP + quorum) → workflow SLAs in Reports → docs/26 Phase 24 (offer drafts + PDF letters) | Already decided; closes the largest recruitment gaps |
| **2. Employee self-service and leave (4-6 weeks)** | docs/30 Phase 29 (My HR, change requests, directory) → Phase 30 (leave) + manager dashboard | Daily-use modules; leave needs the employee record (done) |
| **3. Employee lifecycle (6-8 weeks)** | Phase 31 movements (+ letters, demotion) → HR analytics page → Phase 33 separation (exit interview, clearance, assets) | Completes the lifecycle |
| **4. Strategic** | Phase 32 performance → Phase 34 payroll (or export) → succession planning | Largest and most policy-dependent |

## 6. Exact files to change (waves 0 and 1)

| Step | Files |
|---|---|
| Site filter | `Application/Employees/EmployeeDtos.cs` (`EmployeeFilter.SiteId`), `Infrastructure/Persistence/EmployeeRepository.cs`, `Web/Controllers/EmployeesController.cs`, `Web/Views/Employees/Index.cshtml` |
| Phase 27 stage C | `Domain/Onboarding/OnboardingWorkflow.cs` (working days), `Application/Notifications/ReminderService.cs` (document reminders, digest), `Application/Onboarding/OnboardingService.cs` (extension), `Web/Views/Onboarding/Details.cshtml`, `Web/Views/MyApplications/Documents.cshtml` |
| Interview RSVP + quorum | `Domain/Interviews/Interview.cs` (`InterviewPanelMember.ResponseStatus`, `DeclineReason`, `RespondedAt`; `Interview.QuorumStatus`), `Domain/Interviews/InterviewWorkflow.cs` (quorum rule), `Application/Interviews/InterviewService.cs` (accept / decline / replace), `Web/Controllers/InterviewsController.cs`, `Web/Views/Interviews/Details.cshtml`, new migration + script 30 |
| Workflow SLAs | `Domain/Settings/SettingCatalogue.cs` (targets), `Application/Reports/ReportService.cs` + `ReportDtos.cs` (new report), `Web/Views/Reports/Index.cshtml` |
| Offer drafts + PDF | `Application/Hiring/HiringService.cs`, new `Application/Common/Interfaces/IPdfRenderer.cs`, new `Infrastructure/Documents/ChromiumPdfRenderer.cs`, `Dockerfile` (Chromium in the image), `Web/Controllers/HiringController.cs`, `PersonnelFilesController.cs` (PDF download) |

## 7. Questions requiring business clarification

Decisions already taken are not asked again (they are recorded in docs/26 §12, docs/29 §7, docs/30 §13, docs/32 §12). These are still open:

**Recruitment**
* **R1.** Workflow SLA targets: how many working days for HOD review, HR approval, MD approval, vacancy approval, advert review, offer answer, document verification?
* **R2.** Cost per hire: which costs should be recorded per vacancy (advertising, agency fees, interview travel, medicals), and by whom?
* **R3.** Talent pool and data retention: how long are unsuccessful candidates kept (e.g. 12 months), and must they consent to the talent pool when applying?

**Interviews**
* **I1.** Quorum (approved 9 Nov: 3 present including the HOD and an HR Manager): since HODs no longer decide hires (9 Nov), is the HOD still required on every panel?
* **I2.** When a panel member declines, may the RO replace them, and does the interview wait or go ahead if quorum is still met?
* **I3.** Will GREL IT allow GRSMS to read Microsoft 365 calendars (needed for "suggested free slots"), or is the clash check enough?

**Offers**
* **O1.** PDF letters: on GREL letterhead (provide the file)? Signed by whom (name, title), with a scanned signature or none?
* **O2.** May an offer exceed the approved salary or the grade's range, and if so who approves?

**Onboarding**
* **N1.** Document deadline reminders: to the new employee only, or also the RO and HOD? After how many overdue days is the onboarding escalated to the HR Manager?
* **N2.** Assets at onboarding: which items are issued (laptop, phone, SIM, PPE, uniform, keys, vehicle), and which department records each?

**Employee records**
* **E1.** GREL's grade list (codes, names, salary ranges).
* **E2.** Directory: may every member of staff see colleagues' name, department, site, position and work phone?
* **E3.** Does a personnel file need opening for staff registered directly (no onboarding), with their documents uploaded by HR?

**Leave**
* **L1.** Leave year: calendar year or another period?
* **L2.** Entitlements per leave type (annual, casual, sick, maternity, paternity, compassionate, study, unpaid), and do they depend on grade or years of service?
* **L3.** Carry-forward: allowed? Maximum days? Expiry date?
* **L4.** Half days? Weekends and public holidays excluded? Who maintains the holiday list?
* **L5.** Documents: after how many days of sick leave is a medical certificate required? Which other types need a document?
* **L6.** Approval: line manager then HR (decided). Does the HOD also approve when they are not the line manager? Who approves the HOD's own leave?
* **L7.** Relief officer: required for some roles? Must they accept?

**Payroll**
* **P1.** Which payroll system does GREL use today, and should GRSMS replace it or export to it (decision deferred on 11 Nov)?
* **P2.** If built: who prepares and who approves payroll; how are payslips delivered (portal only, email)?

**Performance**
* **F1.** Appraisal cycle (calendar year?), rating scale (1-5?), who moderates, and does the rating drive increments or bonuses?

**Separation**
* **S1.** Notice periods by grade or employment type.
* **S2.** Clearance: which departments (IT, Finance, Stores, Security, HR) and items, and who signs off each?
* **S3.** Exit interview: mandatory? Who conducts it; is it confidential from the HOD?
* **S4.** When is the account disabled (last working day, or after clearance), and do former employees keep access to their payslips?

**Process**
* **X1.** Who tests and accepts each phase before it goes to the server (an HR Manager sign-off)?
* **X2.** Should every change go through a pull request into `main`, with the server always on `main`?

## 8. Decisions (12 November 2026)

| # | Decision |
|---|---|
| Plan | **Approved.** Start Wave 0, then Wave 1. |
| I1 | **The HOD (or a same-department delegate) and an HR Manager stay required** on every panel; quorum = at least 3 present including them (as approved on 9 November). |
| I2 | **When a panel member declines, the RO is alerted and adds a replacement; the session waits until quorum is met again.** |
| R1 | **Default workflow targets, in working days, editable in System settings:** HOD review 2, HR approval 2, MD approval 3, vacancy approval 2, advert review 2, offer answer 5, document verification 3. |

## 9. Progress

| Step | Status |
|---|---|
| Wave 0: site filter on Employees | ✅ 12 Nov |
| Wave 0: merge into `main` | Pushed; the pull request is to be merged on GitHub |
| Wave 0: review the server's stashed edits | Waiting for `server-edits-12nov.patch` |
| Wave 1.1: onboarding document deadlines (Phase 27 stage C) | ✅ 12 Nov |
| Wave 1.2: interview panel replies and quorum (I1, I2) | ✅ 12 Nov, script 30 |
| Wave 1.3: workflow targets report (R1) | ✅ 12 Nov: Reports → Workflow targets; targets in System settings |
| Wave 1.4: offer drafts and PDF letters | Next (needs O1: letterhead and signatory) |
