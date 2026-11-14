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
public class InterviewsController(InterviewService interviews, Application.Vacancies.InterviewQuestionService questions) : AppController
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
        Fail(result);
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
            // 12 Nov 2026: someone is busy at that time; the page offers "Schedule anyway".
            ViewBag.Clash = result.Errors.Any(e => e.StartsWith(Application.Interviews.InterviewService.ClashPrefix));
        }
        await interviews.FillScheduleFormAsync(form, ct);
        return View(form);
    }

    public async Task<IActionResult> Details(int id, [FromServices] Application.Process.ProcessGuideService guide, CancellationToken ct)
    {
        var model = await interviews.GetDetailsAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Guide = await questions.ForInterviewAsync(model.VacancyId, ct); // docs/36 AI-2
        ViewBag.Process = await guide.ForApplicationAsync(model.ApplicationId, ct); // docs/37 CR5
        return View(model);
    }

    /// <summary>The scorecard form posts "ratings[criterionId]=1..5" for every criterion.</summary>
    [HttpPost]
    public Task<IActionResult> Scorecard(int id, Dictionary<int, int> ratings, Dictionary<int, string?>? comments, PanelRecommendation? recommendation, string? strengths,
        string? concerns, CancellationToken ct) =>
        Act(id, () => interviews.SubmitScorecardAsync(id, ratings, recommendation, strengths, concerns, ct, comments), "Scorecard saved. You can now see the other panel members' scorecards.");

    /// <summary>docs/35: a panel member accepts or declines the invitation.</summary>
    [HttpPost]
    public Task<IActionResult> Respond(int id, bool accept, string? reason, CancellationToken ct) =>
        Act(id, () => interviews.RespondAsync(id, accept, reason, ct),
            accept ? "Thank you: you have accepted the panel invitation." : "You have declined. The Recruitment Officer has been told.");

    /// <summary>docs/35: the RO or HR Manager replaces a panel member who declined.</summary>
    [HttpPost]
    public Task<IActionResult> ReplaceMember(int id, string? oldUserId, string? newUserId, string? reason, CancellationToken ct) =>
        Act(id, () => interviews.ReplacePanelMemberAsync(id, oldUserId, newUserId, ct, reason), "Panel member replaced. The new member has been invited.");

    /// <summary>docs/37 CR3: add a substitute panel member.</summary>
    [HttpPost]
    public Task<IActionResult> AddMember(int id, string? newUserId, string? reason, CancellationToken ct) =>
        Act(id, () => interviews.AddPanelMemberAsync(id, newUserId, reason, ct), "Substitute added and invited.");

    /// <summary>docs/37 CR3: send the invitation again to a member who has not replied.</summary>
    [HttpPost]
    public Task<IActionResult> ResendInvitation(int id, string? userId, CancellationToken ct) =>
        Act(id, () => interviews.ResendInvitationAsync(id, userId, ct), "Invitation sent again.");

    [HttpPost]
    public Task<IActionResult> Reschedule(int id, DateTime? startsAt, string? location, bool ignoreClashes, CancellationToken ct) =>
        Act(id, () => interviews.RescheduleAsync(id, startsAt, location, ct, ignoreClashes), "Rescheduled. The applicant and the panel have been emailed.");

    // ---- Score validation (9 Nov 2026): a Recruitment Officer checks the scores.

    /// <summary>Validates the scores, then goes on to the vacancy's "Ready to recommend" list: the Recruitment Officer chooses who to proceed with.</summary>
    [HttpPost]
    public async Task<IActionResult> Validate(int id, string? note, CancellationToken ct)
    {
        var result = await interviews.ValidateAsync(id, note, ct);
        if (!result.Succeeded)
        {
            Fail(result);
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

    private Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => RedirectToAction(nameof(Details), new { id }));
}
