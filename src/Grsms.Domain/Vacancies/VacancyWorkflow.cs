using Grsms.Domain.Security;

namespace Grsms.Domain.Vacancies;

/// <summary>
/// The rules of a vacancy's life cycle, in one place:
///
///   Draft ──submit──► Pending approval ──approve──► Published ──close / closing date passes──► Closed ──archive──► Archived
///     ▲                     │                                                                 │
///     └──── return ─────────┘                                         reopen (new date) ◄─────┘
///   Draft ──archive──► Archived   (abandoned before publishing)
///
///   * The Recruitment Officer prepares, submits, closes, reopens and archives.
///   * The HR Manager approves (which publishes) or returns with comments.
/// </summary>
public static class VacancyWorkflow
{
    /// <summary>The role that approves a vacancy.</summary>
    public const string ApproverRole = Roles.HrManager;

    /// <summary>Roles that manage vacancies day to day (close, reopen, archive).</summary>
    public static readonly IReadOnlyList<string> ManagerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    public static bool IsEditable(VacancyStatus s) => s == VacancyStatus.Draft;
    public static bool CanSubmit(VacancyStatus s) => s == VacancyStatus.Draft;
    public static bool CanDecide(VacancyStatus s) => s == VacancyStatus.PendingApproval;
    public static bool CanClose(VacancyStatus s) => s == VacancyStatus.Published;
    public static bool CanReopen(VacancyStatus s) => s == VacancyStatus.Closed;
    public static bool CanArchive(VacancyStatus s) => s is VacancyStatus.Draft or VacancyStatus.Closed;

    /// <summary>True when candidates may apply: published and the closing date has not passed.</summary>
    public static bool IsOpenForApplications(Vacancy v, DateOnly today) =>
        v.Status == VacancyStatus.Published && v.ClosingDate >= today;
}
