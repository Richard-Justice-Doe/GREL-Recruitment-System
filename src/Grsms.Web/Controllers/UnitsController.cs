using Grsms.Application.Organization;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>Unit screens (sub-sections of departments). HR Administrator only.</summary>
[Authorize(Policy = Policies.MasterData)]
public class UnitsController(IUnitService units, IDepartmentService departments) : AppController
{
    /// <summary>All units, or only one department's when <paramref name="departmentId"/> is given.</summary>
    public async Task<IActionResult> Index(int? departmentId, CancellationToken ct)
    {
        await LoadDepartmentListAsync(departmentId, ct);
        ViewBag.DepartmentId = departmentId;
        return View(await units.GetAllAsync(departmentId, ct));
    }

    /// <summary>Opened from a department's details page, the department is pre-selected.</summary>
    public async Task<IActionResult> Create(int? departmentId, CancellationToken ct)
    {
        await LoadDepartmentListAsync(departmentId, ct);
        return View("Form", new UnitFormDto { DepartmentId = departmentId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(UnitFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await units.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Unit '{form.Name}' was added.");
                return RedirectToAction("Details", "Departments", new { id = form.DepartmentId });
            }
            AddErrors(result);
        }
        await LoadDepartmentListAsync(form.DepartmentId, ct);
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await units.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadDepartmentListAsync(form.DepartmentId, ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, UnitFormDto form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await units.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Unit '{form.Name}' was saved.");
                return RedirectToAction("Details", "Departments", new { id = form.DepartmentId });
            }
            AddErrors(result);
        }
        await LoadDepartmentListAsync(form.DepartmentId, ct);
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, int? departmentId, CancellationToken ct)
    {
        var result = await units.SetActiveAsync(id, active, ct);
        if (result.Succeeded) Success(active ? "Unit activated." : "Unit deactivated.");
        else Error(string.Join(" ", result.Errors));

        // Go back to wherever the button was pressed.
        return departmentId is null
            ? RedirectToAction(nameof(Index))
            : RedirectToAction("Details", "Departments", new { id = departmentId });
    }

    /// <summary>
    /// Department drop-down. Active departments only, plus the currently selected one even if inactive,
    /// so editing an old unit does not silently change its department.
    /// </summary>
    private async Task LoadDepartmentListAsync(int? selected, CancellationToken ct)
    {
        var items = (await departments.GetActiveLookupAsync(ct)).ToList();
        if (selected is int id && items.All(i => i.Value != id.ToString()))
        {
            var d = await departments.GetDetailsAsync(id, ct);
            if (d is not null) items.Add(new LookupItem(id.ToString(), d.Name + " (inactive)"));
        }
        ViewBag.DepartmentList = new SelectList(items, nameof(LookupItem.Value), nameof(LookupItem.Text), selected?.ToString());
    }
}
