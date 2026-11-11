# 32. UX, navigation, performance and code quality audit

**Status:** approved 11 November 2026 (decisions §12). **Priority 1 is being built first**; priorities 2 to 4 wait for a go-ahead.
**Branch:** `feature/ats-and-super-admin` after commit `4abf0a5` (Phase 28). 209 tests pass.

**Builds on** [27 Codebase audit](27-codebase-audit.md) (dead code, duplicates, N+1 queries: done 10 November) and [28 Usability review](28-usability-review.md) (short menus, next-step banners, form steps: done 10 November). Those findings are not repeated; this report covers **what is still open** and what the new brief asks for.

**Rules for every change proposed here:** no business rule, workflow, approval chain, permission, URL or database data changes. Every item lists its risk.

---

## Summary

| Area | State | Biggest finding |
|---|---|---|
| Architecture | **Clean.** Web uses the database only for the health check and start-up; Application and Domain have no outward dependencies; controllers are thin (largest after `AccountController` is 231 lines). | No violations that need fixing; three small smells (§7). |
| Speed | **No page gets slower as data grows** (measured, §3), so no N+1 is left on the main pages. | Every page pays **~11 queries for the menu and top bar**; the home page **55**. On MySQL across a network each query is a round trip, which is most likely what users feel. |
| Page position | **The main gap.** 117 of 120 redirects return the user to the top of the page; **every "Back to list" link drops the page number, search and tab**. | Fix once, in shared code (§5.1), not page by page. |
| Lists | The big lists (requests, vacancies, adverts, applications, interviews) are **already paged and searched on the server**. | **Users** (all accounts, candidates included) and the new **Employees** list load everything; they will grow into thousands. |
| Consistency | Good base (one stylesheet, shared partials). | *Approve* is blue on some pages and green on others; *Return* amber or grey; **no form marks its required fields**; the hiring case has 9 primary buttons. |
| Navigation | Short menu since doc 28. | **No breadcrumbs**; group names (*Hire*, *Onboard*, *People*) mix verbs and nouns. |

---

## 1. UX findings (by screen)

| # | Screen | Problem | Proposal | Risk |
|---|---|---|---|---|
| UX1 | **Every list → record → back** | *Back to list* (`asp-action="Index"` with no values) goes to page 1, no search, default tab. Example: vacancies, page 7, search "Engineer" → open one → approve → *All vacancies* = page 1. | "Back to list" returns to the **exact list URL** you came from (§5.1). | Low |
| UX2 | **Every record page after Save / Approve / Reject / Return** | The page reloads at the **top**; the user scrolls back to the section they were in (only 3 of 120 redirects keep the place: the onboarding documents). | Restore the **scroll position and open section** after a form posts back to the same page (§5.1). | Low |
| UX3 | **After a decision** | You land on the record you just decided; the next one waiting means going back to the list. | Optional "**Next waiting**" link in the success message (doc 28 N7, still open). | Low |
| UX4 | **Hiring case** (9 primary buttons), **Interview** (6) | Several equal-looking main buttons: which one is *the* next step? | One primary button (the next step); others outline (doc 28 X4, still open). | Low (look only) |
| UX5 | **All forms** | No required-field marker anywhere (0 found); users learn what is required only from the error. | A red `*` on every required label, generated from the `[Required]` attribute (one tag helper or CSS on `required` inputs), plus a "* required" note. | Low |
| UX6 | **Approve / Reject / Return colours** | Approve: 4× blue, 3× green; Return: amber or grey; Reject once green-outlined. | One rule (§6.3). | Low (look only) |
| UX7 | **Dashboard** | For HR: 7 blocks (KPI cards, to-dos, adverts, workforce widgets, requests in approval, latest candidates, calendar). Missing from the brief's list: **interviews this week**, **offers awaiting the candidate**, **vacancies awaiting publication** (only as to-dos). | §9. | Low |
| UX8 | **Employees** (Phase 28) | Long list without paging; filters reset after opening a record. | Server paging (§4) + UX1. | Low |
| UX9 | **Large record pages** (Onboarding 498 lines, Hiring case 437, Interview 414, Request 358) | Already split into topics (doc 28 W3/W4) except **Request, Vacancy, Advert and Application details**, which are long single pages. | Same topic tabs (`section-tabs.js`) on those four. | Low |

## 2. Navigation findings

**Today (doc 28):** Home · **Hire** (Recruitment requests, Vacancies & adverts, Candidates [= applications + screening + shortlisting tabs], Interviews, Hiring & offers) · **Onboard** (Onboarding, Probation, Personnel files) · **People** (Employees) · Reports · Administration (one page). Seven links or fewer: one flat list.

| # | Finding | Proposal | Risk |
|---|---|---|---|
| N1 | Group names mix verbs (*Hire*, *Onboard*) and nouns (*People*). | Nouns that match the brief: **Recruitment**, **Candidates**, **Hiring**, **Employees** (see the tree below). | Low (labels only; URLs unchanged) |
| N2 | *Onboarding* sits under *Onboard*, apart from *Hiring & offers*, though onboarding starts from the accepted offer. | Put **Onboarding** under **Hiring**; **Probation** and **Personnel files** under **Employees**. | Low |
| N3 | **No breadcrumbs.** On a record page the only hint is the highlighted menu item. | A breadcrumb on every staff page: *Recruitment › Vacancies › VAC-2026-0012*, the middle link returning to the list **as you left it** (UX1). Built from one controller → group map, so pages need no edits. | Low |
| N4 | The brief lists *Applications*, *ATS Screening* and *Shortlists* as separate items. Doc 28 (approved) merged them into **Candidates** with tabs to shorten the menu. | **Keep the tabs** (question 2). | n/a |
| N5 | Administration: one page, five sections (doc 28). The brief's tree (Departments, Sites, Roles, Permissions, Settings) is already what the page lists. | No change. | n/a |

Proposed menu (same pages and URLs, regrouped):

```
Home
Recruitment     Recruitment requests · Vacancies & adverts
Candidates      Candidates (applications | screening | shortlisting) · Interviews
Hiring          Hiring & offers · Onboarding
Employees       Employees · Probation · Personnel files
Reports
Administration
```

The "seven links or fewer → flat list" rule stays, so a Head of Department or an employee keeps a short menu.

## 3. Performance findings (measured)

**How:** a temporary test (not kept) loaded each page as an HR Manager with **5** and then **25** requests and vacancies (3 applications each) and counted every SQL command. In-memory SQLite, so the milliseconds are only relative; **the query counts are exact**.

| Page | Queries (5 each) | Queries (25 each) | Of which menu and top bar |
|---|---:|---:|---:|
| Home | 55 | 55 | ~11 |
| Vacancy details | 26 | 26 | ~11 |
| Hiring | 23 | 23 | ~11 |
| Reports | 22 | 22 | ~11 |
| Vacancies | 21 | 21 | ~11 |
| Employees | 20 | 20 | ~11 |
| Job adverts, Onboarding | 19 | 19 | ~11 |
| Requests, Interviews, Request details | 18 | 18 | ~11 |
| Applications, Probation, Personnel files | 17 | 17 | ~11 |
| Screening | 14 | 14 | ~11 |
| Administration (no data of its own) | 11 | 11 | 11 |

**What it means**
* **No N+1 left** on these pages: the counts do not grow with the data (the doc 27 fixes hold).
* **The fixed cost is the problem.** The sidebar badges (`NavCounts`: unread notifications, requests awaiting me, open vacancies, vacancies to approve, scorecards, validations, onboarding tasks, automatic rejections) and the top bar run **~11 queries on every page**, and nothing is cached. The home page adds ~44 (KPI totals and daily series, to-dos per module, workforce widgets, adverts, calendar, latest candidates).
* With MySQL on another machine, **each query costs a network round trip** (typically 1-5 ms on a LAN, more over a VPN). 55 queries ≈ 0.1-0.3 s of waiting before any work; 11 on every page ≈ 10-50 ms.

| # | Root cause | Proposal | Risk |
|---|---|---|---|
| PF1 | Menu badges recomputed on every page | Cache each user's badge counts for **60 seconds** (memory cache, keyed by user); clear that user's entry after they post a form, so their own action shows at once. Others' changes show within a minute. | Low; a badge can lag ≤ 60 s |
| PF2 | Home: 55 queries | KPI cards: one grouped query per card (total + last 14 days together); cache the organisation-wide parts (KPIs, calendar, latest candidates) for **2 minutes**; to-dos stay live. Target ≤ 25. | Low |
| PF3 | Every read is tracked by EF (`Repository.ListAsync` has no `AsNoTracking`) | A read-only list method used by list pages and the dashboard. | Low-medium (must not be used where the result is then saved) |
| PF4 | Users list loads every account incl. candidates into the browser | Server paging and search (§4). | Low |
| PF5 | Employees list loads every employee and every name (Phase 28) | Filter, sort and page in the database. | Low |
| PF6 | DataTables script and styles load on **every** page; 6 pages use them | Load them only on those pages. | Low |
| PF7 | Real-server timing unknown | Turn on EF's slow-query log (e.g. > 200 ms) and the request duration in the log on the server; check the app-to-MySQL latency. **Must be measured on GREL's server** before deeper changes. | None |

## 4. Database findings

| # | Finding | Proposal | Risk |
|---|---|---|---|
| DB1 | Whole-table loads that grow: `Employees` (list, counts, backfill), `PersonnelFiles` (search), `ConfirmationRecords` (report) | Filter in the query; project to the columns needed. | Low |
| DB2 | Tracked reads everywhere (PF3) | Read-only list method. | Low-medium |
| DB3 | Indexes | The filters in use are covered (doc 27 §10; Phase 28 added `Employees(DepartmentId, Status)`). For the employee search add `Employees(Status, EmployeeNumber)` only if paging shows a need. | Low |
| DB4 | No data or schema change is needed for anything in this report. | n/a | n/a |

## 5. Frontend findings

### 5.1 Keeping the user's place (brief phase 4): one shared fix

| Part | How | Covers |
|---|---|---|
| **Return to the list as you left it** | `site.js` remembers the full address (page, search, filters, tab, sort) of each list page in the browser session; every "Back to list" / "All …" link and breadcrumb (marked `data-back-to-list`) uses it. Server unchanged. | Page 7 + "Engineer" stays page 7 + "Engineer" after approving and going back. |
| **Stay where you were on the page** | When a form is submitted, `site.js` saves the scroll position, the open topic tab and the open accordions for that page; when the page reloads after the post (same address, within a minute), it restores them. A link to a specific spot (`#doc-12`) still wins. | Save, Approve, Reject, Return, Update, expanding a section. |
| **Tables** | DataTables `stateSave` (session) on the 6 DataTables pages: page, search, sort survive a reload. | Departments, Units, Sites, Users, Screening. |
| **Topic tabs** | Already remembered (`section-tabs.js`). | n/a |

Risk: **low**: script only, no server or rule change; if the browser blocks storage, pages behave as today.

### 5.2 Other frontend items
* **DataTables (brief phase 6):** only 6 pages use them; the other lists are server-rendered with server paging, search and sort already (requests, vacancies, adverts, applications, interviews). Server-side DataTables are needed only for **Users** (PF4); **Employees** gets server paging like the other lists (PF5). Onboarding, probation, hiring and personnel files stay as they are (hundreds of rows; doc 27 P5).
* **67 inline `style="…"`** in views: move the repeated ones (e.g. `white-space: pre-wrap`, progress heights) to classes. Cosmetic.
* **Mobile (brief phase 8):** 31 of 38 pages with tables use a responsive wrapper or stacked cards. The 7 without are letters and records (`_AppointmentLetter`, `_PersonalRecordView`, `_ProbationRecord`, Probation details, Offer, Home, Employee import) where tables are short. Add the wrapper to Home and Probation details; leave letters (they print). **Not yet checked on a real phone** (as in doc 28).
* **Forms (brief phase 7):** steps already on the recruitment request (5 steps) and personal record (6 steps); *More options* on the vacancy form. Add required markers (UX5) and group the **Register employee** and **Hiring recommendation** forms into titled sections (both already are cards; check labels). No wizard needed elsewhere.

## 6. Consistent UI design

### 6.1 One button rule (proposal)
| Meaning | Style | Examples |
|---|---|---|
| The page's next step (one per page) | `btn-primary` | Submit, Save, Send for review |
| Positive decision | `btn-success` | **Approve**, Verify, Confirm, Accept, Publish |
| Send back | `btn-outline-warning` | **Return**, Request correction |
| Negative / final | `btn-outline-danger` (filled `btn-danger` only in the confirm dialog) | **Reject**, Cancel request, Withdraw, Deactivate |
| Leave without doing anything | `btn-outline-secondary` | Cancel, Back, Close |
| Secondary actions | `btn-outline-primary` | Print, Download, Clone |

### 6.2 Where it differs today
Approve (4 blue, 3 green), Return (3 amber, 1 blue, 1 grey), Reject (3 red outline, 1 green, 1 amber, 1 green outline), the hiring case (9 primary) and interview page (6 primary). The fix is a sweep of the views; **look only**.

### 6.3 Cards, tables, icons, type
Already shared (`glass` cards, `page-header`, one icon set, one font, colour tokens in `site.css` with dark mode). No change beyond 6.1.

## 7. Refactoring recommendations and architecture

| # | Item | Type | Proposal | Risk |
|---|---|---|---|---|
| R1 | `OnboardingService` 995 lines | Large file | Split by topic (letters, activation, tasks, queries): doc 27 **B6**, waiting for Phase 27 stages B and C. | Medium |
| R2 | `AdvertService` 784, `HiringService` 756, `InterviewService` 701 | Large files | Split queries (lists, dashboard) from commands, as for onboarding. Only when next changed. | Medium |
| R3 | `AccountController` 464 lines | Large controller | Uses ASP.NET Identity directly by design (doc 27 §13 D); could move external sign-in to a helper. | Low |
| R4 | Views with logic (e.g. contract-day counting on the employee page, colour choices) | Smell | Move to DTO properties as pages are touched. | Low |
| R5 | `_Sidebar.cshtml` 250 lines with permission logic | Smell | Build the menu in a small class (`MenuBuilder`) that returns groups and links; the view only draws it. Needed for N1-N3 anyway. | Low |
| R6 | `EmployeeService` (Phase 28) loads all employees | Performance | PF5. | Low |
| A1 | Architecture | n/a | **No violations found:** no EF or Infrastructure in Application/Web (apart from the health check and `Program.cs`), workflow rules in Domain (`*Workflow` classes), persistence in Infrastructure, thin controllers. | n/a |

Dead code, unused files and duplicated helpers: re-checked by the build (0 warnings); doc 27's removals hold. Nothing new to delete.

## 8. Dashboard improvements (brief phase 10)

Keep the to-dos first (they are the action items). Replace the generic KPI row with **action cards** that each open the filtered list:

| Card | Who | Opens |
|---|---|---|
| Requests awaiting approval | approvers | Requests, *Awaiting my action* |
| Vacancies awaiting publication | HR Manager | Vacancies, *Pending* |
| Applications received (last 7 days) | recruiters | Candidates |
| Interviews this week | recruiters, panel | Interviews, *Upcoming* |
| Offers awaiting the candidate | recruiters, HR | Hiring, *Offers sent* |
| Employees in onboarding | HR | Onboarding, *In progress* |
| Probation reviews due | HR, HOD | Probation, *Due* |

*Latest candidates* and *Requests in approval* move below the fold (or into Reports); the calendar stays. Each card is one cached count (PF2). **Look and order only**; the numbers come from the existing services.

## 9. Risks and what could break

| Change | What could go wrong | Guard |
|---|---|---|
| Keeping the place (5.1) | Restoring scroll on a page whose content changed (e.g. the approve panel disappeared) lands slightly off. | Restore only on the same address within a minute; clamp to the page height; anchors win. |
| Badge cache (PF1) | Someone else's action shows in your badge up to 60 s late. | Your own posts clear your cache. |
| Read-only lists (PF3) | A service that loads with the read-only method and then saves would silently save nothing. | Use it only in query methods; a test per service. |
| Menu regroup (N1-N3) | Users used to *Hire / Onboard*. | Same pages and URLs; one release note. |
| Button colours (6.1) | Tests that look for a class. | The web tests look for text, not classes (checked). |
| Users / Employees paging | The *Users* page search today searches in the browser across all columns. | The server search covers name, email, staff number and role. |

## 10. Prioritised plan

Each step a separate commit; the full suite runs after each; no rule, permission, URL or data change.

| Priority | Steps |
|---|---|
| **1: Critical** (what users feel every day) | **5.1** keep the place (list return, scroll, open sections, DataTables state) · **PF1** cache the menu badges · **PF5/PF4** page the Employees and Users lists · **PF7** measure on GREL's server |
| **2: High** | **PF2** lighter dashboard + **§8** action cards · **N1-N3** menu regroup and breadcrumbs (with **R5** `MenuBuilder`) · **UX5** required markers · **6.1** button rule |
| **3: Medium** | **PF3** read-only lists · **DB1** remaining whole-table loads · **UX9** topic tabs on request, vacancy, advert and application pages · **UX4** one primary button per page · mobile wrappers on Home and Probation details · **PF6** DataTables only where used |
| **4: Nice to have** | **UX3** "next waiting" after a decision · inline styles to classes · **R1/R2** service splits (with the next feature work) · **R3/R4** |

## 11. Questions before implementing

1. **Approve the plan** as prioritised, or only priority 1 first?
2. **Navigation:** keep *Candidates* as one item with tabs (doc 28, approved), or split into *Applications / Screening / Shortlists* as the brief shows?
3. **Badge freshness:** is up to **60 seconds** acceptable for other people's actions to show in the menu numbers?
4. **Dashboard:** replace the KPI row with the action cards (§8), or add the cards and keep the KPIs?

## 12. Decisions (11 November 2026)

| # | Decision |
|---|---|
| 1 | **Priority 1 first**, then come back for priority 2. |
| 2 | **Keep *Candidates* as one menu item with tabs** (applications, screening, shortlisting). |
| 3 | Menu numbers may be up to **30 seconds** old for other people's actions (PF1 uses 30 s, not 60 s). |
| 4 | When the dashboard is done (priority 2), the **action cards replace the KPI row**. |

## 13. Progress

### Priority 1 (done, 11 November 2026)

| Commit | Step | What changed |
|---|---|---|
| `92ec44a` | **5.1** keep the place | `site.js`: list pages remember their address (page, search, filters, tab) for the session and record pages' plain links back to them use it; after a form posts back to the same page the scroll position, open accordions and `<details>` are restored (first validation error wins; anchors win); DataTables `stateSave`. Checked in headless Chrome (no script errors; back link, scroll, sections; a position older than a minute is ignored). |
| `a40dc50` | **PF1** menu numbers | Kept per user for 30 s (`Navigation:CountsCacheSeconds`); any form the user posts clears theirs; **the bell stays live** (opening a notification is a link, so caching it would show read notifications as unread). Saves ~8 queries per page. Test `NavCountsWebTests`. |
| `cbd979b` | **PF4/PF5** lists | Employees: new `EmployeeRepository` filters, searches, counts and pages in the database (50 per page); grade counts and the backfill no longer read the whole table. Users & roles: server search (name, email, staff no.), sort (name / latest sign-in), paging (50); only the page's roles are read; deactivate and unlock return to the same page and search (local addresses only). Tests in `EmployeeServiceTests` and `UsersListWebTests`. |
| (this commit) | **PF7** measure | `SlowQueryInterceptor` logs commands slower than `Database:SlowQueryMilliseconds` (500 ms) with the SQL, never the values; each request's duration was already logged (Serilog). Tests `SlowQueryInterceptorTests`. |

**Tests:** 214 (the 209 before, plus 5; the two Phase 28 list assertions adapted to the paged result). **Not checked:** the real pages in a browser against MySQL, and timings on GREL's server; after deploying, look in the log for "Slow database command" and the request durations.

**Next:** priority 2 (dashboard action cards replacing the KPI row, menu regroup with breadcrumbs, required-field stars, button colours) waits for a go-ahead.
