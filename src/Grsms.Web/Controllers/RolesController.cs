using Grsms.Application.Users;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 14: Roles &amp; permissions (Super Admin only). One grid of roles × permissions to tick, plus adding, renaming
/// and deleting custom roles. The rules live in <see cref="IRoleAdminService"/>; every change is in the audit trail.
/// </summary>
[Authorize(Policy = Policies.RolesAndPermissions)]
public class RolesController(IRoleAdminService roles) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await roles.GetAllAsync(ct));

    /// <param name="roleNames">Every role column that was on the page (so unticked boxes mean "remove").</param>
    /// <param name="grant">The ticked boxes, each "role name|permission".</param>
    [HttpPost]
    public async Task<IActionResult> Save(List<string> roleNames, List<string> grant, CancellationToken ct)
    {
        // Empty fields arrive as nulls: ignore them rather than fail.
        var ticked = grant.OfType<string>().Select(g => g.Split('|', 2)).Where(p => p.Length == 2).ToList();
        var grants = roleNames.OfType<string>().Where(r => r != Roles.SuperAdmin).Distinct().ToDictionary(
            r => r,
            r => (IReadOnlyCollection<string>)ticked.Where(p => p[0] == r).Select(p => p[1]).ToList());
        var result = await roles.SavePermissionsAsync(grants, ct);
        if (!result.Succeeded) Error(string.Join(" ", result.Errors));
        else if (result.Value is (0, 0)) Success("No changes to save.");
        else Success($"Permissions saved: {result.Value.Added} given, {result.Value.Removed} taken away. They apply straight away.");
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Create() => View("Form", new RoleFormDto());

    [HttpPost]
    public async Task<IActionResult> Create(RoleFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await roles.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Role \"{form.Name}\" added. Tick its permissions below, then give it to users on Users & roles.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var form = await roles.GetForEditAsync(id, ct);
        return form is null ? NotFound() : View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, RoleFormDto form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await roles.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success($"Role \"{form.Name}\" saved.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View("Form", form);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var result = await roles.DeleteAsync(id, ct);
        if (result.Succeeded) Success("Role deleted.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index));
    }
}
