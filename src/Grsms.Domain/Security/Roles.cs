namespace Grsms.Domain.Security;

/// <summary>
/// The built-in security roles in GRSMS. A user can hold more than one role (e.g. a HOD is also an Employee).
/// A Super Admin can also add custom roles (Phase 14); those live only in the database.
///
/// These names are stored in the Roles table and checked on every protected page, so never rename
/// one without also updating the database. The full permission matrix is in docs/04-security-and-roles.md.
/// </summary>
public static class Roles
{
    /// <summary>
    /// Phase 14: full access to the whole system. Holds every permission (they cannot be taken away), is the only role
    /// that may create privileged accounts (HR Managers, Recruitment Officers, HODs, ...) and change permissions.
    /// </summary>
    public const string SuperAdmin = "Super Admin";

    /// <summary>
    /// Admin (system administrator), added 5 November 2026 at GREL's request: between HR Administrator and Super Admin.
    /// Manages every user account except other Admins and Super Admins (details, password resets, unlock, deactivate),
    /// maintains organisation data, reads the whole audit trail and sees the system settings read-only. Like everyone but
    /// the Super Admin, cannot GIVE privileged roles (HR Manager, Recruitment Officer, HOD, ...), change permissions or ATS rules.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>Maintains master data (departments, units, sites) and everyday accounts (Employee, panel member), and can view the audit trail.</summary>
    public const string HrAdministrator = "HR Administrator";

    /// <summary>GREL staff member: views internal vacancies, applies internally, tracks own applications.</summary>
    public const string Employee = "Employee";

    /// <summary>Head of Department: reviews (stage 1) and raises recruitment requests, shortlists, sits on interviews.</summary>
    public const string HeadOfDepartment = "Head of Department";

    /// <summary>Recruitment Officer: manages vacancies, candidates, adverts and interview schedules.</summary>
    public const string RecruitmentOfficer = "Recruitment Officer";

    /// <summary>HR Manager: approves requests, validates budgets, authorises hiring decisions.</summary>
    public const string HrManager = "HR Manager";

    /// <summary>Managing Director: final approval of vacancies and hires, executive dashboard.</summary>
    public const string ManagingDirector = "Managing Director";

    /// <summary>Interview panel member: reviews interviews and completes scorecards.</summary>
    public const string PanelMember = "Interview Panel Member";

    /// <summary>External job seeker who registered on the public candidate portal.</summary>
    public const string Candidate = "Candidate";

    /// <summary>Every built-in role, in the order they are shown on the user management screen.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, Admin, HrAdministrator, HrManager, ManagingDirector, RecruitmentOfficer,
        HeadOfDepartment, PanelMember, Employee, Candidate
    ];

    /// <summary>Built-in roles that belong to GREL staff (everything except Candidate).</summary>
    public static readonly IReadOnlyList<string> Staff = All.Where(r => r != Candidate).ToList();

    /// <summary>
    /// Phase 14: the only roles an HR Administrator may give or take away. Every other role (built-in or custom) can
    /// only be assigned by a Super Admin, and accounts holding one can only be edited by a Super Admin, so nobody can
    /// raise their own (or a colleague's) access.
    /// </summary>
    public static readonly IReadOnlyList<string> AssignableByHrAdministrator = [Employee, PanelMember];

    /// <summary>True when <paramref name="roles"/> include a role only a Super Admin may give or take away.</summary>
    public static bool NeedsSuperAdmin(IEnumerable<string> roles) =>
        roles.Any(r => r != Candidate && !AssignableByHrAdministrator.Contains(r));

    /// <summary>
    /// May someone holding <paramref name="actorRoles"/> edit, reset the password of, or (de)activate an account holding
    /// <paramref name="targetRoles"/>?
    ///   * Super Admin: any account.
    ///   * Admin: any account except Admins and Super Admins (privileged roles on it stay as they are).
    ///   * Everyone else (HR Administrator): only accounts with everyday roles (Employee, Interview Panel Member).
    /// </summary>
    public static bool CanManageAccount(IEnumerable<string> actorRoles, IEnumerable<string> targetRoles)
    {
        var actor = actorRoles.ToList();
        var target = targetRoles.ToList();
        if (actor.Contains(SuperAdmin)) return true;
        if (actor.Contains(Admin)) return !target.Contains(SuperAdmin) && !target.Contains(Admin);
        return !NeedsSuperAdmin(target);
    }

    /// <summary>One-line explanation of each built-in role, for the Roles &amp; permissions screen.</summary>
    public static string Describe(string role) => role switch
    {
        SuperAdmin => "Full access to the whole system. Creates privileged accounts and sets permissions.",
        Admin => "System administrator: manages all user accounts except Admins and Super Admins, organisation data and the audit trail; sees settings read-only.",
        HrAdministrator => "Maintains departments, divisions, units and sites, and everyday staff accounts.",
        HrManager => "Approves requests and vacancies, authorises shortlists and hiring decisions.",
        ManagingDirector => "Final approval of recruitment requests and hires.",
        RecruitmentOfficer => "Runs vacancies, adverts, screening, interviews and offers.",
        HeadOfDepartment => "Reviews (stage 1) and raises recruitment requests for their department, and takes part in selection.",
        PanelMember => "Sits on interview panels and completes scorecards.",
        Employee => "GREL staff member: sees internal vacancies, applies internally and fills in recruitment requests for their department.",
        Candidate => "External job seeker using the candidate portal.",
        _ => ""
    };

    /// <summary>True for the roles that come with GRSMS (their names are fixed and they cannot be deleted).</summary>
    public static bool IsBuiltIn(string role) => All.Contains(role);
}
