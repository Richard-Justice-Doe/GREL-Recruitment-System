using Grsms.Application.Common;
using Grsms.Application.Shortlisting;
using Grsms.Domain.Applications;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// CV screening and shortlisting (Module 6): a vacancy's ranked board, re-screening, recruiter recommendations,
/// sending them to the Head of Department, the HOD's view, and the final shortlist. Rules live in <see cref="ShortlistingService"/>.
/// Actions return to the board, or to the application page when posted from there (<c>returnTo=application</c>).
/// </summary>
[Authorize(Policy = Policies.Applications)]
public class ShortlistingController(ShortlistingService shortlisting) : AppController
{
    /// <param name="id">The vacancy id.</param>
    public async Task<IActionResult> Vacancy(int id, CancellationToken ct)
    {
        var board = await shortlisting.GetBoardAsync(id, ct);
        return board is null ? NotFound() : View(board);
    }

    [HttpPost]
    public async Task<IActionResult> Screen(int vacancyId, CancellationToken ct)
    {
        var result = await shortlisting.ScreenVacancyAsync(vacancyId, ct);
        if (result.Succeeded) Success($"Screening re-run for {result.Value} application(s).");
        else Fail(result);
        return RedirectToAction(nameof(Vacancy), new { id = vacancyId });
    }

    [HttpPost]
    public async Task<IActionResult> SendToHod(int vacancyId, CancellationToken ct)
    {
        var result = await shortlisting.SendToHodAsync(vacancyId, ct);
        if (result.Succeeded) Success($"The Head of Department has been asked for their view on {result.Value} recommendation(s).");
        else Fail(result);
        return RedirectToAction(nameof(Vacancy), new { id = vacancyId });
    }

    [HttpPost]
    public Task<IActionResult> Recommend(int id, int vacancyId, ShortlistRecommendation recommendation, string? note, string? returnTo, CancellationToken ct) =>
        Act(id, vacancyId, returnTo, () => shortlisting.RecommendAsync(id, recommendation, note, ct), "Recommendation saved.");

    [HttpPost]
    public Task<IActionResult> HodReview(int id, int vacancyId, ShortlistRecommendation view, string? note, string? returnTo, CancellationToken ct) =>
        Act(id, vacancyId, returnTo, () => shortlisting.HodReviewAsync(id, view, note, ct), "Your view has been recorded.");

    [HttpPost]
    public Task<IActionResult> Shortlist(int id, int vacancyId, string? reason, string? returnTo, CancellationToken ct) =>
        Act(id, vacancyId, returnTo, () => shortlisting.ShortlistAsync(id, reason, ct), "Shortlisted. The applicant has been emailed.");

    private Task<IActionResult> Act(int applicationId, int vacancyId, string? returnTo, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => returnTo == "application" ? RedirectToAction("Details", "Applications", new { id = applicationId }) : RedirectToAction(nameof(Vacancy), new { id = vacancyId }));
}
