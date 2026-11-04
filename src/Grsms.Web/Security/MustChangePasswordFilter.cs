using Grsms.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Grsms.Web.Security;

/// <summary>
/// Runs before every page. If the signed-in user still has a temporary password (set by an
/// administrator), they are sent to "Change password" and cannot open any other page until they do.
/// The Account pages (change password, sign out) are exempt, otherwise the user would be stuck.
/// </summary>
public class MustChangePasswordFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return;
        if (!user.HasClaim(AppClaimsPrincipalFactory.MustChangePasswordClaim, "true")) return;
        if (context.Controller is AccountController) return;

        context.Result = new RedirectToActionResult(
            nameof(AccountController.ChangePassword), "Account", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
