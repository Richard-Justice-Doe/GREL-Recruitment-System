using Grsms.Application.Applications;
using Grsms.Domain.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// "My profile": every signed-in person (candidate or GREL staff) keeps their job-seeker details, CV and certificates
/// here, ready to apply. People only ever reach their own profile. Rules live in <see cref="CandidateProfileService"/>.
/// </summary>
[Authorize]
public class ProfileController(CandidateProfileService profiles) : AppController
{
    private const long UploadRequestLimitBytes = 25 * 1024 * 1024;

    /// <param name="returnUrl">Where to go back to after saving (e.g. the apply page).</param>
    public async Task<IActionResult> Index(string? returnUrl, CancellationToken ct)
    {
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(await profiles.GetMineAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Index(ProfileFormDto form, string? returnUrl, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await profiles.SaveMineAsync(form, ct);
            if (result.Succeeded)
            {
                Success("Profile saved.");
                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        await profiles.FillReadOnlyAsync(form, ct);
        return View(form);
    }

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<IActionResult> Upload(AttachmentCategory category, IFormFile? file, string? returnUrl, CancellationToken ct)
    {
        if (file is null || file.Length == 0) Error("Choose a file to upload.");
        else
        {
            await using var stream = file.OpenReadStream();
            var result = await profiles.AddDocumentAsync(category, file.FileName, stream, ct);
            if (result.Succeeded) Success($"'{Path.GetFileName(file.FileName)}' uploaded.");
            else Fail(result);
        }
        return RedirectToAction(nameof(Index), new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDocument(int attachmentId, string? returnUrl, CancellationToken ct)
    {
        var result = await profiles.RemoveDocumentAsync(attachmentId, ct);
        if (result.Succeeded) Success("File removed.");
        else Fail(result);
        return RedirectToAction(nameof(Index), new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    /// <summary>Downloads one of my own files (always as a download, never opened inside the site).</summary>
    public async Task<IActionResult> Download(int attachmentId, CancellationToken ct)
    {
        var file = await profiles.OpenDocumentAsync(attachmentId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }
}
