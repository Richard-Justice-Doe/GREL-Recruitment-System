using Grsms.Application.Employees;
using Grsms.Application.Users;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// 22 November 2026: "My photo": each employee uploads, replaces or removes their own profile picture (shown in the employee
/// directory, on their profile and on birthday and anniversary cards). Only the signed-in person's own record is ever touched;
/// the rules (JPEG or PNG, at most 3 MB, the usual upload checks) are in <see cref="EmployeePhotoService"/>.
/// </summary>
[Authorize(Policy = Policies.Staff)]
public class MyPhotoController(EmployeePhotoService photos, IUserDirectory users) : AppController
{
    public sealed record MyPhotoModel(bool IsEmployee, string Name, string Initials, bool HasPhoto, bool Uploaded);

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var e = await photos.MineAsync(ct);
        var name = e is null ? (User.Identity?.Name ?? "") : (await users.GetDisplayNamesAsync([e.UserId], ct)).GetValueOrDefault(e.UserId, e.EmployeeNumber);
        return View(new MyPhotoModel(e is not null, name, EmployeePhotoService.Initials(name),
            e is not null && await photos.HasPhotoAsync(e, ct), e?.PhotoAttachmentId is not null));
    }

    /// <summary>My own photo (uploaded, or the verified onboarding photograph); 404 when there is none.</summary>
    public async Task<IActionResult> Photo(CancellationToken ct)
    {
        if (await photos.MineAsync(ct) is not { } e || await photos.OpenAsync(e.Id, ct) is not { } file) return NotFound();
        Response.Headers.CacheControl = "private, no-cache";
        return File(file.Content, file.ContentType);
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? photo, CancellationToken ct)
    {
        if (await photos.MineAsync(ct) is not { } e)
        {
            Error("You have no employee record yet. Ask HR to register you.");
            return RedirectToAction(nameof(Index));
        }
        await using var stream = photo?.OpenReadStream();
        var result = await photos.UploadAsync(e.Id, photo?.FileName, stream, ct);
        if (result.Succeeded) Success("Your photo is saved. Colleagues see it in the directory and on celebration cards.");
        else Fail(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(CancellationToken ct)
    {
        if (await photos.MineAsync(ct) is not { } e) return RedirectToAction(nameof(Index));
        return await RunAsync(() => photos.RemoveAsync(e.Id, ct), "Your photo was removed.", () => RedirectToAction(nameof(Index)));
    }
}
