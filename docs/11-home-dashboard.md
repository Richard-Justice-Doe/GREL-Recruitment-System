# 11. Home dashboard

The **Home** page is the first thing staff see after signing in. It shows what is happening in recruitment and, above all, **what is waiting for you**. Candidates have their own, simpler home page.

Everything on the page follows the same visibility rules as the lists: a Head of Department only ever sees and counts their own departments' records, while Recruitment Officers, the HR Manager, the Managing Director and the HR Administrator see every department. Panels you have no access to are simply left out.

## What is on the page

### Division / Department filter (top right)

Two drop-downs next to the greeting narrow the page to one **division** (Corporate or Operations) and/or one **department**. They apply to the KPI cards (except *Registered candidates*), *Requests in approval* and the closing calendar. *My tasks* is never filtered, so nothing waiting for you is hidden. The filter is kept when you change month, and clicking a KPI card opens its list with the same filter. **Clear** removes it. See [12 Divisions, departments and sites](12-divisions-and-departments.md#filtering).

### The KPI cards (top row)

| Card | The big number | The 7 bars | Who sees it |
|---|---|---|---|
| **Recruitment requests** | All submitted requests (drafts not counted) | Requests submitted on each of the last 7 days | Anyone who can open Recruitment requests |
| **Approved requests** | Requests that are fully approved | Requests given final approval on each day | Anyone who can open Recruitment requests |
| **Open vacancies** | Vacancies currently published | Vacancies published on each day | Anyone who can open Vacancies |
| **Applications** | All applications received | Applications submitted on each day | Anyone who can open Vacancies (Phase 5) |
| **Registered candidates** | All candidate accounts | Candidate accounts created on each day | All staff |

* The bars read left to right from 6 days ago to **today** (the darker bar). Hover over a bar to see the date and number.
* The coloured figure compares the last 7 days with the 7 days before: green ↗ is up, red ↘ is down, grey — is no change. When there was nothing in the week before, the card shows "+N this week" or "No change this week" instead of a percentage.
* Click a card to open the matching list (Approved opens *All requests* filtered to Approved).

### My tasks

The things waiting for **you**, oldest first, so the longest-waiting item is at the top. At most 6 are shown, with the total next to the title.

| You are | You see |
|---|---|
| Recruitment Officer, HR Manager or Managing Director | Requests at your approval stage (never your own) → *Review and decide* |
| HR Manager | Vacancies pending approval → *Approve or return* |
| Recruitment Officer | Approved requests that still need a vacancy → *Create the vacancy*; and your own draft vacancies → *Finish and submit for approval* |
| Anyone who raised a request | Your own drafts → *Finish and submit*; and requests returned to you → *Make the changes and resubmit* |
| Head of Department | Vacancies in your departments with shortlist recommendations waiting for you → *Give your view on N shortlist recommendations* (opens the shortlisting board; Phase 6) |
| Any staff member on an interview panel | Sessions that have started and still need your scorecard → *Submit your scorecard* (Phase 7) |
| Head of Department, HR Manager, Managing Director | Hiring recommendations at your stage → *Hire …: Endorse or not* / *Approve or reject* (Phase 8) |

Click a task to open it. A red badge (for example **4 d**) means it has waited 3 days or more. On a phone the *From* and *Waiting since* columns are hidden to save space.

### Vacancy closing dates (right-hand side)

A calendar of when vacancies close. Use **‹ previous** and **next ›** to change month (the address shows `?month=2026-12`). Open vacancies and vacancies that already closed that month are listed under their closing day. The coloured edge shows how soon each one closes:

| Edge | Meaning |
|---|---|
| Red | Closes within 3 days |
| Amber | Closes within 7 days |
| Green | More than a week left |
| Grey (faded) | Already closed |

### Requests in approval

The newest-moving requests that are still going through approval, with how far they have got: **25%** at Recruitment Review, **50%** at HR Approval, **75%** at MD Approval. The circle shows who raised it (hover for the name).

### Latest candidates

The five newest candidate accounts and when they registered. *Unconfirmed* means the candidate has not yet clicked the link in their confirmation email. Shown only to Recruitment Officers, the HR Manager, the Managing Director and the HR Administrator.

## Around the page (every screen)

The same visual update applies to every page of the system.

| Part | What it does |
|---|---|
| **Sidebar** | Grouped under *Talent & recruitment*, *Organisation* and *Management*. The number next to **Recruitment requests** (green) is how many requests await your decision. The number next to **Vacancies** is how many are open. *Change password* is at the bottom. |
| **Search box** (top, centre) | Searches recruitment requests by number or position. Users who can't see requests search vacancies instead. Hidden on phones. |
| **Bell** | Shown to everyone who can open Recruitment requests. A red dot means requests (or, for the HR Manager, vacancies) are waiting for your decision. Clicking it opens your *Awaiting my action* list (or *Pending approval* vacancies for the HR Manager when no requests are waiting). |
| **Moon/sun button** | Switches light and dark mode. The choice is remembered on that browser. |

## For developers

| Piece | File | Notes |
|---|---|---|
| Data | `src/Grsms.Application/Dashboard/DashboardService.cs` | Builds the whole page in one call: `GetAsync(DashboardAccess, month)`. Uses `AccessScope` for visibility and reuses `RecruitmentRequestService` / `VacancyService` for the "awaiting me" lists, so those rules exist in one place. |
| Shapes | `src/Grsms.Application/Dashboard/DashboardDtos.cs` | `DashboardDto`, `KpiCardDto` (computes `ThisWeek` and `ChangePercent`), `DashboardTaskDto`, `ClosingDayDto`, `RequestProgressDto` |
| Who sees what | `src/Grsms.Web/Controllers/HomeController.cs` | Turns the web layer's authorization policies into a `DashboardAccess(Requests, Vacancies, Candidates)` flag set, so the Application layer stays free of web code. |
| Page | `src/Grsms.Web/Views/Home/Index.cshtml` | Pure HTML + CSS: the bar charts are styled `<div>`s, so no chart library is loaded. Reuses the request and vacancy `_StatusBadge` partials. |
| Sidebar numbers and bell | `src/Grsms.Web/Helpers/NavCounts.cs` | Registered *scoped* in `Program.cs`; the sidebar and top bar both ask, but the counts are worked out only once per page. |
| Candidate queries | `IUserDirectory` (+ `UserDirectory`) | Three new methods: `CountCandidatesAsync`, `GetCandidateRegistrationDatesAsync`, `GetLatestCandidatesAsync`. |
| Styles | `src/Grsms.Web/wwwroot/css/site.css` | Section 4 (shell) and section 8 (dashboard). |

**Day boundaries are UTC.** GREL is in Ghana (GMT, no daylight saving), so UTC days are local days.

**Rebranding.** The page uses the GREL green (`--grsms-brand` in `site.css`). Changing that token (and `--grsms-brand-rgb`) recolours the bars, badges, active menu item and buttons everywhere.

**Performance.** A dashboard load runs about 15 small queries. The "in approval" and closing-calendar lists are read in full and trimmed in memory, which is fine at GREL's volumes (hundreds of records). If they grow into many thousands, move the `Take(...)` into the repository queries.

**Replaced.** This page replaces the Phase 1–3 home page (organisation counts and a build-progress list). The Phase 11 "Reports and dashboards" module is still to come and will add deeper reporting.
