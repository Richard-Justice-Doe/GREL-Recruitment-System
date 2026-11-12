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
public class OnboardingController(OnboardingService onboarding, DocumentVerificationService documents, PersonalRecordService personalRecords) : AppController
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
    [Authorize(Policy = Policies.ManageOnboarding)]
    public async Task<IActionResult> Start(int id, CancellationToken ct)
    {
        if (await onboarding.IdForApplicationAsync(id, ct) is int existing) return RedirectToAction(nameof(Details), new { id = existing });
        var result = await onboarding.GetStartFormAsync(id, ct);
        if (result.Succeeded) return View(result.Value);
        Fail(result);
        return RedirectToAction(nameof(Index), new { tab = "start" });
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public async Task<IActionResult> Start(StartOnboardingForm form, CancellationToken ct)
    {
        var result = await onboarding.StartAsync(form, ct);
        if (result.Succeeded)
        {
            Success("Onboarding started. The new employee has been welcomed and can upload their documents now. Next: issue the appointment letter.");
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
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> UpdateFirstDay(int id, DateOnly? startDate, TimeOnly? reportTime, string? reportLocation, string? contactPersonUserId,
        string? firstDayNotes, string? employeeNumber, CancellationToken ct) =>
        Act(id, () => onboarding.UpdateFirstDayAsync(id, startDate, reportTime, reportLocation, contactPersonUserId, firstDayNotes, employeeNumber, ct),
            "First-day details saved.");

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> IssueLetter(int id, string? terms, CancellationToken ct) =>
        Act(id, () => onboarding.IssueLetterAsync(id, terms, ct), "Appointment letter issued. The new employee has been emailed and can sign it in their portal.");

    [HttpPost]
    public Task<IActionResult> UpdateTask(int id, int taskId, OnboardingTaskStatus status, string? notes, CancellationToken ct) =>
        Act(id, () => onboarding.UpdateTaskAsync(taskId, status, notes, ct), "Checklist updated.");

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> AssignTask(int id, int taskId, string? userId, DateOnly? dueDate, CancellationToken ct) =>
        Act(id, () => onboarding.AssignTaskAsync(taskId, userId, dueDate, ct), "Task updated.");

    /// <param name="area">Phase 20: the induction area ("" = the new employee's own department).</param>
    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> AddTask(int id, OnboardingTeam team, string? title, DateOnly? dueDate, string? userId, InductionArea? area,
        string? description, CancellationToken ct) =>
        Act(id, () => onboarding.AddTaskAsync(id, team, title, dueDate, userId, ct, area, description), "Task added to the checklist.");

    /// <summary>Phase 20: opens the personnel file of an onboarding completed before personnel files existed.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.PersonnelFiles)]
    public async Task<IActionResult> OpenPersonnelFile(int id, CancellationToken ct)
    {
        var result = await onboarding.OpenPersonnelFileAsync(id, ct);
        if (result.Succeeded)
        {
            Success("Personnel file opened.");
            return RedirectToAction("Details", "PersonnelFiles", new { id = result.Value });
        }
        Fail(result);
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Phase 20: HR confirms a task someone else marked done.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> VerifyTask(int id, int taskId, CancellationToken ct) =>
        Act(id, () => onboarding.VerifyTaskAsync(taskId, ct), "Task verified.");

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> RemoveTask(int id, int taskId, CancellationToken ct) =>
        Act(id, () => onboarding.RemoveTaskAsync(taskId, ct), "Task removed.");

    // ================================================================= Phase 27: workflow stages
    // Before activation the stage moves by itself as documents are uploaded and verified; there is no "submit" or HR approval.

    [HttpPost]
    [Authorize(Policy = Policies.VerifyOnboardingDocuments)]
    public Task<IActionResult> RequestCorrection(int id, string? message, CancellationToken ct) =>
        Act(id, () => onboarding.RequestCorrectionAsync(id, message, ct), "The new employee has been emailed what to correct.");

    /// <param name="education">The codes of the education certificates ticked.</param>
    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> SetRequirements(int id, bool isExpatriate, string[]? education, CancellationToken ct) =>
        Act(id, () => documents.SetRequirementsAsync(id, isExpatriate, education ?? [], ct), "Documents required updated.");

    /// <param name="areas">The departmental induction areas whose tasks are created.</param>
    [HttpPost]
    [Authorize(Policy = Policies.CompleteOnboarding)]
    public Task<IActionResult> Activate(int id, string? staffNumber, InductionArea[]? areas, CancellationToken ct) =>
        Act(id, () => onboarding.ActivateAsync(id, staffNumber, ct, areas ?? []),
            "Employee activated: the account is now a GREL staff account and the probation has started. The induction task owners have been emailed.");

    [HttpPost]
    [Authorize(Policy = Policies.CompleteOnboarding)]
    public Task<IActionResult> CompleteInduction(int id, CancellationToken ct) =>
        Act(id, () => onboarding.CompleteInductionAsync(id, ct), "Induction closed: the onboarding is complete and filed in the personnel file.");

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> SaveNotes(int id, string? notes, CancellationToken ct) =>
        Act(id, () => onboarding.SaveNotesAsync(id, notes, ct), "Notes saved.");

    /// <summary>Phase 19: the new employee's Personal Record Declaration, read-only (HR only).</summary>
    [Authorize(Policy = Policies.ViewOnboarding)]
    public async Task<IActionResult> PersonalRecord(int id, CancellationToken ct)
    {
        var model = await personalRecords.GetForHrAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    // ================================================================= New-hire documents: review only
    // 11 Nov 2026 (docs/29, change 3): staff never upload, replace, remove or mark documents not applicable for the new
    // employee; they only open, review, verify and reject them. The new employee does all of that in My onboarding.

    [HttpPost]
    [Authorize(Policy = Policies.VerifyOnboardingDocuments)]
    public Task<IActionResult> VerifyDocument(int id, int documentId, CancellationToken ct) =>
        ActAt(id, documentId, () => documents.VerifyAsync(documentId, ct), "Document verified.");

    [HttpPost]
    [Authorize(Policy = Policies.VerifyOnboardingDocuments)]
    public Task<IActionResult> StartReview(int id, int documentId, CancellationToken ct) =>
        ActAt(id, documentId, () => documents.StartReviewAsync(documentId, ct), "Review started: the new employee cannot change this document until you verify or reject it.");

    /// <param name="reasonCode">The reason from the list; <paramref name="reason"/> is the RO's note (required for "Other").</param>
    [HttpPost]
    [Authorize(Policy = Policies.VerifyOnboardingDocuments)]
    public Task<IActionResult> RejectDocument(int id, int documentId, DocumentRejectionReason? reasonCode, string? reason, CancellationToken ct) =>
        ActAt(id, documentId, () => documents.RejectAsync(documentId, reasonCode, reason, ct), "Document rejected. The new employee has been emailed the reason.");

    /// <summary>Downloads a new-hire document file (onboarding.view; always as a download, never shown inside the site; every opening is recorded).</summary>
    [Authorize(Policy = Policies.ViewOnboarding)]
    public async Task<IActionResult> DownloadDocument(int id, int documentId, int attachmentId, CancellationToken ct)
    {
        var file = await documents.OpenFileAsync(documentId, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Phase 27 stage C: more time for the documents, once.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> ExtendDeadline(int id, DateOnly? newDate, string? reason, CancellationToken ct) =>
        Act(id, () => onboarding.ExtendDocumentDeadlineAsync(id, newDate, reason, ct), "Deadline extended. The new employee has been emailed.");

    [HttpPost]
    [Authorize(Policy = Policies.ManageOnboarding)]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct) =>
        Act(id, () => onboarding.CancelAsync(id, reason, ct), "Onboarding cancelled.");

    private Task<IActionResult> Act(int id, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => RedirectToAction(nameof(Details), new { id }));

    /// <summary>Usability U3: after acting on a document, come back to that document (not the top of the page).</summary>
    private Task<IActionResult> ActAt(int id, int documentId, Func<Task<Result>> action, string successMessage) =>
        RunAsync(action, successMessage, () => RedirectToAction(nameof(Details), null, new { id }, $"doc-{documentId}"));
}
