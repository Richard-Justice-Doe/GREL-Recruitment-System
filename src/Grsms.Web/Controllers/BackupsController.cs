using Grsms.Application.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 16: Backups (Super Admin): the backups the backup service made, "Back up now", and downloading a database dump.
/// Every request and download is in the audit trail. Restoring stays a server procedure (docs/21-production-deployment.md).
/// </summary>
[Authorize(Policy = Policies.Backups)]
public class BackupsController(IBackupStore backups, Application.Auditing.IAuditLogService audit,
    Application.Common.Interfaces.ICurrentUserService me) : AppController
{
    public IActionResult Index() => View(backups.Overview());

    [HttpPost]
    public async Task<IActionResult> RunNow(CancellationToken ct)
    {
        if (await backups.RequestBackupAsync(ct))
        {
            await audit.LogAsync("BackupRequested", me.UserId, me.UserName, null, ct);
            Success("Backup requested. The backup service starts it within a minute; reload this page to see the new files.");
        }
        else Error("No backup folder is set up on this server (setting Backups:Folder), so no backup can be requested here.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var stream = backups.Open(id);
        if (stream is null) return NotFound();
        await audit.LogAsync("BackupDownloaded", me.UserId, me.UserName, id, ct);
        return File(stream, "application/gzip", id);
    }
}
