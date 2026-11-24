using System.Security.Claims;
using Grsms.Application.Settings;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Grsms.Web.Security;

/// <summary>
/// 22 Nov 2026 (docs/41, H2): staff in a <see cref="Roles.TwoStepRequired"/> role who signed in with a password but without the
/// two-step check (they have not set up an authenticator app yet) are sent to "Two-step sign-in" and cannot open any other page
/// until they set it up. Once it is set up, every password sign-in asks for the code (ASP.NET Identity), and the session carries
/// the "amr = mfa" claim. Microsoft 365 sign-ins (their cookie names the provider) rely on Microsoft's own check. The Account
/// pages (set-up, sign out) are exempt, otherwise the user would be stuck. Only once the Super Admin switches it on (System settings → Sign-in security; off by default while GRSMS is tested).
/// </summary>
public class TwoStepRequiredFilter(ISettingsStore settings) : IActionFilter
{
    /// <summary>The claim ASP.NET Identity writes on sign-in: "pwd" (password only) or "mfa" (password + code).</summary>
    public const string AmrClaim = "amr";

    /// <summary>
    /// True when this person must use two-step sign-in: the company-wide rule for privileged roles (System settings), or the
    /// Super Admin requires it for them personally (Users screen; the "two_step_required" claim).
    /// </summary>
    public static bool RequiredFor(ClaimsPrincipal user, ISettingsStore settings) =>
        (settings.GetBool(SettingCatalogue.RequireTwoStep) && Roles.TwoStepRequired.Any(user.IsInRole))
        || user.HasClaim(AppClaimsPrincipalFactory.TwoStepRequiredClaim, "true");

    /// <summary>True when the person must use it and this session has not passed the two-step check yet.</summary>
    public static bool Required(ClaimsPrincipal user, ISettingsStore settings) =>
        user.Identity?.IsAuthenticated == true
        && RequiredFor(user, settings)
        && !user.HasClaim(AmrClaim, "mfa")
        && !user.HasClaim(c => c.Type == ClaimTypes.AuthenticationMethod); // signed in through Microsoft 365

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is AccountController) return;
        if (!Required(context.HttpContext.User, settings)) return;
        context.Result = new RedirectToActionResult(nameof(AccountController.TwoStep), "Account", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
