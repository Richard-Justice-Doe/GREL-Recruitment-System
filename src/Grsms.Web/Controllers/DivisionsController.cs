using Grsms.Application.Organization;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Division Master Data screens (HR Administrator only): list, add, edit, activate/deactivate.
/// Divisions (Corporate, Operations) group departments. Rules live in <see cref="IDivisionService"/>.
/// </summary>
[Authorize(Policy = Policies.MasterData)]
public class DivisionsController(IDivisionService divisions) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await divisions.GetAllAsync(ct));

    public IActionResult Create() => View("Form", new DivisionFormDto());

    [HttpPost]
    public async Task<IActionResult> Create(DivisionFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await divisions.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Division '{form.Name}' was added.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await divisions.GetForEditAsync(id, ct);
        return form is null ? NotFound() : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DivisionFormDto form, CancellationToken ct)
    {
        form.Id = id; // the id in the address wins over anything posted in the form
        if (ModelState.IsValid)
        {
            var result = await divisions.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Division '{form.Name}' was saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    /// <summary>Activate/Deactivate button on the list.</summary>
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await divisions.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Division activated." : "Division deactivated.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }
}
