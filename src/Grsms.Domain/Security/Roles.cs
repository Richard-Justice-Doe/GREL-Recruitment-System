namespace Grsms.Domain.Security;

/// <summary>
/// The security roles in GRSMS. A user can hold more than one role (e.g. a HOD is also an Employee).
///
/// These names are stored in the Roles table and checked on every protected page, so never rename
/// one without also updating the database. The full permission matrix is in docs/04-security-and-roles.md.
/// </summary>
public static class Roles
{
    /// <summary>Maintains master data (departments, units, sites) and user accounts, and can view the audit trail.</summary>
    public const string HrAdministrator = "HR Administrator";

    /// <summary>GREL staff member: views internal vacancies, applies internally, tracks own applications.</summary>
    public const string Employee = "Employee";

    /// <summary>Head of Department: raises recruitment requests, shortlists, sits on interviews.</summary>
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

    /// <summary>Every role, in the order they are shown on the user management screen.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        HrAdministrator, HrManager, ManagingDirector, RecruitmentOfficer,
        HeadOfDepartment, PanelMember, Employee, Candidate
    ];

    /// <summary>Roles that belong to GREL staff (everything except Candidate).</summary>
    public static readonly IReadOnlyList<string> Staff = All.Where(r => r != Candidate).ToList();
}
