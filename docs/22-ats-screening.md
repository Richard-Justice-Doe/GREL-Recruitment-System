# 22. ATS screening: user guide

Phase 15 adds an **applicant tracking system (ATS) screening engine**. It reads every applicant's CV and scores the application against the vacancy. It can reject an application automatically when a mandatory requirement is not met. **Every automatic rejection waits for HR**: nobody is turned down until a Recruitment Officer or HR Manager confirms it.

This guide is for Recruitment Officers, HR Managers, Heads of Department and the Super Admin.

---

## 1. What happens when someone applies

1. **The applicant answers the knockout questions** on the apply page (Yes / No), if the vacancy has any.
2. **The ATS reads the CV.** GRSMS turns the PDF or Word file into text on GREL's own server and picks out the details:

   | Detail | How it is found |
   |---|---|
   | Name | a "Name:" line, otherwise the first short line made only of words |
   | Email, phone | the first email address and phone number (Ghana or international format) |
   | Education | the highest qualification word in the *Education* section (PhD, MSc/MBA, BSc/BA/B.Ed, HND, Diploma, Certificate, WASSCE, BECE) |
   | Employment history and years of experience | every date range in the *Work experience* section ("Jan 2019 – Present", "2015 - 2018", "03/2017 to 12/2020"); overlapping jobs counted once |
   | Skills, certifications | the items in those sections |

   A **scanned CV** (a picture of paper) has no text. The application page then says so, and screening uses the profile only.
3. **The ATS scores the application** (section 2). It compares the vacancy with the applicant's profile **and** their CV. The CV can add matches but never takes any away.
4. **The ATS applies the automatic-rejection rules** (section 3). If one applies, the status becomes **Automatically rejected** and the reason is recorded.
5. **The recruiter's "new application" email** shows the match and, if it was rejected, why.

The applicant receives the usual *We received your application* email in every case.

## 2. The scores

Each part is a percentage:

| Part | 100 % when... | Otherwise |
|---|---|---|
| **Education** | the highest level (profile or CV, whichever is higher) is at or above the vacancy's minimum | one level below 60 %, two below 30 %, lower 0 %; **minus 20 points** when the vacancy names fields of study and none is found |
| **Experience** | the years in the profile are at or above the minimum | years ÷ minimum, e.g. 2 of 4 years = 50 % |
| **Skills** | every listed skill is found | required skills count **double**, preferred once: (2 × required found + preferred found) ÷ (2 × required + preferred) |
| **Certifications** | every listed certification is found | the same, for certifications |

A part the vacancy leaves empty gets 100 %. A requirement is **found** when all its words appear in the profile or the CV, ignoring case and punctuation. For example, *NEBOSH IGC* is found in *NEBOSH International General Certificate (IGC)*.

**Overall match** = the weighted average of the four parts. The weights are 25 % each until the Super Admin changes them. Example: Education 90 %, Experience 100 %, Skills 75 %, Certifications 80 % gives **86 %**.

**Classification** (default bands, set by the Super Admin):

| Overall | Band |
|---|---|
| 90–100 % | ⭐ Highly recommended |
| 80–89 % | 👍 Recommended |
| 70–79 % | 🔍 Review required |
| 50–69 % | ⬇ Low match |
| below 50 % | ⛔ Auto reject |

## 3. Automatic rejection, held for HR

The Super Admin chooses which rules reject automatically. The defaults are:

| Rule | Default |
|---|---|
| A mandatory knockout question answered **No** | on |
| Education below the vacancy's minimum | on |
| Experience below the vacancy's minimum | on |
| A **required certification** not found | on |
| A **required skill** not found | **off** (people describe the same skill in many words) |
| Overall match in the **Auto reject** band | on |

When a rule applies:
* the status becomes **Automatically rejected**, and every reason is stored on the application and in its history (by *ATS (automatic screening)*);
* **the applicant is not told**: their tracker shows *Under review*, and the ATS step does not appear in their history;
* the application appears under **ATS screening** (with a red count in the menu), on the vacancy's ATS dashboard, and on the **Applications → ATS rejected (awaiting HR)** tab.

A **Recruitment Officer or HR Manager** then decides:
* **Confirm rejection.** The application becomes *Not successful* and the applicant gets the usual polite email, which never gives the reason. You can add an internal note. **Confirm all** does this for every held rejection on a vacancy.
* **Override.** Write why it should be reviewed. The application moves to *Under review* and continues normally (recommend, HOD's view, shortlist).

The ATS **never stops anyone opening, reading or downloading** an application. Heads of Department see the held rejections of their departments but cannot decide them. Nobody decides on their own application.

## 4. Knockout questions

On the vacancy form (while it is a draft), type up to 10 questions, one per line, worded so that **Yes is the answer you need**:

```
Do you have a Bachelor's degree?
Do you have at least 3 years' experience?
Are you willing to relocate to the site?
Do you have a valid driver's licence?
```

Applicants must answer every question. Each application keeps a copy of the exact wording it was asked, so editing a vacancy later never changes old answers.

## 5. The ATS dashboard

**Menu → ATS screening** lists every vacancy with applications: the count in each band, the top match, and the rejections awaiting HR (vacancies with held rejections come first). Click a vacancy for its dashboard:

| Section | Shows |
|---|---|
| **Band tiles** | how many applicants are in each band, and how many rejections await HR |
| **Top ranked candidates** | the five best matches still in the running |
| **Rejected by the ATS: awaiting HR** | each held rejection with its reasons; **Confirm**, **Override** (with a reason), **Confirm all** |
| **Candidate ranking and score breakdown** | everyone, highest match first: overall %, band, the four sub-scores, missing required items, status, and which reader read the CV |
| **Missing skill analysis** | for each listed skill and certification, how many applicants have it and how many don't. A long grey part shows a requirement few applicants have, which can mean the requirement is too strict or the advert is not reaching the right people. |
| **Knockout questions, rules in force** | what the vacancy asks and which rejection rules apply |

**Re-run screening** scores every application still at the screening stage again, with the current settings. It is useful after the Super Admin changes a rule. A held rejection that no longer breaks any rule goes back to *Received* (shown as *cleared on re-screening*). An application already overridden or under review is re-scored but never rejected again.

On each **application page**, the ATS card shows the full breakdown: what was compared, what was found and missing, and the knockout answers. Another card shows **what was read from the CV** (contact details, education, employment history, skills, certifications, plus any warning). CVs have no fixed layout, so check anything important against the file itself.

## 6. ATS settings (Super Admin only)

**Menu → Management → ATS settings**:
* **Weights** of the four parts (they must add up to 100);
* **Bands** (each one lower than the one above);
* **Automatic-rejection rules** (switch each on or off);
* **CV reading**: the optional **Claude AI reader**.

Changes apply to applications screened from then on. Use **Re-run screening** on a vacancy to apply them to its existing applications. Every change is in the audit trail (record type *AtsSettings*), with who changed what and when.

### The optional Claude AI reader

GRSMS reads CVs with its own rules on GREL's server, so by default **no CV leaves GREL**. The Claude AI reader (Anthropic's Claude Opus 5.5) copes better with unusual layouts, but when it is on, **the text of each CV is sent to Anthropic**. It only reads the CV: the scores and rules are exactly the same with either reader. If Claude cannot be reached or gives an unexpected answer, GRSMS uses its own reader for that CV, so applying never fails because of it.

To use it:
1. GREL approves sending CV text to Anthropic under its data-protection rules (Ghana's Data Protection Act).
2. IT sets `Ats:Ai:ApiKey` on the server (see [06 Configuration](06-configuration.md#ats-cv-screening-phase-15)).
3. The Super Admin switches it on in ATS settings. Until the key is set, the switch is locked.

## Who can do what

| Action | Super Admin | Recruitment Officer | HR Manager | HOD | MD / HR Admin |
|---|:-:|:-:|:-:|:-:|:-:|
| See the ATS overview, dashboards and breakdowns | ✅ | ✅ | ✅ | own departments | ✅ |
| Re-run screening; confirm or override a held rejection | with the role | ✅ not their own | ✅ not their own | | |
| Change weights, bands, rules; switch the AI reader | ✅ only | | | | |
| Add knockout questions | (vacancy form, Recruitment Officer) | ✅ | | | |
