using Grsms.Application.Organization;
using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Department Master Data screens (HR Administrator only): list, details, add, edit, activate/deactivate.
/// The controller stays thin: every rule lives in <see cref="IDepartmentService"/>.
/// </summary>
[Authorize(Policy = Policies.MasterData)]
public class DepartmentsController(IDepartmentService departments, IUserDirectory users) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await departments.GetAllAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await departments.GetDetailsAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadHodListAsync(null, ct);
        return View("Form", new DepartmentFormDto());
    }

    [HttpPost]
    public async Task<IActionResult> Create(DepartmentFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await departments.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Department '{form.Name}' was added.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await LoadHodListAsync(form.HeadOfDepartmentUserId, ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await departments.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadHodListAsync(form.HeadOfDepartmentUserId, ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, DepartmentFormDto form, CancellationToken ct)
    {
        form.Id = id; // the id in the address wins over anything posted in the form
        if (ModelState.IsValid)
        {
            var result = await departments.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Department '{form.Name}' was saved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadHodListAsync(form.HeadOfDepartmentUserId, ct);
        return View("Form", form);
    }

    /// <summary>Activate/Deactivate button on the list and details pages.</summary>
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, string? returnTo, CancellationToken ct)
    {
        var result = await departments.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Department activated." : "Department deactivated.");
        else Error(string.Join(" ", result.Errors));
        return returnTo == "details" ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Index));
    }

    /// <summary>Drop-down of users who hold the Head of Department role.</summary>
    private async Task LoadHodListAsync(string? selected, CancellationToken ct)
    {
        var hods = await users.GetActiveUsersInRoleAsync(Roles.HeadOfDepartment, ct);
        ViewBag.HodList = new SelectList(hods, nameof(LookupItem.Value), nameof(LookupItem.Text), selected);
    }
}
