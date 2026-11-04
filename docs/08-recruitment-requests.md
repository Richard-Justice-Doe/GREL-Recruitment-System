# 8. Recruitment requests: user guide

For **Heads of Department, Recruitment Officers, the HR Manager and the Managing Director**. No technical knowledge needed.

A recruitment request asks permission to fill a position. It must be approved by three people, in this order, before a vacancy can be advertised:

```
  Head of Department          Recruitment Officer        HR Manager             Managing Director
  ──────────────────          ───────────────────        ──────────             ─────────────────
  1. Draft  ── submit ──►  2. Recruitment review ──►  3. HR approval ──►  4. MD approval ──►  ✅ Approved
       ▲                          │                       │                     │
       └──── returned for changes ┴───────────────────────┴─────────────────────┘
                                  └──── or rejected permanently ❌
```

**Menu → Recruitment → Recruitment requests**

---

## Before you start (HR Administrator)

* Give each Head of Department the **Head of Department** role and set them as **Head** on their department (see the [Administrator guide](05-admin-guide.md)). A HOD can only raise requests for departments they head.
* Make sure at least one **Recruitment Officer**, one **HR Manager** and one **Managing Director** account exist. Otherwise requests will wait at that stage with nobody to approve them.
* Add the real **Sites**, and each department's **Units**, so they can be chosen on requests.

---

## For Heads of Department

### Raise a request
1. Click **Raise a request**.
2. Fill in:
   * **Department** (only the ones you head are listed), **Unit** (optional) and **Site** (where the person will work);
   * **Position**, **Number of positions**, **Employment type**;
   * **Reason**. For a replacement, also give the name of the employee being replaced;
   * **Justification**: why the hire is needed now;
   * **Budget code** and **Estimated annual cost** in GHS (the total yearly cost of all the positions).
3. Click **Save draft**. The request gets its number, e.g. **RR-APM-2026-0001**.

### Attach documents
On the request page, under **Documents**, choose the document type, pick the file and click **Upload**.

| Document | Required? |
|---|---|
| Job description | **Yes**, before submitting |
| Organisational chart | Optional |
| Induction programme | Optional |
| Other supporting document | Optional |

Accepted: PDF, Word, Excel, PowerPoint, PNG and JPG, up to 10 MB each. A file that isn't really the type its name says (e.g. a program renamed to `.pdf`) is refused.

### Submit
Click **Submit for approval**. The Recruitment Officers get an email. From now on you can't change the request unless it is returned to you.

### If it is returned
You'll get an email, and the request shows **Returned for changes**. Read the reviewer's comments in **History**, click **Edit**, make the changes (and update documents if needed), then click **Resubmit for approval**. It starts again at Recruitment Review. The HR Manager re-checks the budget.

### Cancel
While the request is still open, you can cancel it at the bottom of **Your actions**. Cancelling is permanent.

---

## For approvers (Recruitment Officer, HR Manager, Managing Director)

Requests waiting for you are under the **Awaiting my action** tab, oldest first. The red number on the tab and the tile on the home page show how many there are. A red **"N days"** label marks requests waiting 3 days or more. You also get an email for each one.

Open a request, read the details and documents, then use the **Your decision** panel:

| Button | What happens |
|---|---|
| **Approve** | Moves to the next stage, and the next approver is emailed. |
| **Return for changes** | Back to the HOD to edit and resubmit. **Comments required.** Say exactly what needs changing. |
| **Reject permanently** | Closed for good. **Comments required.** |

What's special at each stage:

| Stage | Who | Extra step |
|---|---|---|
| Recruitment review | Recruitment Officer | Choose the **assigned recruiter** who will run this recruitment. You are selected by default. |
| HR approval | HR Manager | Tick **Budget validated** to confirm the budget code and cost before approving. |
| MD approval | Managing Director | Final approval. The HOD, the assigned recruiter and the HR Manager are emailed. A Recruitment Officer then creates the vacancy (see the [vacancies guide](09-vacancies.md)). |

### Rules the system enforces
* **Nobody can approve their own request.** If you raised it, another person in your role must decide.
* Only the role named for the stage can decide.
* If two people open the same request and both click a decision, only the first counts. The second is asked to reload the page.

---

## Who can see which requests

| Person | Sees |
|---|---|
| Head of Department | Requests for the departments they head, and any they raised |
| Recruitment Officer, HR Manager, Managing Director, HR Administrator | All requests |
| Other staff and candidates | None. The menu item isn't shown. |

Documents can only be downloaded by people who can see the request.

---

## History and audit

Every request page shows a **History** timeline: who submitted, approved, returned or rejected it, when, and their comments. **Full change log** opens the audit trail for that request, showing every field that changed.
