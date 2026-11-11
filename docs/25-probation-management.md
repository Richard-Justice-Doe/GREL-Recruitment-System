# 25 Probation management and confirmation

Probation starts automatically when HR **activates** a new employee at the end of [onboarding](24-onboarding-module.md) and ends when the HR Manager **confirms** the appointment, **extends** the probation or decides **not to confirm**. Added in Phase 21.

## The timeline

| When | What happens | Who |
|---|---|---|
| Activation day | The probation opens (`PRB-<year>-<0000>`): **6 months** from the start date. The HOD and the employee are emailed. | GRSMS |
| Within **7 days** | **Initial meeting**: objectives (each with success indicators and an optional due date) and a development plan (training needs, development needs, timeline). The HOD records it and signs it by typing their full name. | Head of Department |
| After the meeting | The employee reads the objectives in **My probation**, ticks *I have discussed and agree...* and signs by typing their full name. The probation is now *In progress* and the signed meeting is filed in the personnel file (*Probation Records*). | Employee |
| **3 months** | **First assessment**: ratings, comments, an optional improvement plan. Filed in the personnel file. | Head of Department |
| **6 months** (the end date) | **Final assessment**: the same ratings plus a recommendation: *Confirm*, *Extend* or *Do not confirm*. Filed; the HR Managers are emailed. | Head of Department |
| After the final assessment | **HR decision**: approve the recommendation, or return it to the HOD with comments. | HR Manager |

An assessment can be recorded from **14 days before** its due date. It can't be recorded before the employee has signed the initial meeting.

### The ratings
Each assessment rates six areas on a five-point scale (*Outstanding, Very good, Good, Satisfactory, Unacceptable*):
Application of skills, Quality of work, Productivity, Co-operation, Dependability, and **Ability to supervise** (only for roles that supervise; it may be left empty). Comments are required.

### Extending
* The HOD chooses the number of months, a **reason** and an **improvement plan** (all required).
* Extensions add up to **3 months at most** over the whole probation (for example 2 months, then 1 more). The form only offers what is left.
* When the HR Manager approves, the end date moves (the original end date is kept) and the employee is emailed. The next final assessment is due at the new end date.

### Confirming
When the HR Manager approves *Confirm*:
* a **confirmation letter** is generated (`CNF-<year>-<0000>`), frozen as HTML and filed in the personnel file under *Generated Letters*;
* a **confirmation record** stores the employee, the probation, the letter number, the confirmation date and the approving HR Manager;
* the employee is emailed and finds **My confirmation letter** in *My probation* (print it or *Save as PDF* from the print dialog).

### Not confirming
The probation is closed as *Not confirmed* and the employee is emailed that the outcome is available. What follows (separation) is handled outside GRSMS.

### Returning
The HR Manager may return the final assessment to the HOD with comments (required). The HOD sees the comments and records the final assessment again.

## Screens

| Screen | Who | What |
|---|---|---|
| **Probation** (menu) | HR and Heads of Department | Tabs *Reviews due*, *In progress*, *Awaiting HR decision*, *All*; filter by department; overdue steps in red |
| **Probation → details** | the same | Dates, the next step, objectives and development plan, assessments, history; the forms for the step the viewer may do |
| **My probation** (menu) | the employee | Their dates, the objectives to sign, assessments, the outcome and the confirmation letter |
| **Home dashboard** | HR | *Employees onboarding, Documents awaiting verification, Incomplete onboarding cases, Probation reviews due, Probation extensions, Confirmations pending* |
| **Home dashboard** | Heads of Department | *New employees assigned, Objectives pending, Assessments due, Probation cases* (their departments only) |

Each dashboard figure links to the list it counts; figures that need action are highlighted.

## Who can see and do what

| | Sees | Does |
|---|---|---|
| Holders of *Open probation* (Recruitment Officers, HR Managers by default) and of *See all departments* (e.g. the MD) | every probation | — |
| Holders of *Decide probation outcomes* (the HR Manager by default) | every probation | the HR decision (not on an assessment they recorded themselves) |
| Head of Department | probations in the departments they head | the initial meeting and the assessments |
| The employee | their own probation | signs the initial meeting, reads the outcome and the letter |
| Other staff | nothing | — |

Every rule is checked in `ProbationService`, not only on the screens. Since 10 November 2026 these are **permissions** the Super Admin can change on Roles & permissions (before, they followed the RO and HR Manager roles).

## Reports
Six new reports on the [Reports](20-reports.md) page (each downloads as CSV): *Missing documents, Document verification, Beneficiaries, Probation, Probation extensions, Confirmations*. The first three hold personal data, so they need the **Open personnel files** permission; anyone else sees an empty report saying why.

## Notes
* Probation starts only for onboardings **activated from Phase 21 onwards**. Employees activated earlier have no probation record in GRSMS.
* No automatic reminders are sent for due reviews yet: due and overdue steps show on the dashboards and the *Reviews due* tab.
* Performance management after confirmation is out of scope.
