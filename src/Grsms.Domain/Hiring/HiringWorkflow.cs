using Grsms.Domain.Applications;
using Grsms.Domain.Security;

namespace Grsms.Domain.Hiring;

/// <summary>
/// The rules of Module 8, in one place:
///
///   Background checks : recorded by the Recruitment Officer / HR Manager from the interview stage onwards.
///   Hiring recommendation (one active per application):
///       recruiter proposes ──► HOD endorses or not ──► HR Manager approves / rejects ──► MD approves / rejects ──► Approved
///       * needs at least one completed interview or assessment;
///       * if the HOD did not endorse, the HR Manager can only approve with a written reason (override);
///       * nobody approves at two stages, nor a recommendation they proposed, nor their own application.
///   Offer: once Approved, every background check is complete without failure (at least one), and the vacancy still has
///       an unfilled position. Sent → Accepted (application Hired) / Declined / Withdrawn / Expired (after ExpiresOn).
/// </summary>
public static class HiringWorkflow
{
    public static readonly IReadOnlyList<string> RecruiterRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Background checks can be recorded once the applicant is at the interview stage or later.</summary>
    public static bool CanCheck(ApplicationStatus s) => s is ApplicationStatus.Interview or ApplicationStatus.Offer;

    public static bool CanRecommend(ApplicationStatus s) => s == ApplicationStatus.Interview;

    /// <summary>Open = waiting for a decision (HOD, HR Manager or MD). A Draft is not open: nobody decides on it until it is submitted.</summary>
    public static bool IsOpen(HiringStatus s) => s is HiringStatus.AwaitingHod or HiringStatus.AwaitingHr or HiringStatus.AwaitingMd;

    /// <summary>
    /// The background checks opened as Pending when the Recruitment Officer proceeds with a candidate (9 Nov 2026), each with what
    /// is to be checked. Checks the case already has are not added twice.
    /// </summary>
    public static readonly IReadOnlyList<(CheckType Type, string Subject)> StandardChecks =
    [
        (CheckType.References, "Referees named by the candidate"),
        (CheckType.Education, "Certificates submitted with the application"),
        (CheckType.Employment, "Previous employers listed on the CV"),
        (CheckType.CriminalRecord, "Police clearance report"),
        (CheckType.Medical, "Pre-employment medical examination")
    ];

    /// <summary>The proposed start date of a new draft: 30 days from today.</summary>
    public const int DraftStartInDays = 30;

    /// <summary>True when the checks allow an offer: at least one, none pending, none failed.</summary>
    public static bool ChecksAllowOffer(IReadOnlyCollection<BackgroundCheck> checks) =>
        checks.Count > 0 && checks.All(c => c.Status is CheckStatus.Cleared or CheckStatus.Concerns);

    /// <summary>The application stage an offer status puts the application in.</summary>
    public static ApplicationStatus ApplicationStatusFor(OfferStatus s) => s switch
    {
        OfferStatus.Sent => ApplicationStatus.Offer,
        OfferStatus.Accepted => ApplicationStatus.Hired,
        OfferStatus.Declined => ApplicationStatus.OfferDeclined,
        _ => ApplicationStatus.Interview // withdrawn or expired: back to the pool of interviewed applicants
    };
}
