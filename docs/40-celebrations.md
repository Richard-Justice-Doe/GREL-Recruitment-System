# 40. Celebrations: birthdays and work anniversaries

Added 17 November 2026. GRSMS celebrates birthdays and work anniversaries without anyone typing them in. Everything is worked out from data GRSMS already holds.

## Where the dates come from

| | From | Shown |
|---|---|---|
| **Birthday** | The date of birth in the employee's personal record (collected during onboarding) | Day and month only, never the year or age. 29 February is celebrated on 28 February in other years. |
| **Work anniversary** | The date joined, on the employee record | 1, 2, 3 … years of service. 1, 5, 10, 15 … years are highlighted as milestones. |

* Only current employees are celebrated.
* **Staff hired before GRSMS have no date of birth in GRSMS yet.** Their work anniversaries are celebrated; their birthdays will be once employee self-service (docs/30, Phase 29) collects the date.
* **Opting out:** an employee who doesn't want to be celebrated tells HR. HR clicks *Don't celebrate (at their request)* on the employee's profile, and *Celebrate again* to undo it.

## What staff see

* **Home:** a card with today's birthdays and anniversaries and the next few days, each with a photo or initials and the department.
* **Action Center:** *🎂 Birthdays today: 3 · 🎉 Work anniversaries today: 2 · View celebrations →*.
* **Menu → Celebrations:**
  * a banner with gentle confetti (none when the device asks for reduced motion);
  * a card for each person celebrated today (photo, name, position, department, site);
  * colleagues' messages, with one click for *Happy Birthday!*, *Best wishes!*, *Congratulations!* … or their own words, up to 280 characters, on the day and up to 3 days after;
  * the next 7 days.
* **Calendar:** the month at a glance, with filters by department, site / location and month, and the list underneath.
* **Employee profile:** *Birthday: 15 November · in 45 days* and *Next work anniversary: 1 Feb 2027 · 10 years*.

## Announcements (automatic)

Each morning from the configured hour (07:00 GMT by default), once a day:
* **Every member of staff** gets one notification listing the day's celebrations, e.g. *"Today we celebrate the birthday of John Doe (Information Technology)… Join us in saying: Happy Birthday!"*.
* **By email too,** if switched on. One email a day per person, listing all the day's celebrations.
* **The person celebrated** gets a personal greeting: *🎂 Happy birthday from all of us at GREL!* or *🎉 Happy 10th work anniversary!*. They aren't sent the announcement about themselves.
* **No photo in the email.** Photos need a sign-in to open, so emails don't include them; the celebrations page shows them.

## HR's settings

**Celebrations → Settings.** For holders of *Manage employees* (HR Manager, HR Administrator).

| Setting | Default |
|---|---|
| Celebrate birthdays | On |
| Celebrate work anniversaries | On |
| In-app notification each morning | On |
| Email announcement each morning | Off |
| Department only (announce and show only within the person's department; HR still sees everyone) | Off: company-wide |
| Colleagues can leave messages | On |
| Send from (hour, GMT) | 7 |

**Moderation:** HR sees *Hide* next to every message. A hidden message disappears for everyone else, and HR can *Show* it again. Who hid it is recorded.

## For developers

* `Application/Celebrations/CelebrationService.cs`: dates, visibility, the morning job (`RunMorningAsync`, claimed once per date), messages, opt-out.
* `Web/Controllers/CelebrationsController.cs`; views `Celebrations/Index`, `Calendar` and `Settings`; partials `_CelebrationsToday` (Home and Action Center), `_CelebrationsWidget` and `_CelebrantAvatar`.
* Photos for celebration cards are served by `Celebrations/Photo/{id}`: staff only, current and not opted out.
* The scheduled job runs from `Workers/ScheduledJobsWorker.cs`.
* Database: script `39_celebrations.sql` (`Employees.CelebrationsOptOut`, table `CelebrationMessages`).
