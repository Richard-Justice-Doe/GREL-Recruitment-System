# 24. Employee onboarding: user guide

For **Recruitment Officers, HR Managers, Heads of Department, task owners and new employees**. No technical knowledge needed.

Onboarding starts when the Recruitment Officer clicks **Click to proceed to onboarding** for a candidate who **accepted the offer**, and ends when the new employee is an **active GREL employee** whose induction is complete. ([Probation](25-probation-management.md) starts at activation.)

> Updated for Phase 27 (10 November 2026): documents are reviewed **one by one as they arrive**, the stage moves **by itself**, there is **no HR Manager approval**, and the **induction follows activation**. The appointment letter, first-day details and the checklist otherwise work as in [18 Appointment letters and onboarding](18-onboarding.md).

**Menu → Onboarding** (staff). New employees use **My applications → their application → Upload my new-hire documents**.

---

## The stages

```
 RO starts         RO issues the letter      New employee                 RO verifies             RO or HR Manager         Departments, RO
 ─────────         ────────────────────      ────────────                 ───────────             ────────────────         ───────────────
 1. Letter being ──► 2. Awaiting the ◄──────► 3. Awaiting document ──────► 4. Ready for ──activate──► 5. Induction in ──close──► ✅ Completed
    prepared            new employee            verification                 activation               progress
   (uploads already open)     ▲ a document rejected, a correction asked, the record changed ┘
 (any stage before Completed) ── cancel ──► ❌ Cancelled
```

Stages 1 to 4 are **worked out automatically** after every upload, verification, rejection or signature:

| Stage | Means | Who acts |
|---|---|---|
| **Letter being prepared** | Started; the appointment letter is not issued yet. The new employee can already upload documents. | The RO issues the letter |
| **Awaiting the new employee** | Something is outstanding for the new employee: signing the letter, the personal record, a required document, a rejected document, or a correction the RO asked for. The page lists exactly what. | New employee |
| **Awaiting document verification** | Everything is in; documents are waiting for the RO. | Recruitment Officer |
| **Ready for activation** | Every required document is **verified**, the letter and the personal record are **signed**, nothing is **rejected**. | RO or HR Manager activates |
| **Induction in progress** | The employee is **active** (staff account, probation started); the departments do their induction tasks. | Task owners, the HOD, the RO |
| **Completed** | The induction is closed and filed in the personnel file. | — |

The onboarding page shows the stages, the **progress %**, the **documents due date** (10 working days after the start, set in System settings), what is still outstanding, the **expected completion date** and a **History** of every step. **Full change log** opens the audit trail.

---

## For the new employee

1. When the RO starts your onboarding you get a **welcome email**. Sign in: your home page shows **Your next step**, and the menu has **My onboarding** (with your progress) for everything: documents, letter, first day and personal record.
2. For each document, choose the file (you can take a photo with your phone) and click **Upload**. It goes to HR **straight away**; you do not need to submit anything. Accepted: **PDF, JPG, JPEG or PNG**, and **Word (.docx)** for the application letter, CV and recommendation letters; up to 10 MB each. Some documents need several files (two recommendation letters; several professional certificates).
3. Until HR has verified a document you can **Replace** a file or **remove** it; the old file is kept in the document's **History**. While HR shows it **Under review**, wait for the result.
4. Documents marked *(optional)* can be marked **not applicable**, with a short reason (for example "Not married").
5. When the appointment letter is ready (email), **read and sign** it (type your full name), and fill in and **sign your personal record** (below).

The page shows your **progress**, how many documents are **verified**, **being checked**, **rejected** and **not submitted**, the date to finish by, and a **Still to do** list. If HR rejects a document you get an email with the reason; the document shows **Rejected** and why. Upload a replacement: it shows **Resubmitted, awaiting verification**. If HR asks you to correct something else (for example your personal record), make the change and click **I have made the correction**. When everything is verified you get an email: HR then activates your staff account.

### Your personal record (Employee Personal Record Declaration)

Your name, staff number, department, position and service date are filled in from your onboarding. You give:

| Section | What to fill in |
|---|---|
| **Employee information** | Qualification, SSNIT number, date of birth (your age is worked out from it), residential address, home town, region, tribe, religion, email, mobile number, any disability (and support you need at work), visible marks |
| **Languages spoken** | Each language and how well you speak it: *very fluent*, *fluent* or *passable* |
| **Bank accounts** | Bank, branch and account number. Mark one as **primary**: your salary is paid into it |
| **Family records** | Spouse (if any): name, occupation, date of birth, address, telephone. Children: name, gender, date of birth, place of birth, where they live now. Dependants: name, relationship, age. Add as many rows as you need |
| **Next of kin** | Name, relationship, telephone, date of birth, address |
| **Emergency contact** | Name, relationship and one or two telephone numbers (up to three contacts) |
| **Beneficiaries** | Name, relationship, date of birth and **share in %**. The shares must add up to **exactly 100%**: the page shows the running total, and the record is not saved otherwise |

**The declaration.** Tick *"I declare that the above information is accurate and correct to the best of my knowledge"* and type your full name, then **Sign**. GRSMS records your name, the date and time and your IP address, with a frozen copy of everything you declared and a fingerprint of it. **If you change anything afterwards, you must sign again**; every earlier signature is kept, unchanged.

You can change your personal record until you are activated. After that you can still read it.

### The documents

The list is set by the HR Administrator on **Onboarding documents** (below). Out of the box:

| Group | Documents |
|---|---|
| Personal documents | Application letter · Updated CV · Passport-size photograph · Ghana Card / National ID (Ghanaians) · SSNIT card · Birth certificate |
| Education documents | BECE · WASSCE / SSSCE · Diploma · HND · Degree · Master's · Professional certifications: **the RO ticks which you must provide**; the others are optional |
| Employment and verification | Recommendation letters (two) · Police clearance report · Medical examination report |
| Banking information | Bank confirmation letter |
| Family documents (optional) | Marriage certificate · Children's birth certificates · Dependants' passport photographs |
| Expatriate documents (only when the RO ticks *Expatriate*) | Passport biodata page · Non-citizen card · Work permit · Residence permit |

---

## For the Recruitment Officer and the HR Manager

| | Recruitment Officer | HR Manager |
|---|---|---|
| Start onboarding, issue the letter, set the documents required, first-day details, checklist, notes, cancel | ✅ (`onboarding.manage`) | — |
| Review, verify, reject documents; ask for a correction | ✅ (`onboarding.verify-documents`) | — |
| Activate a ready employee; close the induction | ✅ (`onboarding.complete`) | ✅ |
| See every onboarding, the documents and the personal record | ✅ (`onboarding.view`) | ✅ |

The Super Admin can change who holds these permissions on **Roles & permissions**.

### Starting and the letter
Click **Click to proceed to onboarding** on the hiring case (or **Start onboarding** on **Onboarding → Waiting to start**). On the form, besides the first-day details, tick:
* **Expatriate (non-Ghanaian)**: asks for the passport biodata page, non-citizen card, work and residence permits instead of the Ghana Card;
* the **education certificates** this new hire must provide.

Starting creates the document checklist, the departmental checklist and a **provisional personnel file**, and emails the new employee a welcome with the documents due date (template *Onboarding: welcome and documents required*). Both choices can be changed later on the onboarding page (**Documents required**). Then **issue the appointment letter** (template *Onboarding: appointment letter ready*).

### Verifying the documents
Documents arrive one by one; you are emailed once the new employee has provided everything. On the onboarding page, under **Onboarding documents** (grouped by heading):
* open each file (it always downloads; every opening is recorded with your name, the time and your IP address);
* click **Start review** if you need time: the new employee can no longer replace the file until you decide;
* click **Verified against the original**, or choose a **reason** (document unreadable, incorrect document uploaded, expired document, missing pages, other) with an optional note (required for *Other*) and click **Reject**. The new employee is emailed at once and the onboarding goes back to them;
* a document that needs several files (two recommendation letters) cannot be verified until they are all there;
* **History** on each document shows every upload, replacement, review, decision and opening, and the earlier files;
* **Open the personal record** to read the Personal Record Declaration (read-only, printable) and the signature history;
* to have something else corrected (for example the personal record), use **Ask the new employee for a correction** in the *Next step* panel.

The RO can also upload a document for the new employee (for example a scan of an original brought in on paper) until activation.

### Activation and induction
When the onboarding is **Ready for activation**, the *Next step* panel shows the **departmental induction areas** (the defaults already ticked) and the **staff number** box. Click **Activate the employee**: the account becomes a staff account (Employee role added, Candidate role removed, staff number, department and site set), the probation starts, the personnel file is filled, and each ticked area's tasks are added to the checklist, given to the area's owner and due a week later. The new employee (template *Onboarding: staff account active*), the task owners and the HOD are emailed.

### Departmental induction
The checklist on the onboarding page is grouped by area:

| Area | Example tasks (built in; changeable on **Induction areas**) |
|---|---|
| Human Resource | Company orientation; policies review (plus the HR preparation tasks from the start: personal file, induction programme, payroll, benefits) |
| Corporate Information Technology | Email setup; AD account creation; system access (plus the IT tasks from the start) |
| Corporate Procurement · Corporate Supply Chain · Corporate Finance | A briefing on the department's procedures |
| Factory Operations · TBU Operations · Stores Operations · Engineering | A site or workshop induction (stores: stock-issue briefing; engineering: permit-to-work) |
| Security | Access card; security induction |
| Health & Safety | Safety training; PPE issue and emergency procedures |
| Administration | Office, transport and canteen arrangements |
| Own department (HOD) | Workspace, tools and PPE; department induction; probation objectives |

Each task shows its **owner**, **assigned date**, **due date** and, once done, who did it and when. Owners (or the HOD for department tasks, or the RO) mark it **Done** or **Not needed** (with a reason).

**The RO verifies the induction:** a task marked done by anyone else shows *waiting for HR to verify*. The RO clicks **Verify** after checking it really was done. A task the RO marks done counts as verified.

The RO can also **add a task** to any area (with an optional description), reassign one, change its due date, or remove one still to do.

### Induction areas (HR Administrator)
**Menu → Organisation → Induction areas** lists the twelve areas. For each area:
* **Owner**: who receives its tasks. With no owner, they go to HR (the vacancy's recruiter); Corporate IT with no owner goes to the Head of the IT department;
* **Ticked by default**: whether it is already ticked when activating. Out of the box: Human Resource, Corporate IT, Security, and Health & Safety;
* **Tasks**: one per line.

Changes apply to employees activated from then on, never to tasks already created.

### Onboarding documents (HR Administrator)
**Menu → Organisation → Onboarding documents** lists the document types by group. For each one: the **name**, **what the new employee must provide**, the **group**, **when it is required** (everyone / optional / chosen by the RO per hire / expatriates only / Ghanaians only), the **files needed**, whether **Word files** are accepted, where verified files are **filed** in the personnel file, the **order**, and **Active**. **Add a document** at the bottom.

A change **applies at once to every onboarding not yet activated**: a new or re-activated document is added as *Not submitted*; a retired one stops counting, but files already uploaded are kept. The old single *School certificates* document stays read-only for the onboardings that had it.

### Closing the induction
When every checklist item is **Done** or **Not needed**, **every task done by someone else has been verified**, and the start date has come, the RO or HR Manager clicks **Induction complete: close the onboarding**. The onboarding is **Completed**, the induction record is filed in the personnel file, and the HOD is emailed.

### The electronic personnel file
**Menu → Personnel files** (Recruitment Officers and HR Managers; the permission *Open personnel files*), or **Personnel file** on the onboarding page.

Each employee has one file, numbered e.g. **PF-2026-0001**, opened (empty) when the onboarding starts, with seven folders. These are records in the database, not Windows folders:

| Folder | Filed automatically at activation (each document type says where it goes) |
|---|---|
| Recruitment Documents | The application's files (CV, certificates, cover letter as sent); application letter, CV and recommendation letters |
| Employee Records | The signed Personal Record Declaration; education certificates, marriage certificate, children's birth certificates, dependants' photographs |
| Identification Records | Passport picture, birth certificate, Ghana Card, SSNIT card, passport biodata page, non-citizen card |
| Compliance Records | Medical report, police clearance, bank confirmation letter, work and residence permits |
| Onboarding Records | The onboarding and induction record: every task, who did it and who verified it (when the induction closes) |
| Probation Records | The signed initial meeting and each probation assessment ([probation](25-probation-management.md)) |
| Generated Letters | The appointment letter, frozen exactly as it was signed |

Only **verified** new-hire documents are filed. Files are not copied: the personnel file points to the same securely stored file. Click a file to download it, or a letter to read and print it (**Print or save as PDF**). HR can **add a document** to any folder.

An onboarding completed before personnel files existed shows **Open the personnel file** for HR, which opens and fills it the same way.

### Notes and cancelling
**HR notes** are for HR only (never shown to the new employee). **Cancel onboarding** (the RO) needs a reason and works at any stage before Completed.

---

## Who can see what

| Person | Sees |
|---|---|
| Holders of *Open onboarding* (Recruitment Officer, HR Manager) | Every onboarding, the new-hire documents and files, the personal record, HR notes, and the personnel files |
| Head of Department | Their department's onboardings and checklist, **not** the new-hire documents or the personal record (Ghana Card, medical report, family, bank and beneficiary details) |
| A task owner (e.g. an induction area owner) | The onboarding, while they have a task on it |
| The new employee | Their own letter, first-day information, documents and personal record, in their portal |

Every upload, replacement, review, verification, rejection, resubmission, **every opening of a file** and every stage change is recorded (the document History and the audit trail). Files are stored on the server under random names outside the website's public folder, with a SHA-256 fingerprint.
