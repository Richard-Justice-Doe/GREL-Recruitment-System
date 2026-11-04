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
| `Database:ApplyMigrationsOnStartup` | `false` (`true` in Development) | Create or update the tables automatically at start-up. Keep `false` in production and use the SQL scripts. |

### First administrator
| Setting | Default | Meaning |
|---|---|---|
| `SeedAdmin:Email` | *(empty)* | Email of the first HR Administrator, created only if none exists |
| `SeedAdmin:Password` | *(empty)* | Their password. Remove the setting after first start-up. |
| `SeedAdmin:FullName` | `System Administrator` | Their display name |

### Security
| Setting | Default | Meaning |
|---|---|---|
| `Security:SessionTimeoutMinutes` | `20` | Minutes of inactivity before automatic sign-out |
| `DataProtection:KeysFolder` | `App_Data/keys` | Where the keys that protect cookies and reset links are kept. Relative paths are inside the web app folder. **This folder holds secrets:** keep it out of source control, back it up, and let only the app's account read it. |

### Microsoft 365 sign-in (optional)
Leave all three empty to switch it off. See [04 Security → Turning on Microsoft 365](04-security-and-roles.md#turning-on-sign-in-with-microsoft-365).

| Setting | Meaning |
|---|---|
| `AzureAd:TenantId` | Directory (tenant) ID |
| `AzureAd:ClientId` | Application (client) ID |
| `AzureAd:ClientSecret` | Client secret value (a secret: set it via user-secrets or an environment variable) |
| `AzureAd:CallbackPath` | Default `/signin-oidc`. Must match the redirect URI in Entra. |

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

Example for an internal relay server:
```powershell
dotnet user-secrets --project src/Grsms.Web set "Email:SmtpHost" "mail.grel.example"
dotnet user-secrets --project src/Grsms.Web set "Email:FromAddress" "recruitment@grel.example"
```

> **Microsoft 365 mailboxes:** Microsoft is retiring username/password ("basic") SMTP sign-in for Exchange Online. When GREL's mail details are known, a later phase will add the right method (OAuth or a relay connector).

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
```

Full hosting instructions (IIS or Linux) arrive in Phase 13.
