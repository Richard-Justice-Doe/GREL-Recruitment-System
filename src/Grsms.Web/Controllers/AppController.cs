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
    /// <summary>Shows a green "success" banner on the next page.</summary>
    protected void Success(string message) => TempData["Success"] = message;

    /// <summary>Shows a red "error" banner on the next page.</summary>
    protected void Error(string message) => TempData["Error"] = message;

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
