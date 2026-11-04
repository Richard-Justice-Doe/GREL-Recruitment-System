using Grsms.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace Grsms.Web.Security;

/// <summary>
/// Named authorization policies. A controller says <c>[Authorize(Policy = Policies.MasterData)]</c>
/// instead of listing role names, so who may do what is decided in ONE place (this file).
/// The full permission matrix is documented in docs/04-security-and-roles.md.
/// </summary>
public static class Policies
{
    /// <summary>Maintain departments, units and sites.</summary>
    public const string MasterData = nameof(MasterData);

    /// <summary>Create and manage user accounts.</summary>
    public const string UserAdmin = nameof(UserAdmin);

    /// <summary>Read the audit trail.</summary>
    public const string AuditTrail = nameof(AuditTrail);

    /// <summary>Any GREL staff member (every role except Candidate).</summary>
    public const string Staff = nameof(Staff);

    /// <summary>Registers the policies above. Called from Program.cs.</summary>
    public static AuthorizationBuilder AddGrsmsPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(MasterData, p => p.RequireRole(Roles.HrAdministrator))
        .AddPolicy(UserAdmin, p => p.RequireRole(Roles.HrAdministrator))
        .AddPolicy(AuditTrail, p => p.RequireRole(Roles.HrAdministrator, Roles.HrManager))
        .AddPolicy(Staff, p => p.RequireRole(Roles.Staff));
}
