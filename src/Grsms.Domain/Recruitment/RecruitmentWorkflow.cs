using Grsms.Domain.Security;

namespace Grsms.Domain.Recruitment;

/// <summary>
/// The rules of the recruitment request workflow, in one place:
///
///   Draft ──submit──► Recruitment Review ──approve──► HR Approval ──approve──► MD Approval ──approve──► Approved
///     ▲                     │                             │                         │
///     └──── Returned ◄──────┴──────── return ─────────────┴─────────────────────────┘
///                           └──────── reject ──► Rejected (final)
///
///   * Each approval stage is handled by ONE role (see <see cref="ApproverRole"/>).
///   * "Return for changes" sends the request back to the HOD, who can edit and resubmit it.
///     Resubmitting starts again at Recruitment Review.
///   * The requester can cancel the request at any point before a final decision.
///
/// Changing the workflow (e.g. adding a stage) means changing only this class and the
/// <see cref="RecruitmentRequestStatus"/> list.
/// </summary>
public static class RecruitmentWorkflow
{
    /// <summary>The approval stages, in order.</summary>
    public static readonly IReadOnlyList<RecruitmentRequestStatus> ApprovalStages =
    [
        RecruitmentRequestStatus.RecruitmentReview,
        RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.MdApproval
    ];

    /// <summary>The role that decides at <paramref name="status"/>, or null if nobody needs to approve.</summary>
    public static string? ApproverRole(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.RecruitmentReview => Roles.RecruitmentOfficer,
        RecruitmentRequestStatus.HrApproval => Roles.HrManager,
        RecruitmentRequestStatus.MdApproval => Roles.ManagingDirector,
        _ => null
    };

    /// <summary>Where the request goes when the current stage approves it.</summary>
    public static RecruitmentRequestStatus NextAfterApproval(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.RecruitmentReview => RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.HrApproval => RecruitmentRequestStatus.MdApproval,
        RecruitmentRequestStatus.MdApproval => RecruitmentRequestStatus.Approved,
        _ => throw new InvalidOperationException($"A request in status {status} cannot be approved.")
    };

    /// <summary>True while the HOD may still change the request's details and attachments.</summary>
    public static bool IsEditable(RecruitmentRequestStatus status) =>
        status is RecruitmentRequestStatus.Draft or RecruitmentRequestStatus.Returned;

    /// <summary>True while the request waits for an approver.</summary>
    public static bool IsAwaitingApproval(RecruitmentRequestStatus status) => ApproverRole(status) is not null;

    /// <summary>True once nothing more can happen in this workflow.</summary>
    public static bool IsFinal(RecruitmentRequestStatus status) =>
        status is RecruitmentRequestStatus.Approved or RecruitmentRequestStatus.Rejected or RecruitmentRequestStatus.Cancelled;
}
