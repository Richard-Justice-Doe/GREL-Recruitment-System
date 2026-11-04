# 1. Getting started

This guide takes you from a fresh computer to a running GRSMS in about 15 minutes.

## What you need

| Software | Version | Where to get it |
|---|---|---|
| .NET SDK | 10.0 or later | https://dotnet.microsoft.com/download |
| MySQL Server | 8.0 or later (8.4 LTS recommended) | https://dev.mysql.com/downloads/mysql/ |
| A code editor (optional) | Visual Studio 2026, VS Code or Rider | |

Check .NET is installed by opening PowerShell and typing `dotnet --list-sdks`. You should see a line starting with `10.`.

## Have Docker? One command, real MySQL

If Docker Desktop is installed, you can skip everything below. Create a `.env` file with four passwords, run `docker compose up -d --build`, and open **http://localhost:8080**. The database viewer is at http://localhost:8081. Full instructions: [10 Running with Docker](10-docker.md).

## Running `dotnet run` against the Docker database

On this PC, `dotnet run --project src/Grsms.Web` (http://localhost:5014) uses the **same MySQL database as the Docker app** (`localhost:3307`), through a user-secret set on 4 November 2026:

```powershell
dotnet user-secrets --project src/Grsms.Web set "ConnectionStrings:DefaultConnection" "server=localhost;port=3307;database=grsms;user=grsms;password=<MYSQL_PASSWORD from .env>"
```

So http://localhost:5014 and http://localhost:8080 show the same data. Start Docker first (`docker compose up -d`). Sign in with the administrator from `.env`.

> The old **demo mode** (a separate SQLite file filled with made-up people and records) was switched off on 4 November 2026: its `demo` launch profile and demo database were removed. The demo code (`DemoDataSeeder.cs`) is still in the project, but nothing starts it.

## Step 1: Create the database and a database user

Open **MySQL Workbench** or the `mysql` command line as the `root` user, and run the following (change the password):

```sql
-- The database. utf8mb4 stores every language and symbol correctly.
CREATE DATABASE grsms CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

-- A dedicated account for the application (never let the app use root).
CREATE USER 'grsms'@'localhost' IDENTIFIED BY 'Put-A-Strong-Password-Here1';
GRANT ALL PRIVILEGES ON grsms.* TO 'grsms'@'localhost';
FLUSH PRIVILEGES;
```

> **Why `utf8mb4_0900_ai_ci`?** The `ci` part means *case-insensitive*, so "finance" and "Finance" count as the same department name. The duplicate checks depend on this.

## Step 2: Get the code

Open PowerShell in the project folder:

```powershell
cd "C:\Users\<you>\Desktop\project\GREL Recruitment System"
dotnet restore
dotnet tool restore     # installs the "dotnet ef" database tool listed in dotnet-tools.json
```

## Step 3: Tell the app about the database and the first administrator

Passwords must **never** go into `appsettings.json` (that file is shared in source control). On a developer PC, use *user-secrets*, which are stored in your Windows profile:

```powershell
dotnet user-secrets --project src/Grsms.Web set "ConnectionStrings:DefaultConnection" "server=localhost;port=3306;database=grsms;user=grsms;password=Put-A-Strong-Password-Here1"

dotnet user-secrets --project src/Grsms.Web set "SeedAdmin:Email" "your.name@grel.example"
dotnet user-secrets --project src/Grsms.Web set "SeedAdmin:Password" "Choose#A-Strong1"
dotnet user-secrets --project src/Grsms.Web set "SeedAdmin:FullName" "Your Name"
```

The **SeedAdmin** account is created only once, the first time the app starts and finds no HR Administrator. After that these two settings are ignored and you can remove them:

```powershell
dotnet user-secrets --project src/Grsms.Web remove "SeedAdmin:Password"
```

The password must have at least 10 characters, including an upper-case letter, a lower-case letter, a number and a symbol.

On a **server**, use environment variables instead of user-secrets. Replace `:` with `__` (two underscores) in the setting name, for example `ConnectionStrings__DefaultConnection`. See [06 Configuration](06-configuration.md).

## Step 4: Create the tables

Pick **one** of the two options:

* **Option A, automatic (development):** do nothing. When the app runs in the Development environment, it creates or updates the tables itself (`Database:ApplyMigrationsOnStartup` is `true` in `appsettings.Development.json`).
* **Option B, by script (test/production servers):** ask the DBA to run the scripts in the `database/` folder in number order, for example:
  ```powershell
  mysql -u grsms -p grsms < database/01_phase01_foundation.sql
  ```
  Each script runs **once**. The scripts are explained in [03 Database](03-database.md).

## Step 5: Run it

```powershell
dotnet run --project src/Grsms.Web
```

Open the address shown in the console, for example **http://localhost:5014**. Sign in with the SeedAdmin email and password from step 3.

### Where do the emails go?

Until GREL's mail server details are configured, emails (password resets, candidate confirmations) are **saved as files** instead of being sent. Look in:

```
src/Grsms.Web/App_Data/mail/
```

Double-click a `.eml` file to open it in Outlook, then click the link inside. To send real email, see [06 Configuration → Email](06-configuration.md#email).

### Where are the logs?

`src/Grsms.Web/logs/grsms-YYYYMMDD.log`, with one file per day, kept for 30 days. The console window shows the same lines.

## Step 6: First things to do as the HR Administrator

1. **Sites:** GREL's 12 sites are pre-loaded; fill in the missing site types and locations.
2. **Units:** open each department and add its units.
3. **Users:** create accounts for the HR Manager, Managing Director, Recruitment Officers and each Head of Department.
4. **Departments:** edit each department and pick its Head of Department.

The [Administrator guide](05-admin-guide.md) walks through each screen.

## Troubleshooting

| Message | Cause and fix |
|---|---|
| `ConnectionStrings:DefaultConnection is not set` | Step 3 was skipped, or user-secrets were set for a different project. Run `dotnet user-secrets --project src/Grsms.Web list` to check. |
| `Unable to connect to any of the specified MySQL hosts` | MySQL is not running, or the host/port is wrong. On Windows check *Services → MySQL84*. |
| `Access denied for user 'grsms'@'localhost'` | Wrong password in the connection string, or step 1's `GRANT` was not run. |
| The console warns *No HR Administrator exists and SeedAdmin… are not set* | Set `SeedAdmin:Email` and `SeedAdmin:Password` (step 3) and restart. |
| *Could not create the first HR Administrator: Passwords must have…* | The SeedAdmin password is too weak. Choose a stronger one and restart. |
| Sign-in says *Please confirm your email address first* | That account is a candidate account that has not clicked its confirmation link. Open the `.eml` file (see above). |
