using Grsms.Application.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// The applicant's side of applications (Module 5): apply through a live advert, see my applications and their
/// progress, withdraw. Any signed-in person may use it (candidates, and GREL staff for internal jobs).
/// Rules live in <see cref="ApplicationService"/>.
/// </summary>
[Authorize]
public class MyApplicationsController(ApplicationService applications, Application.Interviews.InterviewService interviews,
    Application.Hiring.HiringService hiring, Application.Onboarding.OnboardingService onboarding,
    Application.Surveys.SurveyService surveys, Application.Onboarding.DocumentVerificationService documents,
    Application.Onboarding.PersonalRecordService personalRecords) : AppController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await applications.MyApplicationsAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await applications.GetMineAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.Interviews = await interviews.ForMyApplicationAsync(id, ct); // Phase 7: date, place and instructions only
        ViewBag.Offer = await hiring.GetMyOfferAsync(id, ct); // Phase 8
        ViewBag.Appointment = await onboarding.GetMyAppointmentAsync(id, ct); // Phase 9
        ViewBag.Surveys = await surveys.OpenForApplicationAsync(id, ct); // Phase 10
        return View(model);
    }

    /// <summary>The appointment letter (printable), with the signature box while it is unsigned.</summary>
    public async Task<IActionResult> Appointment(int id, CancellationToken ct)
    {
        var model = await onboarding.GetMyAppointmentAsync(id, ct);
        if (model is null || !model.LetterReady) return NotFound();
        ViewBag.ApplicationId = id;
        return View(model);
    }

    /// <summary>Sign the appointment letter by typing my full name.</summary>
    [HttpPost]
    public async Task<IActionResult> SignLetter(int id, string? fullName, bool confirm, CancellationToken ct)
    {
        if (!confirm)
        {
            Error("Tick the box to confirm you accept the appointment on these terms.");
            return RedirectToAction(nameof(Appointment), new { id });
        }
        var result = await onboarding.SignLetterAsync(id, fullName, ct);
        if (result.Succeeded)
        {
            Success("Thank you! Your appointment letter is signed. See you on your first day.");
            return RedirectToAction(nameof(Details), new { id });
        }
        Fail(result);
        return RedirectToAction(nameof(Appointment), new { id });
    }

    // ================================================================= Phase 18: my new-hire documents
    // "id" is always the application id; every rule (my own documents, my turn) is checked by DocumentVerificationService.

    private const long DocumentUploadLimitBytes = 25 * 1024 * 1024;

    /// <summary>My New Hire Administrative Records checklist: upload, mark not applicable, submit to HR.</summary>
    public async Task<IActionResult> Documents(int id, CancellationToken ct)
    {
        var model = await documents.GetMineAsync(id, ct);
        if (model is null) return NotFound();
        ViewBag.ApplicationId = id;
        return View(model);
    }

    [HttpPost]
    [RequestSizeLimit(DocumentUploadLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DocumentUploadLimitBytes)]
    /// <param name="replacesAttachmentId">Phase 27: the file this one replaces (the old one stays in the history).</param>
    public async Task<IActionResult> UploadDocument(int id, int documentId, IFormFile? file, int? replacesAttachmentId, CancellationToken ct)
    {
        if (file is null || file.Length == 0) Error("Choose a file to upload.");
        else
        {
            await using var stream = file.OpenReadStream();
            var result = await documents.UploadAsync(documentId, file.FileName, stream, ct, replacesAttachmentId);
            if (result.Succeeded) Success($"'{Path.GetFileName(file.FileName)}' uploaded. HR will check it and email you if anything needs attention.");
            else Fail(result);
        }
        return RedirectToAction(nameof(Documents), new { id });
    }

    [HttpPost]
    public Task<IActionResult> RemoveDocumentFile(int id, int documentId, int attachmentId, CancellationToken ct) =>
        DocumentAct(id, () => documents.RemoveFileAsync(documentId, attachmentId, ct), "File removed.");

    [HttpPost]
    public Task<IActionResult> MarkNotApplicable(int id, int documentId, string? reason, CancellationToken ct) =>
        DocumentAct(id, () => documents.MarkNotApplicableAsync(documentId, reason, ct), "Marked not applicable.");

    /// <summary>Phase 27: I have made the correction HR asked for (documents go to HR one by one; there is no "submit" any more).</summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmCorrection(int id, CancellationToken ct)
    {
        var mine = await documents.GetMineAsync(id, ct);
        if (mine is null) return NotFound();
        var result = await onboarding.ConfirmCorrectionAsync(mine.OnboardingId, ct);
        if (result.Succeeded) Success("Thank you! HR has been told that you made the correction.");
        else Fail(result);
        return RedirectToAction(nameof(Documents), new { id });
    }

    /// <summary>Downloads one of my own document files.</summary>
    public async Task<IActionResult> DownloadDocument(int id, int documentId, int attachmentId, CancellationToken ct)
    {
        var file = await documents.OpenFileAsync(documentId, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    // ================================================================= Phase 19: my personal record declaration

    public async Task<IActionResult> PersonalRecord(int id, CancellationToken ct)
    {
        var model = await personalRecords.GetMineAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>Saves the whole form. On a problem, the page is shown again with what was typed and every error.</summary>
    [HttpPost]
    public async Task<IActionResult> PersonalRecord(int id, Application.Onboarding.PersonalRecordForm form, CancellationToken ct)
    {
        var result = await personalRecords.SaveAsync(id, form, ct);
        if (result.Succeeded)
        {
            Success("Personal record saved. Now read the declaration at the top and sign it.");
            return RedirectToAction(nameof(PersonalRecord), new { id });
        }
        var model = await personalRecords.GetMineAsync(id, ct);
        if (model is null) return NotFound();
        AddErrors(result);
        return View(model with { Form = form });
    }

    [HttpPost]
    public async Task<IActionResult> SignPersonalRecord(int id, string? fullName, bool confirm, CancellationToken ct)
    {
        var result = await personalRecords.SignAsync(id, fullName, confirm, ct);
        if (result.Succeeded)
        {
            Success("Thank you. Your personal record is signed.");
            return RedirectToAction(nameof(Documents), new { id });
        }
        Fail(result);
        return RedirectToAction(nameof(PersonalRecord), new { id });
    }

    private async Task<IActionResult> DocumentAct(int id, Func<Task<Application.Common.Result>> action, string successMessage)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Fail(result);
        return RedirectToAction(nameof(Documents), new { id });
    }

    /// <param name="id">The advert number, e.g. ADV-2026-0001.</param>
    public async Task<IActionResult> Apply(string id, CancellationToken ct)
    {
        var result = await applications.GetApplyFormAsync(id, ct);
        if (result.Succeeded) return View(result.Value);

        Fail(result);
        var status = await applications.GetApplyStatusAsync(id, ct);
        return status.ExistingApplicationId is int existing
            ? RedirectToAction(nameof(Details), new { id = existing })
            : RedirectToAction("Index", "Careers");
    }

    [HttpPost]
    public async Task<IActionResult> Apply(ApplyFormDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await applications.ApplyAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Application sent. We have emailed you a confirmation with your reference number.");
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            AddErrors(result);
        }
        await applications.FillApplyFormAsync(form, ct);
        return View(form);
    }

    /// <summary>The offer letter (printable).</summary>
    public async Task<IActionResult> Offer(int id, CancellationToken ct)
    {
        var offer = await hiring.GetMyOfferAsync(id, ct);
        if (offer is null) return NotFound();
        ViewBag.ApplicationId = id;
        return View(offer);
    }

    /// <summary>Accept or decline the offer.</summary>
    [HttpPost]
    public async Task<IActionResult> RespondOffer(int id, bool accept, string? reason, bool confirm, CancellationToken ct)
    {
        if (accept && !confirm)
        {
            Error("Tick the box to confirm you accept the terms of the offer.");
            return RedirectToAction(nameof(Offer), new { id });
        }
        var result = await hiring.RespondToOfferAsync(id, accept, reason, ct);
        if (result.Succeeded) Success(accept ? "Thank you! You have accepted the offer. HR will contact you about your first day." : "You have declined the offer.");
        else Fail(result);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Withdraw(int id, CancellationToken ct)
    {
        var result = await applications.WithdrawAsync(id, ct);
        if (result.Succeeded) Success("Your application has been withdrawn.");
        else Fail(result);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var file = await applications.OpenMyDocumentAsync(id, attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
