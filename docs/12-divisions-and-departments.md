# 12. Divisions, departments and sites

GREL is organised in two levels: **divisions** at the top, with **departments** inside them. Every recruitment record in GRSMS is linked to a division, a department and the Head of Department (HOD) who asked for the hire, so reports can be cut either way.

## The structure

| Division | Departments |
|---|---|
| **Corporate** | Human Resources · Corporate Affairs · Information Technology · Procurement · Supply Chain · Finance · Transport · Administration · Internal Audit |
| **Operations** | Factory APM · Factory TBU · Stores · Garage · RuTAP · DTC · Nucleus Operations · Civil & Infrastructure |

This list comes from the GREL requirements. It is pre-loaded, and an HR Administrator can change it at any time (see [below](#managing-divisions-and-departments)).

Departments that were in earlier lists but are not in the requirements are kept as **inactive** rows: Estates, Progress and Quality, Project, and the original placeholders (HSE, Production, ...). Inactive departments cannot be chosen for new requests, but old records still show them and they can be reactivated with one click.

## Sites

Sites are where the new hire will work. Each site has a **type**. These are GREL's 12 sites, confirmed on 4 November 2026.

| Site | Type |
|---|---|
| Head Office (Takoradi) | Corporate Headquarters |
| Abura | Rubber Plantation Estate |
| Subri | Rubber Plantation Estate |
| Apeminim | Rubber Processing Factory |
| Tsibu | Rubber Processing Factory (the TBU factory, 1D1F Expansion Project) |
| RuTAP | Rubber Outgrower & Technical Assistance Programme Operations |
| Allabokazo, Assin Adubease, Manso Amenfi, Awodua, Nsuame, Agona Abream | *not known yet: fill in on the Sites screen* |

## What is linked to what

| Record | Division | Department | Requesting HOD |
|---|---|---|---|
| Recruitment request | stored on the request (copied from the department) | chosen by the HOD | the HOD who raises it |
| Vacancy | copied from the request | copied from the request | copied from the request |
| Dashboard KPIs | filter at the top of the Home page | filter at the top of the Home page | — |
| Applications, interview panels, hiring reports, onboarding | *later phases* (see [below](#what-later-phases-must-do)) | | |

**Why the division is copied, not looked up.** If a department is moved to the other division later, its old requests and vacancies keep the division they were raised under, so last year's report by division still gives last year's numbers. A request's division is refreshed when it is saved or submitted, because until then it is still a draft.

## Filtering

| Screen | Filters |
|---|---|
| **Recruitment requests** | Search · Division · Department · Period from / to · Status (on *All requests*) |
| **Vacancies** | Search · Division · Department · Period from / to · Status (on *All*) |
| **Home dashboard** | Division · Department (KPI cards, requests in approval, closing calendar) |

* **Period** means the recruitment period. For requests it is the date the request was submitted (or created, for drafts). For vacancies it is the date the vacancy was published (or created, if it has not been published). Both ends are included.
* The Department list is grouped under each division. On the request and vacancy lists it also shows inactive departments, marked *(inactive)*, so older records can still be found.
* Clicking a KPI card on a filtered dashboard opens the list with the same filter.
* On the dashboard, *My tasks* is not filtered (it always shows everything waiting for you), and *Registered candidates* counts all candidates, because candidates are only linked to a department once they apply (Phase 5).
* Filtering never widens what you may see: a HOD still sees only their own departments.

## Managing divisions and departments

HR Administrators only, under **Organisation** in the sidebar.

* **Divisions:** add, rename or deactivate. A division cannot be deactivated while it still has active departments; move or deactivate them first.
* **Departments:** every department must have a division. To move a department, edit it and pick the other division. Existing requests and vacancies keep the division they were raised under.
* **Sites:** add, edit or deactivate, including the site type.

## What later phases must do

These rules come from the same GREL requirements. They are recorded here so each phase builds them in from the start:

1. **Links.** Every candidate application (Phase 5), interview panel and scorecard (Phase 7), hiring report and offer (Phase 8) and onboarding record (Phase 9) stores the division, department and requesting HOD, copied from its vacancy.
2. **Reports** (Phase 11) filter by division, department, vacancy, recruitment status and recruitment period.
3. **HOD participation.** For positions in their department, the HOD:
   * takes part in **candidate shortlisting** (Phase 6);
   * sits on the **interview evaluation** and reviews the **interview scorecards** (Phase 7);
   * gives the **final hiring recommendation** before the offer (Phase 8).

## Open points to confirm with GREL

* **Site types** for Allabokazo, Assin Adubease, Manso Amenfi, Awodua, Nsuame and Agona Abream.
* **Codes** for the divisions (CORP, OPS), Nucleus Operations (NUC) and the sites were chosen here. Department codes appear in request numbers (`RR-APM-2026-0001`).
* **Estates, Progress and Quality and Project** were active before this change (from the earlier department photo) and are now inactive because the requirements list leaves them out. Reactivate any that GREL still uses, and pick its division.

## Confirmed by GREL (4 November 2026)

* The site list is exactly GREL's 12 sites. "TBU Factory" and "Nucleus Operations Areas" from the requirements table are not separate sites, so they are kept but inactive.
* Tsibu **is** the TBU factory (Rubber Processing Factory, 1D1F Expansion Project).
* The APM factory site is spelt **Apeminim**.
