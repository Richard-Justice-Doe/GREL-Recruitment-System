# 19. Notifications, reminders and surveys: user guide

Phase 10 adds three things:

1. the **notification bell** for everyone;
2. **automatic reminders** before things slip;
3. short **feedback surveys** for applicants and new employees.

It also prepares **text messages (SMS)** to applicants. SMS is switched off until GREL has an SMS provider account (see [06 Configuration](06-configuration.md#text-messages-sms-phase-10)).

## 1. The notification bell

The bell at the top of every page shows how many notifications you have not read. Click it to see your notifications, newest first.

* **Every email GRSMS sends you is also a notification**, with the same title and link: requests to approve, offers, interview invitations, onboarding tasks, reminders, surveys, and so on.
* Clicking a notification marks it read and opens the page it is about.
* **Mark all as read** clears the count.
* Read notifications are removed after 6 months. The emails remain in your mailbox.

Candidates have the bell too: they see their offer, interview invitations, appointment letter and surveys there.

**Tasks** (things only you can do, such as an approval) are still listed in *My tasks* on the Home page. The bell tells you something happened; *My tasks* shows what is still waiting for you.

## 2. Automatic reminders

GRSMS checks every hour, from 07:00 GMT, and sends each reminder **once**, by email and the bell:

| When | Who is reminded | Also by SMS |
|---|---|---|
| An offer's reply-by date is **tomorrow** | the candidate | ✅ |
| An interview or assessment is **tomorrow** | the applicant and every panel member | ✅ (applicant) |
| A new employee **starts tomorrow** | the new employee and the person meeting them | ✅ (new employee) |
| Onboarding items are **due tomorrow or overdue** | each item's owner, in one daily summary | |
| A recruitment request, vacancy or hiring recommendation has **waited 3 days** at one stage | whoever must decide at that stage (not the person who raised it) | |

The waiting-approval reminder is repeated **every 7 days** while the item stays at the same stage. The 3 days can be changed (`Reminders:ApprovalWaitingDays`).

## 3. Text messages (SMS)

When switched on, applicants also get a short text message for:
* an interview invitation, reschedule or cancellation;
* an offer, and the reminder the day before it expires;
* their appointment letter, and the reminder the day before they start.

Messages go to the phone number on the application. Local numbers such as *024 123 4567* are sent as *233241234567*. Numbers that can't be used are skipped. Staff are not texted about their work (approvals, panels, onboarding items), only as applicants.

While SMS is off (the default), nothing is sent; each message is saved as a file in `App_Data/sms` so it can be checked.

## 4. Feedback surveys

### Who is asked, and when
| Survey | Sent to | When |
|---|---|---|
| **Candidate experience** | every applicant whose application ended *not successful*, *offer declined* or *hired* | the day after it ended |
| **New employee onboarding** | every new employee | the day after their onboarding is completed |

Applicants who withdrew are not asked. Each person is asked once per application, by email and the bell, and the survey also appears on their application page. It stays open for **30 days**.

### The questions
Each survey has four or five statements rated **1 (poor) to 5 (excellent)**, or *not applicable*, plus one comment box. Only the **overall** rating is required. It takes about two minutes.

| Candidate experience | New employee |
|---|---|
| The job advert and how to apply were clear | The appointment letter and first-day information were clear |
| We kept you informed about your application | You were expected and welcomed on your first day |
| The interview or assessment was fair and well organised | Your workspace, tools and system access were ready |
| The process took a reasonable time | The induction helped you understand GREL and your role |
| **Overall experience** | **Overall start at GREL** |
| *What could we do better?* | *What could we do better for the next new employee?* |

### Results (HR Manager, Recruitment Officers, Managing Director)
**Menu → Survey results** shows, for each survey:
* how many people were invited, how many answered, and the response rate;
* the average and the spread (how many 1s, 2s, ... 5s) for each statement;
* the comments, with only the month, department and outcome.

Filter by **division, department and period**, like every other report.

### Confidentiality
* Results **never show names**.
* Nothing is shown until **at least 3 people** matching the filters have answered, so a single answer can't be singled out.
* Answers are **not copied into the audit trail**, so they can't be read there either.
* Heads of Department do not see survey results: in a small department, even anonymous comments could identify someone.

## Who can do what

| | HR Manager | Recruitment Officer | MD | HOD / other staff | Candidate / new employee |
|---|:-:|:-:|:-:|:-:|:-:|
| Own notifications (the bell) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Answer own surveys | | | | ✅ (staff who applied internally) | ✅ |
| See survey results | ✅ | ✅ | ✅ | | |
