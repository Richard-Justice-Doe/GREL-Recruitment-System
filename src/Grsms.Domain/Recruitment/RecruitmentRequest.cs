using Grsms.Domain.Common;

namespace Grsms.Domain.Recruitment;

/// <summary>
/// A department's formal request to recruit for a position (Module 2): the digital form QF-81-11 Recruitment Request Form.
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

    /// <summary>"Specific job title" on QF-81-11, e.g. "Safety Officer".</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>"Number of staff required" (usually 1).</summary>
    public int NumberOfPositions { get; set; } = 1;

    /// <summary>"Engagement type": Permanent or Contract on the form (older requests may hold other types).</summary>
    public EmploymentType EmploymentType { get; set; }

    /// <summary>"Category": Executive, Senior or Junior. Null only on requests raised before QF-81-11 was digitised.</summary>
    public StaffCategory? StaffCategory { get; set; }

    /// <summary>"Purpose" of the request.</summary>
    public VacancyReason VacancyReason { get; set; }

    /// <summary>When the purpose is "Other": what it is.</summary>
    public string? PurposeOther { get; set; }

    /// <summary>For replacements: who is being replaced (name / staff number). Optional.</summary>
    public string? ReplacedEmployee { get; set; }

    /// <summary>"Request date" as written on the form (defaults to the day it is raised).</summary>
    public DateOnly? RequestDate { get; set; }

    /// <summary>"Proposed starting date" of the new hire(s).</summary>
    public DateOnly? ProposedStartDate { get; set; }

    /// <summary>
    /// "Description of the post": formatted text (a small, safe subset of HTML: paragraphs, bold, italic, underline,
    /// headings and lists). Always cleaned by the HTML sanitiser before it is saved, so it can be shown as-is.
    /// </summary>
    public string? PostDescription { get; set; }

    /// <summary>Why the hire is needed and what happens if it is not approved.</summary>
    public string Justification { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Qualification requirements (QF-81-11)

    /// <summary>Minimum educational level; becomes the vacancy's minimum education.</summary>
    public Vacancies.EducationLevel? EducationLevel { get; set; }

    /// <summary>Category / field of the qualification, e.g. "Mechanical engineering".</summary>
    public string? QualificationCategory { get; set; }

    /// <summary>Level of training required, e.g. "Trade test grade 1", "Advanced".</summary>
    public string? TrainingLevel { get; set; }

    /// <summary>Certificate(s) required, one per line, e.g. "NEBOSH IGC".</summary>
    public string? CertificateRequired { get; set; }

    /// <summary>Minimum years of professional experience.</summary>
    public int? MinYearsExperience { get; set; }

    /// <summary>What professional experience is needed.</summary>
    public string? ProfessionalExperience { get; set; }

    /// <summary>Driving licence requirement: not needed (default), desirable or mandatory.</summary>
    public DrivingLicenceRequirement DrivingLicence { get; set; }

    /// <summary>The licence class (Ghana DVLA classes A–F), optional; only kept when a licence is desirable or mandatory.</summary>
    public string? DrivingLicenceClass { get; set; }

    // ---------------------------------------------------------------- Additional information

    /// <summary>"Observations / remarks".</summary>
    public string? Remarks { get; set; }

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

    /// <summary>The person who raised the request (Users.Id): a staff member of the department or its HOD. They edit, submit or cancel it.</summary>
    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Recruitment Officer chosen at the earlier workflow's Recruitment review (Users.Id). New requests leave it empty: the vacancy owner is chosen when the vacancy is created.</summary>
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
