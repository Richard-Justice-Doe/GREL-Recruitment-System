using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Advertisements;

/// <summary>
/// The rules of a job advert's life cycle, in one place. Since 11 November 2026 (docs/29 change 2) every advert is
/// reviewed by the HR Manager and approved by the IT Manager before it is published:
///
///   Draft / Returned for correction ──submit (RO)──► Awaiting HR review
///   Awaiting HR review   ──approve (HR Manager)──► Awaiting IT approval     ──return──► Returned for correction   ──reject──► Rejected (final)
///   Awaiting IT approval ──approve (IT Manager)──► Published (start today)  ──return──► Returned for correction   ──reject──► Rejected (final)
///                                              or Scheduled (start later) ──start date reached (automatic)──► Published
///   Scheduled            ──cancel schedule (RO)──► Draft
///   Published / Scheduled ──take offline (IT)──► Returned for correction;  ──edited by the RO──► Awaiting HR review (offline)
///   Published / Scheduled ──end date passes / vacancy closes / close──► Closed
///   Any advert ──clone──► new Draft          (Published is stored as "Live", so existing data and the careers pages are unchanged)
///
///   * Recruitment Officers (and HR Managers) write adverts and send them for review (<see cref="AuthorRoles"/>).
///   * The review steps are permissions: adverts.review.hr, adverts.review.it, adverts.publish, adverts.return, adverts.unpublish.
///   * Editing a published or scheduled advert takes it offline and sends it back through both reviews.
///   * Only the IT Manager (adverts.publish) records external channels as posted or removed.
///   * An advert can only be sent for review or published while its vacancy is Published (approval complete) and still open.
///   * Internal vacancies may only be advertised on the internal portal.
/// </summary>
public static class AdvertWorkflow
{
    /// <summary>Roles that write, submit, close and clone adverts.</summary>
    public static readonly IReadOnlyList<string> AuthorRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Channels GRSMS publishes by itself when the advert goes live.</summary>
    public static readonly IReadOnlyList<AdvertChannel> AutomaticChannels = [AdvertChannel.InternalPortal, AdvertChannel.CompanyWebsite];

    /// <summary>The steps under review (HR, then IT).</summary>
    public static readonly IReadOnlyList<AdvertStatus> InReview = [AdvertStatus.AwaitingHrReview, AdvertStatus.AwaitingItApproval];

    public static bool IsAutomatic(AdvertChannel c) => AutomaticChannels.Contains(c);

    /// <summary>Channels allowed for a vacancy: staff-only vacancies may only use the internal portal.</summary>
    public static IReadOnlyList<AdvertChannel> AllowedChannels(VacancyType type) =>
        type == VacancyType.Internal ? [AdvertChannel.InternalPortal] : Enum.GetValues<AdvertChannel>();

    /// <summary>Published (live) or scheduled: approved and on its way to, or on, the channels.</summary>
    public static bool IsPublished(AdvertStatus s) => s is AdvertStatus.Live or AdvertStatus.Scheduled;

    /// <summary>The author may change it: a draft, a returned advert, or (taking it offline) a published one.</summary>
    public static bool IsEditable(AdvertStatus s) => s is AdvertStatus.Draft or AdvertStatus.ReturnedForCorrection || IsPublished(s);

    /// <summary>The author may send it for the HR review.</summary>
    public static bool CanSubmit(AdvertStatus s) => s is AdvertStatus.Draft or AdvertStatus.ReturnedForCorrection;

    public static bool CanUnschedule(AdvertStatus s) => s == AdvertStatus.Scheduled;
    public static bool CanClose(AdvertStatus s) => IsPublished(s);

    /// <summary>The permission that acts at this review step (approve or reject), or null when it is not under review.</summary>
    public static string? ReviewPermission(AdvertStatus s) => s switch
    {
        AdvertStatus.AwaitingHrReview => Permissions.ReviewAdvertsHr,
        AdvertStatus.AwaitingItApproval => Permissions.ReviewAdvertsIt,
        _ => null
    };

    /// <summary>The vacancy must have finished approval (Published) and not passed its closing date.</summary>
    public static bool VacancyAllowsAdvertising(Vacancy v, DateOnly today) => VacancyWorkflow.IsOpenForApplications(v, today);

    /// <summary>True when the advert is running today (live and within its dates).</summary>
    public static bool IsRunning(JobAdvert a, DateOnly today) =>
        a.Status == AdvertStatus.Live && a.StartDate <= today && a.EndDate >= today;
}
