# 28. Usability review: making GRSMS simple

**Status:** APPROVED 10 November 2026 (all four stages; decisions §9). Built in stages U1 → U4.
**Date:** 10 November 2026 · **Branch:** `feature/ats-and-super-admin` (after commit 754b0d7)

**What you asked for:** make GRSMS very simple and easy to use, for **every kind of user** (candidates and new employees, HODs and panel members, Recruitment Officers and HR, administrators), with **fewer menus and clicks**, a clear **"what to do next"** everywhere, **plainer words and shorter forms**, and pages that **work well on a phone**. **Only the look and the flow change:** every business rule, approval step and permission stays as it is.

**How it was done:** each role's menu was worked out from the permissions; the home pages, the record pages and the forms were read and measured (fields, buttons, tabs); the text users see was searched for jargon and developer notes; the layout was checked at phone width. Nothing was run in a browser.

---

## 1. Where things stand today

| Who | Menu links | To reach their main task | Pain points |
|---|---|---|---|
| **Candidate / new employee** | 5 | Home → My applications → the application → *Respond to the offer* / *Sign the letter* / *Upload documents*: **3 clicks**, and the home page does not say anything is waiting | Nothing on the home page says "you have an offer to answer"; the personal record is **one page of 54 fields**; document upload is a plain file box (no camera hint) |
| **Panel member / employee** | ~8 (Requests, Interviews, a "My career" group of 5) | Home to-do → scorecard: 1 click (good) | The "My career" group (Job openings, My applications, My profile, My probation, My audit trail) takes half the menu, for things staff rarely use |
| **Head of Department** | ~15 in 4 groups | Requests and scorecards are on the home to-dos; **probation meetings and assessments are not** | Sees *ATS screening* and *Hiring & offers* although the HOD no longer decides hires; probation work is found only through the menu |
| **Recruitment Officer** | ~17 in 5 groups | Most work is on the to-dos, but **documents to verify, employees ready to activate, ATS rejections to confirm and letters to issue are not** | *Applications* and *ATS screening* are two menu items for the same candidates; the onboarding page has **28 fields and 16 buttons on one screen**; the hiring case 18 fields / 13 buttons |
| **HR Manager** | ~19 | As the RO, plus probation decisions **not** on the to-dos | As above |
| **Administrator / Super Admin** | up to **31**, 13 of them in *Admin* | — | A long Admin list mixing organisation data, onboarding set-up, people, system settings and audit |

**Already good, to keep:** the home **"Your to-dos"** list (one card per thing waiting, oldest first); **"Your next step"** panels on the hiring case, the interview and the onboarding pages; menu counts; search in the top bar; the sidebar slides in on phones; the vacancy form is pre-filled from the approved request; consistent status colours.

**Small problems found:** two pages still show **developer notes** to users: *"Shortlisting arrives in Phase 6"* (application page) and *"…made in the next step (Phase 8)"* with *"with the Head of Department"*, which is **no longer true** (the RO approves hires since 9 November) (interview results page).

---

## 2. Fewer menus and clicks

| # | Change | Before → after | For |
|---|---|---|---|
| **N1** | Move the staff **"My career"** links into the menu under your name (top right), next to *My profile*, *Change password*, *My audit trail* | 5 sidebar links → 0 for staff (candidates keep them: it is their whole menu) | staff |
| **N2** | One **Administration** page with cards in 5 sections (Organisation: divisions, departments, units, sites · Onboarding set-up: induction areas, onboarding documents · People: users, roles & permissions · System: ATS settings, system settings, email templates, backups · Audit: monitor, trail) | 13 sidebar links → **1** | admins |
| **N3** | **Applications** and **ATS screening** become one menu item, *Candidates*, with a *Screening* tab | 2 → 1 | RO, HR |
| **N4** | **Reports** and **Survey results** become one *Reports* item (survey results is one of the reports) | 2 → 1, and the *Insights* group disappears | RO, HR, MD |
| **N5** | **Short flat menu** for occasional users: when someone has 7 links or fewer, show them without group headings (Home, Requests, Interviews, Onboarding tasks, Probation) | groups of 1-2 links → one short list | HOD, panel, employee |
| **N6** | Show each link **only when it can be used**: HODs no longer see *ATS screening* and *Hiring & offers* (they don't decide hires any more); a candidate sees *My offer* / *My Onboarding* only while they have one | fewer dead ends | HOD, candidate |
| **N7** | After an action, **land on the next thing**: after submitting a scorecard → back to your to-dos; after verifying a document → the next one waiting; after approving a request → the next request waiting | 1-2 clicks saved per action | everyone |

**Result:** a candidate keeps 5 links; a panel member about 3; a HOD about 6; a Recruitment Officer about 10; a Super Admin about 12 (instead of 31).

## 3. "What to do next" everywhere

| # | Change | For |
|---|---|---|
| **X1** | **Complete the home to-dos** with what is missing: ATS rejections to confirm, appointment letters to issue, documents to verify, employees ready to activate, induction to close, probation meetings and assessments due (HOD), probation decisions (HR Manager) | RO, HR, HOD |
| **X2** | **Candidate home "Your next step"** card at the top: the one thing to do now, with one big button: answer your offer (with the reply-by date) · sign your appointment letter · upload documents (3 of 16 left) · fill in your personal record · your interview is tomorrow · complete your profile | candidates, new employees |
| **X3** | The **"Your next step" panel on every record page** (today: hiring case, interview, onboarding): add it to the request, vacancy, application and probation pages, always first, always saying who acts next ("Waiting for the HOD") when it isn't you | everyone |
| **X4** | **One primary button per page** (the next step); everything else as secondary buttons or under *More actions* | everyone |

## 4. Plainer words and shorter forms

| # | Change | For |
|---|---|---|
| **W1** | **Wording pass** on everything users read: remove the two developer notes; "ATS" → "Screening" in menus and titles; "HOD" spelled out where candidates read it; one name per thing (e.g. always "Recruitment Officer", never "recruiter" and "RO" mixed); short sentences in help texts | everyone |
| **W2** | **Personal record in 6 short steps** (About you · Languages · Bank · Family · Next of kin & emergency · Beneficiaries & signature), each saved on its own, with a progress bar and "Back / Save and continue"; nothing lost if the phone sleeps | new employees |
| **W3** | **Onboarding page in tabs**: *Next step* (always visible) · Documents · Letter & first day · Checklist · History & notes | RO, HR |
| **W4** | **Hiring case and interview page**: show only the current stage's form; earlier stages fold into a summary ("Offer sent 3 Nov, accepted 5 Nov ✓") | RO, HR, panel |
| **W5** | **Recruitment request form (QF-81-11)** as 4 steps (Post · Reason & budget · Description · Documents) with "Step 2 of 4", department and site pre-filled from your account | HOD, staff |
| **W6** | **Vacancy form**: the main fields first; knockout questions, requirements and advert options under *More options* | RO |
| **W7** | **Helpful empty pages**: instead of an empty table, one sentence and one button ("No vacancies yet. *Create a vacancy*") | everyone |
| **W8** | **Plain status names** in lists, with a hint on hover ("Awaiting the new employee: they still need to upload 3 documents") | everyone |

## 5. Mobile-friendly

| # | Change | For |
|---|---|---|
| **M1** | **Lists become cards on phones** (my applications, my documents, to-dos, the applications list): no sideways scrolling of wide tables | everyone, mostly candidates |
| **M2** | **Take a photo** on document uploads (the phone offers the camera), with a preview of the chosen file before uploading | new employees |
| **M3** | On phones, the **next-step button sticks to the bottom** of the screen | everyone |
| **M4** | A **360-pixel check** of every candidate and new-employee page (careers, apply, offer, letter, documents, personal record): large tap targets, nothing cut off | candidates |

## 6. What does not change

Every approval step, permission, document rule and email stays as it is. Nothing is removed from what a person *can* do: links move or merge, forms split into steps, rarely used actions move under *More*. Phase 27 stage B (the *My Onboarding* dashboard) and stage C (the RO's tracking dashboard, reminders) are still to come and fit into X2, M2 and X1.

## 7. Proposed stages (each a separate commit, tests after each)

| Stage | Items | Size |
|---|---|---|
| **U1: Quick wins** | W1 (wording and the two developer notes), N1, N3, N4, N2, N6, X1 | small-medium |
| **U2: Candidates and new employees** (together with Phase 27 stage B) | X2, W2, M1, M2, M3, M4 | medium-large |
| **U3: Staff record pages** | X3, X4, W3, W4, N7 | medium-large |
| **U4: Forms and lists** | W5, W6, W7, W8, N5 | medium |

## 8. Questions

1. **Which items or stages** do you want? (All of them is fine; U1 first is the suggestion.)
2. **N3:** call the merged Applications + ATS item **"Candidates"** or keep **"Applications"**?
3. **N6:** should HODs **stop seeing** *ATS screening* and *Hiring & offers* (they would still see their candidates under *Applications* and on the vacancy), or keep read-only access in the menu?
4. **W2 / W5:** OK to turn the personal record and the recruitment request form into **step-by-step forms**? (Same fields and rules, just one section per screen.)

## 9. Decisions (user's answers, 10 November 2026)

1. **All four stages** (U1, U2, U3, U4), U1 first; U2 together with Phase 27 stage B.
2. **N3:** the merged menu item is called **"Candidates"** (tabs: All, Screening, Shortlisting).
3. **N6:** **HODs stop seeing** *ATS screening* and *Hiring & offers* in the menu (access rules unchanged).
4. **W2 and W5 approved:** the personal record in **6 steps**, the recruitment request in **4 steps**.

## 10. Progress

### U1: quick wins (done, 10 November 2026)
* **N1:** staff's *Job openings*, *My applications*, *My probation* moved to the menu under their name; candidates keep a flat menu (Current vacancies, My applications, My profile).
* **N2:** one **Administration** link and page (`AdministrationController`), listing only the screens the person may open, in five sections.
* **N3:** **Candidates** = Applications + Screening, with tabs (`_CandidatesTabs`); "ATS screening" is now "Screening".
* **N4:** survey results live under **Reports** (one link; a holder of survey results only sees *Survey results*).
* **N5 (part):** a person with 7 links or fewer gets one flat list without group headings.
* **N6:** Heads of Department without a recruitment role no longer see *Screening* or *Hiring & offers* (`Helpers/MenuRules.cs`).
* **X1:** the home to-dos now also show automatic rejections to confirm, appointment letters to issue, documents to verify, employees ready to activate, inductions ready to close, the HOD's probation meetings and assessments (once open) and probation decisions.
* **W1:** the two developer notes are gone (the interview results page now says the RO chooses and approves); "ATS" replaced by "screening" / "automatic rejection" in what users read (the settings page is "Screening (ATS) settings").
* Tests: **192** (new: onboarding work on the to-dos; short menus per person).

### U2: candidates and new employees (done, 10 November 2026; with Phase 27 stage B)
* **X2:** the candidate home starts with **"Your next step"** (`MyNextStepService`, partial `_MyNextSteps`): answer an offer (with the reply-by date) → a correction HR asked for → sign the appointment letter → fill in the personal record → upload documents ("5 still needed", with the deadline) → an interview in the next 7 days → complete the profile. The first step is big with one button; the rest are listed under "Then".
* **Phase 27 stage B:** a **My onboarding** menu link (with the progress %) while an onboarding is in progress; the documents page is now **My onboarding**, with a welcome line and links to the letter, first-day information and personal record, next to the progress, counts and "Still to do".
* **W2:** the **personal record in 6 steps** (About you · Languages · Bank account · Family · Next of kin and emergency contacts · Beneficiaries), with "Step 2 of 6", a progress bar and Back / Next (`js/form-steps.js`). It is still **one form saved at the end**, so the rules (e.g. beneficiaries = 100%) are unchanged; without JavaScript, or after an error, every section shows at once. *Not done:* saving each step on its own (it would need partial validation, i.e. a rule change) and keeping an unsaved draft on the phone (personal data on shared phones).
* **M1:** *My applications* turns into small cards on a phone (`table-stack`).
* **M2:** document uploads say "Take a photo or choose a file", have large tap targets on phones and show a preview of the chosen photo (or the file name) before uploading.
* **M3:** on a phone the next-step button sticks to the bottom of the screen.
* **M4:** done through the CSS above; **not checked on a real phone** here.
* Tests: **193** (new: the candidate's next steps).

### U3: staff record pages (done, 10 November 2026)
* **X3:** a **"Your next step"** banner at the top of the request, vacancy, application and probation pages (`_NextStepBanner`): what *you* can do now, with a *Go* button to the form further down, or what the record is waiting for. (The hiring case, interview and onboarding pages already had their next-step panels.)
* **W3:** the **onboarding page in topics**: Documents · Checklist · Letter and first day · History and notes · Everything (`js/section-tabs.js`). The next-step panel always stays in view; the page opens on *Documents* before activation and *Checklist* after; your last choice is kept while you work; links into a hidden topic open it.
* **W4:** the **hiring case in topics** (Interview scores · Recommendation · Offer · Background checks), opening on the current stage; its next-step buttons open the right topic. The interview page keeps its next-step panel as is (it is already one form per stage).
* **N7 (part):** after verifying, rejecting or starting the review of a document, and after a candidate uploads one, the page comes back **to that document** instead of the top.
* *Not done:* **X4** (one primary button on every page) as a sweep; the banners and next-step panels give each page its main button, but some pages still have several primary buttons. Landing on the *next waiting item* after a decision (e.g. the next request to approve) is not done either.
* Tests: 193.
