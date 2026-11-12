# 20. Reports: user guide

**Menu → Reports** gives the recruitment figures for GREL, from the executive overview down to every request, vacancy, application, offer and onboarding.

## Who sees what

| | Sees |
|---|---|
| HR Manager, Recruitment Officers, Managing Director, HR Administrator | every division and department |
| Head of Department | only the departments they head (the page says so) |
| Other staff and candidates | no access |

## The filters (the same on every report)

GREL's requirement is that reports filter by **division, department, vacancy, recruitment status and recruitment period**:

| Filter | Notes |
|---|---|
| Division | Corporate or Operations |
| Department | grouped under its division; inactive departments are included so old records can be found |
| Vacancy | narrows every report to one vacancy (the requests report shows the request behind it) |
| Status | the statuses of that report's records, e.g. request statuses on *Recruitment requests*, offer statuses on *Offers* |
| Period from / to | applies to each report's own main date, shown under the filters |

Switching between reports keeps the division, department, vacancy and period.

## The reports

| Report | Shows | The period applies to |
|---|---|---|
| **Overview** | the executive dashboard: requests raised, positions approved, vacancies published, applications, hires, offer acceptance rate, average time to hire, positions still open; the **recruitment funnel**; **applications** and **hires per month** (12 months); **divisions compared** | the date of each event |
| **Recruitment requests** | each request: position, positions, estimated annual cost, status, dates, days to approve; totals | the date requested |
| **Vacancies** | each vacancy: positions, and applications that reached each stage (applied, shortlisted, interviewed, offered), filled and still open | the date published |
| **Applications** | each application: applicant, vacancy, staff or external, source, screening score, **furthest stage reached**, status | the date received |
| **Application sources** | per source (careers page, LinkedIn, Jobberman, newspapers, referrals...): applications, shortlisted, interviewed, hired, and hires per application | the date received |
| **Offers** | each offer: candidate, salary, sent, reply-by, status, days to answer; acceptance rate and average salary | the date sent |
| **Time to hire** | each hire: requested → approved → published → offer accepted, in days; averages and a chart by department | the date the offer was accepted |
| **Workflow targets** (12 Nov 2026) | each wait at each step (request HOD / HR / MD review, vacancy approval, advert HR review and IT approval, offer answer, document verification) in working days against the target in System settings: *finished on time*, *finished late*, *waiting* or *waiting, past target* (shown first); % on time per step. A request returned and resubmitted counts each wait; an offer that expired unanswered is late. Status filter = the result | the date the step started |
| **Onboarding status** | each new employee: start date, status, appointment letter, checklist progress, overdue items | the start date |
| **Missing documents** (Phase 21) 🔒 | each onboarding still missing new-hire documents: stage, how many are missing or rejected, and which | the start date |
| **Document verification** (Phase 21) 🔒 | each new-hire document: status, submitted, verified by and on, the rejection reason | the date the document was submitted |
| **Beneficiaries** (Phase 21) 🔒 | each employee's beneficiaries from the signed Personal Record Declaration, with their percentages | the employee's start date |
| **Probation** (Phase 21) | each probation: dates, status, initial meeting, assessments, the next step and whether it is overdue | the probation start date |
| **Probation extensions** (Phase 21) | each extended probation: original and new end dates, months, status, reason, improvement plan | the probation start date |
| **Confirmations** (Phase 21) | each confirmation: letter number, employee, position, start and confirmation dates, approving HR Manager, months extended | the confirmation date |

🔒 These hold personal data and need the **Open personnel files** permission (Recruitment Officers and HR Managers by default); anyone else sees an empty report saying why.

**Survey results** (Phase 10) are linked from the Reports page for HR and the MD.

Click the reference in the first column to open the record.

### How the figures are worked out
* **Funnel and "furthest stage":** an application counts at every stage it ever reached, according to its history. An applicant who was interviewed and then turned down still counts as *interviewed*. An offer that was declined counts as *offered*.
* **Time to hire:** from the date the request was made to the date the offer was accepted. The three parts are request → approval, approval → vacancy published, and published → offer accepted.
* **Offer acceptance rate:** accepted out of accepted + declined + expired. Withdrawn offers are left out.
* **Positions still open:** positions on published or closed vacancies, minus accepted offers. This ignores the period, because it describes today.
* **Drafts are left out** of the request figures.
* Figures are always live: each page reads the current records.

## Charts
Charts use one colour. Every bar has its number, and hovering shows the details. **Show as a table** under each chart lists the same numbers.

## Downloading (CSV for Excel)
**Download CSV** saves the report's table, with the same filters, as a `.csv` file that opens in Excel. Dates are written as `2026-09-21` and amounts without thousands separators, so Excel can sort and add them up.

**Safety:** names and other text typed by applicants are written so that Excel can never run them as a formula (a value starting with `=`, `+`, `-` or `@` gets a leading `'`).

## Good to know
* Reports read the live data each time. That is quick for GREL's volumes (thousands of applications a year). If GRSMS ever holds hundreds of thousands, the slowest reports can be moved to pre-calculated tables without changing the pages.
* Every application, offer and onboarding stores the **division, department and requesting HOD** of its vacancy (the rule since Phase 3), which is what makes these filters possible.
