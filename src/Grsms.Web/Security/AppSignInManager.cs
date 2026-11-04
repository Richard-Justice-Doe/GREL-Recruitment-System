using Grsms.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grsms.Web.Security;

/// <summary>
/// ASP.NET Identity's sign-in manager, with one GREL rule added: accounts that the HR Administrator
/// has deactivated cannot sign in, by password OR by Microsoft 365.
/// </summary>
public class AppSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    /// <summary>Returning false makes the sign-in result "NotAllowed"; AccountController explains why.</summary>
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        user.IsActive && await base.CanSignInAsync(user);
}

/// <summary>
/// Adds GREL-specific values to the sign-in cookie so pages can use them without a database lookup:
///   * "full_name"             - shown in the top bar;
///   * "must_change_password"  - read by <see cref="MustChangePasswordFilter"/>.
/// </summary>
public class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    public const string FullNameClaim = "full_name";
    public const string MustChangePasswordClaim = "must_change_password";

    protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new(FullNameClaim, user.FullName));
        if (user.MustChangePassword)
            identity.AddClaim(new(MustChangePasswordClaim, "true"));
        return identity;
    }
}
