using Grsms.Application.Common;
using Grsms.Application.Hiring;
using Grsms.Domain.Hiring;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Background checks, the hiring recommendation with its approvals (HOD → HR Manager → MD), and offers (Module 8).
/// Rules live in <see cref="HiringService"/>.
/// </summary>
[Authorize(Policy = Policies.Applications)]
public class HiringController(HiringService hiring, Application.Onboarding.OnboardingService onboarding) : AppController
{
    /// <param name="filter">Tab "awaiting" (my decision), "approved", "offers" or "all", plus the report filters.</param>
    public async Task<IActionResult> Index([FromQuery] HiringFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await hiring.SearchAsync(filter, ct));
    }

    /// <param name="id">The application id.</param>
    public async Task<IActionResult> Case(int id, CancellationToken ct)
    {
        var model = await hiring.GetCaseAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.OnboardingId = await onboarding.IdForApplicationAsync(id, ct); // Phase 9
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> AddCheck(int id, CheckType type, string? subject, CancellationToken ct) =>
        Act(id, () => hiring.AddCheckAsync(id, type, subject, ct), "Background check added as pending.");

    [HttpPost]
    public Task<IActionResult> CompleteCheck(int id, int checkId, CheckStatus status, string? notes, CancellationToken ct) =>
        Act(id, () => hiring.CompleteCheckAsync(checkId, status, notes, ct), "Check outcome recorded.");

    [HttpPost]
    public Task<IActionResult> Recommend(int id, decimal? salary, DateOnly? startDate, string? justification, CancellationToken ct) =>
        Act(id, async () => Plain(await hiring.RecommendAsync(id, salary, startDate, justification, ct)),
            "Hiring recommendation sent to the Head of Department for endorsement.");

    [HttpPost]
    public Task<IActionResult> Decide(int id, HiringDecision decision, string? comment, CancellationToken ct) =>
        Act(id, () => hiring.DecideAsync(id, decision, comment, ct), "Decision recorded. The next person has been notified.");

    [HttpPost]
    public Task<IActionResult> WithdrawRecommendation(int id, string? reason, CancellationToken ct) =>
        Act(id, () => hiring.WithdrawRecommendationAsync(id, reason, ct), "Recommendation withdrawn.");

    [HttpPost]
    public Task<IActionResult> SendOffer(int id, decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? terms, CancellationToken ct) =>
        Act(id, async () => Plain(await hiring.SendOfferAsync(id, salary, startDate, expiresOn, terms, ct)),
            "Offer sent. The candidate has been emailed and can accept or decline in their portal.");

    [HttpPost]
    public Task<IActionResult> WithdrawOffer(int id, string? reason, CancellationToken ct) =>
        Act(id, () => hiring.WithdrawOfferAsync(id, reason, ct), "Offer withdrawn. The candidate has been told.");

    private static Result Plain<T>(Result<T> r) => r.Succeeded ? Result.Success() : Result.Failure([.. r.Errors]);

    private async Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Case), new { id });
    }
}
