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

    /// <summary>Read survey results (Phase 10): HR Manager, Recruitment Officers and the Managing Director.</summary>
    public const string SurveyResults = nameof(SurveyResults);

    /// <summary>Open the Reports (Phase 11). HODs see only their own departments (decided by ReportService).</summary>
    public const string Reports = nameof(Reports);

    /// <summary>Registers the policies above. Called from Program.cs.</summary>
    public static AuthorizationBuilder AddGrsmsPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(MasterData, p => p.RequireRole(Roles.HrAdministrator))
        .AddPolicy(UserAdmin, p => p.RequireRole(Roles.HrAdministrator))
        .AddPolicy(AuditTrail, p => p.RequireRole(Roles.HrAdministrator, Roles.HrManager))
        .AddPolicy(Staff, p => p.RequireRole(Roles.Staff))
        .AddPolicy(RecruitmentRequests, p => p.RequireRole(
            Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector, Roles.HrAdministrator))
        .AddPolicy(RaiseRecruitmentRequest, p => p.RequireRole(Roles.HeadOfDepartment))
        .AddPolicy(Vacancies, p => p.RequireRole(
            Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector, Roles.HrAdministrator))
        .AddPolicy(Adverts, p => p.RequireRole(
            Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector, Roles.HrAdministrator))
        .AddPolicy(SurveyResults, p => p.RequireRole(Roles.HrManager, Roles.RecruitmentOfficer, Roles.ManagingDirector))
        .AddPolicy(Reports, p => p.RequireRole(
            Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector, Roles.HrAdministrator))
        .AddPolicy(Applications, p => p.RequireRole(
            Roles.HeadOfDepartment, Roles.RecruitmentOfficer, Roles.HrManager, Roles.ManagingDirector, Roles.HrAdministrator));
}
