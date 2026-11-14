# 37. Change requests of 13 November 2026: impact analysis and plan

Five change requests:
1. remove salary;
2. a comment per scorecard criterion;
3. panel replacement;
4. document preview;
5. guided workflow and an Action Center (highest priority).

This document compares each with what GRSMS already does, lists the changes (database included), and asks the decisions in §7. Nothing is changed yet.

Rules kept throughout: nothing removed that the workflows need, database changes additive, data kept (hidden rather than deleted), Clean Architecture, tests before every commit.

---

## 1. CR1: remove salary from GRSMS

**Where salary is today** (about 45 files):

| Place | Field / screen | Used by |
|---|---|---|
| Recruitment request (QF-81-11) | *Estimated annual cost* (required) | Request form, details, HOD/HR/MD approval, requests report |
| Vacancy | Salary min / max, *show salary on adverts* | Vacancy form and approval ("wording, requirements and salary"), advert text, careers page, AI advert writer |
| Hiring recommendation (hire approval) | Proposed monthly salary (required; offer can't exceed it) | Hiring case, *Approve hire* form, Hiring list |
| Offer | Monthly salary | Offer form, **offer letter** and its PDF, offers report (salaries, totals) |
| Onboarding | Monthly salary (copied from the offer) | **Appointment letter** and its PDF, onboarding page |
| Grades | Min / max monthly salary | Grades screen, employee record |
| Personal record | "The account salary is paid into" (wording only) | Bank account section |
| Reports | Offers report salary columns; Requests report cost; Overview cost totals | Reports, CSV |
| AI | Advert writer mentions salary when shown; CV summary no | docs/36 |

**Approach: hide, don't delete.**
* The columns stay in the database with their values, so nothing is lost and history stays intact. Required salary fields become optional, defaulting to 0, so no validation asks for them.
* Forms, details pages, letters, reports, CSV, careers page, AI prompts and email templates no longer show or ask for salary. Report columns and totals about salary are removed.
* Rules that use salary go: "the offer cannot exceed the approved salary" and the vacancy approval wording.
* Workflows are otherwise unchanged: request → approvals → vacancy → advert → screening → interview → hire approval → offer → onboarding.
* One switch, `Recruitment:ManageSalaries` (off), keeps the code paths, so the decision could be reversed without a release. Recommended, since letters are legal documents.

**Open point (D1):**
* The offer and appointment letters are contracts. Without salary they would say e.g. *"Your remuneration will be communicated to you separately in line with GREL's conditions of service."*
* *Estimated annual cost* on the request is a **budget** figure, not a person's salary; finance may still want it.

Database: none (the switch is a setting).

## 2. CR2: a comment per scorecard criterion

Today each criterion gets a rating (1–5); the scorecard has overall *Strengths* and *Concerns*.

Change:
* `ScorecardRatings.Comment` (text, optional, up to 1,000).
* The scorecard form shows a comment box under each criterion; submitting without comments still works.
* Comments show:
  * on the session page (each panel member's scorecard),
  * on the hiring case (*Interview scores*),
  * in the interview results,
  * in the hiring recommendation's justification draft (when the RO proceeds),
  * in the AI interview summary input (docs/36 AI-5).
* **Scale:** the brief's example is "8/10". GRSMS uses 1–5 with weights. **Kept at 1–5**, since changing it would make validated scores of past interviews incomparable. Say if you want 1–10 (D4).

Database: one nullable column (script 36).

## 3. CR3: panel replacement

Mostly built on 12 November (docs/35 Wave 1.2):
* invitations ask to **accept or decline**;
* a decline, with its reason, **alerts the scheduler, the recruiter and the HR Managers at once**;
* the RO or HR Manager **replaces** the member: the new member is invited, clash-checked, and becomes chair or HOD delegate if needed.

Gaps against the brief, to add:
* Replace **any** member before the session (not only one who declined), e.g. someone who phones in ill, with a **reason** (required).
* **Add a substitute** (an extra member) without removing anyone.
* **Resend the invitation** to a member who hasn't replied.
* **Panel change history** (new table `InterviewPanelChanges`): original member, replacement, reason, who and when. Shown on the session page and kept in the audit trail.
* The candidate is not told (no change for them).

Database: one table (script 36).

## 4. CR4: document preview

Today every file is downloaded.

Plan: a **Preview** button next to each file, opening a full-screen modal.
* **PDF and scanned PDFs:** **PDF.js** (Mozilla, Apache-2.0), served from GRSMS itself (fits the content-security policy), with page navigation, zoom in and out, and fit to width.
* **JPEG and PNG:** an image viewer with zoom and rotate.
* **Word (.docx):** see D2.
  * Option (a) converts on the server to simple HTML (headings, paragraphs, lists, tables, embedded pictures) with the OpenXML library GRSMS already uses for CV text. Light, nothing to install; layout approximate.
  * Option (b) converts to PDF with LibreOffice in the Docker image. Exact layout, but about +400 MB and slower.
* **Security:**
  * the file is served `inline` through the same permission-checked endpoint as downloads, never from a public path;
  * opening a preview is recorded like a download (the documents portal already logs *Viewed*);
  * `Content-Security-Policy` stays `'self'`.
* **Where:** application review (CV, documents), candidate profile, interview or hiring case attachments, onboarding documents (RO verification), and personnel files (employee documents).

Database: none.

## 5. CR5: guided workflow (highest priority)

**What exists:**
* a *Next step* panel on requests, vacancies, applications, interviews, hiring cases and onboarding;
* steppers on requests and onboarding;
* *My tasks* and action cards on Home;
* the candidate's *My next step*;
* workflow targets (working-day SLA per approval step, docs/35);
* the badge counts in the menu.

**What's missing** is one consistent answer, on every record, to *where am I, what's next, who owns it, since when, is it late*, plus one place listing everything a person must do.

### 5.1 One process model

`Application/Process/ProcessGuide` builds, for any record, its **journey**. The stages:
1. Requisition submitted
2. Department (HOD) review
3. HR review
4. MD approval
5. Vacancy
6. Advert
7. Screening
8. Interview
9. Hire approval
10. Offer
11. Onboarding
12. Probation

Each stage is *done* (✔ with date), *current* (highlighted), *to come* or *skipped* (e.g. no advert for an internal-only vacancy). The stages are worked out from the existing statuses and history; no new workflow data.

For the current stage it gives:
* **Next action**, e.g. "Waiting for the HR Manager's approval";
* **Current owner**: the role and, where known, the person(s), e.g. *HR Manager: Ama Mensah, Kofi Boateng*;
* **Next approver**;
* **Pending for**: time since the stage started (working days and hours);
* **Status colour** against the workflow target for that step (docs/35 settings):
  * green: within 75 % of the target;
  * amber: 75–100 %;
  * red: overdue;
  * steps without a target show no colour;
* **Follow-up**: last action (what, who, when), next action, responsible person, **expected due date** (start plus target working days).

### 5.2 On every record page

A shared **process header** (partial `_ProcessGuide`) at the top of the request, vacancy, advert, application, interview, hiring case and onboarding pages:
* the horizontal **timeline** (stacks vertically on phones);
* a **status card** with the next action, owner, next approver, pending for (colour) and expected date;
* a **follow-up** line with the last action.

It replaces the present steppers and merges with the *Next step* panel, so there's one panel, not three.

### 5.3 Action Center

**Menu → Action Center** (also the top of Home): *my pending actions* across every module, one list.
* Actions include: approve requests, vacancies and adverts; review candidates (ATS confirmations, shortlisting); reply to panel invitations; score interviews; validate scores; approve hires and send offers; verify documents; onboarding and induction tasks; probation assessments; and for candidates, upload documents and sign letters.
* Each row shows: what, record, since, due date and **priority**:
  * **High:** overdue against its target, or due today;
  * **Medium:** approaching the target (amber) or due within 2 working days;
  * **Low:** the rest.
* Filters: priority, type. Count badge in the menu.
* Built from the queries behind *My tasks* and the menu counts (one service). Today's *My tasks* becomes a short view of the same list.

### 5.4 Usability pass

* Consistent page header (title, reference, status badge, primary action on the right).
* Breadcrumbs on every record page (they exist under Administration).
* One primary button per page.
* Mobile check of every record page.
* Slow-page budget: the existing query-count tests extended to the new header (it reuses the data the page already loads).

Database: none for CR5 (computed from the existing history and settings).

---

## 6. Plan (each step tested, documented, committed)

| Step | Content | Size |
|---|---|---|
| **C1** | CR2 scorecard comments + CR3 panel replacement (script 36) | S |
| **C2** | CR5.1–5.2 process guide on request, vacancy and application pages | L |
| **C3** | CR5.2 on interview, hiring case and onboarding; CR5.3 Action Center | L |
| **C4** | CR4 document preview (PDF.js, images, Word per D2) | M |
| **C5** | CR1 salary removal (per D1) | M |
| **C6** | CR5.4 usability pass, mobile check | M |

The order follows "CR5 is the highest priority" after a quick start with the two small ones. D3 can change it.

## 7. Decisions needed

* **D1. Salary scope.** (a) Remove salary everywhere, letters included (the letters refer to conditions of service), but keep the request's *estimated annual cost* as a budget figure. (b) Everywhere including the cost. (c) Everywhere except the offer and appointment letters.
* **D2. Word preview.** (a) Server-side HTML (light, approximate layout). (b) LibreOffice PDF (exact, +400 MB). (c) No preview for Word; download only.
* **D3. Order.** As §6, or CR5 first, or another order.
* **D4. Score scale.** Keep 1–5 (recommended) or move to 1–10.

## 8. Decisions (13 November 2026)

| # | Decision |
|---|---|
| D1 | **"Remove anything about money."** Removed: monthly salary everywhere (vacancy range and *show salary*, hire approval, offer, **offer and appointment letters**, onboarding), the request's **estimated annual cost** and **budget code**, the grades' **salary bands**, salary and cost columns in reports and CSV, and salary in AI prompts. **Kept:** the bank account in the personal record and the benefits wording in adverts. Data stays in the database, hidden; the letters say that remuneration follows GREL's conditions of service. |
| D2 | **Word preview: server-side HTML.** |
| D3 | **Order as §6.** |
| D4 | **Scores stay 1–5**, with an optional comment per criterion. |

## 9. Progress

| Step | Status |
|---|---|
| C1 scorecard comments + panel replacement | ✅ 13 Nov: comment per criterion (form, session page, hiring case, draft justification, AI summary); replace any member with a reason, add a substitute, resend an invitation, panel change history (script 36) |
| C2 process guide on request, vacancy and application pages | ✅ 13 Nov: `Application/Process/ProcessGuideService.cs`, `Views/Shared/_ProcessGuide.cshtml`; requisition and candidate journeys; next action, owner, next approver, pending for, green/amber/red against the workflow targets, expected date, last action; replaces the old steppers |
| C3 guide on interview, hiring case and onboarding pages; Action Center | ✅ 14 Nov: `Application/Process/ActionCenterService.cs`, menu → Action Center (staff: all pending actions with High/Medium/Low priority, due date and reason; candidates: their next steps); Home links to it |
| C4 document preview | ✅ 14 Nov: Preview button on application documents and CV, the request's attachments, the candidate profile, onboarding documents and personnel files; PDF.js 4.10.38 vendored in `wwwroot/lib/pdfjs` (pages, zoom, fit width), images (zoom, rotate), Word as server-side HTML (`Infrastructure/Documents/DocxHtmlPreview.cs`); same permission checks through `?view=inline` / `?view=html` |
| C5 remove everything about money | ✅ 14 Nov: no salary on vacancies, adverts, AI advert prompts, hire approval, offers, offer and appointment letters, candidate portal, onboarding and personnel file; no cost or budget code on requests; no pay band on grades; no salary or cost in reports and CSV exports. Old values stay in the database, hidden; no migration. Bank account kept |
| C6 | To do |
