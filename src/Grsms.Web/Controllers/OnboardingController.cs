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
    /// <summary>Upload limit for document files, slightly above the file limit to allow for form overhead.</summary>
    private const long UploadRequestLimitBytes = 25 * 1024 * 1024;

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

    /// <param name="area">Phase 20: the induction area ("" = the new employee's own department).</param>
    [HttpPost]
    public Task<IActionResult> AddTask(int id, OnboardingTeam team, string? title, DateOnly? dueDate, string? userId, InductionArea? area,
        string? description, CancellationToken ct) =>
        Act(id, () => onboarding.AddTaskAsync(id, team, title, dueDate, userId, ct, area, description), "Task added to the checklist.");

    /// <summary>Phase 20: opens the personnel file of an onboarding completed before personnel files existed.</summary>
    [HttpPost]
    public async Task<IActionResult> OpenPersonnelFile(int id, CancellationToken ct)
    {
        var result = await onboarding.OpenPersonnelFileAsync(id, ct);
        if (result.Succeeded)
        {
            Success("Personnel file opened.");
            return RedirectToAction("Details", "PersonnelFiles", new { id = result.Value });
        }
        Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Phase 20: HR confirms a task someone else marked done.</summary>
    [HttpPost]
    public Task<IActionResult> VerifyTask(int id, int taskId, CancellationToken ct) =>
        Act(id, () => onboarding.VerifyTaskAsync(taskId, ct), "Task verified.");

    [HttpPost]
    public Task<IActionResult> RemoveTask(int id, int taskId, CancellationToken ct) =>
        Act(id, () => onboarding.RemoveTaskAsync(taskId, ct), "Task removed.");

    // ================================================================= Phase 27: workflow stages
    // Before activation the stage moves by itself as documents are uploaded and verified; there is no "submit" or HR approval.

    [HttpPost]
    public Task<IActionResult> RequestCorrection(int id, string? message, CancellationToken ct) =>
        Act(id, () => onboarding.RequestCorrectionAsync(id, message, ct), "The new employee has been emailed what to correct.");

    /// <param name="education">The codes of the education certificates ticked.</param>
    [HttpPost]
    public Task<IActionResult> SetRequirements(int id, bool isExpatriate, string[]? education, CancellationToken ct) =>
        Act(id, () => documents.SetRequirementsAsync(id, isExpatriate, education ?? [], ct), "Documents required updated.");

    /// <param name="areas">The departmental induction areas whose tasks are created.</param>
    [HttpPost]
    public Task<IActionResult> Activate(int id, string? staffNumber, InductionArea[]? areas, CancellationToken ct) =>
        Act(id, () => onboarding.ActivateAsync(id, staffNumber, ct, areas ?? []),
            "Employee activated: the account is now a GREL staff account and the probation has started. The induction task owners have been emailed.");

    [HttpPost]
    public Task<IActionResult> CompleteInduction(int id, CancellationToken ct) =>
        Act(id, () => onboarding.CompleteInductionAsync(id, ct), "Induction closed: the onboarding is complete and filed in the personnel file.");

    [HttpPost]
    public Task<IActionResult> SaveNotes(int id, string? notes, CancellationToken ct) =>
        Act(id, () => onboarding.SaveNotesAsync(id, notes, ct), "Notes saved.");

    /// <summary>Phase 19: the new employee's Personal Record Declaration, read-only (HR only).</summary>
    public async Task<IActionResult> PersonalRecord(int id, CancellationToken ct)
    {
        var model = await personalRecords.GetForHrAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    // ================================================================= Phase 18: new-hire documents (HR)

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<IActionResult> UploadDocument(int id, int documentId, IFormFile? file, int? replacesAttachmentId, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return await Act(id, () => Task.FromResult(Result.Failure("Choose a file to upload.")), "");
        await using var stream = file.OpenReadStream();
        var result = await documents.UploadAsync(documentId, file.FileName, stream, ct, replacesAttachmentId);
        return await Act(id, () => Task.FromResult(result.Succeeded ? Result.Success() : Result.Failure([.. result.Errors])), $"'{Path.GetFileName(file.FileName)}' uploaded.");
    }

    [HttpPost]
    public Task<IActionResult> RemoveDocumentFile(int id, int documentId, int attachmentId, CancellationToken ct) =>
        Act(id, () => documents.RemoveFileAsync(documentId, attachmentId, ct), "File removed.");

    [HttpPost]
    public Task<IActionResult> MarkNotApplicable(int id, int documentId, string? reason, CancellationToken ct) =>
        Act(id, () => documents.MarkNotApplicableAsync(documentId, reason, ct), "Marked not applicable.");

    [HttpPost]
    public Task<IActionResult> VerifyDocument(int id, int documentId, CancellationToken ct) =>
        Act(id, () => documents.VerifyAsync(documentId, ct), "Document verified.");

    [HttpPost]
    public Task<IActionResult> StartReview(int id, int documentId, CancellationToken ct) =>
        Act(id, () => documents.StartReviewAsync(documentId, ct), "Review started: the new employee cannot change this document until you verify or reject it.");

    /// <param name="reasonCode">The reason from the list; <paramref name="reason"/> is the RO's note (required for "Other").</param>
    [HttpPost]
    public Task<IActionResult> RejectDocument(int id, int documentId, DocumentRejectionReason? reasonCode, string? reason, CancellationToken ct) =>
        Act(id, () => documents.RejectAsync(documentId, reasonCode, reason, ct), "Document rejected. The new employee has been emailed the reason.");

    /// <summary>Downloads a new-hire document file (onboarding.view; always as a download, never shown inside the site; every opening is recorded).</summary>
    public async Task<IActionResult> DownloadDocument(int id, int documentId, int attachmentId, CancellationToken ct)
    {
        var file = await documents.OpenFileAsync(documentId, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

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
