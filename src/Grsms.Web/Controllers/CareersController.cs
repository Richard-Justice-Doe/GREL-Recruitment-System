using Grsms.Application.Adverts;
using Grsms.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The careers pages: the jobs GRSMS publishes itself.
///   * Anyone (no sign-in)  : adverts running on the <b>Company website</b> channel. Link GREL's website here.
///   * Signed-in GREL staff : the same, plus adverts running on the <b>Internal portal</b> channel (staff-only jobs).
/// Signed-in people apply from the job page (Phase 5); visitors are asked to sign in or create a candidate account.
/// </summary>
[AllowAnonymous]
public class CareersController(AdvertService adverts, Application.Applications.ApplicationService applications) : Controller
{
    private bool IsStaff => User.Identity?.IsAuthenticated == true && Roles.Staff.Any(User.IsInRole);

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.IsStaff = IsStaff;
        return View(await adverts.GetRunningJobsAsync(IsStaff, ct));
    }

    /// <param name="id">The advert number, e.g. ADV-2026-0001.</param>
    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var job = await adverts.GetRunningJobAsync(id, IsStaff, ct);
        if (job is null) return View("NotFound");
        ViewBag.IsStaff = IsStaff;
        ViewBag.ApplyStatus = await applications.GetApplyStatusAsync(id, ct); // (CanApply, Reason, ExistingApplicationId)
        return View(job);
    }
}
