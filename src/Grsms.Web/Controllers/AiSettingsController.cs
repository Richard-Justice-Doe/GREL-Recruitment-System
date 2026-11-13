using Grsms.Application.Ai;
using Grsms.Domain.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// 13 November 2026 (docs/36): Administration → AI settings &amp; usage. The Super Admin edits; the Admin sees it read-only.
/// The rules live in <see cref="AiAdminService"/> and <see cref="AiGateway"/>.
/// </summary>
[Authorize(Policy = Policies.SystemSettings)]
public class AiSettingsController(AiAdminService ai) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.CanManage = ai.CanManage;
        return View(await ai.GetPageAsync(ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> SaveService(CancellationToken ct)
    {
        var result = await ai.SaveServiceAsync(Form(SettingCatalogue.AiService), ct);
        if (result.Succeeded) Success("AI service saved. Use \"Test the connection\" to check it.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> SaveFeatures(CancellationToken ct)
    {
        var result = await ai.SaveFeaturesAsync(Form(SettingCatalogue.AiFeaturesGroup), ct);
        if (result.Succeeded) Success("AI features saved. They apply straight away.");
        else Fail(result);
        return RedirectToAction(nameof(Index), null, "features");
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> ClearKey(CancellationToken ct)
    {
        var result = await ai.ClearKeyAsync(ct);
        if (result.Succeeded) Success("The saved API key was removed.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var result = await ai.TestAsync(ct);
        if (result.Succeeded) Success(result.Value!);
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Field names are the setting keys; an unticked switch sends nothing, which means "off".</summary>
    private Dictionary<string, string?> Form(string group) =>
        SettingCatalogue.All.Where(d => d.Group == group).ToDictionary(d => d.Key, d => (string?)Request.Form[d.Key].FirstOrDefault());
}
