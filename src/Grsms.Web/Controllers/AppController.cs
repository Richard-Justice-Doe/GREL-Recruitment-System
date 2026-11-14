using Grsms.Application.Common;
using Grsms.Application.Organization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Grsms.Web.Controllers;

/// <summary>
/// Shared helpers for GRSMS controllers: green/red banner messages, copying service errors onto forms,
/// and the Division / Department drop-downs used by every list's report filters.
/// The banners are shown by Views/Shared/_Alerts.cshtml.
/// </summary>
public abstract class AppController : Controller
{
    /// <summary>
    /// 14 November 2026 (docs/37 CR4): serves a file the caller has already checked access to, three ways:
    ///   * <paramref name="view"/> null: a download, as before;
    ///   * "inline": shown in the browser (PDF and pictures only; the document preview uses it), never cached;
    ///   * "html": a Word (.docx) file converted to safe HTML for the preview.
    /// Anything else is downloaded. The same permission checks and "viewed" log apply, because every route goes through the caller.
    /// </summary>
    protected async Task<IActionResult> ServeFileAsync(Application.Documents.FileDownload? file, string? view, CancellationToken ct)
    {
        if (file is null) return NotFound();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        Response.Headers.CacheControl = "no-store";
        if (view == "inline" && ext is ".pdf" or ".jpg" or ".jpeg" or ".png")
        {
            Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = file.FileName }.ToString();
            return File(file.Content, file.ContentType);
        }
        if (view == "html" && ext == ".docx")
        {
            var preview = HttpContext.RequestServices.GetRequiredService<Application.Common.Interfaces.IDocumentHtmlPreview>();
            await using (file.Content)
            {
                var html = await preview.ToHtmlAsync(file.Content, ct);
                return html is null ? StatusCode(StatusCodes.Status422UnprocessableEntity) : Content(html, "text/html; charset=utf-8");
            }
        }
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Shows a green "success" banner on the next page.</summary>
    protected void Success(string message) => TempData["Success"] = message;

    /// <summary>Shows a red "error" banner on the next page.</summary>
    protected void Error(string message) => TempData["Error"] = message;

    /// <summary>Shows a failed Result's messages in the red banner on the next page.</summary>
    protected void Fail(Result result) => Error(string.Join(" ", result.Errors));

    /// <summary>
    /// The common "post an action, show the outcome, go back" step (codebase audit DU5): runs the service action, shows the
    /// success message or the errors as a banner, then returns <paramref name="redirect"/> (usually the record's page).
    /// </summary>
    protected async Task<IActionResult> RunAsync(Func<Task<Result>> action, string successMessage, Func<IActionResult> redirect)
    {
        var result = await action();
        if (result.Succeeded) Success(successMessage);
        else Fail(result);
        return redirect();
    }

    /// <summary>Puts a failed Result's messages at the top of the current form.</summary>
    protected void AddErrors(Result result)
    {
        foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
    }

    /// <summary>
    /// Fills ViewBag.DivisionList and ViewBag.DepartmentList (departments grouped under their division) for a
    /// list page's filter form. Inactive divisions and departments are included so older records can be found.
    /// </summary>
    protected async Task LoadFilterListsAsync(int? divisionId, int? departmentId, CancellationToken ct)
    {
        var divisions = HttpContext.RequestServices.GetRequiredService<IDivisionService>();
        var departments = HttpContext.RequestServices.GetRequiredService<IDepartmentService>();

        ViewBag.DivisionList = new SelectList(await divisions.GetAllLookupAsync(ct),
            nameof(LookupItem.Value), nameof(LookupItem.Text), divisionId?.ToString());
        ViewBag.DepartmentList = new SelectList(await departments.GetGroupedLookupAsync(includeInactive: true, ct),
            nameof(GroupedLookupItem.Value), nameof(GroupedLookupItem.Text), departmentId?.ToString(), nameof(GroupedLookupItem.Group));
    }
}
