using Grsms.Domain.Security;

namespace Grsms.Domain.Recruitment;

/// <summary>
/// The rules of the recruitment request workflow (form QF-81-11), in one place:
///
///   Draft ──submit──► 1. Department Head review ──approve──► 2. HR Manager review ──approve──► 3. MD approval ──approve──► Approved
///     ▲                         │                                  │                               │
///     └──── Returned ◄──────────┴────── return for correction ─────┴───────────────────────────────┘
///                               └────── reject ──► Rejected (final)
///
///   * A request is raised by a department staff member (or the HOD) for their own department.
///   * Stage 1 is decided by the Head of THAT department; stages 2 and 3 by the role in <see cref="ApproverRole"/>.
///     When the HOD raises the request themselves, their submission is their stage-1 sign-off and the request goes
///     straight to the HR Manager.
///   * "Return for correction" sends the request back to the requester, who edits and resubmits it.
///     Resubmitting starts again at stage 1.
///   * The requester can cancel the request at any point before a final decision.
///   * Only after the MD's final approval may recruitment begin (a vacancy is created from the request).
///
/// Changing the workflow (e.g. adding a stage) means changing only this class and the
/// <see cref="RecruitmentRequestStatus"/> list.
/// </summary>
public static class RecruitmentWorkflow
{
    /// <summary>The approval stages of the current workflow, in order.</summary>
    public static readonly IReadOnlyList<RecruitmentRequestStatus> ApprovalStages =
    [
        RecruitmentRequestStatus.DepartmentHeadReview,
        RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.MdApproval
    ];

    /// <summary>Every status that waits for somebody's decision, including the earlier workflow's Recruitment review.</summary>
    public static readonly IReadOnlyList<RecruitmentRequestStatus> DecisionStages =
    [
        RecruitmentRequestStatus.DepartmentHeadReview,
        RecruitmentRequestStatus.RecruitmentReview,
        RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.MdApproval
    ];

    /// <summary>Engagement types offered on the form: Permanent and Contract.</summary>
    public static readonly IReadOnlyList<EmploymentType> FormEngagementTypes = [EmploymentType.Permanent, EmploymentType.FixedTermContract];

    /// <summary>Purposes offered on the form, in the order printed on QF-81-11.</summary>
    public static readonly IReadOnlyList<VacancyReason> FormPurposes =
        [VacancyReason.Restructuring, VacancyReason.Replacement, VacancyReason.Expansion, VacancyReason.NewPosition, VacancyReason.Other];

    /// <summary>
    /// The role that decides at <paramref name="status"/>, or null if nobody needs to decide.
    /// At <see cref="RecruitmentRequestStatus.DepartmentHeadReview"/> the person must also head the request's department.
    /// </summary>
    public static string? ApproverRole(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.DepartmentHeadReview => Roles.HeadOfDepartment,
        RecruitmentRequestStatus.RecruitmentReview => Roles.RecruitmentOfficer,
        RecruitmentRequestStatus.HrApproval => Roles.HrManager,
        RecruitmentRequestStatus.MdApproval => Roles.ManagingDirector,
        _ => null
    };

    /// <summary>The stage's name as used in emails and messages.</summary>
    public static string StageName(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.DepartmentHeadReview => "Department Head review",
        RecruitmentRequestStatus.RecruitmentReview => "Recruitment review",
        RecruitmentRequestStatus.HrApproval => "HR Manager review",
        RecruitmentRequestStatus.MdApproval => "MD approval",
        _ => status.ToString()
    };

    /// <summary>Where the request goes when the current stage approves it.</summary>
    public static RecruitmentRequestStatus NextAfterApproval(RecruitmentRequestStatus status) => status switch
    {
        RecruitmentRequestStatus.DepartmentHeadReview => RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.RecruitmentReview => RecruitmentRequestStatus.HrApproval,
        RecruitmentRequestStatus.HrApproval => RecruitmentRequestStatus.MdApproval,
        RecruitmentRequestStatus.MdApproval => RecruitmentRequestStatus.Approved,
        _ => throw new InvalidOperationException($"A request in status {status} cannot be approved.")
    };

    /// <summary>True while the requester may still change the request's details and attachments.</summary>
    public static bool IsEditable(RecruitmentRequestStatus status) =>
        status is RecruitmentRequestStatus.Draft or RecruitmentRequestStatus.Returned;

    /// <summary>True once nothing more can happen in this workflow.</summary>
    public static bool IsFinal(RecruitmentRequestStatus status) =>
        status is RecruitmentRequestStatus.Approved or RecruitmentRequestStatus.Rejected or RecruitmentRequestStatus.Cancelled;

    /// <summary>True while the request waits for an approver (any stage).</summary>
    public static bool IsInApproval(RecruitmentRequestStatus status) => DecisionStages.Contains(status);
}
