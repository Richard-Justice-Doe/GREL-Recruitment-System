using Grsms.Application.Common;
using Grsms.Application.Probation;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 21: probation management for HR and Heads of Department (list, the probation page, initial meeting, assessments,
/// the HR Manager's decision, the confirmation letter). Thin: every rule is in <see cref="ProbationService"/>.
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class ProbationController(ProbationService probation) : AppController
{
    /// <param name="filter">Tab "due", "active", "decision" or "all", and the department.</param>
    public async Task<IActionResult> Index([FromQuery] ProbationFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        await LoadFilterListsAsync(null, filter.DepartmentId, ct);
        return View(await probation.SearchAsync(filter, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await probation.GetAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public Task<IActionResult> InitialMeeting(int id, InitialMeetingForm form, CancellationToken ct) =>
        Act(id, () => probation.RecordInitialMeetingAsync(id, form, ct), "Initial meeting recorded and signed. The employee has been asked to sign it.");

    [HttpPost]
    public Task<IActionResult> Assess(int id, AssessmentForm form, CancellationToken ct) =>
        Act(id, () => probation.AssessAsync(id, form, ct), "Assessment recorded and filed in the personnel file.");

    [HttpPost]
    [Authorize(Policy = Policies.DecideProbation)]
    public Task<IActionResult> HrDecide(int id, bool approve, string? comments, CancellationToken ct) =>
        Act(id, () => probation.HrDecideAsync(id, approve, comments, ct), approve ? "Decision recorded. The employee and the HOD have been emailed." : "Returned to the Head of Department.");

    /// <summary>The confirmation letter (printable; "Save as PDF" from the print dialog).</summary>
    public async Task<IActionResult> Letter(int id, CancellationToken ct)
    {
        var model = await probation.GetConfirmationLetterAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.BackUrl = Url.Action(nameof(Details), new { id });
        return View("~/Views/Probation/Letter.cshtml", model);
    }

    private Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => RedirectToAction(nameof(Details), new { id }));
}
