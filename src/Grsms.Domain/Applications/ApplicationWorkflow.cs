using Grsms.Domain.Advertisements;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>
/// The rules of a job application, in one place:
///
///   Submitted (Received) ──start review──► Under review ──(Phase 6+)──► Shortlisted ► Interview ► Offer ► Hired
///        │                                      │
///        ├──── not successful ◄─────────────────┤        (staff, with an internal reason)
///        └──── withdrawn ◄──────────────────────┘        (the applicant)
///
///   * Anyone signed in may apply: candidates to adverts on the company website, GREL staff also to internal-portal
///     adverts. Staff-only (Internal) vacancies accept staff only.
///   * One application per person per vacancy, while the vacancy is published, open and advertised.
///   * Recruitment Officers and HR Managers review; HODs see their departments' applications (and shortlist in Phase 6).
///   * Shortlisting (Phase 6): the recruiter recommends, the department's HOD gives their view, then the recruiter or
///     HR Manager shortlists; without the HOD's agreement only an HR Manager can, with a reason.
///   * Nobody may act on their own application.
/// </summary>
public static class ApplicationWorkflow
{
    /// <summary>Roles that review applications.</summary>
    public static readonly IReadOnlyList<string> ReviewerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Statuses in which the application is still going (can be withdrawn or closed).</summary>
    public static readonly IReadOnlyList<ApplicationStatus> Active =
        [ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.Offer];

    /// <summary>The stages shown on the applicant's progress tracker, in order.</summary>
    public static readonly IReadOnlyList<ApplicationStatus> TrackerStages =
        [ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Hired];

    public static bool IsActive(ApplicationStatus s) => Active.Contains(s);
    public static bool CanWithdraw(ApplicationStatus s) => IsActive(s) && s != ApplicationStatus.Offer;
    public static bool CanStartReview(ApplicationStatus s) => s == ApplicationStatus.Submitted;
    public static bool CanMarkNotSuccessful(ApplicationStatus s) => s is ApplicationStatus.Submitted or ApplicationStatus.UnderReview;

    // ---- Phase 6: shortlisting. Recruiter recommends → the department's HOD gives their view → the recruiter or
    // HR Manager shortlists. Without the HOD's agreement, only an HR Manager can shortlist, with a recorded reason.

    /// <summary>Role that may shortlist without (or against) the HOD's agreement, giving a reason.</summary>
    public const string OverrideRole = Roles.HrManager;

    public static bool CanRecommend(ApplicationStatus s) => s is ApplicationStatus.Submitted or ApplicationStatus.UnderReview;
    public static bool CanHodReview(JobApplication a) =>
        a.Status == ApplicationStatus.UnderReview && a.RecruiterRecommendation != ShortlistRecommendation.None;
    public static bool CanShortlist(ApplicationStatus s) => s == ApplicationStatus.UnderReview;

    /// <summary>True when shortlisting needs an HR Manager override (the HOD has not agreed).</summary>
    public static bool NeedsOverride(JobApplication a) => a.HodRecommendation != ShortlistRecommendation.Shortlist;

    /// <summary>
    /// May a person apply through this advert? <paramref name="isStaff"/> = signed in as GREL staff.
    /// Returns null if yes, otherwise the reason.
    /// </summary>
    public static string? WhyCannotApply(Vacancy v, JobAdvert advert, IEnumerable<AdvertChannel> postedChannels, bool isStaff, DateOnly today)
    {
        var channels = postedChannels.ToList();
        if (!VacancyWorkflow.IsOpenForApplications(v, today) || !AdvertWorkflow.IsRunning(advert, today))
            return "This vacancy is no longer accepting applications.";
        if (v.VacancyType == VacancyType.Internal && !isStaff)
            return "This vacancy is open to GREL staff only.";
        var visible = channels.Contains(AdvertChannel.CompanyWebsite) || (isStaff && channels.Contains(AdvertChannel.InternalPortal));
        return visible ? null : "This vacancy is open to GREL staff only.";
    }
}
