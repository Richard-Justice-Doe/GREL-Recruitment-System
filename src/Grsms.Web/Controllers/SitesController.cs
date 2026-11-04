using Grsms.Application.Organization;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>Site (work location) screens. HR Administrator only.</summary>
[Authorize(Policy = Policies.MasterData)]
public class SitesController(ISiteService sites) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await sites.GetAllAsync(ct));

    public IActionResult Create() => View("Form", new SiteFormDto());

    [HttpPost]
    public async Task<IActionResult> Create(SiteFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await sites.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Site '{form.Name}' was added.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await sites.GetForEditAsync(id, ct);
        return form is null ? NotFound() : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, SiteFormDto form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await sites.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Site '{form.Name}' was saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await sites.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Site activated." : "Site deactivated.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index));
    }
}
