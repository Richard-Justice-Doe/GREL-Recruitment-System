# 24. Employee onboarding: user guide

For **HR (Recruitment Officers and HR Managers), Heads of Department, task owners and new employees**. No technical knowledge needed.

Onboarding starts the moment a candidate **accepts the offer** and ends when the new employee is an **active GREL employee** with a staff account. (After that, probation begins; probation management is added in Phase 21.)

> This guide describes the onboarding workflow added in Phase 18 (6 November 2026). The appointment letter, first-day details and the departmental checklist work as described in [18 Appointment letters and onboarding](18-onboarding.md); this page covers the stages and the new-hire documents around them.

**Menu → Onboarding** (staff). New employees use **My applications → their application → Upload my new-hire documents**.

---

## The stages

```
 HR starts             HR Manager            New employee            HR                    HR Manager        Departments           HR
 ─────────             ──────────            ────────────            ──                    ──────────        ───────────           ──
 1. Draft ──letter──► 2. Awaiting ──submit──► 3. Awaiting ──verified──► 4. Awaiting ──approve──► 5. Awaiting ──done──► 6. Awaiting ──activate──► ✅ Completed
                         employee               document                 HR approval            departmental          activation
                         submission             verification                 │                  induction
                            ▲                       │                        │
                            └── rejected documents ─┘ ◄── returned for verification
 (any stage before Completed) ── cancel ──► ❌ Cancelled
```

| Stage | What happens | Who acts |
|---|---|---|
| **Draft** | HR has started the onboarding (first-day details, checklist). | HR Manager issues the appointment letter |
| **Awaiting employee submission** | The new employee signs the letter and uploads the new-hire documents. | New employee clicks **Submit to HR** (HR can submit for them) |
| **Awaiting document verification** | HR checks each document against the original. | Recruitment Officer or HR Manager |
| **Awaiting HR approval** | The HR Manager approves the new hire's records. | HR Manager (not the person who verified the documents) |
| **Awaiting departmental induction** | The departments complete their checklist tasks (IT accounts, workspace, induction, ...). | Task owners, the HOD, HR |
| **Awaiting activation** | HR records the staff number from payroll. | Recruitment Officer or HR Manager |
| **Completed** | The account is a GREL **staff account** (Employee role, department and site set). | — |

The onboarding page shows the stages as a progress bar, the **expected completion date** (30 days after the start date by default) and a **History** of every step with who did it and when. **Full change log** opens the audit trail.

---

## For the new employee

1. You receive an email when your **appointment letter** is ready. Sign in, open **My applications**, then your application.
2. **Read and sign** the letter (type your full name).
3. Click **Upload my new-hire documents**. For each document, choose the file and click **Upload**. You can add several files to one document (for example, several school certificates). Accepted: **PDF, JPG or PNG**, up to 10 MB each.
4. Documents marked *(if applicable)* can be marked **not applicable** instead, with a short reason (for example "Not married", "Ghanaian citizen").
5. When everything is uploaded and the letter is signed, click **Submit to HR**.

If HR rejects a document, you get an email listing what needs attention, and the document shows **Rejected** with HR's reason. Upload it again and click **Submit to HR** again. Verified documents are locked.

### The documents (New Hire Administrative Records)

| Required | If applicable |
|---|---|
| Application letter · CV · Passport picture · Recommendation letters · Medical report · Police clearance · School certificates · Birth certificate · Ghana Card · SSNIT card · Bank confirmation letter | Marriage certificate · Children's birth certificates · Dependants' photographs · Passport biodata page · Non-citizen card |

---

## For HR

### Starting and the letter
As before: **Start onboarding** from the hiring case or **Onboarding → Waiting to start**; the HR Manager **issues the appointment letter**. Issuing the letter for the first time moves the onboarding to **Awaiting employee submission** and emails the new employee (template *Onboarding: letter and documents*).

### Verifying the documents
When the new employee submits, the vacancy's recruiter and whoever started the onboarding are emailed. On the onboarding page, under **New-hire documents**:
* open each file (it always downloads; nothing opens inside the site);
* click **Verified against the original**, or type a reason and click **Reject**. For a "not applicable" declaration, click **Accept** or reject it with a reason;
* if anything was rejected, click **Send rejected documents back** (optionally with a message). The new employee is emailed the list with your reasons;
* when every document is verified or accepted, click **All documents verified: send to the HR Manager**. The HR Managers are emailed.

HR can also upload a document for the new employee (for example a scan of an original brought in on paper) until the HR Manager approves, and can **Submit for the new employee**.

### HR approval
An HR Manager **approves** (the checklist owners and the HOD are emailed that the induction can begin) or **returns for verification** with comments. **The HR Manager who marked the documents verified cannot approve the same onboarding**; another HR Manager must.

### Induction and activation
* When every checklist item is **Done** or **Not needed** and the start date has come, click **Induction complete: ready to activate**.
* Enter the **staff number** from payroll and click **Activate the employee**. The new employee's account becomes a staff account: the Employee role is added, the Candidate role removed, and the staff number, department and site are set. The new employee (template *Onboarding: staff account active*) and the HOD are emailed.

### Notes and cancelling
**HR notes** are for HR only (never shown to the new employee). **Cancel onboarding** needs a reason and works at any stage before Completed.

---

## Who can see what

| Person | Sees |
|---|---|
| HR (Recruitment Officer, HR Manager) | Every onboarding, the new-hire documents and files, HR notes |
| Head of Department | Their department's onboardings and checklist, **not** the new-hire documents (personal records such as the Ghana Card and medical report) |
| A task owner | The onboarding, while they have a task on it |
| The new employee | Their own letter, first-day information and documents, in their portal |

Every upload, verification, rejection and stage change is in the audit trail. Files are stored on the server under random names outside the website's public folder, with a SHA-256 fingerprint.
