using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Advertisements;

/// <summary>
/// The rules of a job advert's life cycle, in one place. Since 17 November 2026 the Managing Director's approval of the request
/// is the last approval: the Recruitment Officer creates, edits, publishes, schedules and closes adverts with no review.
///
///   Draft / Returned for correction ──publish (RO)──► Published (start today)
///                                                  or Scheduled (start later) ──start date reached (automatic)──► Published
///   Scheduled            ──cancel schedule (RO)──► Draft
///   Published / Scheduled ──edited by the RO──► stays published, with the new text (no review)
///   Published / Scheduled ──take offline (IT)──► Returned for correction
///   Published / Scheduled ──end date passes / vacancy closes / close──► Closed
///   Any advert ──clone──► new Draft          (Published is stored as "Live", so existing data and the careers pages are unchanged)
///
///   * Recruitment Officers (and HR Managers) write and publish adverts (<see cref="AuthorRoles"/>), and record external channels
///     as posted or removed (so can the IT Manager).
///   * Adverts sent for review before 17 Nov 2026 (Awaiting HR review / Awaiting IT approval) can be published by the Recruitment
///     Officer straight away; the reviewers can still return or reject them.
///   * An advert can only be published while its vacancy is Published and still open.
///   * Internal vacancies may only be advertised on the internal portal.
/// </summary>
public static class AdvertWorkflow
{
    /// <summary>Roles that write, publish, close and clone adverts.</summary>
    public static readonly IReadOnlyList<string> AuthorRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Channels GRSMS publishes by itself when the advert goes live.</summary>
    public static readonly IReadOnlyList<AdvertChannel> AutomaticChannels = [AdvertChannel.InternalPortal, AdvertChannel.CompanyWebsite];

    /// <summary>The review steps of the earlier workflow (HR, then IT); no new advert enters them.</summary>
    public static readonly IReadOnlyList<AdvertStatus> InReview = [AdvertStatus.AwaitingHrReview, AdvertStatus.AwaitingItApproval];

    public static bool IsAutomatic(AdvertChannel c) => AutomaticChannels.Contains(c);

    /// <summary>Channels allowed for a vacancy: staff-only vacancies may only use the internal portal.</summary>
    public static IReadOnlyList<AdvertChannel> AllowedChannels(VacancyType type) =>
        type == VacancyType.Internal ? [AdvertChannel.InternalPortal] : Enum.GetValues<AdvertChannel>();

    /// <summary>Published (live) or scheduled: approved and on its way to, or on, the channels.</summary>
    public static bool IsPublished(AdvertStatus s) => s is AdvertStatus.Live or AdvertStatus.Scheduled;

    /// <summary>The author may change it: a draft, a returned one, one still in an earlier review, or a published one (it stays published).</summary>
    public static bool IsEditable(AdvertStatus s) => CanPublish(s) || IsPublished(s);

    /// <summary>The author may publish it (no review since 17 Nov 2026), including one left in an earlier review step.</summary>
    public static bool CanPublish(AdvertStatus s) => s is AdvertStatus.Draft or AdvertStatus.ReturnedForCorrection || InReview.Contains(s);

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
