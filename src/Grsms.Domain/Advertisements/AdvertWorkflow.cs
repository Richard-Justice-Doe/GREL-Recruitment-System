using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Advertisements;

/// <summary>
/// The rules of a job advert's life cycle, in one place:
///
///   Draft ──publish (start date today)──────► Live ──end date passes / vacancy closes / close──► Closed
///     │                                         ▲
///     └──publish (start date later)──► Scheduled ──start date reached (automatic)
///                                       │  └──close──► Closed
///                                       └──cancel schedule──► Draft
///   Any advert ──clone──► new Draft
///
///   * Recruitment Officers and HR Managers manage adverts. Heads of Department can view their departments' adverts.
///   * An advert can only be published while its vacancy is Published (approval complete) and still open.
///   * Internal vacancies may only be advertised on the internal portal.
/// </summary>
public static class AdvertWorkflow
{
    /// <summary>Roles that create, publish, close and clone adverts.</summary>
    public static readonly IReadOnlyList<string> ManagerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Channels GRSMS publishes by itself when the advert goes live.</summary>
    public static readonly IReadOnlyList<AdvertChannel> AutomaticChannels = [AdvertChannel.InternalPortal, AdvertChannel.CompanyWebsite];

    public static bool IsAutomatic(AdvertChannel c) => AutomaticChannels.Contains(c);

    /// <summary>Channels allowed for a vacancy: staff-only vacancies may only use the internal portal.</summary>
    public static IReadOnlyList<AdvertChannel> AllowedChannels(VacancyType type) =>
        type == VacancyType.Internal ? [AdvertChannel.InternalPortal] : Enum.GetValues<AdvertChannel>();

    public static bool IsEditable(AdvertStatus s) => s is AdvertStatus.Draft or AdvertStatus.Scheduled;
    public static bool CanPublish(AdvertStatus s) => s == AdvertStatus.Draft;
    public static bool CanUnschedule(AdvertStatus s) => s == AdvertStatus.Scheduled;
    public static bool CanClose(AdvertStatus s) => s is AdvertStatus.Live or AdvertStatus.Scheduled;

    /// <summary>The vacancy must have finished approval (Published) and not passed its closing date.</summary>
    public static bool VacancyAllowsAdvertising(Vacancy v, DateOnly today) => VacancyWorkflow.IsOpenForApplications(v, today);

    /// <summary>True when the advert is running today (live and within its dates).</summary>
    public static bool IsRunning(JobAdvert a, DateOnly today) =>
        a.Status == AdvertStatus.Live && a.StartDate <= today && a.EndDate >= today;
}
