using Grsms.Domain.Common;

namespace Grsms.Domain.Recruitment;

/// <summary>
/// A Head of Department's formal request to recruit for a position (Module 2).
/// It travels through the approval workflow described in <see cref="RecruitmentWorkflow"/>.
/// Once approved, a vacancy is created from it (Phase 3).
/// </summary>
public class RecruitmentRequest : BaseEntity
{
    /// <summary>Human-friendly reference, e.g. "RR-HSE-2026-0001". Unique. Given when the request is first saved.</summary>
    public string RequestNumber { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Where the position sits

    /// <summary>
    /// Division the request belongs to (Divisions.Id): a copy of the department's division, taken when the request
    /// is saved. Kept on the request so reports by division stay correct if the department moves division later.
    /// </summary>
    public int DivisionId { get; set; }

    /// <summary>Department that needs the hire (Departments.Id).</summary>
    public int DepartmentId { get; set; }

    /// <summary>Optional unit inside that department (Units.Id).</summary>
    public int? UnitId { get; set; }

    /// <summary>Work location of the new hire (Sites.Id).</summary>
    public int SiteId { get; set; }

    // ---------------------------------------------------------------- The position

    /// <summary>Job title, e.g. "Safety Officer".</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>How many people are needed for this position (usually 1).</summary>
    public int NumberOfPositions { get; set; } = 1;

    public EmploymentType EmploymentType { get; set; }

    public VacancyReason VacancyReason { get; set; }

    /// <summary>For replacements: who is being replaced (name / staff number). Optional.</summary>
    public string? ReplacedEmployee { get; set; }

    /// <summary>Why the hire is needed and what happens if it is not approved.</summary>
    public string Justification { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Budget

    /// <summary>Finance budget line the cost is charged to.</summary>
    public string BudgetCode { get; set; } = string.Empty;

    /// <summary>Estimated annual cost of the position(s) in Ghana cedis (GHS).</summary>
    public decimal EstimatedAnnualCost { get; set; }

    /// <summary>Ticked by the HR Manager when the budget code and cost have been checked.</summary>
    public bool BudgetValidated { get; set; }

    /// <summary>Who validated the budget (Users.Id).</summary>
    public string? BudgetValidatedByUserId { get; set; }

    // ---------------------------------------------------------------- People

    /// <summary>The HOD who raised the request (Users.Id). They edit, submit or cancel it.</summary>
    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Recruitment Officer responsible for filling the position; set at Recruitment Review (Users.Id).</summary>
    public string? AssignedRecruiterUserId { get; set; }

    // ---------------------------------------------------------------- Workflow state

    public RecruitmentRequestStatus Status { get; set; } = RecruitmentRequestStatus.Draft;

    /// <summary>The date the request was first submitted ("Date Requested" in the specification).</summary>
    public DateTime? DateRequested { get; set; }

    /// <summary>When the request entered its current status; used to show how long it has been waiting.</summary>
    public DateTime StatusChangedAt { get; set; }

    /// <summary>When the Managing Director gave final approval.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Changes on every workflow step. If two approvers act on the same request at the same moment,
    /// the second save fails instead of silently overwriting the first decision (optimistic concurrency).
    /// </summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>The full history of the request: every submission and decision, oldest first.</summary>
    public ICollection<RecruitmentRequestAction> Actions { get; set; } = new List<RecruitmentRequestAction>();

    /// <summary>Moves the request to <paramref name="newStatus"/> and records the step in its history.</summary>
    public void RecordStep(WorkflowAction action, RecruitmentRequestStatus newStatus, string actorUserId, string actorName,
        string? comments, DateTime now)
    {
        Actions.Add(new RecruitmentRequestAction
        {
            Action = action,
            FromStatus = Status,
            ToStatus = newStatus,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Comments = comments,
            ActionAt = now
        });

        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>
/// One line in a recruitment request's history: who did what, at which stage, and their comments.
/// Never edited after it is written.
/// </summary>
public class RecruitmentRequestAction : BaseEntity
{
    public int RecruitmentRequestId { get; set; }
    public RecruitmentRequest? RecruitmentRequest { get; set; }

    public WorkflowAction Action { get; set; }

    /// <summary>Status before this step.</summary>
    public RecruitmentRequestStatus FromStatus { get; set; }

    /// <summary>Status after this step.</summary>
    public RecruitmentRequestStatus ToStatus { get; set; }

    /// <summary>Who acted (Users.Id).</summary>
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>Their name at the time, kept even if the user is renamed later.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Reviewer's comments. Required when returning or rejecting.</summary>
    public string? Comments { get; set; }

    public DateTime ActionAt { get; set; }
}
