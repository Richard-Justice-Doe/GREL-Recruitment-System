using Grsms.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Grsms.Web.Controllers;

/// <summary>
/// Shared helpers for GRSMS controllers: green/red banner messages and copying service errors onto forms.
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
}
