using Grsms.Application.Common;
using Grsms.Application.Onboarding;
using Grsms.Domain.Onboarding;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Appointment letters and onboarding (Module 9): start onboarding for an accepted offer, the checklist for HR, the
/// department and IT, the first-day information and the appointment letter. Open to all staff, because checklist items
/// can be given to anyone (e.g. IT staff); what each person sees and may do is decided by <see cref="OnboardingService"/>.
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class OnboardingController(OnboardingService onboarding) : AppController
{
    /// <param name="filter">Tab "tasks" (my checklist items), "progress", "start" (accepted offers waiting) or "all", plus filters.</param>
    public async Task<IActionResult> Index([FromQuery] OnboardingFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        ViewBag.MyTasks = filter.Tab == "tasks" ? await onboarding.MyTasksAsync(ct) : null;
        ViewBag.ToStart = filter.Tab == "start" ? await onboarding.ToStartAsync(filter, ct) : null;
        return View(filter.Tab is "tasks" or "start" ? [] : await onboarding.SearchAsync(filter, ct));
    }

    /// <param name="id">The application id.</param>
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        if (await onboarding.IdForApplicationAsync(id, ct) is int existing) return RedirectToAction(nameof(Details), new { id = existing });
        var result = await onboarding.GetStartFormAsync(id, ct);
        if (result.Succeeded) return View(result.Value);
        Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Index), new { tab = "start" });
    }

    [HttpPost]
    public async Task<IActionResult> Start(StartOnboardingForm form, CancellationToken ct)
    {
        var result = await onboarding.StartAsync(form, ct);
        if (result.Succeeded)
        {
            Success("Onboarding started. The checklist owners have been emailed. Next: the HR Manager issues the appointment letter.");
            return RedirectToAction(nameof(Details), new { id = result.Value });
        }
        AddErrors(result);
        await onboarding.FillStartFormAsync(form, ct);
        return View(form);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await onboarding.GetAsync(id, ct);
        if (model is null) return NotFound();
        if (model.CanManage) ViewBag.Staff = new SelectList(await onboarding.StaffAsync(ct), "Value", "Text");
        return View(model);
    }

    /// <summary>The appointment letter as the new employee sees it (printable).</summary>
    public async Task<IActionResult> Letter(int id, CancellationToken ct)
    {
        var model = await onboarding.GetLetterPreviewAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.OnboardingId = id;
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> UpdateFirstDay(int id, DateOnly? startDate, TimeOnly? reportTime, string? reportLocation, string? contactPersonUserId,
        string? firstDayNotes, string? employeeNumber, CancellationToken ct) =>
        Act(id, () => onboarding.UpdateFirstDayAsync(id, startDate, reportTime, reportLocation, contactPersonUserId, firstDayNotes, employeeNumber, ct),
            "First-day details saved.");

    [HttpPost]
    public Task<IActionResult> IssueLetter(int id, string? terms, CancellationToken ct) =>
        Act(id, () => onboarding.IssueLetterAsync(id, terms, ct), "Appointment letter issued. The new employee has been emailed and can sign it in their portal.");

    [HttpPost]
    public Task<IActionResult> UpdateTask(int id, int taskId, OnboardingTaskStatus status, string? notes, CancellationToken ct) =>
        Act(id, () => onboarding.UpdateTaskAsync(taskId, status, notes, ct), "Checklist updated.");

    [HttpPost]
    public Task<IActionResult> AssignTask(int id, int taskId, string? userId, DateOnly? dueDate, CancellationToken ct) =>
        Act(id, () => onboarding.AssignTaskAsync(taskId, userId, dueDate, ct), "Task updated.");

    [HttpPost]
    public Task<IActionResult> AddTask(int id, OnboardingTeam team, string? title, DateOnly? dueDate, string? userId, CancellationToken ct) =>
        Act(id, () => onboarding.AddTaskAsync(id, team, title, dueDate, userId, ct), "Task added to the checklist.");

    [HttpPost]
    public Task<IActionResult> RemoveTask(int id, int taskId, CancellationToken ct) =>
        Act(id, () => onboarding.RemoveTaskAsync(taskId, ct), "Task removed.");

    [HttpPost]
    public Task<IActionResult> Complete(int id, CancellationToken ct) =>
        Act(id, () => onboarding.CompleteAsync(id, ct), "Onboarding completed. Welcome aboard!");

    [HttpPost]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct) =>
        Act(id, () => onboarding.CancelAsync(id, reason, ct), "Onboarding cancelled.");

    private async Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }
}
