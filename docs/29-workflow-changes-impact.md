# 29. Workflow changes: impact analysis (budget, IT advert approval, candidate-only uploads, bank details)

**Status:** ANALYSIS ONLY: nothing has been changed. Questions in §6 must be answered first.
**Date:** 11 November 2026 · **Branch:** `feature/ats-and-super-admin` (after commit df43086, pushed)

Four approved business-rule changes were checked against the code as it is today. For each: what the code does now, what changes, and every place affected. §5 lists the conflicts with earlier decisions; §6 the questions.

---

## 1. Change 1: budget approval is not mandatory for HR Manager approval

**Today:**
* The **budget approval document is already optional.** Of the five QF-81-11 supporting documents, only the **job description** is required to submit (`RecruitmentRequestService` line 280). Nothing checks for `AttachmentCategory.BudgetApproval`.
* What actually blocks the HR Manager is the **"Budget confirmed" tick**. At the HR approval stage, approving fails with *"Tick 'Budget confirmed' to confirm the budget for this post before approving"* (`RecruitmentRequestService.DecideAsync`, line ~349). Returning and rejecting are already possible without it.

**Change:**
* Approving no longer requires the tick. If ticked, the system still records who confirmed the budget (`BudgetValidated`, `BudgetValidatedByUserId`); if not, the request is approved without a budget confirmation.
* The request page shows "Budget: not confirmed / no budget document" as information only.
* Existing requests are unchanged.

| Area | Affected |
|---|---|
| Domain | `RecruitmentRequest.BudgetValidated` (comment only); `Roles.HrManager` description ("validates budgets"); `RecruitmentRequestStatus.HrApproval` comment |
| Application | `RecruitmentRequestService.DecideAsync` (remove the block); `RecruitmentRequestDtos` (comments, `BudgetValidated` stays) |
| Web | `Views/RecruitmentRequests/Details.cshtml` (tick wording: optional) |
| Database | **no change** (the columns stay; no constraint enforces the tick) |
| Tests | `RecruitmentRequestServiceTests`, `RecruitmentRequestWebTests` (the "tick required" assertions change) |
| Docs | 08 Recruitment requests, 04 Security and roles (HR Manager description) |

---

## 2. Change 2: job adverts approved by IT before publishing

**Today:**
* Statuses: `Draft → Scheduled → Live → Closed` (`AdvertEnums.cs`).
* The **Recruitment Officer or HR Manager** (`AdvertWorkflow.ManagerRoles`) writes and publishes adverts; publishing goes Live straight away or on the start date.
* Each channel (internal portal, company website, LinkedIn, Indeed, Jobberman, newspaper) is ticked *Posted* / *Removed* by the same people.
* The hourly job publishes scheduled adverts and closes expired ones.
* **There is no "IT Manager" role** in GRSMS.

**New flow:**
```
RO creates Draft → submits → AwaitingHrReview (HR Manager) → AwaitingItApproval (IT Manager) → Published (now or on the start date) → Closed
                   ▲ ReturnedForCorrection (from HR or IT, with a reason) ┘      Rejected (final)
```

**Proposed implementation (keeps existing data and the careers page working):**
* Add statuses `AwaitingHrReview`, `AwaitingItApproval`, `ReturnedForCorrection` and `Rejected`.
* Keep `Draft`, `Scheduled` (published with a future start date), `Live` (shown as **"Published"**) and `Closed`. Then the careers page, applications and the hourly job need no change, and no existing data has to move.

| Area | Affected |
|---|---|
| Domain | `AdvertEnums` (statuses + actions: Submitted, HrApproved, ItApproved, Returned, Rejected, Unpublished); `AdvertWorkflow` (moves, who acts, `ManagerRoles` split); `JobAdvert` (submitted/reviewed by and when, return/reject reason); **new role "IT Manager"** (or the Head of IT, Q1); `Roles`, `Permissions` (+ `adverts.review.it`, `adverts.publish`, `adverts.return`, `adverts.unpublish`, plus an HR review permission, Q3) and their defaults |
| Application | `AdvertService` (submit, HR review, IT approve/publish, return, reject, unpublish; edits only in Draft/Returned; channel *Posted/Removed* by IT); `AdvertDtos` (Can… flags); `DashboardService` (to-dos: adverts to review for HR and IT; **IT dashboard** cards: awaiting approval, scheduled, active, expired); `ReminderService` (adverts waiting, optional); emails via `WorkflowMailer` (HR → IT on submit, IT → HR on approve, IT → RO + HR on return) |
| Web | `AdvertsController` (new actions Submit, Review, Approve, Return, Reject, Unpublish; policies); `Views/Adverts/Details`, `Index` (tabs: To review, Awaiting IT, Published, …), `Form`, `_AdvertStatusBadge`; `Views/Vacancies/Details` (advert box); `Home/Index` (IT cards); sidebar (IT Manager sees *Job adverts*); `Policies` |
| Infrastructure | `AdvertRepository` (status filters); EF configuration (new columns); migration + SQL script 26 |
| Reports | advert counts by status (report "Vacancies and adverts", if it groups by status) |
| Tests | `AdvertServiceTests`, `AdvertWebTests`; tests that create Live adverts directly (`ApplicationServiceTests`, `ApplicationWebTests`, `AtsServiceTests`, `AtsWebTests`, `ShortlistingServiceTests`) keep working if `Live` stays |
| Docs | 13 Job adverts, 04 Security and roles, 11 Home dashboard |

---

## 3. Change 3: only the candidate uploads onboarding documents

**Today:**
* The new employee uploads, replaces, removes and marks documents "not applicable".
* **The Recruitment Officer (onboarding.manage) can also do all of that on their behalf** until activation (`OnboardingWorkflow.HrMayEditDocuments`, `DocumentVerificationService.CanEdit`, `OnboardingController.UploadDocument / RemoveDocumentFile / MarkNotApplicable`, the upload boxes in `_EmployeeDocuments` on the staff page). This was a Phase 18 decision: "HR can upload a scan of an original brought in on paper".
* Verifying and rejecting is the **RO only** (Phase 27 decision, 10 November); HR Managers view only.
* The audit already records upload date and uploader, verification date and verifier, rejection reason and count, the full history (`EmployeeDocumentEvent`), and every opening.

**Change:**
* Staff can no longer upload, replace, remove or mark not applicable. The three controller actions and their permission checks go, and the staff page shows no upload boxes.
* Staff keep: view, open files, start review, verify, reject (which is "request resubmission"), ask for a correction, track progress.
* The history records **"by the new employee"** for every upload; already-recorded staff uploads stay in the history as they are.

| Area | Affected |
|---|---|
| Domain | `OnboardingWorkflow.HrMayEditDocuments` (removed) |
| Application | `DocumentVerificationService` (`CanEdit` = own only; `LoadForEditAsync` refuses staff); class comment |
| Web | `OnboardingController` (remove `UploadDocument`, `RemoveDocumentFile`, `MarkNotApplicable`); `_EmployeeDocuments` (no edit controls for staff); `Onboarding/Details` text |
| Permissions | `onboarding.manage` description (no longer "upload on their behalf") |
| Tests | `DocumentVerificationServiceTests` (a staff upload must now fail) |
| Docs | 24 Onboarding (remove "HR can also upload a document for the new employee"), 27/phases notes |
| **Not affected** | Personnel files: HR may still **file** documents in a personnel file (Phase 20). That is the employee's record, not an onboarding document (see Q8). |

---

## 4. Change 4: bank details as data, not a document

**Today:**
* Bank details are **already captured as data** in the Personal Record Declaration (Phase 19): one or more `EmployeeBankAccount` rows with bank, branch and account number (all required), exactly one marked **primary**. They are part of the signed declaration snapshot.
* In addition, the **"Bank confirmation letter"** is a required onboarding document type (code `BankConfirmationLetter`, group *Banking information*, filed under *Compliance Records*).

**Change:**
* `EmployeeBankAccount`: add **Account name** (required? Q10), **Account type** (e.g. Current / Savings / Mobile money? Q10) and **Mobile number linked to the account** (optional). Bank name, branch and account number stay required.
* Remove the bank letter from new and running onboardings by **retiring** the `BankConfirmationLetter` type. With the Phase 27 rules it then stops counting everywhere (checklist, readiness, missing-documents report, digest); files already uploaded stay in the history and the personnel file.
* The candidate keeps entering bank details in the personal record (step 3, *Bank account*).
* HR can **view** the bank details (already: onboarding.view sees the personal record).
* HR **validates** them: a new "Bank details checked" mark (by and when) on the personal record (Q11).
* HR can **request a correction**: already possible ("Ask the new employee for a correction"); the message can say what to fix.

| Area | Affected |
|---|---|
| Domain | `EmployeeBankAccount` (+3 fields); `EmployeePersonalRecord` (+ BankDetailsCheckedAt/By, if Q11 = yes); `DefaultDocumentTypes` (BankConfirmationLetter seed retired); `DocumentGroup.Banking` (no longer used by a document) |
| Application | `PersonalRecordService` (validation, snapshot, read-only view); `PersonalRecordDtos`; `OnboardingWorkflow.CheckReadiness` (bank check, if Q11 makes it required for activation); `OnboardingService` (a "Bank details checked" action); `WorkforceReportService` (no change needed: retired types drop out) |
| Web | `MyApplications/PersonalRecord` (bank rows: new fields); `Shared/_PersonalRecordView` (HR view); `Onboarding/Details` (check button); `Onboarding/PersonalRecord` |
| Database | migration: 3 columns on `EmployeeBankAccounts`, 2 on `EmployeePersonalRecords`, `UPDATE OnboardingDocumentTypes SET IsActive = 0` for the bank letter, and `IsRequired = 0, IsApplicable = 0` for bank-letter rows **without files** on running onboardings; script 26 |
| Notifications | the welcome email (template text mentions documents only: unchanged); the correction email exists |
| Reports | *Missing documents* and *Document verification* drop the bank letter automatically; *Beneficiaries* unchanged |
| Tests | `PersonalRecordServiceTests`, test base `ValidRecord()` (new fields), `OnboardingWebTests` (posted bank fields), personnel-file count (11 → 10 verified files), probation report count (10 → 9) |
| Docs | 24 Onboarding (documents table, personal record table), 03 Database |

---

## 5. Conflicts with earlier decisions

| # | Earlier decision | New rule | Effect |
|---|---|---|---|
| C1 | **Phase 18**: HR may upload a scan of an original the new employee brings on paper | Only the candidate uploads | A new employee without a phone or e-mail access has no way in; HR must help them use the portal (or a kiosk PC) |
| C2 | **Phase 27 (10 Nov)**: **only the RO verifies**, the HR Manager only views | "HR can verify, reject, request resubmission" | Either the HR Manager verifies again (a permission change) or "HR" here means the RO (Q7) |
| C3 | **Phase 4**: the RO and HR Manager publish adverts | IT publishes after HR and IT review | Adverts take longer to go live; *Scheduled* stays, but only after IT approval |
| C4 | **Phase 19**: several bank accounts, one primary | "EmployeeBankAccount" with one set of fields | Keep the list with one primary, or allow just one account (Q9) |
| C5 | **Phase 27 default checklist** includes the bank letter as *required* | No bank document | Running onboardings that already uploaded it keep the file (filed in the personnel file, no longer counted) |

## 6. Questions before implementation

**Change 1**
1. Should the HR Manager still **see and optionally tick** "Budget confirmed" (recorded when ticked), or should the tick be **removed** altogether?

**Change 2**

2. **Who is the "IT Manager"?** Options: (a) a new role **IT Manager** given by the Super Admin, (b) the **Head of the IT department** (department code IT, as for induction), or (c) any holder of the new permission `adverts.review.it`.
3. The HR review step needs a permission too: add **`adverts.review.hr`** (HR Manager by default)? Or does the HR Manager review through `adverts.return` and a role check?
4. Who may **Reject** an advert (final): the HR Manager, the IT Manager, or both? And is a rejected advert final, or can the RO clone it into a new draft (already possible)?
5. After an advert is **Published**, can the RO still edit it? Options: (a) any change sends it back for HR and IT approval, (b) only the closing date can change without approval, (c) no edits: close and clone.
6. **Existing adverts:** live and scheduled ones stay published (recommended), and drafts must go through the new review. Agree? And who ticks each channel as *Posted*: IT only, or IT and the RO?

**Change 3**

7. "HR can verify / reject / request resubmission": does this mean the **HR Manager verifies again** (reversing the 10 Nov RO-only rule), or only the **Recruitment Officer** as now?
8. HR may still **file** documents in an employee's **personnel file** (not onboarding). Keep that?

**Change 4**

9. **One bank account** per employee, or keep **several with one primary** (salary account)?
10. Is **Account name** required? Which **account types** (Current, Savings, Mobile money wallet, Other)?
11. Should HR **check the bank details** (a "Bank details checked" mark) and should activation **wait** for that check?
12. For **running onboardings** that already uploaded a bank letter: keep the file in the history and personnel file (recommended), or also show it as "no longer required" on the checklist?

## 7. Decisions (user's answers, 11 November 2026)

**Change 1:** the **"Budget confirmed" tick is removed**. HR Managers approve, return or reject without it; the budget document stays an optional upload. The old `BudgetValidated` values stay in the database for history.

**Change 2:**
* A **new role "IT Manager"**, given by the Super Admin, holds the advert IT permissions by default.
* **Editing a published advert takes it offline** and sends it back through HR and IT review.
* **The HR Manager and the IT Manager can both reject** (final, with a reason); the RO can still clone it into a new draft.
* **Live and scheduled adverts stay published**; drafts go through the new review.
* **The IT Manager alone** ticks channels *Posted* / *Removed*.
* *Proposed (not asked):* an **`adverts.review.hr`** permission for the HR review step (HR Manager by default), next to `adverts.review.it`, `adverts.publish`, `adverts.return` and `adverts.unpublish` (IT Manager by default; `adverts.return` also HR Manager).

**Change 3:** **only the candidate uploads** onboarding documents. **Verification stays with the Recruitment Officer only** (the 10 Nov rule; HR Managers view). HR can still **file documents in personnel files**.

**Change 4:**
* **One bank account per employee**, with **only bank name and account number**, both required. Branch, account name, type and mobile number are not captured; existing branch values stay in the database and in signed declarations.
* **No bank check mark**: HR views the details and can ask for a correction.
* The **bank letter is no longer asked for anywhere**; letters already uploaded stay in the document history and the personnel file.
