namespace Grsms.Domain.Security;

/// <summary>One permission in the catalogue, as shown on the Roles &amp; permissions screen.</summary>
/// <param name="Name">Stored value, e.g. "vacancies.view". Never rename one: it is saved against roles in the database.</param>
/// <param name="Group">Heading it is listed under.</param>
/// <param name="Label">Short name.</param>
/// <param name="Description">What it allows, in plain words.</param>
/// <param name="SuperAdminOnly">True = held by the Super Admin alone; it can never be given to another role.</param>
public record PermissionInfo(string Name, string Group, string Label, string Description, bool SuperAdminOnly = false);

/// <summary>
/// Phase 14: granular role-based access control (RBAC).
///
/// A PERMISSION opens a screen or a function ("open the Vacancies screens", "maintain master data"). The list of
/// permissions is fixed here in code, because each one is checked by code. WHICH ROLES hold each permission is stored
/// in the database (table RoleClaims, claim type <see cref="ClaimType"/>) and a Super Admin changes it on the
/// Roles &amp; permissions screen. Changes take effect within a minute, without anyone signing out.
///
/// Rules that are NOT permissions, on purpose (they are segregation of duties, so no setting may switch them off):
///   * Who acts at each workflow step (the HOD raises, the HR Manager approves, the MD gives final approval, ...) stays
///     tied to the role, as written in each workflow class.
///   * The Super Admin always holds every permission. Permissions marked <see cref="PermissionInfo.SuperAdminOnly"/>
///     can never be given to another role.
///   * Only a Super Admin may give privileged roles (see <see cref="Roles.AssignableByHrAdministrator"/>).
///
/// When a later phase adds a permission, add it to <see cref="Catalogue"/> and <see cref="Defaults"/>. At the next
/// start-up GRSMS grants the defaults ONCE (see Infrastructure/Identity/PermissionSeeder.cs); after that only the
/// Super Admin's choices count.
/// </summary>
public static class Permissions
{
    /// <summary>The claim type used for permissions in the RoleClaims table.</summary>
    public const string ClaimType = "grsms:permission";

    // ---------------------------------------------------------------- Administration
    public const string ManageUsers = "users.manage";
    public const string ManageMasterData = "masterdata.manage";
    public const string ViewAuditTrail = "audit.view";

    // ---------------------------------------------------------------- Recruitment screens
    public const string ViewRecruitmentRequests = "requests.view";
    public const string RaiseRecruitmentRequests = "requests.raise";
    public const string ViewVacancies = "vacancies.view";
    public const string ViewAdverts = "adverts.view";
    public const string ViewApplications = "applications.view";
    public const string ViewReports = "reports.view";
    public const string ViewSurveyResults = "surveys.results";

    // ---------------------------------------------------------------- Data visibility
    /// <summary>See the records of every department (otherwise a HOD sees only their own departments).</summary>
    public const string SeeAllDepartments = "data.all-departments";

    // ---------------------------------------------------------------- Super Admin only
    public const string ManagePermissions = "permissions.manage";
    public const string ManagePrivilegedAccounts = "users.manage-privileged";

    /// <summary>Every permission, in the order shown on screen.</summary>
    public static readonly IReadOnlyList<PermissionInfo> Catalogue =
    [
        new(ManageUsers, "Administration", "Manage user accounts",
            "Open Users & roles: add staff accounts, reset passwords, unlock, deactivate. Only Employee and Interview Panel Member can be given without the Super Admin."),
        new(ManageMasterData, "Administration", "Maintain organisation data",
            "Add and edit divisions, departments, units and sites, and choose each department's HOD."),
        new(ViewAuditTrail, "Administration", "Read the audit trail",
            "See every change and sign-in recorded by the system."),

        new(ViewRecruitmentRequests, "Recruitment", "Open recruitment requests",
            "See recruitment requests (which ones depends on the departments the person may see)."),
        new(RaiseRecruitmentRequests, "Recruitment", "Raise recruitment requests",
            "Create and submit new recruitment requests for their department."),
        new(ViewVacancies, "Recruitment", "Open vacancies", "See vacancies and their history."),
        new(ViewAdverts, "Recruitment", "Open job adverts", "See job adverts and where they are posted."),
        new(ViewApplications, "Recruitment", "Open applications",
            "See applications, the shortlisting board, hiring and onboarding."),
        new(ViewReports, "Recruitment", "Open reports", "See the recruitment reports and download them as CSV."),
        new(ViewSurveyResults, "Recruitment", "Read survey results", "See the candidate and new-hire survey results."),

        new(SeeAllDepartments, "Data visibility", "See all departments",
            "See the records of every department. Without it, a Head of Department sees only their own departments."),

        new(ManagePermissions, "Super Admin only", "Manage roles and permissions",
            "Add custom roles and decide which permissions each role holds.", SuperAdminOnly: true),
        new(ManagePrivilegedAccounts, "Super Admin only", "Create privileged accounts",
            "Create and edit HR Managers, Recruitment Officers, Heads of Department, HR Administrators, the Managing Director and Super Admins.",
            SuperAdminOnly: true),
    ];

    /// <summary>
    /// The permissions each built-in role starts with. These reproduce exactly what each role could open before
    /// Phase 14, so nobody gained or lost access when permissions were introduced.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Defaults = new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.HrAdministrator] =
        [
            ManageUsers, ManageMasterData, ViewAuditTrail, ViewRecruitmentRequests, ViewVacancies, ViewAdverts,
            ViewApplications, ViewReports, SeeAllDepartments
        ],
        [Roles.HrManager] =
        [
            ViewAuditTrail, ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports,
            ViewSurveyResults, SeeAllDepartments
        ],
        [Roles.ManagingDirector] =
            [ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports, ViewSurveyResults, SeeAllDepartments],
        [Roles.RecruitmentOfficer] =
            [ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports, ViewSurveyResults, SeeAllDepartments],
        [Roles.HeadOfDepartment] =
            [ViewRecruitmentRequests, RaiseRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports],
        [Roles.PanelMember] = [],
        [Roles.Employee] = [],
        [Roles.Candidate] = [],
    };

    public static PermissionInfo? Find(string name) => Catalogue.FirstOrDefault(p => p.Name == name);

    /// <summary>True when <paramref name="name"/> may never be given to a role other than Super Admin.</summary>
    public static bool IsSuperAdminOnly(string name) => Find(name)?.SuperAdminOnly == true;

    /// <summary>
    /// The one rule for "does someone with these roles hold this permission?", given what each role has been granted.
    /// Used by the web site (with the database's grants) and by the service tests (with <see cref="Defaults"/>).
    /// </summary>
    public static bool Holds(IEnumerable<string> userRoles, string permission, Func<string, IEnumerable<string>> grantedTo)
    {
        var roles = userRoles.ToList();
        if (roles.Contains(Roles.SuperAdmin)) return true;
        if (roles.Contains(Roles.Candidate) || IsSuperAdminOnly(permission)) return false; // candidates never get staff permissions
        return roles.Any(r => grantedTo(r).Contains(permission));
    }
}
