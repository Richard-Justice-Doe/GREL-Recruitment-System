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

    /// <summary>Phase 20: open employees' electronic personnel files (personal and identification documents).</summary>
    public const string ViewPersonnelFiles = "personnel.view";

    // ---------------------------------------------------------------- Phase 27: onboarding
    /// <summary>See every onboarding, the new-hire documents and the personal record (read-only) and the tracking dashboard.</summary>
    public const string ViewOnboarding = "onboarding.view";

    /// <summary>Start onboarding, issue the letter, set the document requirements, first-day details, checklist, notes, cancel.</summary>
    public const string ManageOnboarding = "onboarding.manage";

    /// <summary>Start the review of, verify and reject new-hire documents, and ask the new employee for corrections.</summary>
    public const string VerifyOnboardingDocuments = "onboarding.verify-documents";

    /// <summary>Activate the employee (staff number, staff account) and close the induction.</summary>
    public const string CompleteOnboarding = "onboarding.complete";

    // ---------------------------------------------------------------- Probation (codebase audit decision, 10 November 2026)
    /// <summary>See every probation and the probation reports (the HOD still sees their own department without it).</summary>
    public const string ViewProbation = "probation.view";

    /// <summary>Decide the outcome of a probation (confirm, extend, do not confirm) and issue the confirmation letter.</summary>
    public const string DecideProbation = "probation.decide";

    // ---------------------------------------------------------------- Data visibility
    /// <summary>See the records of every department (otherwise a HOD sees only their own departments).</summary>
    public const string SeeAllDepartments = "data.all-departments";

    /// <summary>See Roles &amp; permissions and the ATS settings, read-only (added with the Admin role, 5 November 2026).</summary>
    public const string ViewSystemSettings = "settings.view";

    // ---------------------------------------------------------------- Super Admin only
    public const string ManagePermissions = "permissions.manage";

    /// <summary>Phase 15: change the ATS weights, bands and automatic-rejection rules, and switch the AI CV reader on or off.</summary>
    public const string ConfigureAts = "ats.configure";

    /// <summary>Phase 16: change system settings (email server, Microsoft 365, workflow) and email templates.</summary>
    public const string ManageSystemSettings = "settings.manage";

    /// <summary>Phase 16: see the backups, start one, and download one (a backup holds ALL of GREL's recruitment data).</summary>
    public const string ManageBackups = "backups.manage";

    /// <summary>Every permission, in the order shown on screen.</summary>
    public static readonly IReadOnlyList<PermissionInfo> Catalogue =
    [
        new(ManageUsers, "Administration", "Manage user accounts",
            "Open Users & roles: add staff accounts, reset passwords, unlock, deactivate. Only Employee and Interview Panel Member can be given without the Super Admin."),
        new(ManageMasterData, "Administration", "Maintain organisation data",
            "Add and edit divisions, departments, units and sites, and choose each department's HOD."),
        new(ViewAuditTrail, "Administration", "Read the audit trail",
            "See every change and sign-in recorded by the system. (Everyone can always see their own audit trail.)"),
        new(ViewSystemSettings, "Administration", "See system settings (read-only)",
            "Open Roles & permissions, ATS settings, System settings and Email templates without being able to change them (secrets are never shown)."),

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
        new(ViewPersonnelFiles, "Recruitment", "Open personnel files",
            "See employees' electronic personnel files (identification, compliance and onboarding records) and file documents in them."),

        new(ViewOnboarding, "Onboarding", "Open onboarding",
            "See every onboarding, the new-hire documents, the personal record (read-only) and the onboarding tracking dashboard."),
        new(ManageOnboarding, "Onboarding", "Manage onboarding",
            "Start onboarding for an accepted offer, issue the appointment letter, choose the documents each new hire must provide, change first-day details, the checklist and notes, and cancel."),
        new(VerifyOnboardingDocuments, "Onboarding", "Verify onboarding documents",
            "Review, verify and reject new-hire documents (Ghana Card, medical report, police clearance, ...) and ask the new employee for corrections."),
        new(CompleteOnboarding, "Onboarding", "Activate employees",
            "Activate a new employee who is ready (staff number, staff account) and close the departmental induction."),
        new(ViewProbation, "Probation", "Open probation",
            "See every probation, its objectives, assessments and confirmation letter, and the probation reports."),
        new(DecideProbation, "Probation", "Decide probation outcomes",
            "Confirm the employment, extend the probation or decide not to confirm, on the HOD's recommendation (issues the confirmation letter)."),

        new(SeeAllDepartments, "Data visibility", "See all departments",
            "See the records of every department. Without it, a Head of Department sees only their own departments."),

        new(ManagePermissions, "Super Admin only", "Manage roles and permissions",
            "Add custom roles and decide which permissions each role holds.", SuperAdminOnly: true),
        new(ConfigureAts, "Super Admin only", "Configure the ATS",
            "Change the ATS score weights, classification bands and automatic-rejection rules, and switch the AI CV reader on or off.",
            SuperAdminOnly: true),
        new(ManageSystemSettings, "Super Admin only", "Change system settings and email templates",
            "Change the email server, Microsoft 365 sign-in, recruitment workflow settings and the wording of candidate emails.",
            SuperAdminOnly: true),
        new(ManageBackups, "Super Admin only", "Manage backups",
            "See the database backups, start one now and download one. A backup holds all recruitment data.", SuperAdminOnly: true),
    ];

    /// <summary>
    /// The permissions each built-in role starts with. These reproduce exactly what each role could open before
    /// Phase 14, so nobody gained or lost access when permissions were introduced.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Defaults = new Dictionary<string, IReadOnlyList<string>>
    {
        [Roles.Admin] =
        [
            ManageUsers, ManageMasterData, ViewAuditTrail, ViewSystemSettings, ViewRecruitmentRequests, ViewVacancies, ViewAdverts,
            ViewApplications, ViewReports, ViewSurveyResults, SeeAllDepartments
        ],
        [Roles.HrAdministrator] =
        [
            ManageUsers, ManageMasterData, ViewAuditTrail, ViewRecruitmentRequests, ViewVacancies, ViewAdverts,
            ViewApplications, ViewReports, SeeAllDepartments
        ],
        [Roles.HrManager] =
        [
            ViewAuditTrail, ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports,
            ViewSurveyResults, SeeAllDepartments, ViewPersonnelFiles, ViewOnboarding, CompleteOnboarding, ViewProbation, DecideProbation
        ],
        [Roles.ManagingDirector] =
            [ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports, ViewSurveyResults, SeeAllDepartments],
        [Roles.RecruitmentOfficer] =
        [
            ViewRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports, ViewSurveyResults, SeeAllDepartments, ViewPersonnelFiles,
            ViewOnboarding, ManageOnboarding, VerifyOnboardingDocuments, CompleteOnboarding, ViewProbation
        ],
        [Roles.HeadOfDepartment] =
            [ViewRecruitmentRequests, RaiseRecruitmentRequests, ViewVacancies, ViewAdverts, ViewApplications, ViewReports],
        [Roles.PanelMember] = [],
        // QF-81-11 (November 2026): department staff fill in recruitment requests for their own department; the HOD reviews them.
        [Roles.Employee] = [ViewRecruitmentRequests, RaiseRecruitmentRequests],
        [Roles.Candidate] = [],
    };

    /// <summary>
    /// Built-in roles added after permissions were introduced (Phase 14). At the first start-up that knows them, each gets
    /// ALL its <see cref="Defaults"/> once (its permissions already existed, so the per-permission defaults never reached it).
    /// Roles that existed before are not touched, so the Super Admin's choices for them stay.
    /// </summary>
    public static readonly IReadOnlyList<string> RolesAddedLater = [Roles.Admin];

    /// <summary>
    /// Single grants added to <see cref="Defaults"/> after the permission itself already existed. Each is applied ONCE at the
    /// next start-up (recorded in KnownPermissions as "grant:&lt;role&gt;:&lt;permission&gt;"), so if the Super Admin later
    /// takes it away again, it stays away.
    /// </summary>
    public static readonly IReadOnlyList<(string Role, string Permission)> GrantsAddedLater =
    [
        (Roles.Employee, ViewRecruitmentRequests),
        (Roles.Employee, RaiseRecruitmentRequests),
    ];

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
