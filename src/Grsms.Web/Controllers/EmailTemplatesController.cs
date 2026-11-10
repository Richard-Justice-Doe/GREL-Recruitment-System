using Grsms.Application.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

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
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }
}
