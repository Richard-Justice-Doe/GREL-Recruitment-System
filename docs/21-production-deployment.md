# 21. Production deployment (GREL IT guide)

This guide puts GRSMS on GREL's server with HTTPS, email, nightly backups and monitoring. It uses the files in the `deploy/` folder. The whole set-up was tested end to end on 4 November 2026 (see *What was tested* at the end).

```
  Users ──HTTPS 443──►  proxy (Caddy)  ──►  web (GRSMS, Production)  ──►  db (MySQL 8.4)
                         certificates          app-data volume               mysql-data volume
                         http → https          (uploads, keys)
                                                                  backup ──► deploy/backups (nightly)
```

Only the proxy is reachable from outside. The app and the database have no open ports. phpMyAdmin is **not** part of production.

## 1. What you need

| Item | Notes |
|---|---|
| A server | Linux (e.g. Ubuntu 24.04 LTS) or Windows Server with Docker. **2 CPU cores, 4 GB RAM, 40 GB disk** is plenty for GREL's volumes; add disk for backups. |
| Docker | Docker Engine with the Compose plugin (`docker compose version`) |
| A name | e.g. `recruitment.grel.com.gh`, pointing (DNS) to the server |
| Firewall | Ports **80 and 443** open to users. For an automatic Let's Encrypt certificate, also open to the internet. |
| Email | SMTP server, port, user and password from IT, for notifications and password resets |
| Optional | GREL's own certificate files, if Let's Encrypt can't be used; Microsoft 365 app registration (see [04 Security](04-security-and-roles.md#turning-on-sign-in-with-microsoft-365)); an SMS account (see [19](19-notifications-and-surveys.md#3-text-messages-sms)) |

## 2. First installation

```bash
git clone <the GRSMS repository> grsms
cd grsms/deploy
cp .env.production.example .env
chmod 600 .env          # Linux: only the administrators can read it
nano .env               # fill in every value (see below)
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f web     # wait for "Now listening"
```

In `.env`:
* **GRSMS_DOMAIN**: the name users will type.
* **MYSQL_ROOT_PASSWORD / MYSQL_PASSWORD**: long random passwords (e.g. `openssl rand -base64 24`). They are fixed when the database is first created.
* **SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD**: the first Super Admin (Phase 14; before that, the first HR Administrator).
* **SMTP_*** from IT. Until SMTP is set, emails are only saved as files in the app-data volume, and nobody receives them.

Open `https://<GRSMS_DOMAIN>`, sign in as the first administrator, then:
1. **Change the administrator password** (top-right menu), and **delete `SEED_ADMIN_PASSWORD` from `.env`**.
2. Create the staff accounts and roles (**Users & roles**), set each department's **Head of Department**, including **Information Technology** (onboarding IT items go to them).
3. Confirm the department codes with GREL ([12 Divisions and departments](12-divisions-and-departments.md)).

## 3. Certificates (HTTPS)

Set `GRSMS_TLS` in `.env`:

| Situation | Setting |
|---|---|
| Public name, reachable from the internet on ports 80/443 | leave **empty**: Caddy gets a free Let's Encrypt certificate and renews it automatically |
| GREL's own certificate (e.g. from a company CA or a purchased one) | put the files in `deploy/certs/` and set `GRSMS_TLS=tls /certs/grsms.crt /certs/grsms.key` |
| Intranet name with no certificate yet (testing only) | `GRSMS_TLS=tls internal`. Browsers will warn until Caddy's root certificate is trusted on GREL PCs. |

`http://` is always redirected to `https://`. Certificates are kept in the `caddy-data` volume.

## 4. Updating to a new version

```bash
cd grsms/deploy
docker compose -f docker-compose.prod.yml exec backup bash /backup.sh now   # 1. backup first
git pull                                                                    # 2. new code
# 3. optional: set GRSMS_VERSION=1.1.0 in .env, so the previous image (1.0.0) stays available
docker compose -f docker-compose.prod.yml up -d --build                     # 4. rebuild and restart
docker compose -f docker-compose.prod.yml logs -f web                       # 5. "Applying database migrations..." then "Now listening"
```

Database changes are applied automatically at start-up (`APPLY_MIGRATIONS=true`). If GREL prefers a DBA to run the SQL scripts in `database/` instead, set `APPLY_MIGRATIONS=false`. GRSMS then **refuses to start** while any update is missing, and its log names the missing script, rather than failing later on a page.

**Rolling back:** restore the backup taken in step 1 (section 5), set `GRSMS_VERSION` back to the previous version and run `docker compose -f docker-compose.prod.yml up -d` (no `--build`).

Downtime for an update is usually under a minute.

## 5. Backups

The `backup` container makes, every night after 01:00 GMT (`BACKUP_HOUR_GMT`):

| File | Contains |
|---|---|
| `grsms-db-YYYYMMDD-HHMM.sql.gz` | the whole database (consistent snapshot, no downtime) |
| `grsms-files-YYYYMMDD-HHMM.tar.gz` | uploaded documents (CVs, certificates, job descriptions), the **data-protection keys**, saved emails |

They go to `deploy/backups/` (`BACKUP_FOLDER`) and are kept for 30 days (`BACKUP_KEEP_DAYS`).

**Copy them off the server every day** (GREL's backup system, another server or cloud storage). A backup that lives only on the server is lost with it. The files archive contains the keys that protect sign-in cookies and emailed links, so treat backups as confidential.

Take one at any time:
```bash
docker compose -f docker-compose.prod.yml exec backup bash /backup.sh now
```

**From GRSMS (Phase 16):** a Super Admin sees the backups on **Management → Backups**, can press **Back up now**, and can download a database dump. The `web` container sees the backup folder at `/app/backups` (`Backups__Folder`). For *Back up now*, GRSMS writes `requests/backup-request` there. The backup service creates that `requests` folder at start-up, owned by the app's non-root user (uid 1654), and checks it every 30 seconds. The *files and keys* archive can't be downloaded through the web. Restoring stays the procedure below. See [23 System settings → Backups](23-system-settings.md#3-backups).

### Restoring
```bash
docker compose -f docker-compose.prod.yml stop web
# Database
gunzip -c backups/grsms-db-20261104-0100.sql.gz | docker compose -f docker-compose.prod.yml exec -T db sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" grsms'
# Files (uploads and keys)
docker run --rm -v grsms_app-data:/data -v "$PWD/backups:/backups" alpine sh -c "rm -rf /data/* && tar -xzf /backups/grsms-files-20261104-0100.tar.gz -C /data"
docker compose -f docker-compose.prod.yml start web
```
**Test a restore at least every three months**, on a separate machine or under another project name (`-p grsms-restoretest`).

## 6. Monitoring

| What | How |
|---|---|
| Is GRSMS up? | `https://<GRSMS_DOMAIN>/health` answers **Healthy** (200) when the app and database work, **Unhealthy** (503) otherwise. Point GREL's uptime monitor at it every few minutes. It shows no details. |
| Errors | `docker compose -f docker-compose.prod.yml logs web --since 24h \| grep -E "ERR\|FTL"`. Daily log files are also in the `app-logs` volume (kept 30 days). |
| Background jobs | the log shows vacancies closed, reminders sent, survey invitations, offers expired |
| Backups | `logs backup` shows "backup done" every night, or "BACKUP FAILED" |
| Disk | MySQL data, uploads and backups grow slowly; check free space monthly |
| Security events | the **Audit trail** (HR Administrator / HR Manager): sign-ins, failures, lockouts, every record change, with the real visitor address |

## 7. How production differs from the developer set-up

| | Developer (`docker-compose.yml`) | Production (`deploy/docker-compose.prod.yml`) |
|---|---|---|
| Mode | Development: detailed error pages | **Production**: a plain error page; details only in the log |
| Address | http://localhost:8080 | **https://** your domain; cookies HTTPS-only; HSTS |
| Open ports | app 8080, MySQL 3307, phpMyAdmin 8081 | **only 80 and 443** (the proxy) |
| Visitor address | direct | from the proxy's `X-Forwarded-For`, trusted only from the proxy network (`ReverseProxy:KnownNetworks`) |
| Backups | by hand | nightly, 30 days |
| Host names | any | only `GRSMS_DOMAIN` (`AllowedHosts`) |

## 8. Go-live checklist

**Server and security**
- [ ] DNS name points to the server; `https://<domain>` shows the sign-in page without a certificate warning
- [ ] `http://<domain>` redirects to https
- [ ] Only ports 80/443 reachable from outside (check from another machine: 3306, 8080 and 8081 closed)
- [ ] `deploy/.env` readable only by administrators; `SEED_ADMIN_PASSWORD` removed after the first sign-in
- [ ] Administrator password changed; at least two Super Admins exist (so one can stand in for the other)
- [ ] Server operating system updates switched on

**Email and integrations**
- [ ] SMTP set; a "forgot password" email arrives, and its link opens `https://<domain>/...`
- [ ] (Optional) Microsoft 365 sign-in tested with a staff account
- [ ] SMS left off, or tested with one real message (see [06 Configuration](06-configuration.md))

**Data**
- [ ] Departments, divisions, sites and department codes confirmed with GREL; every department has a Head (IT included)
- [ ] Staff accounts and roles created; each HOD's home department set

**Operations**
- [ ] First nightly backup present in `deploy/backups`; copied off the server; one **test restore** done
- [ ] `/health` added to the uptime monitor
- [ ] Someone named to check the log and backups weekly
- [ ] One full journey done on the live system with test data (request → vacancy → advert → application → shortlist → interview → offer → onboarding), then the test records removed or marked

## 8b. Windows Server with Docker (first installation)

The containers (GRSMS, MySQL 8.4, Caddy) are **Linux containers**. Windows Server's own Docker runs only Windows containers and Docker Desktop is not supported on Windows Server, so Docker runs inside a small Linux (Ubuntu) under **WSL2**, and Windows passes ports 80 and 443 to it. Needs **Windows Server 2022 or 2025** with current updates; if the server is a virtual machine, **nested virtualisation** must be on (Hyper-V: `Set-VMProcessor -VMName <vm> -ExposeVirtualizationExtensions $true`; VMware: "Expose hardware assisted virtualization"). On Windows Server 2019 or older, use IIS (section 9) instead.

**Step 1: Ubuntu under WSL2** (PowerShell as Administrator):
```powershell
wsl --install -d Ubuntu-24.04      # restart the server if asked, then open "Ubuntu" and create a Linux user
wsl --set-default-version 2
wsl -l -v                          # Ubuntu-24.04 must show VERSION 2
```

**Step 2: Docker inside Ubuntu** (in the Ubuntu window):
```bash
sudo tee /etc/wsl.conf > /dev/null <<'EOF'
[boot]
systemd=true
EOF
```
Back in PowerShell: `wsl --shutdown`, then open Ubuntu again and install Docker Engine:
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER      # then close and reopen the Ubuntu window
docker compose version             # must print a version
```

**Step 3: the code** (keep it in the Linux file system, `~/grsms`, not under `/mnt/c`: much faster). The repository is private, so use a GitHub **personal access token** (GitHub → Settings → Developer settings → Fine-grained token, *Contents: read-only* on this repository) as the password when asked:
```bash
cd ~
git clone -b feature/ats-and-super-admin https://github.com/Richard-Justice-Doe/GREL-Recruitment-System.git grsms
cd grsms/deploy
cp .env.production.example .env && chmod 600 .env && nano .env
```
Fill in `.env` as in section 2. For GREL's Hostinger mailbox: `SMTP_HOST=smtp.hostinger.com`, `SMTP_PORT=587` (**not 465**: .NET's mail client only does STARTTLS on 587), `SMTP_SSL=true`, `SMTP_USER` / `SMTP_PASSWORD` = the mailbox and its password, `EMAIL_FROM` = the same mailbox. `GRSMS_TLS`: empty for a public name (Let's Encrypt), `tls internal` for an intranet name, or GREL's certificate files (section 3).

**Step 4: start it**:
```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f web    # wait for "Now listening"; Ctrl+C to leave
curl -k https://localhost/health                         # "Healthy"
```
The first build takes several minutes. All tables are created at start-up (`APPLY_MIGRATIONS=true`): **no SQL script needs running by hand** on a new installation.

**Step 5: let other PCs reach it.** WSL2 is reachable from the server itself (`https://localhost`) but not from the network until Windows forwards the ports. PowerShell as Administrator, saved as `C:\grsms\wsl-ports.ps1`:
```powershell
$ip = (wsl -d Ubuntu-24.04 hostname -I).Trim().Split(' ')[0]
foreach ($port in 80, 443) {
    netsh interface portproxy delete v4tov4 listenport=$port listenaddress=0.0.0.0 2>$null
    netsh interface portproxy add v4tov4 listenport=$port listenaddress=0.0.0.0 connectport=$port connectaddress=$ip
}
```
Run it once, then open the firewall (once):
```powershell
New-NetFirewallRule -DisplayName "GRSMS HTTP/HTTPS" -Direction Inbound -Protocol TCP -LocalPort 80,443 -Action Allow
```
The WSL address changes when the server restarts, which is why the script is run again at start-up (step 6).

**Step 6: start everything when the server starts** (Task Scheduler, PowerShell as Administrator):
```powershell
$keepUp = New-ScheduledTaskAction -Execute "wsl.exe" -Argument "-d Ubuntu-24.04 -u root -- bash -c 'sleep infinity'"
$ports  = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -Command `"Start-Sleep 60; & C:\grsms\wsl-ports.ps1`""
$atBoot = New-ScheduledTaskTrigger -AtStartup
Register-ScheduledTask -TaskName "GRSMS WSL" -Action $keepUp -Trigger $atBoot -User "SYSTEM" -RunLevel Highest
Register-ScheduledTask -TaskName "GRSMS ports" -Action $ports -Trigger $atBoot -User "SYSTEM" -RunLevel Highest
```
The first task starts Ubuntu (systemd then starts Docker, and Docker restarts the containers: `restart: unless-stopped`); the second waits a minute and forwards the ports. **Test it once: restart the server** and open `https://<GRSMS_DOMAIN>` from another PC.

**Step 7: first sign-in and checks**: as in section 2 (change the administrator password, remove `SEED_ADMIN_PASSWORD` from `.env`, create accounts and roles, give someone the **IT Manager** role), then **Administration → System settings → Send a test email**. Backups are written to `~/grsms/deploy/backups`, which Windows sees as `\\wsl$\Ubuntu-24.04\home\<linux user>\grsms\deploy\backups`: copy them off the server regularly (section 5).

**Updating later**: section 4, in the Ubuntu window (`cd ~/grsms/deploy`, backup, `git pull`, `docker compose ... up -d --build`).

## 9. Without Docker (Windows Server + IIS)

GRSMS can also run on IIS: install the **ASP.NET Core 10 Hosting Bundle** and MySQL 8.4, publish with `dotnet publish src/Grsms.Web -c Release`, and set the same settings as environment variables or in `appsettings.Production.json` (see [06 Configuration](06-configuration.md)). Under IIS, HTTPS ends in IIS itself, so `ReverseProxy:Enabled` stays `false`. Run the SQL scripts in `database/` in order, or set `Database:ApplyMigrationsOnStartup=true`. Backups must then be scheduled with Windows Task Scheduler (`mysqldump` plus a copy of `App_Data`).

## What was tested (4 November 2026)

The production stack was started on a development PC (as `localhost`, with Caddy's internal certificate):
* the app started in **Production**, created all tables and the first administrator;
* `http://` redirected (308) to `https://`; the sign-in page carried the Content-Security-Policy; cookies were **Secure**; no `Server` header;
* sign-in over HTTPS worked; reports, users and audit trail opened;
* the audit trail recorded the **visitor's** address, not the proxy's (forwarded headers trusted from the proxy network only);
* `/health` answered *Healthy*;
* a backup was taken and **restored** into an empty database: all 40 tables, the accounts and the latest database version came back; the files archive held the uploads and keys;
* with `APPLY_MIGRATIONS=false` and one database update missing, GRSMS refused to start and named the missing update.

Not tested here (needs GREL's environment): a real domain with a Let's Encrypt certificate, GREL's SMTP server, Microsoft 365 sign-in.
