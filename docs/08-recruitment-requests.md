# 8. Recruitment requests: user guide

For **department staff, Heads of Department, Recruitment Officers and the Managing Director**. No technical knowledge needed.

A recruitment request is the digital version of form **QF-81-11 Recruitment Request Form**. It asks permission to fill a position, and it must be approved at three stages, in this order, before recruitment may begin (since 17 November 2026):

```
  Department staff        Stage 1                      Stage 2                        Stage 3
  (or the HOD)            Head of Department           Recruitment Officer            Managing Director
  ────────────────        ──────────────────           ───────────────────            ─────────────────
  Draft ── submit ──► Department Head approval ──► Recruitment Officer review ──► MD approval ──► ✅ Approved for recruitment
    ▲                        │                            │                            │                    │
    └──── returned for correction ┴────────────────────────────┴────────────────────────────┘                    ▼
                             └──── or rejected ❌                              The Recruitment Officer creates the
                                                                               vacancy and the advert: no further approval
```

**The MD's approval is the last approval.** After it, nobody else approves anything: not the HR Manager, an HR Director or an administrator. The Recruitment Officer owns the process: they create and publish the vacancy, then create and publish the job advertisement, then move on to the candidates.

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

**If you are the Head of the department yourself**, submitting is your stage-1 sign-off: the history records "Department Head approval: signed off by the Head of Department" and the request goes straight to the Recruitment Officer review.

### If it is returned for correction
You'll get an email, and the request shows **Returned for correction**. Read the reviewer's comments in **History**, click **Edit**, make the corrections (and update documents if needed), then click **Resubmit for approval**. It starts again at stage 1.

### Cancel
While the request is still open, you can cancel it at the bottom of **Your actions**. Cancelling is permanent.

---

## For approvers (Head of Department, Recruitment Officer, Managing Director)

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
| 1. Department Head approval | The **Head of the request's department** (not any HOD) | None. |
| 2. Recruitment Officer review | Any **Recruitment Officer** | To approve, tick all three checks: **Request verified** (department, purpose, justification, documents), **Vacancy details validated** (position, number, engagement type, site, start date) and **Requirements confirmed** (education, experience, certificates, licence). The checks are recorded in the history. The Recruitment Officer who approves becomes the request's **Recruitment Officer**, the process owner after the MD. No budget confirmation is needed; a Budget approval document, if attached, can be read in Supporting documents. |
| 3. MD approval | Managing Director | **The last approval.** The request becomes **Approved for recruitment**. The requester and the HOD are emailed, and the Recruitment Officer gets **"Job request approved"**: *The Managing Director has approved the requisition. Your next action is to create the vacancy and publish the job advertisement.* |

Requests that were already at the **HR Manager review** on 17 November 2026 are still decided by the HR Manager there, then go to the MD. The status shows **HR Manager review (earlier workflow)**.

### Rules the system enforces
* **Nobody can approve their own request.** If you raised it, another person in your role must decide.
* Only the person or role named for the stage can decide.
* If two people open the same request and both click a decision, only the first counts. The second is asked to reload the page.
* **Recruitment can only begin after the MD's approval:** a vacancy can only be created from a request approved for recruitment.
* **Nothing is approved after the MD.** The vacancy and the advert are published by the Recruitment Officer.

---

## After the MD's approval: the Recruitment Officer creates the vacancy

The request page then shows:
* **Approved for recruitment** by the Managing Director, with *No further approvals are required*;
* the **process owner**: the Recruitment Officer (by name);
* the next action.

The process guide at the top of the page shows the journey:
* ✅ Department Head approval, ✅ Recruitment Officer review and ✅ Managing Director approval;
* **Vacancy creation** as the current step;
* then Advertisement and Candidate applications.

It also names the owner, the next action (*Create the vacancy*) and the step after it (*Advertisement*).

On the Recruitment Officer's **Action Center** and Home, the request appears as **Create the vacancy**. It is always **High** priority, marked *Approved by the Managing Director*.

The **Recruitment Officer** opens the approved request and clicks **Create the vacancy** (or uses **Vacancies → Approved requests awaiting a vacancy**). The vacancy form opens already filled in from the request: job title, unit, site, the description of the post (as plain text), educational level, field of study, level of training, experience, the required certificates, and the driving licence: if *Mandatory*, an essential certificate line and a knockout question that screens out applicants without one; if *Desirable*, a nice-to-have certificate line only.

At the top of the form, under **Job requisition**, HR then:

* sees the **Job Requisition Number** that will be given (e.g. **JRN-2026-0001**, given when the vacancy is saved);
* chooses the **Vacancy owner**: the Recruitment Officer who runs this recruitment (you, by default);
* sets the **Recruitment closing date**;
* selects **Internal or external recruitment**: internal (GREL staff only), external (public), or both.

The Recruitment Officer then publishes the vacancy, with no approval (see the [vacancies guide](09-vacancies.md)). After that, they create and publish the job advert, also with no approval (see the [job adverts guide](13-job-adverts.md)).

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
