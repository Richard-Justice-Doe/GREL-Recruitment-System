using Grsms.Application.Onboarding;
using Grsms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Phase 20: the electronic personnel files (list, a file's folders, filed letters, downloads, HR uploads).
/// Thin: every rule is in <see cref="PersonnelFileService"/>.
/// </summary>
[Authorize(Policy = Policies.PersonnelFiles)]
public class PersonnelFilesController(PersonnelFileService files) : AppController
{
    private const long UploadRequestLimitBytes = 25 * 1024 * 1024;

    public async Task<IActionResult> Index(string? search, int? departmentId, CancellationToken ct)
    {
        ViewBag.Search = search;
        ViewBag.DepartmentId = departmentId;
        await LoadFilterListsAsync(null, departmentId, ct);
        return View(await files.SearchAsync(search, departmentId, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await files.GetAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>A letter filed in the personnel file (printable).</summary>
    public async Task<IActionResult> Letter(int id, CancellationToken ct)
    {
        var model = await files.GetLetterAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>Downloads a filed file (always as a download, never shown inside the site).</summary>
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await files.OpenAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost]
    [RequestSizeLimit(UploadRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimitBytes)]
    public async Task<IActionResult> Upload(int id, int categoryId, string? title, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) Error("Choose a file to upload.");
        else
        {
            await using var stream = file.OpenReadStream();
            var result = await files.UploadAsync(id, categoryId, title, file.FileName, stream, ct);
            if (result.Succeeded) Success($"'{Path.GetFileName(file.FileName)}' filed.");
            else Error(string.Join(" ", result.Errors));
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
