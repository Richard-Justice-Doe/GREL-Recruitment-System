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
        else Fail(result);
        return RedirectToAction(nameof(Index), null, Anchor(group));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> ClearSecret(string key, CancellationToken ct)
    {
        var result = await settings.ClearSecretAsync(key, ct);
        if (result.Succeeded) Success("The saved secret was removed. The server configuration's value (if any) applies again.");
        else Fail(result);
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
        else Fail(result);
        return RedirectToAction(nameof(Index), null, Anchor(SettingCatalogue.Smtp));
    }

    /// <summary>The id of a group's card on the page, so saving returns to it.</summary>
    public static string Anchor(string group) => "s-" + new string(group.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
