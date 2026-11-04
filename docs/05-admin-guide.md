# 5. Administrator guide

For **HR Administrators**: how to use the Phase 1 screens. No technical knowledge needed.

All screens are in the left-hand menu. On a phone, tap the ☰ button at the top left to open the menu. The 🌙 / ☀️ button at the top right switches between light and dark mode.

---

## Signing in for the first time

1. Open GRSMS in your browser and enter the email and password IT gave you.
2. If you were given a **temporary password**, you'll be asked to choose your own straight away.
3. To change your password later: click your name (top right) → **Change password**.
4. You are signed out automatically after 20 minutes without activity. Any unsaved form is lost, so save regularly.

---

## Departments

**Menu → Organisation → Departments**

The list shows every department, its Head of Department (HOD), how many units it has, and whether it is active. Type in the **Search** box to filter, or click a column heading to sort.

### Add a department
1. Click **Add department**.
2. Enter the **name** (e.g. *Plantation Operations*) and a short **code** (e.g. *PLANT*). The code appears in reference numbers, so keep it short. Letters, numbers, `&` and `-` only.
3. Optionally choose the **Head of Department** (see *Users* below to give someone that role first).
4. Click **Save**.

### Edit a department
Click the ✏️ button, or open the department and click **Edit**.

### Deactivate or activate
Click 🚫 (deactivate) or ✔️ (activate).
* A deactivated department **stays in the system** with all its history, but can't be chosen for new recruitment requests.
* You can activate it again at any time.

### See a department's history
Open the department and click **View change history**. It shows every change: who made it, when, and the before and after values.

---

## Units

Units are the sections inside a department, for example **Finance → Payroll** or **IT → Infrastructure**.

**Add a unit:** open the department and click **Add unit** (the department is filled in for you), or use **Menu → Units → Add unit**.

* Two different departments may both have a unit called *Admin*, but one department cannot have two.
* You cannot add a unit to an inactive department, or activate a unit while its department is inactive.

**Menu → Units** lists units from all departments. Use the **Department** filter to see just one.

---

## Sites

Sites are physical work locations (Head Office, estates, depots). Each recruitment request will record where the new hire will work.

**Menu → Organisation → Sites → Add site:** enter the name, a short code and the location (town or address).

*Head Office* is already set up. Add GREL's other locations before Phase 2 starts.

---

## Users & roles

**Menu → Administration → Users & roles**

Two tabs:
* **GREL staff:** accounts you create and manage.
* **Candidates:** people who registered on the careers portal themselves. You can only deactivate or unlock these.

### Create a staff account
1. Click **Add staff account**.
2. Fill in name, email (this is what they sign in with), phone, staff number, department and site.
3. Tick their **roles**. Most people have *Employee* plus one more:

   | Person | Tick |
   |---|---|
   | HR Manager | HR Manager, Employee |
   | Managing Director | Managing Director, Employee |
   | Recruiter | Recruitment Officer, Employee |
   | Head of a department | Head of Department, Employee (and choose their department) |
   | Someone who sits on interview panels | add Interview Panel Member |

4. Type a **temporary password** (10+ characters with upper case, lower case, a number and a symbol). Give it to the person privately, never by email. They must change it the first time they sign in.
5. Click **Save**.

> If GREL has switched on **Sign in with Microsoft 365**, staff can simply click that button. An Employee account is then created for them automatically, and you only need to add their extra roles.

### Change someone's roles or details
Click ✏️, change what you need, and click **Save**. Role changes reach the person within 5 minutes.

### Reset someone's password
Click ✏️, type a new **temporary password**, and click **Save**. They'll choose their own at their next sign-in.

### Someone is locked out
After 5 wrong passwords an account is locked for 15 minutes and shows a red **Locked** label. Click 🔓 to unlock it straight away.

### Someone leaves GREL
Click the deactivate button. They can no longer sign in, and any open session ends within 5 minutes. Their history is kept. **Don't** delete people: their approvals and interview scores must stay on record.

### Things the system will stop you doing (on purpose)
* Deactivating yourself or removing your own HR Administrator role.
* Leaving the system with no active HR Administrator.
* Removing the HOD role from someone who is still a department's Head. Choose a new HOD for that department first.

---

## Audit trail

**Menu → Administration → Audit trail** (HR Administrators and HR Managers)

Every change and every sign-in is recorded here. Use the filters to narrow it down:

| To find... | Filter |
|---|---|
| Everything one person did | **User:** part of their name or email |
| Changes to departments | **Record type:** Department |
| Failed sign-in attempts | **Action:** SignInFailed (or LockedOut) |
| The full history of one record | **Record type** + **Record id** (or use *View change history* on the record) |
| Activity in a date range | **From** / **To** |

The **Before** and **After** columns show only the fields that changed. The audit trail can't be edited or deleted by anyone.
