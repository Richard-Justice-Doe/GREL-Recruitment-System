# 8. Recruitment requests: user guide

For **department staff, Heads of Department, the HR Manager, the Managing Director and Recruitment Officers**. No technical knowledge needed.

A recruitment request is the digital version of form **QF-81-11 Recruitment Request Form**. It asks permission to fill a position, and it must be approved at three stages, in this order, before recruitment may begin:

```
  Department staff            Stage 1                    Stage 2                  Stage 3
  (or the HOD)                Head of Department         HR Manager               Managing Director
  ────────────────            ──────────────────         ──────────               ─────────────────
  Draft  ── submit ──►  Department Head review ──►  HR Manager review ──►  MD approval ──►  ✅ Approved
    ▲                          │                         │                      │              │
    └──── returned for correction ┴─────────────────────────┴──────────────────────┘              ▼
                               └──── or rejected ❌                                  HR converts it into a
                                                                                     job vacancy
```

**Menu → Recruitment → Recruitment requests**

---

## Before you start (HR Administrator / Super Admin)

* Every staff member who raises requests needs their **Department** set on their account (Users & roles). They can only raise requests for that department.
* Set the **Head** of each department (Departments). Their staff's requests go to them for stage 1. A request can't be submitted while its department has no Head.
* Make sure at least one **HR Manager**, one **Managing Director** and one **Recruitment Officer** account exist.
* Add the real **Sites**, and each department's **Units**, so they can be chosen on requests.
* By default every **Employee** and **Head of Department** may open and raise requests. A Super Admin can change this on **Roles & permissions** ("Open recruitment requests", "Raise recruitment requests"), e.g. to let only supervisors raise them.

---

## For department staff and Heads of Department: raising a request

### Fill in the form
Click **Raise a request**. The form follows QF-81-11:

| Section | Fields |
|---|---|
| **1. Employment details** | Department, Unit (optional), Site; **Engagement type** (Permanent or Contract); **Category** (Executive, Senior or Junior); **Purpose** (Internal restructuring, Replacement, Expansion, New position or Other). For Replacement, give the employee being replaced; for Other, say what the purpose is. |
| **2. Request information** | **Request date** (today by default, can't be in the future), **Specific job title**, **Number of staff required**, **Proposed starting date** (after today). |
| **3. Description of the post** | A large text area with a formatting toolbar: **bold**, *italic*, underline, headings, bulleted and numbered lists. The counter underneath shows the characters used (at least 50, at most 10,000; formatting doesn't count). Also a short **Justification**. |
| **4. Qualification requirements** | **Educational level**, Category (field of study), Level of training, Certificate(s) required, Years and description of professional experience, and the **driving licence** (optional: *Not needed* by default, *Desirable* or *Mandatory*, with the class if you know it). |
| **5. Additional information** | Observations / remarks. |

Click **Save draft**. The request gets its number, e.g. **RR-APM-2026-0001**.

Pasting from Word keeps the text and line breaks but drops Word's fonts and colours. Use the toolbar to format it.

### Attach the supporting documents
On the request page, under **Supporting documents**, choose the document type, pick the file and click **Upload**. The checklist shows which ones are attached.

| Document | Required? |
|---|---|
| Job description | **Yes**, before submitting |
| Main accountabilities | Optional |
| Organisational structure | Optional |
| Department justification | Optional |
| Budget approval document | Optional |

Accepted: **PDF, Word (DOCX) and Excel (XLSX)** only, up to 10 MB each. A file that isn't really the type its name says (e.g. a program renamed to `.pdf`) is refused. Files are stored on the server under random names, outside the website's public folder; the database keeps only their description and location. Only people who can see the request can download them.

### Submit
Click **Submit for approval**. Your Head of Department gets an email. From now on you can't change the request unless it is returned to you.

**If you are the Head of the department yourself**, submitting is your stage-1 sign-off: the history records "Department Head review: signed off by the Head of Department" and the request goes straight to the HR Manager.

### If it is returned for correction
You'll get an email, and the request shows **Returned for correction**. Read the reviewer's comments in **History**, click **Edit**, make the corrections (and update documents if needed), then click **Resubmit for approval**. It starts again at stage 1, and the HR Manager confirms the budget again.

### Cancel
While the request is still open, you can cancel it at the bottom of **Your actions**. Cancelling is permanent.

---

## For approvers (Head of Department, HR Manager, Managing Director)

Requests waiting for you are under the **Awaiting my action** tab, oldest first. The red number on the tab and the tile on the home page show how many there are. A red **"N days"** label marks requests waiting 3 days or more. You also get an email for each one, and a reminder if one waits too long.

Open a request, read the form and the documents, then use the **Your decision** panel:

| Button | What happens |
|---|---|
| **Approve** | Moves to the next stage, and the next approver is emailed. |
| **Return for correction** | Back to the person who raised it, to correct and resubmit. **Comments required.** Say exactly what needs changing. |
| **Reject** | Closed for good. **Comments required.** |

What's special at each stage:

| Stage | Who | Extra step |
|---|---|---|
| 1. Department Head review | The **Head of the request's department** (not any HOD) | None. |
| 2. HR Manager review | HR Manager | Tick **Budget confirmed** to confirm the post is budgeted for (see the Budget approval document, if attached) before approving. |
| 3. MD approval | Managing Director | Final approval. The requester, the HOD, the HR Manager and the Recruitment Officers are emailed. |

### Rules the system enforces
* **Nobody can approve their own request.** If you raised it, another person in your role must decide.
* Only the person or role named for the stage can decide.
* If two people open the same request and both click a decision, only the first counts. The second is asked to reload the page.
* **Recruitment can only begin after the MD's final approval:** a vacancy can only be created from an approved request.

---

## After approval: converting the request into a job vacancy (HR)

A **Recruitment Officer** opens the approved request and clicks **Convert into a job vacancy** (or uses **Vacancies → Approved requests awaiting a vacancy**). The vacancy form opens already filled in from the request: job title, unit, site, the description of the post (as plain text), educational level, field of study, level of training, experience, the required certificates, and the driving licence: if *Mandatory*, an essential certificate line and a knockout question that screens out applicants without one; if *Desirable*, a nice-to-have certificate line only.

At the top of the form, under **Job requisition**, HR then:

* sees the **Job Requisition Number** that will be given (e.g. **JRN-2026-0001**, given when the vacancy is saved);
* chooses the **Vacancy owner**: the Recruitment Officer who runs this recruitment (you, by default);
* sets the **Recruitment closing date**;
* selects **Internal or external recruitment**: internal (GREL staff only), external (public), or both.

The vacancy then follows its own approval by the HR Manager (see the [vacancies guide](09-vacancies.md)).

---

## Who can see which requests

| Person | Sees |
|---|---|
| Staff member | Requests they raised |
| Head of Department | Requests for the departments they head, and any they raised |
| Recruitment Officer, HR Manager, Managing Director, HR Administrator, Admin | All requests |
| Candidates | None. The menu item isn't shown. |

Documents can only be downloaded by people who can see the request.

---

## History and audit

Every request page shows a **History** timeline: who submitted, approved, returned or rejected it, when, and their comments. **Full change log** opens the audit trail for that request, showing every field that changed.

---

## Requests from before the digital QF-81-11 form

Requests raised before 5 November 2026 don't have the new fields (category, dates, description of the post, qualification requirements). They show "—" for these. A draft or returned request must be completed (click **Edit**) before it can be submitted. A request still waiting at the old **Recruitment review** stage can be decided by a Recruitment Officer as before; it then goes on to the HR Manager.
