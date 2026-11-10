using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 15: the ATS screens. The overview of every vacancy's screening, a vacancy's recruiter dashboard (top ranked,
/// held rejections, ranking table with score breakdowns, missing skill analysis), the HR decisions on held rejections,
/// and the ATS settings (Super Admin only). The rules live in <see cref="AtsService"/>.
/// </summary>
[Authorize(Policy = Policies.Applications)]
public class AtsController(AtsService ats) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await ats.OverviewAsync(ct));

    /// <param name="id">The vacancy id.</param>
    public async Task<IActionResult> Vacancy(int id, CancellationToken ct)
    {
        var board = await ats.GetBoardAsync(id, ct);
        return board is null ? NotFound() : View(board);
    }

    [HttpPost]
    public async Task<IActionResult> Rescreen(int vacancyId, CancellationToken ct)
    {
        var result = await ats.RescreenVacancyAsync(vacancyId, ct);
        if (result.Succeeded) Success($"The ATS re-screened {result.Value} application(s) with the current settings.");
        else Fail(result);
        return RedirectToAction(nameof(Vacancy), new { id = vacancyId });
    }

    [HttpPost]
    public Task<IActionResult> Confirm(int id, int vacancyId, string? note, string? returnTo, CancellationToken ct) =>
        Act(id, vacancyId, returnTo, () => ats.ConfirmRejectionAsync(id, note, ct), "Rejection confirmed. The applicant has been emailed.");

    [HttpPost]
    public Task<IActionResult> Override(int id, int vacancyId, string? reason, string? returnTo, CancellationToken ct) =>
        Act(id, vacancyId, returnTo, () => ats.OverrideAsync(id, reason, ct), "ATS decision overridden: the application is now under review.");

    [HttpPost]
    public async Task<IActionResult> ConfirmAll(int vacancyId, CancellationToken ct)
    {
        var result = await ats.ConfirmAllAsync(vacancyId, ct);
        if (result.Succeeded) Success($"{result.Value} ATS rejection(s) confirmed. The applicants have been emailed.");
        else Fail(result);
        return RedirectToAction(nameof(Vacancy), new { id = vacancyId });
    }

    // ===================================================================== Settings (Super Admin)

    [Authorize(Policy = Policies.SystemSettings)] // the Admin sees the settings read-only; saving needs AtsConfiguration (below)
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        ViewBag.CanManage = (await HttpContext.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(User, Policies.AtsConfiguration)).Succeeded;
        return View(await ats.GetSettingsFormAsync(ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.AtsConfiguration)]
    public async Task<IActionResult> Settings(AtsSettingsFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await ats.SaveSettingsAsync(form, ct);
            if (result.Succeeded)
            {
                Success("ATS settings saved. They apply to applications screened from now on; use Re-run screening on a vacancy to apply them to its applications.");
                return RedirectToAction(nameof(Settings));
            }
            AddErrors(result);
        }
        var current = await ats.GetSettingsFormAsync(ct);
        form.AiConfigured = current.AiConfigured;
        form.UpdatedAt = current.UpdatedAt;
        form.UpdatedBy = current.UpdatedBy;
        ViewBag.CanManage = true;
        return View(form);
    }

    private Task<IActionResult> Act(int applicationId, int vacancyId, string? returnTo, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => returnTo == "application" ? RedirectToAction("Details", "Applications", new { id = applicationId }) : RedirectToAction(nameof(Vacancy), new { id = vacancyId }));
}
