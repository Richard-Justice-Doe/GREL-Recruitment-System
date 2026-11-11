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
public class DepartmentsController(IDepartmentService departments, IDivisionService divisions, IUserDirectory users) : AppController
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
        await LoadListsAsync(null, null, ct);
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
        await LoadListsAsync(form.HeadOfDepartmentUserId, form.DivisionId, ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await departments.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadListsAsync(form.HeadOfDepartmentUserId, form.DivisionId, ct);
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
        await LoadListsAsync(form.HeadOfDepartmentUserId, form.DivisionId, ct);
        return View("Form", form);
    }

    /// <summary>Activate/Deactivate button on the list and details pages.</summary>
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, string? returnTo, CancellationToken ct)
    {
        var result = await departments.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Department activated." : "Department deactivated.");
        else Fail(result);
        return returnTo == "details" ? RedirectToAction(nameof(Details), new { id }) : RedirectToAction(nameof(Index));
    }

    /// <summary>Drop-downs: users who hold the Head of Department role, and the active divisions.</summary>
    private async Task LoadListsAsync(string? selectedHod, int? selectedDivision, CancellationToken ct)
    {
        var hods = await users.GetActiveUsersInRoleAsync(Roles.HeadOfDepartment, ct);
        ViewBag.HodList = new SelectList(hods, nameof(LookupItem.Value), nameof(LookupItem.Text), selectedHod);

        // Active divisions, plus the department's current one if it has since been deactivated.
        var list = (await divisions.GetActiveLookupAsync(ct)).ToList();
        if (selectedDivision is int id && list.All(d => d.Value != id.ToString()))
            list.AddRange((await divisions.GetAllLookupAsync(ct)).Where(d => d.Value == id.ToString()));
        ViewBag.DivisionList = new SelectList(list, nameof(LookupItem.Value), nameof(LookupItem.Text), selectedDivision?.ToString());
    }
}
