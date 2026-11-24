# 6. Configuration

Settings are read from these sources, **later ones win**:

1. `src/Grsms.Web/appsettings.json`: defaults, shared in source control. **No passwords here.**
2. `appsettings.{Environment}.json`: e.g. `appsettings.Development.json`.
3. **User-secrets** (developer PCs only): `dotnet user-secrets --project src/Grsms.Web set "Key" "value"`.
4. **Environment variables** (servers): replace `:` with `__`, e.g. `ConnectionStrings__DefaultConnection`.

The environment is chosen by the `ASPNETCORE_ENVIRONMENT` variable: `Development` on developer PCs, and `Production` (the default) on servers.

## All settings

### Database
| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | *(empty: required)* | MySQL connection, e.g. `server=localhost;port=3306;database=grsms;user=grsms;password=...` |
| `Database:ApplyMigrationsOnStartup` | `false` (`true` in Development and in the `deploy/` production set-up) | Create or update the tables automatically at start-up. When `false`, a DBA runs the SQL scripts in `database/`; GRSMS then **refuses to start** while any update is missing and names it in the log (Phase 13). |
| `Database:SlowQueryMilliseconds` | `500` | docs/32: any database command slower than this is written to the log as a warning, with its time and SQL (never the values). `0` switches it off. Use it to find what is slow on the real server. |
| `Navigation:CountsCacheSeconds` | `30` | docs/32: how long each user's menu numbers (requests awaiting you, open vacancies, ...) are kept before being counted again. A form the user posts refreshes theirs at once; the bell is always live. `0` = count on every page. |

### First administrator
| Setting | Default | Meaning |
|---|---|---|
| `SeedAdmin:Email` | *(empty)* | Email of the first Super Admin, created only if no active Super Admin exists (an existing account with this email is given the role instead, Phase 14) |
| `SeedAdmin:Password` | *(empty)* | Their password. Remove the setting after first start-up. |
| `SeedAdmin:FullName` | `System Administrator` | Their display name |

### Security
| Setting | Default | Meaning |
|---|---|---|
| `Security:SessionTimeoutMinutes` | `20` | Minutes of inactivity before automatic sign-out |
| `Security:SignInAttemptsPerMinute` | `10` | Posts per minute from one network address to the sign-in, registration and password forms; more get *Too many attempts* (Phase 12). Behind a reverse proxy, set up forwarded headers first (Phase 13) or every user shares the proxy's address. |
| `DataProtection:KeysFolder` | `App_Data/keys` | Where the keys that protect cookies and reset links are kept. Relative paths are inside the web app folder. **This folder holds secrets:** keep it out of source control, back it up, and let only the app's account read it. |

### Microsoft 365 sign-in (optional)
Leave all three empty to switch it off. See [04 Security → Turning on Microsoft 365](04-security-and-roles.md#turning-on-sign-in-with-microsoft-365).

| Setting | Meaning |
|---|---|
| `AzureAd:TenantId` | Directory (tenant) ID |
| `AzureAd:ClientId` | Application (client) ID |
| `AzureAd:ClientSecret` | Client secret value (a secret: set it via user-secrets or an environment variable) |
| `AzureAd:CallbackPath` | Default `/signin-oidc`. Must match the redirect URI in Entra. |

### Settings a Super Admin can change on screen (Phase 16)
The **Email**, **AzureAd** (tenant, client, secret) and **Reminders:ApprovalWaitingDays** settings below can also be set on **Management → System settings**, along with the organisation name, the email greeting and signature, and the recruitment workflow settings (`Workflow:VacancyClosingDays` 14, `Workflow:OfferReplyMaxDays` 30, `Workflow:DefaultKnockoutQuestions`). **A value saved on screen wins; an empty one falls back to the value here**, then to the default. Secrets saved on screen are encrypted with the data-protection keys. See [23 System settings](23-system-settings.md).

| Setting | Default | Meaning |
|---|---|---|
| `Backups:Folder` | *(empty)* | Phase 16: the folder where the backup service writes its backups, as seen by GRSMS (`/app/backups` in the production set-up). Empty = the Backups screen shows *No backup folder*. |

### Email
| Setting | Default | Meaning |
|---|---|---|
| `Email:FromAddress` | `noreply@grsms.local` | Sender address |
| `Email:FromName` | `GREL Recruitment` | Sender name |
| `Email:SmtpHost` | *(empty)* | **Empty = don't send; save `.eml` files instead.** Set it to send real email. |
| `Email:SmtpPort` | `587` | |
| `Email:EnableSsl` | `true` | Use STARTTLS/SSL |
| `Email:UserName` / `Email:Password` | *(empty)* | SMTP login, if the server needs one |
| `Email:PickupDirectory` | `App_Data/mail` | Folder for `.eml` files when `SmtpHost` is empty |

**Emails are sent in the background (11 November 2026).** With a mail server set, an action that emails people (approving a request, applying for a job, ...) no longer waits for the server: the emails are queued and a background sender (`EmailSendingWorker`) sends them a moment later, one at a time, each with a 30-second limit, trying a failed email again after 10 seconds and after 1 minute. The log shows *queued*, then *sent in N ms*, or *could not be sent ... dropped*. **Send a test email** on System settings still waits for the server and reports the real result. Before this, GREL's server (Hostinger) took 4-5 s per email, so approving a request took 17 s and applying for a job 18 s. The queue is in memory: emails still waiting when GRSMS stops are sent during shutdown for up to 30 seconds; anything left is logged as not sent. Saving `.eml` files (no server) stays immediate.

Example for an internal relay server:
```powershell
dotnet user-secrets --project src/Grsms.Web set "Email:SmtpHost" "mail.grel.example"
dotnet user-secrets --project src/Grsms.Web set "Email:FromAddress" "recruitment@grel.example"
```

> **Microsoft 365 mailboxes:** Microsoft is retiring username/password ("basic") SMTP sign-in for Exchange Online. When GREL's mail details are known, a later phase will add the right method (OAuth or a relay connector).

### Uploaded files
| Setting | Default | Meaning |
|---|---|---|
| `Storage:RootFolder` | `App_Data/uploads` | Where uploaded documents are kept: a local folder (`D:\GRSMS\uploads`) or a file-server share (`\\fileserver\grsms`). Relative paths are inside the web app folder. **Never** point it inside `wwwroot`. Back it up with the database. The app's account needs read/write access. |
| `Pdf:ChromiumPath` | *(found automatically)* | 13 Nov 2026: the browser that prints the offer and appointment letters to PDF (headless Chromium). The Docker image installs `chromium` and sets it to `/usr/bin/chromium`. Without it, GRSMS tries Chrome and Edge in their usual places; if none is found, the *Download PDF* buttons are hidden and letters can still be printed from the browser. |
| `Uploads:MaxFileSizeMb` | `10` | Largest file accepted, in MB (the upload page allows at most 25 MB per request) |

### Virus scanning of uploads (connect before go-live)
Every upload (CVs, new-hire documents, request attachments, personnel files) goes through `IVirusScanner` (Application/Common/Interfaces) **after** the type, size and real-content checks and **before** it is written to disk. A file that is not clean is refused with the scanner's finding and never stored.

Out of the box GRSMS registers `NoVirusScanner` (Infrastructure/Storage), which **lets every file through**: a placeholder until GREL connects a real scanner (decision of 10 November 2026). To connect one:

1. Install a scanner the server can reach, e.g. **ClamAV** (`clamd`, TCP port 3310) on the server or a small VM, or use **Microsoft Defender** on the Windows server (`MpCmdRun.exe -Scan -ScanType 3 -File <path> -DisableRemediation`, exit code 2 = threat found; this needs the file saved to a temporary folder first).
2. Add a class in `Grsms.Infrastructure/Storage` that implements `IVirusScanner`. For ClamAV (no extra package needed):
   ```csharp
   public sealed class ClamAvScanner(IConfiguration config, ILogger<ClamAvScanner> logger) : IVirusScanner
   {
       public async Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default)
       {
           try
           {
               using var tcp = new System.Net.Sockets.TcpClient();
               await tcp.ConnectAsync(config["Antivirus:Host"] ?? "localhost", config.GetValue("Antivirus:Port", 3310), ct);
               await using var net = tcp.GetStream();
               await net.WriteAsync("zINSTREAM\0"u8.ToArray(), ct);
               var buffer = new byte[64 * 1024];
               int read;
               while ((read = await content.ReadAsync(buffer, ct)) > 0)
               {
                   await net.WriteAsync(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(read)), ct); // chunk length, big-endian
                   await net.WriteAsync(buffer.AsMemory(0, read), ct);
               }
               await net.WriteAsync(new byte[4], ct); // a zero-length chunk ends the stream
               using var reader = new StreamReader(net);
               var reply = (await reader.ReadToEndAsync(ct)).TrimEnd('\0', '\n'); // "stream: OK" or "stream: <name> FOUND"
               return reply.EndsWith(" OK") ? VirusScanResult.Passed : new VirusScanResult(false, reply.Replace("stream: ", "").Replace(" FOUND", ""));
           }
           catch (Exception e) when (e is not OperationCanceledException)
           {
               logger.LogError(e, "Virus scanner unavailable; upload {File} refused", fileName);
               return new VirusScanResult(false, "the virus scanner is unavailable, please try again later"); // fail closed
           }
       }
   }
   ```
3. In `Grsms.Infrastructure/DependencyInjection.cs` replace `services.AddSingleton<IVirusScanner, Storage.NoVirusScanner>();` with `services.AddSingleton<IVirusScanner, Storage.ClamAvScanner>();`, and set `Antivirus:Host` / `Antivirus:Port` (environment variables `Antivirus__Host`, `Antivirus__Port`).
4. Test with the standard **EICAR test file** (a harmless string every scanner reports): uploading it must be refused with "refused by the virus scan (Eicar-Signature)". The automated test `A_file_refused_by_the_virus_scanner_is_never_stored` already checks that a refusal stores nothing.

Keep `clamd`'s `StreamMaxLength` at or above `Uploads:MaxFileSizeMb`. The example **fails closed** (refuses uploads while the scanner is down); change the `catch` if GREL prefers uploads to continue unscanned during an outage.

### Vacancies
| Setting | Default | Meaning |
|---|---|---|
| `Vacancies:AutoClose` | `true` | Close published vacancies automatically once their closing date has passed. The same job also publishes scheduled **job adverts** on their start date and closes adverts after their end date or once their vacancy closes (Phase 4). |
| `Vacancies:AutoCloseIntervalMinutes` | `60` | How often the check runs (it also runs about 30 seconds after start-up) |

> If GRSMS ever runs on **several servers at once**, leave `AutoClose` on for only one of them.

### Reminders and surveys (Phase 10)
They run in the same hourly job, so they also stop when `Vacancies:AutoClose` is `false`.

| Setting | Default | Meaning |
|---|---|---|
| `Reminders:Enabled` | `true` | Send automatic reminders and survey invitations, and remove read notifications older than 6 months |
| `Reminders:FromHourGmt` | `7` | Nothing is sent before this hour (GMT, Ghana's time), so nobody is messaged at night |
| `Reminders:ApprovalWaitingDays` | `3` | A request, vacancy or hiring decision waiting this many days at one stage is reminded, then weekly |

### Text messages (SMS, Phase 10)
**Switched off by default.** Until `Sms:Enabled` is `true`, each message is only written as a `.txt` file in `Sms:PickupDirectory`, so you can see what would have been sent.

| Setting | Default | Meaning |
|---|---|---|
| `Sms:Enabled` | `false` | `true` sends real text messages through the provider |
| `Sms:Provider` | `Hubtel` | The only provider built in |
| `Sms:SenderId` | `GREL` | Name shown on the phone (up to 11 characters, registered with the provider) |
| `Sms:ClientId` / `Sms:ClientSecret` | empty | The provider account's API keys. **Secret: put them in user-secrets or environment variables, never in appsettings.json.** |
| `Sms:ApiUrl` | `https://smsc.hubtel.com/v1/messages/send` | The provider's send address |
| `Sms:PickupDirectory` | `App_Data/sms` | Where messages are saved while SMS is off |

> **Before switching SMS on:** the Hubtel connection was written from Hubtel's published "quick send" format but has **not been tested against a real Hubtel account**. Check `Sms:ApiUrl` and the parameters against Hubtel's current documentation, then send one test message (e.g. schedule an interview for a test applicant with your own phone number). Messages cost money per text.

### ATS CV screening (Phase 15)
The ATS weights, bands and rejection rules are **not** settings here: the Super Admin sets them on **ATS settings**, and they are stored in the database. Only the optional Claude AI CV reader is configured here.

| Setting | Default | Meaning |
|---|---|---|
| `Ats:Ai:ApiKey` | *(empty)* | Anthropic API key for the optional Claude CV reader. Empty = not available (GRSMS's own reader is always used). **Secret: user-secrets or environment variable (`Ats__Ai__ApiKey`), never appsettings.json.** Setting it does not switch the reader on; the Super Admin does that on ATS settings, and only after GREL has approved sending CV text to Anthropic. |
| `Ats:Ai:Model` | `claude-opus-5-5` | The Claude model used |
| `Ats:Ai:TimeoutSeconds` | `60` | How long to wait for one CV before falling back to GRSMS's own reader |

### Links in emails
| Setting | Default | Meaning |
|---|---|---|
| `App:BaseUrl` | *(empty)* | The site's public address, e.g. `https://recruitment.grel.example`. Emails normally use the address the user is browsing on; this is the fallback for emails sent by the background job (reminders, survey invitations, expired offers). **Set it in production** (the production compose file does this from `GRSMS_DOMAIN`). |

### Behind a reverse proxy (Phase 13)
| Setting | Default | Meaning |
|---|---|---|
| `ReverseProxy:Enabled` | `false` | `true` when GRSMS sits behind a proxy that ends HTTPS (Caddy in `deploy/`, nginx, a load balancer). GRSMS then takes the visitor's address and "this was HTTPS" from `X-Forwarded-For` / `X-Forwarded-Proto`. Leave `false` under IIS or when browsers connect directly. |
| `ReverseProxy:KnownNetworks` | *(empty)* | The proxy's network(s), e.g. `172.29.13.0/24` (comma-separated). Forwarded headers from anywhere else are ignored, so nobody can fake their address. Required (or `KnownProxies`) when enabled; GRSMS refuses to start otherwise. |
| `ReverseProxy:KnownProxies` | *(empty)* | Alternatively, the proxy's exact address(es), e.g. `10.0.5.20` |
| `AllowedHosts` | `*` | In production, the site's name (e.g. `recruitment.grel.example`): requests for any other host name are refused |

### Health check (Phase 13)
`/health` needs no sign-in and answers `Healthy` (200) when GRSMS can reach its database, `Unhealthy` (503) otherwise. For uptime monitoring.

### Logging (Serilog)
| Setting | Default | Meaning |
|---|---|---|
| `Logs:Folder` | `logs` | Folder for the daily log files `grsms-YYYYMMDD.log` (30 days kept). Relative paths are inside the web app folder. |
| `Serilog:MinimumLevel:Default` | `Information` (`Debug` in Development) | How much detail to log: `Debug`, `Information`, `Warning` or `Error` |

## Example: production server environment variables

```
ASPNETCORE_ENVIRONMENT                = Production
ConnectionStrings__DefaultConnection  = server=db01;port=3306;database=grsms;user=grsms;password=********;SslMode=Required
Email__SmtpHost                       = mail.grel.example
Email__FromAddress                    = recruitment@grel.example
DataProtection__KeysFolder            = D:\GRSMS\keys
Logs__Folder                          = D:\GRSMS\logs
Storage__RootFolder                   = \\fileserver\grsms\uploads
App__BaseUrl                          = https://recruitment.grel.example
```

Full hosting instructions (IIS or Linux) arrive in Phase 13.
