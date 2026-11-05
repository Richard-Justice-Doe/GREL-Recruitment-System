# 5. Administrator guide

For **Super Admins and HR Administrators**: how to use the administration screens. No technical knowledge needed.

All screens are in the left-hand menu. On a phone, tap the ☰ button at the top left to open the menu. The 🌙 / ☀️ button at the top right switches between light and dark mode.

---

## Signing in for the first time

1. Open GRSMS in your browser and enter the email and password IT gave you.
2. If you were given a **temporary password**, you'll be asked to choose your own straight away.
3. To change your password later: click your name (top right) → **Change password**.
4. You are signed out automatically after 20 minutes without activity. Any unsaved form is lost, so save regularly.

---

## Divisions

**Menu → Organisation → Divisions**

GREL has two divisions, **Corporate** and **Operations**, and every department belongs to one of them. You can add, rename or deactivate a division. A division can only be deactivated once none of its departments are active. To move a department to the other division, edit the department (below). See [12 Divisions, departments and sites](12-divisions-and-departments.md).

## Departments

**Menu → Organisation → Departments**

The list shows every department, its division, its Head of Department (HOD), how many units it has, and whether it is active. Type in the **Search** box to filter, or click a column heading to sort.

### Add a department
1. Click **Add department**.
2. Enter the **name** (e.g. *Plantation Operations*) and a short **code** (e.g. *PLANT*). The code appears in reference numbers, so keep it short. Letters, numbers, `&` and `-` only.
3. Choose the **division** (Corporate or Operations). This is required.
4. Optionally choose the **Head of Department** (see *Users* below to give someone that role first).
5. Click **Save**.

### Edit a department
Click the ✏️ button, or open the department and click **Edit**. Changing the division moves the department; its existing requests and vacancies keep the division they were raised under.

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

**Menu → Organisation → Sites → Add site:** enter the name, a short code, the site type (e.g. Rubber Plantation Estate) and the location (town or address).

GREL's 12 sites are already set up. Six of them have no site type yet (Allabokazo, Assin Adubease, Manso Amenfi, Awodua, Nsuame, Agona Abream): edit them to add it.

---

## Users & roles

**Menu → Administration → Users & roles**

Two tabs:
* **GREL staff:** accounts you create and manage.
* **Candidates:** people who registered on the careers portal themselves. You can only deactivate or unlock these.

### Create a staff account
1. Click **Add staff account**.
2. Fill in name, email (this is what they sign in with), phone, staff number, department and site.
3. Tick their **roles**. Most people have *Employee* plus one more. Since Phase 14, **only a Super Admin** can tick the locked (🔒) roles: everything except Employee and Interview Panel Member. An HR Administrator creates the everyday accounts; a Super Admin creates the HR Managers, the MD, Recruitment Officers, HODs and other administrators:

   | Person | Tick |
   |---|---|
   | Another Super Admin | Super Admin, Employee |
   | A system administrator (manages all accounts except Admins and Super Admins; sees settings read-only) | Admin, Employee |
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
* Deactivating yourself or removing your own Super Admin role.
* Leaving the system with no active Super Admin.
* Removing the HOD role from someone who is still a department's Head. Choose a new HOD for that department first.
* As an HR Administrator: giving or removing a privileged role, or editing, resetting the password of, or deactivating an account that holds one. These accounts show a 🔒 **Admin** label instead of the buttons. You can still unlock them.
* As an **Admin**: giving or removing a privileged role (only a Super Admin does that), or touching another Admin's or a Super Admin's account (🔒 **Super Admin** label). You *can* edit, reset and deactivate HR Managers, Recruitment Officers, HODs, HR Administrators and the MD; their roles stay as they are.

---

## Roles & permissions (Super Admin only)

**Menu → Management → Roles & permissions**

### Change what a role may open
1. Tick or untick boxes in the grid: permissions down the side, roles across the top. Each permission says what it opens.
2. Click **Save permissions**. The change applies straight away to everyone holding that role, without signing out, and is recorded in the audit trail.

The **Super Admin** column is always ticked and cannot be changed. The two **Super Admin only** permissions cannot be given to anyone else. Candidates never get staff permissions.

> A permission opens a screen. It does not change who approves what: the HR Manager still approves at HR stage, the MD at final stage, and so on. See [04 Security → Rules that are not permissions](04-security-and-roles.md#rules-that-are-not-permissions-on-purpose).

### Add a custom role
1. Click **Add custom role**, give it a name (e.g. *Recruitment Auditor*) and say what it is for.
2. Tick its permissions in the grid and click **Save permissions**.
3. On **Users & roles**, give it to people (with **Employee** too).

A custom role can be renamed (✏️) and deleted (🗑) once nobody holds it. Built-in roles can't be renamed or deleted.

---

## Audit trail

**Menu → Administration → Audit trail** (Super Admin, Admin, HR Administrators and HR Managers). Everyone, candidates included, also has **Menu → My audit trail**: what they did, and what others did to their account (see [04 Security → My audit trail](04-security-and-roles.md#my-audit-trail-everyone)).

Every change and every sign-in is recorded here. Use the filters to narrow it down:

| To find... | Filter |
|---|---|
| Everything one person did | **User:** part of their name or email |
| Changes to departments | **Record type:** Department |
| Failed sign-in attempts | **Action:** SignInFailed (or LockedOut) |
| The full history of one record | **Record type** + **Record id** (or use *View change history* on the record) |
| Activity in a date range | **From** / **To** |

The **Before** and **After** columns show only the fields that changed. The audit trail can't be edited or deleted by anyone.
