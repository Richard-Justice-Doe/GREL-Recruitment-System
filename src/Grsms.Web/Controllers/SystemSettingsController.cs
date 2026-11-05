using Grsms.Application.Settings;
using Grsms.Domain.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 16: System settings (organisation and email wording, email server, Microsoft 365 sign-in, recruitment workflow)
/// and Email templates. The Super Admin changes them; the Admin sees them read-only. Secrets are never shown.
/// The rules live in <see cref="SystemSettingsService"/>.
/// </summary>
[Authorize(Policy = Policies.SystemSettings)]
public class SystemSettingsController(SystemSettingsService settings, ISettingsStore store) : AppController
{
    public IActionResult Index()
    {
        ViewBag.CanManage = settings.CanManage;
        ViewBag.MicrosoftReady = MicrosoftSignIn.IsConfigured(store);
        ViewBag.MicrosoftCallback = $"{Request.Scheme}://{Request.Host}{HttpContext.RequestServices.GetRequiredService<IConfiguration>()["AzureAd:CallbackPath"] ?? "/signin-oidc"}";
        ViewBag.EmailSending = !string.IsNullOrWhiteSpace(store.Get(SettingCatalogue.SmtpHost));
        return View(settings.GetAll());
    }

    /// <param name="group">Which card was saved (one of the SettingCatalogue groups).</param>
    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> Save(string group, CancellationToken ct)
    {
        // Field names are the setting keys (e.g. "Email:SmtpHost"); an unticked switch sends nothing, which means "off".
        var values = SettingCatalogue.All.Where(d => d.Group == group)
            .ToDictionary(d => d.Key, d => (string?)Request.Form[d.Key].FirstOrDefault());
        var result = await settings.SaveGroupAsync(group, values, ct);
        if (result.Succeeded) Success($"{group}: saved. The new values apply straight away.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index), null, Anchor(group));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> ClearSecret(string key, CancellationToken ct)
    {
        var result = await settings.ClearSecretAsync(key, ct);
        if (result.Succeeded) Success("The saved secret was removed. The server configuration's value (if any) applies again.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index), null, Anchor(SettingCatalogue.Find(key)?.Group ?? ""));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> TestEmail(string? to, CancellationToken ct)
    {
        var result = await settings.SendTestEmailAsync(to, ct);
        if (result.Succeeded)
            Success(string.IsNullOrWhiteSpace(store.Get(SettingCatalogue.SmtpHost))
                ? "No SMTP server is set, so the test email was saved as a file on the server instead of being sent."
                : $"Test email sent to {to}. Check that mailbox (and its spam folder).");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index), null, Anchor(SettingCatalogue.Smtp));
    }

    /// <summary>The id of a group's card on the page, so saving returns to it.</summary>
    public static string Anchor(string group) => "s-" + new string(group.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

/// <summary>Phase 16: Email templates (the candidate emails). Super Admin changes; Admin reads.</summary>
[Authorize(Policy = Policies.SystemSettings)]
public class EmailTemplatesController(SystemSettingsService settings) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.CanManage = settings.CanManage;
        return View(await settings.GetTemplatesAsync(ct));
    }

    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var form = await settings.GetTemplateAsync(id, ct);
        if (form is null) return NotFound();
        ViewBag.CanManage = settings.CanManage;
        return View(form);
    }

    /// <param name="preview">Set by the "Preview" button: show the result without saving.</param>
    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> Edit(string id, EmailTemplateFormDto form, bool preview, CancellationToken ct)
    {
        form.Key = id;
        var saved = await settings.GetTemplateAsync(id, ct);
        if (saved is null) return NotFound();
        form.Definition = saved.Definition;
        form.IsCustomised = saved.IsCustomised;
        if (!preview)
        {
            var result = await settings.SaveTemplateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"\"{form.Definition!.Name}\" saved. It is used for every email of this kind from now on.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        settings.Preview(form);
        ViewBag.CanManage = true;
        return View(form);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> Reset(string id, CancellationToken ct)
    {
        var result = await settings.ResetTemplateAsync(id, ct);
        if (result.Succeeded) Success("Back to the built-in wording.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index));
    }
}

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

/// <summary>Phase 16: Audit monitor: sign-in problems, configuration changes and activity at a glance (audit-trail readers).</summary>
[Authorize(Policy = Policies.AuditTrail)]
public class AuditMonitorController(Application.Auditing.IAuditLogService audit) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await audit.MonitorAsync(DateTime.UtcNow, ct));
}
