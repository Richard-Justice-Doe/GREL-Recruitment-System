using System.Security.Claims;
using Grsms.Domain.Security;

namespace Grsms.Web.Helpers;

/// <summary>
/// Usability U1 (10 November 2026): which menu items and tabs a person sees, beyond their permissions. Only for tidiness: hiding
/// a link never changes what someone may do (the server still checks every request).
/// </summary>
public static class MenuRules
{
    /// <summary>Roles that run recruitment day to day (screening, hiring, offers).</summary>
    private static readonly string[] RecruitmentRoles =
        [Roles.RecruitmentOfficer, Roles.HrManager, Roles.HrAdministrator, Roles.Admin, Roles.SuperAdmin, Roles.ManagingDirector];

    /// <summary>
    /// A Head of Department with no recruitment role. HODs no longer decide hires (user's decision, 9 November 2026), so they
    /// don't need the Screening and Hiring &amp; offers menu items; they still see their candidates under Candidates and on
    /// each vacancy.
    /// </summary>
    public static bool IsHodOnly(ClaimsPrincipal user) =>
        user.IsInRole(Roles.HeadOfDepartment) && !RecruitmentRoles.Any(user.IsInRole);
}
