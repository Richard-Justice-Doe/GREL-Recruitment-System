using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace Grsms.Web.Security;

/// <summary>
/// Named authorization policies. A controller says <c>[Authorize(Policy = Policies.MasterData)]</c>
/// instead of listing role names, so who may do what is decided in ONE place.
///
/// Phase 14: most policies are now a PERMISSION (Domain/Security/Permissions.cs). Which roles hold each permission is
/// set by the Super Admin on the Roles &amp; permissions screen; the defaults reproduce what each role could do before.
/// The full permission matrix is documented in docs/04-security-and-roles.md.
/// </summary>
public static class Policies
{
    /// <summary>Maintain divisions, departments, units and sites.</summary>
    public const string MasterData = nameof(MasterData);

    /// <summary>Create and manage user accounts.</summary>
    public const string UserAdmin = nameof(UserAdmin);

    /// <summary>Read the audit trail.</summary>
    public const string AuditTrail = nameof(AuditTrail);

    /// <summary>Any GREL staff member (every role except Candidate).</summary>
    public const string Staff = nameof(Staff);

    /// <summary>Open the Recruitment Requests screens (which requests each person sees is decided by the service).</summary>
    public const string RecruitmentRequests = nameof(RecruitmentRequests);

    /// <summary>Raise a new recruitment request.</summary>
    public const string RaiseRecruitmentRequest = nameof(RaiseRecruitmentRequest);

    /// <summary>Open the Vacancies screens (what each person may do there is decided by VacancyService).</summary>
    public const string Vacancies = nameof(Vacancies);

    /// <summary>Open the staff Applications screens (which applications each person sees is decided by ApplicationService).</summary>
    public const string Applications = nameof(Applications);

    /// <summary>Open the Job adverts screens (what each person may do there is decided by AdvertService).</summary>
    public const string Adverts = nameof(Adverts);

    /// <summary>Read survey results (Phase 10).</summary>
    public const string SurveyResults = nameof(SurveyResults);

    /// <summary>Open the Reports (Phase 11). HODs see only their own departments (decided by ReportService).</summary>
    public const string Reports = nameof(Reports);

    /// <summary>Phase 14: manage roles and permissions (Super Admin only).</summary>
    public const string RolesAndPermissions = nameof(RolesAndPermissions);

    /// <summary>Phase 15: change the ATS weights, bands and automatic-rejection rules (Super Admin only).</summary>
    public const string AtsConfiguration = nameof(AtsConfiguration);

    /// <summary>Registers the policies above. Called from Program.cs.</summary>
    public static AuthorizationBuilder AddGrsmsPolicies(this AuthorizationBuilder builder)
    {
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        return builder
            .AddPermissionPolicy(MasterData, Permissions.ManageMasterData)
            .AddPermissionPolicy(UserAdmin, Permissions.ManageUsers)
            .AddPermissionPolicy(AuditTrail, Permissions.ViewAuditTrail)
            .AddPermissionPolicy(RecruitmentRequests, Permissions.ViewRecruitmentRequests)
            .AddPermissionPolicy(RaiseRecruitmentRequest, Permissions.RaiseRecruitmentRequests)
            .AddPermissionPolicy(Vacancies, Permissions.ViewVacancies)
            .AddPermissionPolicy(Adverts, Permissions.ViewAdverts)
            .AddPermissionPolicy(Applications, Permissions.ViewApplications)
            .AddPermissionPolicy(SurveyResults, Permissions.ViewSurveyResults)
            .AddPermissionPolicy(Reports, Permissions.ViewReports)
            .AddPermissionPolicy(RolesAndPermissions, Permissions.ManagePermissions)
            .AddPermissionPolicy(AtsConfiguration, Permissions.ConfigureAts)
            // Staff = signed in with any role other than Candidate (built-in or custom).
            .AddPolicy(Staff, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
                !c.User.IsInRole(Roles.Candidate) &&
                c.User.FindAll(System.Security.Claims.ClaimTypes.Role).Any()));
    }

    private static AuthorizationBuilder AddPermissionPolicy(this AuthorizationBuilder builder, string policy, string permission) =>
        builder.AddPolicy(policy, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
}

/// <summary>Phase 14: "the user must hold this permission".</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Phase 14: checks a <see cref="PermissionRequirement"/> against the roles the Super Admin has given it to.</summary>
public sealed class PermissionHandler(IPermissionStore store) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (CurrentUserService.HasPermission(context.User, requirement.Permission, store))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
