using System.Security.Claims;
using Grsms.Application.Common.Interfaces;

namespace Grsms.Web.Security;

/// <summary>
/// Web implementation of <see cref="ICurrentUserService"/>: reads the signed-in user, their roles and IP address
/// from the current HTTP request. Lower layers use it for CreatedBy/UpdatedBy, the audit trail and permission checks.
/// </summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

    public string? FullName => User?.FindFirstValue(AppClaimsPrincipalFactory.FullNameClaim) ?? UserName;

    // Behind a reverse proxy (IIS/Nginx) this is the proxy's address unless forwarded headers are
    // enabled; that is set up in Phase 13 (production deployment).
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}
