# 33. Presentation readiness (12 November 2026 demonstration)

**Prepared:** 11 November 2026, on `feature/ats-and-super-admin`. **Rules kept:** no workflow, approval chain, permission or data was changed; every fix is navigation, display or speed. 220 automated tests pass.

---

## 1. Must do before the demo (not code)

| # | What | Why it matters |
|---|---|---|
| 1 | **Run `database/26`, `27` and `28` on the demo MySQL database, in that order** (or start with `Database:ApplyMigrationsOnStartup = true`), then restart GRSMS. | GRSMS **refuses to start** while a database update is missing (it names it in the log). |
| 2 | **Give someone the IT Manager role** (Super Admin → Users & roles). | Since 11 November an advert is published only after the IT Manager approves it. Without one, the advert flow stops at *Awaiting IT approval*. |
| 3 | **Prepare one account per role you will show** (Managing Director, HR Manager, Recruitment Officer, Head of Department, IT Manager, a candidate) and use a **separate browser or private window** for each. | Switching roles by signing out and in during a demo is slow and error-prone. |
| 4 | **Have demo data at every stage**: a request awaiting HR, a vacancy pending approval, a published vacancy with an advert, applications, an interview with scorecards, an accepted offer, an onboarding with some documents verified. | Empty lists look unfinished; the home page cards and to-dos only show what exists. |
| 5 | **Rehearse once in a real browser** on the demo machine. | All checks below are automated or headless; nothing was clicked through by a person in this session. |
| 6 | Add GREL's **grades** (Administration → Grades) if you will show Employees. | The grade column is empty until then. |

## 2. What was checked, and fixed

**A link checker** (`tests/Grsms.Tests/LinkCrawlWebTests.cs`) now signs in as each of **nine roles** (Super Admin, HR Manager, Recruitment Officer, MD, HOD, HR Administrator, IT Manager, employee, candidate), starts at the home page and follows every link on every page it reaches (up to 400 pages each, 3,600 in all). It fails on any error page, missing page, or link to a page the person may not open. It runs with the other tests.

| Found | Fixed |
|---|---|
| **No error pages and no missing pages** for any role. | n/a |
| *Full change log (audit trail)* on request, vacancy, application, advert, onboarding and probation pages shown to the **RO, MD and HOD**, who then got *Access denied*. | Shown only to those who can open the audit trail (`_AuditTrailLink`). |
| *Survey results* refused the **Super Admin and the Admin**: the page checked the permission, the service a fixed list of roles. | The service checks the same permission. |
| The **IT Manager's** empty advert list linked to Vacancies, which they cannot open. | Different wording for them. |

## 3. The brief, item by item

| Brief | State |
|---|---|
| 1 Navigation audit | Link checker above; no broken or dead links left. Menu in the brief's flow (§4). |
| 2 Back navigation | Every **administration** screen starts with **← Administration › screen** (Departments, Divisions, Units, Sites, Users & roles, Roles & permissions, System settings, Email templates, Grades, ...); pages inside a screen link back to its list. Every **other staff page** has a breadcrumb (*Recruitment › Vacancies › VAC-2026-0012*) whose middle link returns to the list **as you left it**. |
| 3 Position reset | Done (docs/32 step 1): after Approve, Reject, Save, Verify, Return the page comes back **where you were** with the same sections open; lists keep page, search, filters, tab and sort; DataTables keep page, search and sort. *Example:* applications page 8 → open one → act → *Applications* returns to page 8. |
| 4 Interface | Required fields marked **\***; one button colour per kind of decision (approve green, return amber, reject red); long forms already in steps (request: 5, personal record: 6) and *More options* (vacancy). |
| 5 Speed | Menu numbers kept 30 s per user (~8 fewer queries on **every** page); home page **55 → 39** queries; Employees and Users & roles paged in the database; no list grows queries with data (measured, docs/32 §3); slow queries logged (`Database:SlowQueryMilliseconds`). |
| 6 Administration | One Administration page with sections; breadcrumbs on every screen; search on Users & roles (name, email, staff number) with paging; the small lists (departments, sites, units, divisions) have search and sort and keep them. |
| 7 Interviews | Already clear: the next-step panel says *"2 of 3 panel members still to score: …"*, the panel card shows each member as *Submitted / Waiting / Absent / Excused*, the list shows scorecards in and the average. **There is no accept / decline step for panel invitations** in GRSMS today; adding one is a new workflow rule (after the demo, if wanted). |
| 8 Onboarding | The *In progress* list now has a **Documents** column: *7 / 9 verified*, plus *2 missing*, *1 to verify*, *1 rejected*, as on each onboarding page. The onboarding page itself opens on *Documents* with each document's state. |
| 9 Consistency | One badge set per status (shared partials), one button rule, one breadcrumb, one card style. |
| 10 Presentation review | §5 and §6. |

## 4. The menu (as the brief proposes; same pages and addresses)

```
Recruitment     Recruitment requests · Vacancies & adverts
Candidates      Candidates (applications | screening | shortlisting) · Interviews
Hiring          Hiring & offers (recommendations and offers)
Onboarding      Onboarding (documents, verification, induction) · Probation (and confirmation)
Employees       Employees · Personnel files
Reports
Administration  (one page: departments, sites, units, divisions, users, roles & permissions, settings, ...)
```

*Movements* is not built yet (docs/30, Phase 31), so it is not in the menu; do not promise it as present in the demo.

## 5. Suggested demo route (about 20 minutes)

| Who | Show | Point out |
|---|---|---|
| **Managing Director** (2 min) | Home → *Requests awaiting your approval* card → a request → approve. | One click from home to the decision; the page stays where it was; the history on the right. |
| **HR Manager** (5 min) | Home cards → approve a vacancy → review an advert (*Approve and send to IT*) → Onboarding *In progress* (Documents column) → Probation. | The flow left to right in the menu; the breadcrumb; the document counts. |
| **IT Manager** (2 min) | Home advert cards → approve and publish → the public careers page. | The new approval step (docs/29). |
| **Recruitment Officer** (8 min) | Candidates (screening scores) → shortlist → an interview (scorecards, validate) → Hiring & offers → start onboarding → verify a document. | Scores and the next step on every page; *Back* returns to the same list page. |
| **Candidate** (3 min) | Phone-sized window: home *Your next step* → upload a document → sign the appointment letter. | The candidate portal on a phone. |

## 6. Presentation risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| GRSMS does not start on the demo server (database updates 26-28 missing). | High if not done | §1.1, the day before. |
| Advert stuck at *Awaiting IT approval*. | High without an IT Manager | §1.2. |
| Empty lists and cards. | Medium | §1.4: seed a case at each stage. |
| Emails not sent (mail server not configured on the demo machine). | Medium | Emails are not needed to move a workflow; say they are sent in production, or show the saved .eml files. |
| Slow first page after a restart (application warm-up). | Medium | Open each page once before the audience arrives. |
| Something looks different in a real browser (only headless checks here). | Low-medium | §1.5 rehearsal. |
| Being asked for payroll, leave, movements. | Medium | They are planned (docs/30 Phases 29-34), not built; show the employee record (Phase 28) as the base. |

## 7. After the presentation

| Priority | Item |
|---|---|
| High | Measure on the real server (slow-query log, request times) and act on the slowest pages. |
| Medium | docs/32 priority 3: read-only queries, topic tabs on the long request / vacancy / advert / application pages, one primary button per page, phone layout of Home and Probation details. |
| Medium | Push the branch and open the pull request to `main` (nothing is pushed yet). |
| Low | Panel invitation accept / decline (new rule, needs a decision); "next waiting item" after a decision. |
