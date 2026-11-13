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
public class HiringController(HiringService hiring, Application.Onboarding.OnboardingService onboarding, Application.Ai.AiSummaryService summaries) : AppController
{
    /// <param name="filter">Tab "ready" (validated, to recommend), "awaiting" (my decision), "approved", "offers" or "all", plus the report filters.</param>
    public async Task<IActionResult> Index([FromQuery] HiringFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        ViewBag.Ready = await hiring.ListReadyToRecommendAsync(ct: ct); // 9 Nov 2026: candidates with validated interview scores
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(filter.Tab == "ready" ? [] : await hiring.SearchAsync(filter, ct));
    }

    /// <param name="id">The application id.</param>
    public async Task<IActionResult> Case(int id, CancellationToken ct)
    {
        var model = await hiring.GetCaseAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.OnboardingId = await onboarding.IdForApplicationAsync(id, ct); // Phase 9
        ViewBag.AiSummary = Helpers.AiInsightCard.Interview(await summaries.GetInterviewPanelAsync(id, ct), id); // docs/36 AI-5
        return View(model);
    }

    /// <summary>docs/36 AI-5: the AI interview summary (validated scores only, advisory).</summary>
    [HttpPost]
    public Task<IActionResult> GenerateInterviewSummary(int id, CancellationToken ct) =>
        Act(id, () => summaries.GenerateInterviewSummaryAsync(id, ct), "Interview summary written. It is advice only.");

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

    /// <summary>
    /// Proceed with the ticked candidates (9 Nov 2026): the hiring process starts for each (draft recommendation + pending
    /// background checks). From one hiring case it returns to that case; from the "Ready to recommend" list, to the first one.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Proceed(int[] applicationIds, int? vacancyId, CancellationToken ct)
    {
        var result = await hiring.ProceedAsync(applicationIds, ct);
        if (!result.Succeeded)
        {
            Fail(result);
            return applicationIds.Length == 1
                ? RedirectToAction(nameof(Case), new { id = applicationIds[0] })
                : RedirectToAction(nameof(Index), new { tab = "ready", vacancyId });
        }
        Success(result.Value == 1
            ? "Hiring process started: a draft recommendation and the background checks are ready. Review the draft and submit it to the Head of Department."
            : $"Hiring process started for {result.Value} candidates. Review each draft recommendation and submit it to the Head of Department.");
        return applicationIds.Length == 1
            ? RedirectToAction(nameof(Case), new { id = applicationIds[0] })
            : RedirectToAction(nameof(Index), new { tab = "awaiting" });
    }

    [HttpPost]
    public Task<IActionResult> DiscardDraft(int id, CancellationToken ct) =>
        Act(id, () => hiring.DiscardDraftAsync(id, ct), "Draft discarded. The candidate is back in \"Ready to recommend\".");

    /// <param name="save">"Save draft" was clicked (13 Nov 2026); otherwise "Send offer".</param>
    [HttpPost]
    public Task<IActionResult> SendOffer(int id, decimal? salary, DateOnly? startDate, DateOnly? expiresOn, string? terms, bool save, CancellationToken ct) =>
        save
            ? Act(id, async () => Plain(await hiring.SaveOfferDraftAsync(id, salary, startDate, expiresOn, terms ?? "", ct)),
                "Draft saved. Preview the letter, then send it when ready.")
            : Act(id, async () => Plain(await hiring.SendOfferAsync(id, salary, startDate, expiresOn, terms ?? "", ct)),
                "Offer sent. The candidate has been emailed and can accept or decline in their portal.");

    /// <summary>13 Nov 2026: the offer letter as the candidate will see it (the draft before sending, or the offer sent).</summary>
    public async Task<IActionResult> OfferLetter(int id, CancellationToken ct)
    {
        var letter = await hiring.GetOfferLetterAsync(id, ct);
        if (letter is null) return NotFound();
        ViewBag.ApplicationId = id;
        return View(letter);
    }

    /// <summary>13 Nov 2026: the offer letter as a PDF (a draft is marked DRAFT).</summary>
    public async Task<IActionResult> OfferPdf(int id, [FromServices] Helpers.LetterPdfBuilder pdf, CancellationToken ct)
    {
        var letter = await hiring.GetOfferLetterAsync(id, ct);
        if (letter is null) return NotFound();
        var draft = letter.Status == Domain.Hiring.OfferStatus.Draft;
        var bytes = await pdf.BuildAsync(ControllerContext, $"Offer {letter.Number}", "_OfferLetter", letter, draft, ct);
        if (bytes is null)
        {
            Error("The PDF could not be made on this server. Use Print instead (choose \"Save as PDF\").");
            return RedirectToAction(nameof(OfferLetter), new { id });
        }
        return File(bytes, "application/pdf", $"{letter.Number}{(draft ? "-DRAFT" : "")}.pdf");
    }

    [HttpPost]
    public Task<IActionResult> WithdrawOffer(int id, string? reason, CancellationToken ct) =>
        Act(id, () => hiring.WithdrawOfferAsync(id, reason, ct), "Offer withdrawn. The candidate has been told.");

    private static Result Plain<T>(Result<T> r) => r.Succeeded ? Result.Success() : Result.Failure([.. r.Errors]);

    private Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => RedirectToAction(nameof(Case), new { id }));
}
