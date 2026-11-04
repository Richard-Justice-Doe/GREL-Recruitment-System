using Grsms.Application.Applications;
using Grsms.Application.Common;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The staff side of applications (Module 5): list with report filters, the application page with the applicant's
/// details and documents, "start review" and "not successful". Rules live in <see cref="ApplicationService"/>.
/// </summary>
[Authorize(Policy = Policies.Applications)]
public class ApplicationsController(ApplicationService applications, Application.Shortlisting.ShortlistingService shortlisting,
    Application.Interviews.InterviewService interviews) : AppController
{
    /// <param name="filter">Tab "new", "review" or "all", plus the report filters.</param>
    public async Task<IActionResult> Index([FromQuery] ApplicationFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        ViewBag.Counts = await applications.CountsAsync(ct);
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await applications.SearchAsync(filter, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await applications.GetDetailsAsync(id, ct);
        if (model is null) return NotFound();

        // Phase 6: this application's row on the shortlisting board (recommendations and what the user may do).
        ViewBag.ShortlistRow = (await shortlisting.GetBoardAsync(model.VacancyId, ct))?.Rows.FirstOrDefault(r => r.ApplicationId == id);
        // Phase 7: can an interview / assessment be scheduled now?
        ViewBag.CanSchedule = await interviews.CanScheduleForAsync(id, ct);
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> StartReview(int id, CancellationToken ct) =>
        Act(id, () => applications.StartReviewAsync(id, ct), "Moved to 'Under review'. The applicant can see this on their tracker.");

    [HttpPost]
    public Task<IActionResult> NotSuccessful(int id, string? reason, CancellationToken ct) =>
        Act(id, () => applications.MarkNotSuccessfulAsync(id, reason, ct), "Marked not successful. The applicant has been emailed (without the reason).");

    /// <summary>Downloads an applicant's document (always as a download, never opened inside the site).</summary>
    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var file = await applications.OpenDocumentAsync(id, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    private async Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }
}
