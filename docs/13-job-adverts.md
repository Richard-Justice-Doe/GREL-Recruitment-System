# 13. Job adverts: user guide

Once a vacancy is **published** (the HR Manager has approved it), the Recruitment Officer writes its advert. Since 11 November 2026 every advert is **reviewed by the HR Manager** and **approved by the IT Manager** before it is published (docs/29, change 2). GRSMS publishes adverts on its own **internal portal** and **careers page**, and prepares ready-to-paste text for **LinkedIn, Indeed, Jobberman and newspapers**, which the IT Manager posts.

**Menu → Vacancies & adverts → Job adverts tab.** Vacancies and job adverts share one menu item; the tabs at the top of the page switch between them. The IT Manager sees **Job adverts** only.

**Who can do what**

| | Recruitment Officer | HR Manager | IT Manager | Head of Department | MD, HR Administrator |
|---|:-:|:-:|:-:|:-:|:-:|
| See adverts | all | all | all | own departments | all |
| Create, edit, send for review, cancel a schedule, clone | ✅ | ✅ | | | |
| HR review: approve for IT, return, reject | | ✅ | | | |
| IT approval (publishes it), return, reject | | | ✅ | | |
| Take a published advert offline | | | ✅ | | |
| Close a published advert early | ✅ | ✅ | ✅ | | |
| Record external channels as posted / taken down | | | ✅ | | |

The review steps are permissions (Roles & permissions, group *Job adverts*): `adverts.review.hr` (HR Manager), `adverts.review.it`, `adverts.publish`, `adverts.unpublish` (IT Manager) and `adverts.return` (both). The Super Admin gives the **IT Manager** role on the user's account.

## The channels

| Channel | How it is published | Who sees it |
|---|---|---|
| **Internal portal** | Automatically, while the advert is published | Signed-in GREL staff, on the careers page (marked *GREL staff only*) |
| **Company website** | Automatically, while the advert is published | **Anyone**, on the public careers page: `https://<grsms address>/Careers`. Link GREL's website to this address. |
| **LinkedIn, Indeed, Jobberman** | By the IT Manager: copy the ready-made text into the site, then record the link | Visitors to those sites |
| **Newspaper** | By the IT Manager: send the ready-made text to the paper, then record the edition | Readers |

GRSMS is not connected to LinkedIn, Indeed, Jobberman or the newspapers, and stores no passwords for them.

A vacancy marked **Internal (GREL staff only)** can only be advertised on the internal portal.

## Life of an advert

```
Draft ──Send for review (RO)──► Awaiting HR review ──Approve (HR Manager)──► Awaiting IT approval ──Approve (IT Manager)──► Published
                                  ├──Return──► Returned for correction        ├──Return──► Returned for correction
                                  └──Reject──► Rejected (final)               └──Reject──► Rejected (final)
Approve (IT Manager) with a later start date ──► Scheduled ──start date reached (automatic)──► Published
Returned for correction ──correct, then Send for review──► Awaiting HR review
Published / Scheduled ──edited by the RO──► offline, Awaiting HR review again
Published / Scheduled ──Take offline (IT Manager)──► Returned for correction
Published / Scheduled ──end date passes / vacancy closes / Close──► Closed
Scheduled ──Cancel schedule──► Draft
Any advert ──Clone──► a new Draft
```

*Published* is the old *Live*: adverts that were live or scheduled on 11 November 2026 stayed published; drafts go through the review.

## Step by step

### 1. Create the advert (Recruitment Officer)
1. Open the vacancy (**Vacancies → Open**) and click **Create advert** in the *Job adverts* box. The button only appears while the vacancy is published and open.
2. The form is filled in from the vacancy: title, advert text (description, requirements, location; no salary: docs/37 CR1) and *How to apply*. Edit anything you like.
   * **With AI** (if switched on in *AI settings & usage*): **Write the advert with AI** writes four versions from the vacancy: the careers text, a job-board and newspaper version, an internal notice for staff, and a LinkedIn post. Add the benefits to mention if you like. **Use these versions** puts them in the form to edit. The requirements are kept exactly as approved.
   * **Versions for other channels (optional)**: the job-board, LinkedIn and internal-notice texts. Empty = the advert text is used. They become the copy-ready text for Indeed, Jobberman, newspapers and LinkedIn; the internal notice is shown copy-ready on the advert page.
3. Choose the **start date** (today = live as soon as the IT Manager approves it; a later date = scheduled) and **end date** (at the latest the vacancy's closing date).
4. Tick the **channels**. The internal portal and company website are ticked to start with.
5. **Save draft**. The advert gets its number, e.g. **ADV-2026-0001**.

### 2. Send for review (Recruitment Officer)
On the advert page, click **Send for review**. The HR Managers are emailed and see it in *Your to-dos*. While it is under review you cannot edit it; the reviewer returns it if it needs changes.

You cannot send it if the vacancy is no longer published (e.g. it was closed), its closing date has passed, or the end date has passed. The page tells you why.

### 3. HR review (HR Manager)
Read the advert, then:
* **Approve and send to IT** (an optional comment goes into the history). The IT Managers are emailed.
* **Return for correction**, saying what to change. The Recruitment Officer is emailed and sees *What to correct* on the advert; they make the changes and **Send for review** again.
* **Reject** (final), with the reason. The advert is kept; it can be cloned into a new draft.

### 4. IT approval (IT Manager)
* **Approve and publish now** (start date today) or **Approve and schedule** (later start date). The HR reviewer is emailed; for a live advert the vacancy's recruiter and the Head of Department are emailed too, with the external channels still to post.
* **Return for correction** or **Reject**, with a reason: the Recruitment Officer **and** the HR reviewer are emailed.

The IT Manager's home page shows four cards: **awaiting your approval, scheduled, active (published), expired or closed**.

### 5. Post on the external channels (IT Manager)
For each external channel the advert page shows **Copy-ready text**:
1. Click **Copy** and paste it into LinkedIn, Indeed or Jobberman, or send it to the newspaper. The text ends with the link to the careers page and the closing date.
2. Once it is up, paste the posting's link (or the newspaper name and date of the edition) and click **Record as posted**.

The *Job adverts* list shows how many channels are still **to post**, and a reminder sits above the list.

### 6. Changing a published advert
* **Edit** a published or scheduled advert (Recruitment Officer): saving takes it **offline** at once (off the careers page and internal portal) and sends it back to the HR Manager, then the IT Manager. The form warns you first.
* **Take offline** (IT Manager), with a reason: it comes off the careers page and goes back to the Recruitment Officer as *Returned for correction*.
* External postings are **not** removed automatically: the IT Manager takes the advert down from each site and clicks **Record as taken down**.

### 7. Closing
* An advert closes **by itself** the day after its end date, or as soon as its vacancy closes (the check runs every hour). It then disappears from the careers page.
* The Recruitment Officer or the IT Manager can **Close now** at any time.
* External postings are **not** removed automatically. The IT Manager takes the advert down from each site, then clicks **Record as taken down**.

### 8. Clone
**Clone into a new draft** copies the text and channels into a new advert for the same vacancy, with new dates (today until the vacancy's closing date). Use it to re-advertise, for example after the vacancy is reopened, after a rejection, or to fix a mistake in a closed advert. The copy goes through the review like any draft.

## The careers page

* **Public** (no sign-in): `/Careers` lists every published advert on the company-website channel, with department, division, location, employment type and closing date. Each job has its own page, `/Careers/Details/ADV-2026-0001`, which is the link used in the copy-ready texts.
* **Staff:** signed-in staff also see internal-portal adverts, marked *GREL staff only*. The sign-in page and the candidate home page link to the careers page.
* **Applying online** opens in Phase 5 (candidate portal). Until then the page shows the advert's *How to apply* text and invites visitors to create a candidate account.

## Reports

The *Job adverts* list has the tabs **Published, Scheduled, In review** (HR or IT), **Drafts** (with returned ones), **Closed** (with rejected ones) and **All**. It filters by **division, department, channel, period** (adverts running in that period) and **status**, and searches advert numbers, vacancy numbers and titles. Each advert keeps the division, department and requesting HOD of its vacancy.
