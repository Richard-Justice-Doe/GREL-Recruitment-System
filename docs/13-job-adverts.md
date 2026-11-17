# 13. Job adverts: user guide

Once a vacancy is **published** (by the Recruitment Officer), the Recruitment Officer writes its advert and **publishes it**. Since 17 November 2026 there is **no HR review and no IT approval**: the Managing Director's approval of the request is the last approval, and the Recruitment Officer owns the process from there. (The HR review and IT approval of 11–17 November 2026, docs/29 change 2, are removed.) GRSMS publishes adverts on its own **internal portal** and **careers page**, and prepares ready-to-paste text for **LinkedIn, Indeed, Jobberman and newspapers**, which the Recruitment Officer (or the IT Manager) posts.

**Menu → Vacancies & adverts → Job adverts tab.** Vacancies and job adverts share one menu item; the tabs at the top of the page switch between them. The IT Manager sees **Job adverts** only.

**Who can do what**

| | Recruitment Officer | HR Manager | IT Manager | Head of Department | MD, HR Administrator |
|---|:-:|:-:|:-:|:-:|:-:|
| See adverts | all | all | all | own departments | all |
| Create, edit, publish or schedule, cancel a schedule, clone | ✅ | ✅ | publish only | | |
| Close a published advert early | ✅ | ✅ | ✅ | | |
| Record external channels as posted / taken down | ✅ | ✅ | ✅ | | |
| Take a published advert offline | | | ✅ | | |
| Return or reject an advert left in the earlier review | | ✅ (HR step) | ✅ (IT step) | | |

The permissions are in Roles & permissions, group *Job adverts*: `adverts.publish` (record postings and publish, for roles that do not write adverts), `adverts.unpublish` (take offline), and, only for adverts sent for review before 17 November 2026, `adverts.review.hr`, `adverts.review.it` and `adverts.return`. The Super Admin gives the **IT Manager** role on the user's account.

## The channels

| Channel | How it is published | Who sees it |
|---|---|---|
| **Internal portal** | Automatically, while the advert is published | Signed-in GREL staff, on the careers page (marked *GREL staff only*) |
| **Company website** | Automatically, while the advert is published | **Anyone**, on the public careers page: `https://<grsms address>/Careers`. Link GREL's website to this address. |
| **LinkedIn, Indeed, Jobberman** | By hand: copy the ready-made text into the site, then record the link | Visitors to those sites |
| **Newspaper** | By hand: send the ready-made text to the paper, then record the edition | Readers |

GRSMS is not connected to LinkedIn, Indeed, Jobberman or the newspapers, and stores no passwords for them.

A vacancy marked **Internal (GREL staff only)** can only be advertised on the internal portal.

## Life of an advert

```
Draft ──Publish now (RO)──► Published
Draft ──Schedule (RO, later start date)──► Scheduled ──start date reached (automatic)──► Published
Published / Scheduled ──edited by the RO──► stays published, with the new text (no review)
Published / Scheduled ──Take offline (IT Manager)──► Returned for correction ──correct, then Publish──► Published
Published / Scheduled ──end date passes / vacancy closes / Close──► Closed
Scheduled ──Cancel schedule──► Draft
Any advert ──Clone──► a new Draft
```

*Published* is the old *Live*. Adverts sent for review before 17 November 2026 (**Awaiting HR review** or **Awaiting IT approval**) are listed under **Earlier review** while any remain. The Recruitment Officer can publish them straight away. Their reviewer can still **return** or **reject** them.

## Step by step

### 1. Create the advert (Recruitment Officer)
1. Open the vacancy (**Vacancies → Open**) and click **Create advert** in the *Job adverts* box. The button only appears while the vacancy is published and open.
2. The form is filled in from the vacancy: title, advert text (description, requirements, location; no salary: docs/37 CR1) and *How to apply*. Edit anything you like.
   * **With AI** (if switched on in *AI settings & usage*): **Write the advert with AI** writes four versions from the vacancy: the careers text, a job-board and newspaper version, an internal notice for staff, and a LinkedIn post. Add the benefits to mention if you like. **Use these versions** puts them in the form to edit. The requirements are kept exactly as approved.
   * **Versions for other channels (optional)**: the job-board, LinkedIn and internal-notice texts. Empty = the advert text is used. They become the copy-ready text for Indeed, Jobberman, newspapers and LinkedIn; the internal notice is shown copy-ready on the advert page.
3. Choose the **start date** (today = live as soon as you publish it; a later date = scheduled) and **end date** (at the latest the vacancy's closing date).
4. Tick the **channels**. The internal portal and company website are ticked to start with.
5. **Save draft**. The advert gets its number, e.g. **ADV-2026-0001**. The draft is on your Action Center as *Finish and publish the advert*.

### 2. Publish (Recruitment Officer)
On the advert page, click **Publish now** (start date today) or **Schedule the advert** (later start date). **No approval is needed.** A live advert shows on the careers page and internal portal at once. The vacancy's recruiter and the Head of Department are emailed, with the external channels still to post. The history records **Published** (or **Scheduled**).

You cannot publish if the vacancy is no longer published (e.g. it was closed), its closing date has passed, or the end date has passed. The page tells you why.

### 3. Post on the external channels (Recruitment Officer or IT Manager)
For each external channel the advert page shows **Copy-ready text**:
1. Click **Copy** and paste it into LinkedIn, Indeed or Jobberman, or send it to the newspaper. The text ends with the link to the careers page and the closing date.
2. Once it is up, paste the posting's link (or the newspaper name and date of the edition) and click **Record as posted**.

The *Job adverts* list shows how many channels are still **to post**, and a reminder sits above the list.

### 4. Changing a published advert
* **Edit** a published or scheduled advert (Recruitment Officer): it **stays published** with the new text; the history records *Edited while published*. Changing the start date to a later day schedules it again; bringing a scheduled advert's start date to today publishes it.
* **Take offline** (IT Manager), with a reason: it comes off the careers page and goes back to the Recruitment Officer as *Returned for correction*; they correct it and publish it again.
* External postings are **not** changed or removed automatically: update or take them down on each site, then click **Record as taken down** if it was removed.

### 5. Closing
* An advert closes **by itself** the day after its end date, or as soon as its vacancy closes (the check runs every hour). It then disappears from the careers page.
* The Recruitment Officer or the IT Manager can **Close now** at any time.
* External postings are **not** removed automatically. Take the advert down from each site, then click **Record as taken down**.

### 6. Clone
**Clone into a new draft** copies the text and channels into a new advert for the same vacancy, with new dates (today until the vacancy's closing date). Use it to re-advertise, for example after the vacancy is reopened, after a rejection, or to fix a mistake in a closed advert. Publish the copy like any draft.

## The careers page

* **Public** (no sign-in): `/Careers` lists every published advert on the company-website channel, with department, division, location, employment type and closing date. Each job has its own page, `/Careers/Details/ADV-2026-0001`, which is the link used in the copy-ready texts.
* **Staff:** signed-in staff also see internal-portal adverts, marked *GREL staff only*. The sign-in page and the candidate home page link to the careers page.
* **Applying online** opens in Phase 5 (candidate portal). Until then the page shows the advert's *How to apply* text and invites visitors to create a candidate account.

## Reports

The *Job adverts* list has the tabs **Published, Scheduled, Earlier review** (only while adverts sent for the HR / IT review before 17 Nov 2026 remain), **Drafts** (with returned ones), **Closed** (with rejected ones) and **All**. It filters by **division, department, channel, period** (adverts running in that period) and **status**, and searches advert numbers, vacancy numbers and titles. Each advert keeps the division, department and requesting HOD of its vacancy.
