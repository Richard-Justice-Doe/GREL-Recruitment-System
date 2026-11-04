# 10. Running GRSMS with Docker

Docker runs the whole system (the web app, a MySQL 8.4 database and a web-based database viewer) without installing .NET or MySQL on the PC. It is the quickest way to get a **real MySQL** copy of GRSMS running for development and testing.

> **This set-up is for a developer PC or test machine, not production.** It runs the app in the `Development` environment over plain HTTP. Production hosting (HTTPS, IIS/Linux) is Phase 13.

## What you need

* **Docker Desktop** (any recent version; tested with Docker 29.9 and Compose 5.6) running on the PC.

## The files

| File | What it does |
|---|---|
| `Dockerfile` | Builds the web app in two stages: compiles with the .NET 10 SDK image, then copies the result into the slim ASP.NET 10 runtime image. The app runs as the image's non-root `app` user on port 8080. |
| `docker-compose.yml` | Starts three containers (below) and wires them together. |
| `.dockerignore` | Keeps `bin/`, `obj/`, `App_Data/`, `logs/`, tests, docs and `.env` out of the image. |
| `.env` | **Passwords. Not in source control** (already listed in `.gitignore`). You create it once (next section). |

### The three containers

| Container | Image | Open it at | Purpose |
|---|---|---|---|
| `web` | built from `Dockerfile` | **http://localhost:8080** | GRSMS itself |
| `db` | `mysql:8.4` | `localhost:3307` (from MySQL Workbench etc.) | The database. Started with `utf8mb4` / `utf8mb4_0900_ai_ci`, which the duplicate-name checks rely on. |
| `phpmyadmin` | `phpmyadmin:5.2` | **http://localhost:8081** | Browse and query the database in a web browser (see [Viewing the database](#viewing-the-database-in-a-browser)) |

The host ports were chosen to avoid clashes on a developer PC:
* **8080** for the app, because `dotnet run` already uses **5014**. If both are running, `http://localhost:5014` is the `dotnet run` copy, *not* the container.
* **3307** for MySQL, because a locally installed MySQL normally uses 3306.
* **8081** for phpMyAdmin, bound to `127.0.0.1` so **only this PC** can open it, not other machines on the network.

### Data that survives restarts (volumes)

| Volume | Holds |
|---|---|
| `mysql-data` | The MySQL database files |
| `app-data` | `/app/App_Data`: uploaded documents, saved `.eml` emails, data-protection keys (keep these, or everyone is signed out and emailed links stop working) |
| `app-logs` | `/app/logs`: the daily log files |

## First run

1. Create a file called `.env` in the project folder (next to `docker-compose.yml`):

   ```
   MYSQL_ROOT_PASSWORD=choose-a-long-random-password
   MYSQL_PASSWORD=choose-another-long-random-password
   SEED_ADMIN_EMAIL=admin@grel.local
   SEED_ADMIN_PASSWORD=Choose#A-Strong1
   SEED_ADMIN_FULLNAME=System Administrator
   ```

   The admin password must have at least 10 characters with upper case, lower case, a number and a symbol. If any of the first four values is missing, `docker compose` stops with a message naming it.

2. Build and start everything:

   ```powershell
   docker compose up -d --build
   ```

   The first build downloads the .NET and MySQL images and takes a few minutes. The app waits until MySQL reports healthy, then **creates all the tables automatically** (EF Core migrations, because the container runs in `Development`) and creates the first HR Administrator from `SEED_ADMIN_*`.

3. Open **http://localhost:8080** and sign in with `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD`.

## Everyday commands

```powershell
docker compose ps                 # what is running
docker compose logs -f web        # follow the app's log (Ctrl+C to stop following)
docker compose up -d --build      # rebuild after changing the code, then restart
docker compose down               # stop everything (data is kept)
docker compose down -v            # stop AND delete the database, uploads and keys (start again from empty)
```

After code changes that add a database migration, `docker compose up -d --build` is enough: the app applies new migrations when it starts.

## Viewing the database in a browser

1. Open **http://localhost:8081**.
2. Sign in with user **`grsms`** and the `MYSQL_PASSWORD` from `.env`. (User `root` with `MYSQL_ROOT_PASSWORD` also shows MySQL's own system databases.)
3. Click **grsms** on the left to see the tables. Click a table to browse its rows, or use the **SQL** tab to run a query.

> **Careful:** the `grsms` account can change and delete data, and phpMyAdmin writes straight into the database the app is using. Prefer the app's own screens for real changes. The app normally *soft-deletes* (`IsDeleted = 1`) rather than removing rows, and writes an audit-trail entry, which phpMyAdmin does not.

Table names keep their capitals (`Users`, `Departments`, `RecruitmentRequests`, ...). MySQL in Docker runs on Linux, where table names are case-sensitive, so `SELECT * FROM users` fails and `SELECT * FROM Users` works.

## Why the container runs as `Development`

`docker-compose.yml` sets `ASPNETCORE_ENVIRONMENT=Development` on purpose:

* In `Production` the sign-in cookie is marked **Secure** (HTTPS only). The container serves plain HTTP, so in `Production` the browser would drop the cookie and **nobody could sign in**.
* `Development` also turns on automatic migrations (`Database:ApplyMigrationsOnStartup`).

Running the container in `Production` needs an HTTPS front end (a reverse proxy with a certificate) and forwarded-headers set-up. That is part of Phase 13.

## Backups

```powershell
# Back up: dump inside the container, then copy the file to the PC
docker compose exec -T db sh -c 'mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction --routines grsms > /tmp/grsms-backup.sql'
docker compose cp db:/tmp/grsms-backup.sql ./grsms-backup.sql
docker compose exec -T db sh -c 'rm /tmp/grsms-backup.sql'

# Restore: copy the file in, then load it
docker compose cp ./grsms-backup.sql db:/tmp/grsms-backup.sql
docker compose exec -T db sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" grsms < /tmp/grsms-backup.sql && rm /tmp/grsms-backup.sql'
```

The dump is made and loaded *inside* the container on purpose. Piping it through Windows PowerShell 5.1 (`> file` or `Get-Content | ...`) can change the file's text encoding and corrupt it.

Uploaded files live in the `app-data` volume. Back them up together with the database (see [03 Database → Backups](03-database.md#backups)).

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `set MYSQL_ROOT_PASSWORD in .env` (or another variable) | `.env` is missing or incomplete. See [First run](#first-run). |
| Sign-in says *Incorrect email or password* although the password is right, and `docker compose logs web` shows no request lines | The browser is talking to a **different** copy of GRSMS, usually `dotnet run` on port 5014. Use **http://localhost:8080** for the container. |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Something else uses 8080. Change the left-hand number in `ports: - "8080:8080"` (and `App__BaseUrl`) to a free port. |
| The admin account was not created | The admin is created only when **no** HR Administrator exists. Check the log for *Could not create the first HR Administrator* (usually a weak password), fix `.env`, then `docker compose up -d`. |
| Changed `MYSQL_PASSWORD` in `.env` and now the app can't connect | MySQL only reads the password when the volume is first created. Either change it back, or start again with `docker compose down -v` (this deletes the data). |
| `dotnet build` fails with *file is locked by Grsms.Web* | Unrelated to Docker: a `dotnet run` copy of the app is still running and holds the files in `src/Grsms.Web/bin`. Stop it first. |
