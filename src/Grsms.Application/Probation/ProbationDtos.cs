using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Probation;

namespace Grsms.Application.Probation;

/// <summary>Loads a probation with its objectives, development plan and history.</summary>
public interface IProbationRepository : IRepository<ProbationRecord>
{
    Task<ProbationRecord?> GetFullAsync(int id, CancellationToken ct = default);
}

public class ObjectiveRow
{
    [StringLength(500)] public string? Objective { get; set; }
    [StringLength(1000)] public string? SuccessIndicators { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Objective) && string.IsNullOrWhiteSpace(SuccessIndicators) && DueDate is null;
}

public class DevelopmentPlanRow
{
    [StringLength(500)] public string? TrainingNeed { get; set; }
    [StringLength(500)] public string? DevelopmentNeed { get; set; }
    [StringLength(200)] public string? Timeline { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(TrainingNeed) && string.IsNullOrWhiteSpace(DevelopmentNeed) && string.IsNullOrWhiteSpace(Timeline);
}

/// <summary>The initial meeting form (HOD): objectives, development plan, and the HOD's signature.</summary>
public class InitialMeetingForm
{
    public DateOnly? MeetingDate { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
    public List<ObjectiveRow> Objectives { get; set; } = [];
    public List<DevelopmentPlanRow> DevelopmentPlan { get; set; } = [];

    /// <summary>The HOD's full name, typed to sign.</summary>
    [StringLength(200)] public string? SignatureName { get; set; }
}

/// <summary>A first or final assessment (HOD).</summary>
public class AssessmentForm
{
    public ProbationRating? ApplicationOfSkills { get; set; }
    public ProbationRating? QualityOfWork { get; set; }
    public ProbationRating? Productivity { get; set; }
    public ProbationRating? Cooperation { get; set; }
    public ProbationRating? Dependability { get; set; }

    /// <summary>Empty when the job has no supervisory duties.</summary>
    public ProbationRating? AbilityToSupervise { get; set; }

    [StringLength(4000)] public string? Comments { get; set; }
    [StringLength(4000)] public string? ImprovementPlan { get; set; }

    // Final assessment only
    public ProbationOutcome? Outcome { get; set; }
    public int? ExtensionMonths { get; set; }
    [StringLength(4000)] public string? ExtensionReason { get; set; }
}

public record ProbationListItemDto(int Id, string Number, string EmployeeName, string? StaffNumber, string JobTitle, string DepartmentName,
    DateOnly StartDate, DateOnly EndDate, ProbationStatus Status, string? NextStep, DateOnly? NextDue, bool Overdue);

public record ProbationHistoryDto(ProbationAction Action, ProbationStatus FromStatus, ProbationStatus ToStatus, string ActorName, string? Comments, DateTime ActionAt);

public record AssessmentDto(int Id, ProbationAssessmentKind Kind, string Assessor, DateTime AssessedAt, IReadOnlyList<(ProbationRatingCategory Category, ProbationRating? Rating)> Ratings,
    string Comments, string? ImprovementPlan, ProbationOutcome? Outcome, int? ExtensionMonths, string? ExtensionReason, bool? HrApproved, string? HrComments);

/// <summary>Everything about one probation, plus what the current user may do.</summary>
public record ProbationDetailsDto
{
    public int Id { get; init; }
    public string Number { get; init; } = "";
    public string EmployeeName { get; init; } = "";
    public string? StaffNumber { get; init; }
    public string JobTitle { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string? HodName { get; init; }
    public string? HrManagerName { get; init; }
    public int OnboardingRecordId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateOnly OriginalEndDate { get; init; }
    public ProbationStatus Status { get; init; }

    public DateOnly InitialMeetingDueDate { get; init; }
    public DateOnly? InitialMeetingDate { get; init; }
    public string? InitialMeetingNotes { get; init; }
    public DateTime? EmployeeSignedAt { get; init; }
    public string? EmployeeSignatureName { get; init; }
    public DateTime? HodSignedAt { get; init; }
    public string? HodSignatureName { get; init; }
    public IReadOnlyList<ObjectiveRow> Objectives { get; init; } = [];
    public IReadOnlyList<DevelopmentPlanRow> DevelopmentPlan { get; init; } = [];

    public DateOnly FirstAssessmentDueDate { get; init; }
    public int ExtendedMonths { get; init; }
    public string? ExtensionReason { get; init; }
    public string? ExtensionImprovementPlan { get; init; }

    public IReadOnlyList<AssessmentDto> Assessments { get; init; } = [];
    public IReadOnlyList<ProbationHistoryDto> History { get; init; } = [];

    /// <summary>The confirmation letter, once confirmed (a document in the personnel file).</summary>
    public string? ConfirmationLetterNumber { get; init; }
    public int? ConfirmationLetterDocumentId { get; init; }

    // ---- What the current user may do
    public bool CanRecordInitialMeeting { get; init; }
    public bool CanSignAsEmployee { get; init; }
    public bool CanAssess { get; init; }
    public ProbationAssessmentKind? NextAssessment { get; init; }
    public string? AssessBlockedReason { get; init; }
    public bool CanHrDecide { get; init; }
    public string? HrDecisionBlockedReason { get; init; }

    /// <summary>Months still available for an extension.</summary>
    public int ExtensionMonthsLeft => ProbationWorkflow.MaxExtensionMonths - ExtendedMonths;
}

/// <summary>Filters of the probation list.</summary>
public class ProbationFilter
{
    /// <summary>"due" (something due or overdue now), "active", "decision" (awaiting HR), or "all".</summary>
    public string Tab { get; set; } = "active";
    public int? DepartmentId { get; set; }
}

/// <summary>Usability U1 (10 Nov 2026): a probation step waiting for the current user (the HOD's meeting or assessment, or the HR decision).</summary>
public record ProbationWorkItemDto(int ProbationId, string Number, string EmployeeName, string Step, DateOnly? Due, bool IsDecision, DateTime Since);
