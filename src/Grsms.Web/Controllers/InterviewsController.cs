using Grsms.Application.Common;
using Grsms.Application.Interviews;
using Grsms.Domain.Interviews;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Assessments, interviews and scorecards (Module 7): lists, scheduling, the session page with the scorecard,
/// reschedule / cancel / no-show, score validation by a Recruitment Officer, and a vacancy's results. Open to all GREL staff, because any staff member
/// can sit on a panel; what each person may see and do is decided by <see cref="InterviewService"/>.
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class InterviewsController(InterviewService interviews) : AppController
{
    /// <param name="filter">Tab "mine", "scorecards", "validate", "upcoming" or "all", plus the report filters.</param>
    public async Task<IActionResult> Index([FromQuery] InterviewFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        ViewBag.PendingScorecards = await interviews.CountMyPendingScorecardsAsync(ct);
        ViewBag.ToValidate = await interviews.CountAwaitingMyValidationAsync(ct);
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await interviews.SearchAsync(filter, ct));
    }

    public async Task<IActionResult> Schedule(int applicationId, CancellationToken ct)
    {
        var result = await interviews.GetScheduleFormAsync(applicationId, ct);
        if (result.Succeeded) return View(result.Value);
        Error(string.Join(" ", result.Errors));
        return RedirectToAction("Details", "Applications", new { id = applicationId });
    }

    [HttpPost]
    public async Task<IActionResult> Schedule(ScheduleFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await interviews.ScheduleAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Scheduled. The applicant and the panel have been emailed.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await interviews.FillScheduleFormAsync(form, ct);
        return View(form);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await interviews.GetDetailsAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>The scorecard form posts "ratings[criterionId]=1..5" for every criterion.</summary>
    [HttpPost]
    public Task<IActionResult> Scorecard(int id, Dictionary<int, int> ratings, PanelRecommendation? recommendation, string? strengths,
        string? concerns, CancellationToken ct) =>
        Act(id, () => interviews.SubmitScorecardAsync(id, ratings, recommendation, strengths, concerns, ct), "Scorecard saved. You can now see the other panel members' scorecards.");

    [HttpPost]
    public Task<IActionResult> Reschedule(int id, DateTime? startsAt, string? location, CancellationToken ct) =>
        Act(id, () => interviews.RescheduleAsync(id, startsAt, location, ct), "Rescheduled. The applicant and the panel have been emailed.");

    // ---- Score validation (9 Nov 2026): a Recruitment Officer checks the scores.

    /// <summary>Validates the scores, then goes on to the vacancy's "Ready to recommend" list: the Recruitment Officer chooses who to proceed with.</summary>
    [HttpPost]
    public async Task<IActionResult> Validate(int id, string? note, CancellationToken ct)
    {
        var result = await interviews.ValidateAsync(id, note, ct);
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors));
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Scores validated and final. Next step: tick the candidate(s) you want to proceed with to start the hiring process.");
        var details = await interviews.GetDetailsAsync(id, ct);
        return details is null
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction("Index", "Hiring", new { tab = "ready", vacancyId = details.VacancyId });
    }

    [HttpPost]
    public Task<IActionResult> ReturnScorecard(int id, string? panelUserId, string? reason, CancellationToken ct) =>
        Act(id, () => interviews.ReturnScorecardAsync(id, panelUserId, reason, ct), "Scorecard returned. The panel member has been emailed what to correct.");

    [HttpPost]
    public Task<IActionResult> Attendance(int id, string? panelUserId, PanelAttendance attendance, string? note, CancellationToken ct) =>
        Act(id, () => interviews.SetAttendanceAsync(id, panelUserId, attendance, note, ct),
            attendance == PanelAttendance.NotRecorded ? "Attendance cleared." : "Recorded. Their scorecard is no longer waited for.");

    [HttpPost]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct) =>
        Act(id, () => interviews.CancelAsync(id, reason, ct), "Cancelled. The applicant and the panel have been emailed.");

    [HttpPost]
    public Task<IActionResult> NoShow(int id, CancellationToken ct) =>
        Act(id, () => interviews.NoShowAsync(id, ct), "Recorded that the applicant did not attend.");

    /// <summary>Interview results of a vacancy: every interviewed applicant with their average score and the panel's recommendations.</summary>
    public async Task<IActionResult> Results(int id, CancellationToken ct)
    {
        var results = await interviews.GetResultsAsync(id, ct);
        if (results is null) return NotFound();
        ViewBag.VacancyId = id;
        ViewBag.VacancyNumber = results.Value.VacancyNumber;
        ViewBag.JobTitle = results.Value.JobTitle;
        return View(results.Value.Results);
    }

    private async Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }
}
