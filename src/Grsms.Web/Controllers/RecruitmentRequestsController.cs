using System.Security.Claims;
using Grsms.Application.Documents;
using Grsms.Application.Organization;
using Grsms.Application.Recruitment;
using Grsms.Domain.Documents;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Recruitment Request screens (Module 2): list, raise, edit, details, submit, approve/return/reject,
/// cancel, and attachment upload/download.
///
/// The controller is deliberately thin. Every permission and workflow rule lives in
/// <see cref="RecruitmentRequestService"/>, so the same rules apply however a request is reached.
/// </summary>
[Authorize(Policy = Policies.RecruitmentRequests)]
public class RecruitmentRequestsController(RecruitmentRequestService service) : AppController
{
    /// <summary>Upload limit for the attachment form, slightly above the file limit to allow for form overhead.</summary>
    private const long UploadRequestLimitBytes = 25 * 1024 * 1024;

    /// <summary>Roles that approve at some stage, so their list opens on "Awaiting my action".</summary>
    private static readonly string[] ApproverRoles = [Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector];

    // ================================================================= List

    /// <param name="tab">"awaiting" (requests waiting for my decision) or "all". Approvers start on "awaiting".</param>
    public async Task<IActionResult> Index([FromQuery] RecruitmentRequestFilter filter, string? tab, CancellationToken ct)
    {
        tab ??= ApproverRoles.Any(User.IsInRole) ? "awaiting" : "all";
        filter.AwaitingMe = tab == "awaiting";

        ViewBag.Tab = tab;
        ViewBag.Filter = filter;
        ViewBag.AwaitingCount = await service.CountAwaitingMeAsync(ct);
        ViewBag.CanRaise = User.IsInRole(Roles.HeadOfDepartment);
        await LoadFilterListsAsync(filter.DivisionId, filter.DepartmentId, ct);
        return View(await service.SearchAsync(filter, ct));
    }

    // ================================================================= Raise / edit (HOD)

    [Authorize(Policy = Policies.RaiseRecruitmentRequest)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var form = new RecruitmentRequestFormDto();
        var depts = await service.GetCreatableDepartmentsAsync(ct);
        if (depts.Count == 0)
        {
            Error("You are not set as Head of an active department, so you cannot raise requests yet. Ask the HR Administrator to update the department.");
            return RedirectToAction(nameof(Index));
        }
        if (depts.Count == 1) form.DepartmentId = int.Parse(depts[0].Value); // save a click
        await LoadFormListsAsync(form, ct);
        return View("Form", form);
    }

    [HttpPost]
    [Authorize(Policy = Policies.RaiseRecruitmentRequest)]
    public async Task<IActionResult> Create(RecruitmentRequestFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Draft saved. Now attach the job description (and any other documents), then click 'Submit for approval'.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await LoadFormListsAsync(form, ct);
        return View("Form", form);
    }

    [Authorize(Policy = Policies.RaiseRecruitmentRequest)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await service.GetForEditAsync(id, ct);
        if (form is null) return NotFound();
        await LoadFormListsAsync(form, ct);
        return View("Form", form);
    }

    [HttpPost]
    [Authorize(Policy = Policies.RaiseRecruitmentRequest)]
    public async Task<IActionResult> Edit(int id, RecruitmentRequestFormDto form, CancellationToken ct)
    {
        form.Id = id;
        if (ModelState.IsValid)
        {
            var result = await service.UpdateAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Changes saved.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadFormListsAsync(form, ct);
        return View("Form", form);
    }

    /// <summary>Units of a department as JSON, so the Unit drop-down refreshes when the department changes.</summary>
    [HttpGet]
    public async Task<IActionResult> Units(int departmentId, CancellationToken ct) =>
        Json(await service.GetUnitsAsync(departmentId, ct));

    // ================================================================= Details + workflow actions

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await service.GetDetailsAsync(id, ct);
        if (model is null) return NotFound();

        // The reviewing Recruitment Officer is pre-selected as the assigned recruiter.
        if (model.CanDecide && model.Status == RecruitmentRequestStatus.RecruitmentReview)
            ViewBag.RecruiterList = new SelectList(await service.GetRecruitersAsync(ct),
                nameof(LookupItem.Value), nameof(LookupItem.Text), User.FindFirstValue(ClaimTypes.NameIdentifier));
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(int id, CancellationToken ct)
    {
        var result = await service.SubmitAsync(id, ct);
        if (result.Succeeded) Success("Request submitted. The Recruitment Officers have been notified.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, ReviewDecisionDto form, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await service.DecideAsync(id, form, ct)
            : Application.Common.Result.Failure("Choose Approve, Return for changes, or Reject.");

        if (result.Succeeded)
        {
            Success(form.Decision switch
            {
                ReviewDecision.Approve => "Approved. The next approver has been notified.",
                ReviewDecision.Return => "Returned to the Head of Department for changes.",
                _ => "Request rejected. The Head of Department has been notified."
            });
            // Back to the waiting list: the reviewer usually has more to process.
            return RedirectToAction(nameof(Index), new { tab = "awaiting" });
        }

        Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, reason, ct);
        if (result.Succeeded) Success("Request cancelled.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    // ================================================================= Attachments

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<IActionResult> Upload(int id, AttachmentCategory category, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            Error("Choose a file to upload.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var stream = file.OpenReadStream();
        var result = await service.AddAttachmentAsync(id, category, file.FileName, stream, ct);
        if (result.Succeeded) Success($"'{Path.GetFileName(file.FileName)}' uploaded.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var result = await service.RemoveAttachmentAsync(id, attachmentId, ct);
        if (result.Succeeded) Success("File removed.");
        else Error(string.Join(" ", result.Errors));
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Downloads a file. Always sent as a download (never opened inside the site) for safety.</summary>
    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var file = await service.OpenAttachmentAsync(id, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    // ================================================================= Helpers

    /// <summary>Drop-downs for the form: the user's departments, that department's units, and the sites.</summary>
    private async Task LoadFormListsAsync(RecruitmentRequestFormDto form, CancellationToken ct)
    {
        ViewBag.DepartmentList = new SelectList(await service.GetCreatableDepartmentsAsync(ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.DepartmentId?.ToString());
        ViewBag.UnitList = new SelectList(
            form.DepartmentId is int d ? await service.GetUnitsAsync(d, ct) : [],
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.UnitId?.ToString());
        ViewBag.SiteList = new SelectList(await service.GetSitesAsync(ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), form.SiteId?.ToString());
        ViewBag.AcceptList = AttachmentService.AcceptList;
    }
}
