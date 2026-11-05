using System.Security.Claims;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Security;

namespace Grsms.Web.Security;

/// <summary>
/// Web implementation of <see cref="ICurrentUserService"/>: reads the signed-in user, their roles and IP address
/// from the current HTTP request. Lower layers use it for CreatedBy/UpdatedBy, the audit trail and permission checks.
/// </summary>
public class CurrentUserService(IHttpContextAccessor accessor, IPermissionStore permissions) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

    public string? FullName => User?.FindFirstValue(AppClaimsPrincipalFactory.FullNameClaim) ?? UserName;

    // Behind a reverse proxy this is the visitor's real address only when forwarded headers are enabled (Phase 13).
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => User?.IsInRole(role) == true;

    public bool HasPermission(string permission) => User is { } user && HasPermission(user, permission, permissions);

    /// <summary>Phase 14: the permission check shared by this service and the authorization policies.</summary>
    public static bool HasPermission(ClaimsPrincipal user, string permission, IPermissionStore store) =>
        user.Identity?.IsAuthenticated == true &&
        Permissions.Holds(user.FindAll(ClaimTypes.Role).Select(c => c.Value), permission, store.GrantedTo);
}
