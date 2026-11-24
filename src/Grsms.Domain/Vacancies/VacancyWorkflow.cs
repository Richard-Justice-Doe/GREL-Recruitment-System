using Grsms.Domain.Security;

namespace Grsms.Domain.Vacancies;

/// <summary>
/// The rules of a vacancy's life cycle, in one place. Since 17 November 2026 the Managing Director's approval of the request is
/// the last approval: the Recruitment Officer publishes the vacancy without any further approval.
///
///   Draft ──publish (RO)──► Published ──close / closing date passes──► Closed ──archive──► Archived
///                                                                      │
///                                              reopen (new date) ◄─────┘
///   Draft ──archive──► Archived   (abandoned before publishing)
///
///   * The Recruitment Officer creates (from a request approved for recruitment), publishes, closes, reopens and archives.
///   * Pending approval: only vacancies submitted to the HR Manager before 17 Nov 2026. The Recruitment Officer can publish them
///     now; the HR Manager can still approve (publish) or return them.
/// </summary>
public static class VacancyWorkflow
{
    /// <summary>The role that could approve a vacancy pending under the earlier workflow.</summary>
    public const string ApproverRole = Roles.HrManager;

    /// <summary>Roles that manage vacancies day to day (close, reopen, archive).</summary>
    public static readonly IReadOnlyList<string> ManagerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    public static bool IsEditable(VacancyStatus s) => s == VacancyStatus.Draft;

    /// <summary>The Recruitment Officer may publish it: a draft, or one still pending from the earlier approval step.</summary>
    public static bool CanPublish(VacancyStatus s) => s is VacancyStatus.Draft or VacancyStatus.PendingApproval;

    /// <summary>Earlier workflow only: the HR Manager may still decide on a vacancy submitted before 17 Nov 2026.</summary>
    public static bool CanDecide(VacancyStatus s) => s == VacancyStatus.PendingApproval;
    public static bool CanClose(VacancyStatus s) => s == VacancyStatus.Published;
    public static bool CanReopen(VacancyStatus s) => s == VacancyStatus.Closed;
    public static bool CanArchive(VacancyStatus s) => s is VacancyStatus.Draft or VacancyStatus.Closed;

    /// <summary>True when candidates may apply: published and the closing date has not passed.</summary>
    public static bool IsOpenForApplications(Vacancy v, DateOnly today) =>
        v.Status == VacancyStatus.Published && v.ClosingDate >= today;
}
