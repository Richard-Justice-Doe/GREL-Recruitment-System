# 34. GRSMS effectiveness audit (HR, recruitment, onboarding, employee management)

**Date:** 12 November 2026 · **Code reviewed:** `feature/ats-and-super-admin` at `60bf479` · **No code was changed for this audit.**

**How to read the scores.** Each module is scored 0-100 against what a modern mid-market ATS / HRMS (BambooHR, Greenhouse, Lever, Zoho People; the recruiting and core-HR parts of Workday, SuccessFactors, Oracle HCM) typically offers for the same job. They are a reviewer's judgement from the code and documentation, not a measurement, and are meant to show *where to invest*, not to rank GRSMS against a product. The benchmark column is what a mature product in that class usually reaches (no product is 100).

---

## 1. Executive summary

* **What GRSMS is today:** a complete, audited **recruitment-to-confirmation** system built for GREL's own process: the digital QF-81-11 request and its approval chain, vacancies and adverts with an IT approval step, a candidate portal, weighted ATS screening, interviews with weighted scorecards and score validation, hiring and offers, a document-verified onboarding with departmental induction, a digital personnel file, probation with confirmation letters, and (since Phase 28) the employee master record.
* **Where it is strong:** the process from *request* to *confirmed employee* runs in one system, on one person record, with every step in an audit trail and a personnel file filled automatically. Few mid-market tools do the onboarding-to-confirmation half this thoroughly.
* **Where it is behind:** interview logistics (no panel accept/decline, quorum or availability), document output (letters are print-ready HTML, not PDF), analytics (tables and figures, no trends or cost per hire), talent re-use (no talent pool), and everything after confirmation (leave, movements, performance, payroll: planned in docs/30).
* **Overall maturity:** **72 / 100** for its intended scope (recruitment to confirmation); **45 / 100** if judged as a full HRMS.
* **Presentation readiness:** **80 / 100**. The software is ready; the open risks are operational (section 9): the server deployment, an IT Manager account, and demo data.

## 2. The three strengths to lead with

| Strength | Why it matters | Where in GRSMS |
|---|---|---|
| **One workflow from request to confirmation** | Nothing re-keyed between HR, recruitment and the department; each step hands over to the next and emails the next person. | Requests → vacancies → adverts → applications → screening → interviews → hiring → offer → onboarding → probation → confirmation |
| **Candidate becomes employee without re-entry** | The applicant's account becomes the staff account at activation; the personal record they filled in is the employee's; the employee record is created automatically. | `OnboardingService.ActivateAsync`, `EmployeeLifecycle` (Phase 28) |
| **A complete digital personnel file** | Audits and disputes answered from one folder: CV and application, offer, interview results, verified documents, signed declaration, appointment letter, induction record, probation assessments, confirmation letter; all filed automatically and frozen as signed. | Personnel files (Phase 20, 28) |

## 3. Scores by module

| Module | Score | Benchmark | Strengths | Main gaps | Recommendation |
|---|:-:|:-:|---|---|---|
| Recruitment requests | **85** | 85 | Digital QF-81-11 in 5 steps, staff raise, HOD → HR → MD chain, history, reminders when an approval waits, average time to approve | No link to an approved headcount / position budget | Position control with the employee record (after Phase 31) |
| Vacancies & adverts | **75** | 85 | HR approval, internal / external, scheduling, auto-close, HR → IT advert approval, copy-ready text per channel, public careers page | Job boards are posted by hand (no LinkedIn / Jobberman integration); no job alerts for candidates | Job alerts by email from the careers page (quick); board integrations only if volume justifies |
| Candidate portal & applications | **75** | 85 | Profile and CV once, apply in a few clicks, "your next step", phone-friendly uploads, candidate survey | **No talent pool**; candidates cannot be invited to a new vacancy | Talent pool (§5, quick win) |
| ATS screening & shortlisting | **80** | 80 | Weighted score, knockout questions, optional AI CV reader, automatic rejections held for HR, HOD view on the shortlist | No skills taxonomy / ranking across vacancies | Re-use scores in the talent pool |
| Interviews | **65** | 85 | Panels with HOD, weighted criteria, scorecards, attendance, RO validation, reminders | **No panel accept / decline, quorum, availability check or calendar invite** | Build the approved docs/26 Phase 23, plus a clash check (§5) |
| Hiring & offers | **70** | 85 | Recommendation, background checks, offer with expiry and reminders, acceptance in the portal | **No PDF**; offer not drafted automatically; no e-signature beyond a typed name | docs/26 Phase 24 (offer drafts, PDF via Chromium) |
| Onboarding | **85** | 80 | Document portal with verification and rejection reasons, personal record in 6 steps, letter signed online, **12 induction areas with owners and automatic tasks**, progress and documents at a glance | No asset handover record | Asset register (with separation, §5) |
| Probation & confirmation | **80** | 75 | Initial meeting, objectives, development plan, 3-month and final assessments, extension, HR decision, confirmation letter filed | No objective status tracking between assessments | docs/26 Phase 26 |
| Employee records | **50** | 85 | Master record, job history never overwritten, grades, CSV import of existing staff, HOD visibility | Just started: no self-service, movements, leave, performance, documents for registered staff | docs/30 Phases 29-34 |
| Reporting & analytics | **60** | 80 | Overview (time to hire, offer acceptance, open positions), requests (time to approve), vacancies funnel, sources of hire, offers, onboarding and probation reports, CSV export, action cards on Home | **No trends / charts, no cost per hire, no per-step SLA targets**, no scheduled reports | Analytics page with trends and SLA (§6) |
| Automation | **60** | 80 | Reminders (offer expiring, interview / first day tomorrow, approvals waiting), auto-close, scheduled adverts, induction tasks, background email | Approved docs/26 automations not built (RSVP, quorum, offer drafts, PDF, one-click action links) | Deliver docs/26 Phases 22-26 |
| Notifications | **75** | 80 | Bell with unread count, "your to-dos" on Home, email per step, SMS to applicants (when switched on) | No contract-expiry or document-expiry alerts (no module yet) | With Phase 31 |
| User experience | **75** | 85 | Menu by stage, breadcrumbs, place kept after actions, required-field stars, one button colour per decision, next-step banners, phone layouts for candidates | Some pages still have several primary buttons; long record pages not yet in topics | docs/32 priority 3 |
| Security & compliance | **75** | 85 | Granular permissions, Super Admin-only settings, segregation of duties in code, full audit trail, lockout, CSP, anti-forgery, encrypted secrets, backups | **No two-factor sign-in** (unless Microsoft 365 sign-in is used); **no retention rule for candidate data** (Ghana Data Protection Act, 2012, Act 843); virus scanning is a placeholder | Turn on Microsoft 365 sign-in (MFA comes with it); a retention rule (§6) |
| Performance | **75** | 85 | No query count grows with data (measured); menu numbers cached; lists paged; emails in the background; slow-query log | Not yet measured on GREL's server | Read the slow-query log after go-live |

## 4. The consultant's 15 features, against what exists

| # | Feature | In GRSMS | Notes / plan |
|---|---|---|---|
| 1 | Talent pool / candidate database | ❌ | Data is all there (profiles, CVs, scores, interview results); needs a search page and "invite to apply". **Quick win.** |
| 2 | Panel availability calendar | ❌ | docs/26 chose RSVP + calendar file (.ics) without Outlook integration. A **clash check** against interviews already in GRSMS is a quick win. |
| 3 | Interview quorum | ❌ approved | docs/26 §12: 3 present including the HOD and an HR Manager. Phase 23, not built. |
| 4 | Departmental task automation | ✅ | 12 induction areas (IT, security, HR, HSE, ...), each with an owner and tasks, created automatically at activation; due dates, reminders, completion and verification. |
| 5 | Employee asset management | ❌ | Recommend an asset register (issue / return) used at separation (docs/30 Phase 33). |
| 6 | Contract expiry engine | ◐ | Contract end date on the employee and a warning on the page (Phase 28). Alerts planned in Phase 31 at 60 / 30 days; **extend to 90 / 60 / 30 / 7** as suggested. |
| 7 | Organisational chart | ❌ | Reporting lines exist since Phase 28 (`ReportsToUserId`); a chart can be drawn from them. **Quick win** once lines are entered. |
| 8 | Movement history | ◐ | Job history lines, never overwritten (Phase 28). Promotions, transfers, acting appointments and renewals with approvals: Phase 31. Add **demotion** as a movement type. |
| 9 | Workflow SLA monitoring | ◐ | Average time to approve and time to hire in reports; reminders when approvals wait. Missing: a target per step and an on-time / late view. |
| 10 | HR analytics dashboard | ◐ | Reports and Home cards. Missing: trends over months, cost per hire (no cost data captured), onboarding completion time, confirmation / extension / failure rates on one page. |
| 11 | Employee timeline | ◐ | Every step is recorded (application, interview, offer, onboarding, probation, job history), but on separate pages. One combined timeline on the employee page is a **quick win** from existing data. |
| 12 | Notification centre | ✅ | Bell, to-dos, reminders. Contract expiries join with Phase 31. |
| 13 | Letter generator | ◐ | Offer, appointment and confirmation letters generated from data and frozen in the personnel file; **print-ready HTML, not PDF**. Transfer / promotion with Phase 31; warning letters need a discipline module (not planned). |
| 14 | Employee directory | ◐ | The Employees list (search by name, staff number, position; department, grade, status) is for HR and HODs. A staff-wide directory is a privacy decision. |
| 15 | Succession planning | ❌ | Future, after performance management (Phase 32). |

## 5. Quick wins (days each, no workflow change)

| Quick win | Effort | Value |
|---|---|---|
| **Talent pool**: search past applicants by skills, education, score, last stage reached; "invite to apply" to a new vacancy (email with the link) | 2-3 days | Faster hiring, fewer adverts |
| **Interview clash check**: warn when a panel member or the candidate already has a GRSMS interview at that time | 1 day | Fewer rescheduling emails |
| **Employee timeline**: one list on the employee page from the existing histories (applied → interviewed → offer → onboarded → confirmed → job changes) | 1-2 days | Audits, management questions |
| **Organisation chart** from reporting lines (department view and MD view) | 2 days | Executive visibility |
| **Contract expiry alerts** at 90 / 60 / 30 / 7 days to HR, HOD and employee (daily job) | 1 day | No contract lapses unnoticed |
| **Job alerts** for registered candidates when a vacancy in their field is published | 1-2 days | Better candidate pool |

## 6. Strategic recommendations

1. **Deliver the approved automation plan (docs/26, Phases 22-26)**: panel accept / decline and quorum, calendar invites, offer drafts, PDF letters, one-click action links, objective status. This closes the largest functional gaps in interviews and offers.
2. **Continue the HR platform (docs/30)** in the agreed order: self-service, leave, movements (with contract alerts and transfer / promotion letters), performance, separation (with the asset register), payroll last.
3. **Analytics page**: monthly trends, time to hire by stage with targets (SLA: e.g. request approval ≤ 3 days, vacancy approval ≤ 2, offer answer ≤ 5), onboarding completion time, confirmation / extension / failure rates; capture recruitment costs per vacancy to report cost per hire.
4. **Compliance**: a data-retention rule for unsuccessful candidates (e.g. keep 12 months, then anonymise unless they consent to the talent pool), consistent with Act 843; Microsoft 365 sign-in for staff (two-factor); a real virus scanner before uploads go public.
5. **Measure in production**: the slow-query log and request times on GREL's server in the first two weeks, then fix what is actually slow.

## 7. Weaknesses and risks

| Risk | Impact | Mitigation |
|---|---|---|
| Server deployment not finished (Windows Server / Docker issues reported 11 Nov) | Demo or go-live blocked | docs/21 §8b; resolve before the presentation, or demo on the developer PC |
| No IT Manager account | Adverts cannot be published | Give the role (docs/33 §1) |
| Candidate data kept indefinitely | Compliance (Act 843) | Retention rule (§6.4) |
| Email queue in memory | An email can be lost if GRSMS stops at the wrong moment (logged) | Acceptable now; an outbox table if volume grows |
| Letters only as HTML | Some recipients expect PDF attachments | Print to PDF meanwhile; Chromium PDF (Phase 24) |
| Single server, nightly backup | Up to a day of data at risk | Copy backups off the server daily (docs/21 §5) |

## 8. Improvement plan

| When | What |
|---|---|
| **Before the presentation** | docs/33 §1 checklist; server running; IT Manager; demo data |
| **Next 2 weeks** | Quick wins §5 (talent pool, clash check, employee timeline, contract alerts); production measurement |
| **Next 1-2 months** | docs/26 Phases 22-26 (interviews, offers, PDF); docs/30 Phase 29 (self-service) and 30 (leave) |
| **Next quarter** | Movements, performance, separation with assets; analytics page; retention rule; Microsoft 365 sign-in |
| **Later** | Payroll (or export), succession planning, job-board integrations |

## 9. Scores at a glance

| | Score |
|---|:-:|
| **Overall maturity (recruitment to confirmation)** | **72 / 100** |
| Overall maturity as a full HRMS | 45 / 100 |
| **Presentation readiness** | **80 / 100** (software ready; operational items in §7) |
