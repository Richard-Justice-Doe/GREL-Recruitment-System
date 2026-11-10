using Grsms.Application.Onboarding;
using Grsms.Domain.Onboarding;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 20: the twelve departmental induction areas: who owns each area's tasks, whether it is ticked by default when the
/// RO or HR Manager activates a new employee (Phase 27), and its tasks. For holders of "Maintain organisation data".
/// </summary>
[Authorize(Policy = Policies.MasterData)]
public class InductionAreasController(InductionAreaService areas) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Staff = new SelectList(await areas.StaffAsync(ct), "Value", "Text");
        return View(await areas.GetAllAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Save(InductionArea area, string? ownerUserId, bool isDefault, string? tasks, CancellationToken ct)
    {
        var result = await areas.SaveAsync(area, ownerUserId, isDefault, tasks, ct);
        if (result.Succeeded) Success("Induction area saved. It applies to employees activated from now on.");
        else Fail(result);
        return Redirect(Url.Action(nameof(Index)) + "#area-" + area);
    }
}
