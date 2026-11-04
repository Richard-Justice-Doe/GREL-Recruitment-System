namespace Grsms.Application.Common.Interfaces;

/// <summary>
/// Tells the lower layers who is using the system right now, without them needing to know about
/// web requests. Used to fill in CreatedBy/UpdatedBy, the audit trail, and workflow permission checks.
/// The real implementation lives in the Web project and reads the signed-in user from the HTTP request.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Identity user id, or null when nobody is signed in (e.g. background jobs, seeding).</summary>
    string? UserId { get; }

    /// <summary>User name (email), or null when nobody is signed in.</summary>
    string? UserName { get; }

    /// <summary>Display name (e.g. "Ama Mensah"), falling back to the user name.</summary>
    string? FullName { get; }

    /// <summary>IP address of the current browser request, if there is one.</summary>
    string? IpAddress { get; }

    /// <summary>True when the signed-in user holds <paramref name="role"/> (see Domain/Security/Roles.cs).</summary>
    bool IsInRole(string role);
}
