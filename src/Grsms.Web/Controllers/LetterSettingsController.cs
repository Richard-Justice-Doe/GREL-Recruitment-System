using Grsms.Application.Settings;
using Grsms.Domain.Settings;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// 12 November 2026: Administration → Letterhead &amp; signature. The Super Admin sets the address, contacts and signatory printed
/// on the offer and appointment letters, and may upload a scanned signature; the Admin sees the page read-only.
/// The rules live in <see cref="LetterSettingsService"/>.
/// </summary>
[Authorize(Policy = Policies.SystemSettings)]
public class LetterSettingsController(LetterSettingsService letters) : AppController
{
    public IActionResult Index()
    {
        ViewBag.CanManage = letters.CanManage;
        ViewBag.Details = letters.Get();
        return View(letters.GetFields());
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> Save(CancellationToken ct)
    {
        var values = SettingCatalogue.All.Where(d => d.Group == SettingCatalogue.Letters)
            .ToDictionary(d => d.Key, d => (string?)Request.Form[d.Key].FirstOrDefault());
        var result = await letters.SaveAsync(values, ct);
        if (result.Succeeded) Success("Saved. New and reopened letters show the new details.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> UploadSignature(IFormFile? signature, CancellationToken ct)
    {
        if (signature is null) Fail(Application.Common.Result.Failure("Choose the signature image to upload."));
        else
        {
            await using var stream = signature.OpenReadStream();
            var result = await letters.SaveSignatureAsync(stream, signature.FileName, signature.Length, ct);
            if (result.Succeeded) Success("Signature uploaded. It now appears above the signatory's name on the letters.");
            else Fail(result);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageSystemSettings)]
    public async Task<IActionResult> RemoveSignature(CancellationToken ct)
    {
        var result = await letters.RemoveSignatureAsync(ct);
        if (result.Succeeded) Success("Signature removed from the letters.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>The signature image on the letters: any signed-in user who can open a letter (candidates, new employees, staff), never anonymous.</summary>
[Authorize]
public class LetterImagesController(LetterSettingsService letters) : Controller
{
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Signature(CancellationToken ct)
    {
        var file = await letters.OpenSignatureAsync(ct);
        return file is null ? NotFound() : File(file.Value.Content, file.Value.ContentType);
    }
}
