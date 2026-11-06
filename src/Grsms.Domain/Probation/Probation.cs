using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Probation;

// Phase 21: probation management and employment confirmation. See ProbationWorkflow for the rules.

/// <summary>Where a probation is.</summary>
public enum ProbationStatus
{
    /// <summary>Started; the HOD holds the initial meeting (objectives and development plan) within 7 days, signed by both.</summary>
    [Display(Name = "Initial meeting")] InitialMeeting,

    /// <summary>Objectives agreed; the 3-month first assessment, then the final assessment at the end date.</summary>
    [Display(Name = "In progress")] InProgress,

    /// <summary>Extended after a final assessment; a new final assessment is due at the new end date.</summary>
    [Display(Name = "Extended")] Extended,

    /// <summary>The HOD's final assessment is waiting for the HR Manager's decision.</summary>
    [Display(Name = "Awaiting HR decision")] AwaitingHrDecision,

    /// <summary>Employment confirmed: the confirmation letter is in the personnel file. Final.</summary>
    [Display(Name = "Confirmed")] Confirmed,

    /// <summary>Employment not confirmed. Final.</summary>
    [Display(Name = "Not confirmed")] NotConfirmed
}

/// <summary>The rating scale of the probation assessment form.</summary>
public enum ProbationRating
{
    [Display(Name = "Outstanding")] Outstanding,
    [Display(Name = "Very good")] VeryGood,
    [Display(Name = "Good")] Good,
    [Display(Name = "Satisfactory")] Satisfactory,
    [Display(Name = "Unacceptable")] Unacceptable
}

/// <summary>The rating categories of the probation assessment form.</summary>
public enum ProbationRatingCategory
{
    [Display(Name = "Application of skills and knowledge")] ApplicationOfSkills,
    [Display(Name = "Quality of work")] QualityOfWork,
    [Display(Name = "Productivity")] Productivity,
    [Display(Name = "Cooperation")] Cooperation,
    [Display(Name = "Dependability")] Dependability,
    [Display(Name = "Ability to supervise")] AbilityToSupervise
}

public enum ProbationAssessmentKind
{
    [Display(Name = "First assessment (3 months)")] First,
    [Display(Name = "Final assessment")] Final
}

/// <summary>The final assessment's recommendation.</summary>
public enum ProbationOutcome
{
    [Display(Name = "Confirm employment")] Confirm,
    [Display(Name = "Extend probation")] Extend,
    [Display(Name = "Do not confirm employment")] DoNotConfirm
}

public enum ProbationAction
{
    [Display(Name = "Probation started")] Started,
    [Display(Name = "Initial meeting recorded")] InitialMeetingRecorded,
    [Display(Name = "Signed by the employee")] EmployeeSigned,
    [Display(Name = "Signed by the Head of Department")] HodSigned,
    [Display(Name = "First assessment")] FirstAssessment,
    [Display(Name = "Final assessment")] FinalAssessment,
    [Display(Name = "Employment confirmed")] Confirmed,
    [Display(Name = "Probation extended")] Extended,
    [Display(Name = "Employment not confirmed")] NotConfirmed,
    [Display(Name = "Returned to the Head of Department")] ReturnedToHod
}

/// <summary>
/// A new employee's probation (the spec's ProbationRecord): created automatically when their onboarding is completed,
/// 6 months by default. Stores the department and the HOD responsible, like every recruitment record.
/// </summary>
public class ProbationRecord : BaseEntity
{
    /// <summary>Reference, e.g. "PRB-2026-0001". Unique.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>The employee (Users.Id).</summary>
    public string EmployeeUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? StaffNumber { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }

    /// <summary>The onboarding it follows (unique: one probation per onboarding).</summary>
    public int OnboardingRecordId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>The current end date (moves when probation is extended).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>The end date before any extension.</summary>
    public DateOnly OriginalEndDate { get; set; }

    public ProbationStatus Status { get; set; } = ProbationStatus.InitialMeeting;
    public DateTime StatusChangedAt { get; set; }

    /// <summary>The Head of Department responsible (Users.Id), from the department when the probation starts.</summary>
    public string? HodUserId { get; set; }

    /// <summary>The HR Manager who decided the outcome (Users.Id).</summary>
    public string? HrManagerUserId { get; set; }

    // ---- Initial meeting (within 7 days of the start)
    public DateOnly InitialMeetingDueDate { get; set; }
    public DateOnly? InitialMeetingDate { get; set; }
    public string? InitialMeetingNotes { get; set; }
    public DateTime? EmployeeSignedAt { get; set; }
    public string? EmployeeSignatureName { get; set; }
    public DateTime? HodSignedAt { get; set; }
    public string? HodSignatureName { get; set; }

    // ---- Extension (the latest one)
    /// <summary>Total months the probation has been extended (at most <see cref="ProbationWorkflow.MaxExtensionMonths"/>).</summary>
    public int ExtendedMonths { get; set; }
    public string? ExtensionReason { get; set; }
    public string? ExtensionImprovementPlan { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<ProbationObjective> Objectives { get; set; } = new List<ProbationObjective>();
    public ICollection<ProbationDevelopmentPlan> DevelopmentPlan { get; set; } = new List<ProbationDevelopmentPlan>();
    public ICollection<ProbationActionEntry> Actions { get; set; } = new List<ProbationActionEntry>();

    /// <summary>The 3-month point.</summary>
    public DateOnly FirstAssessmentDueDate => StartDate.AddMonths(ProbationWorkflow.FirstAssessmentMonths);

    public bool InitialMeetingSigned => EmployeeSignedAt is not null && HodSignedAt is not null;

    public void RecordStep(ProbationAction action, ProbationStatus newStatus, string actorUserId, string actorName, string? comments, DateTime now)
    {
        Actions.Add(new ProbationActionEntry
        {
            Action = action, FromStatus = Status, ToStatus = newStatus, ActorUserId = actorUserId, ActorName = actorName, Comments = comments, ActionAt = now
        });
        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>One objective agreed at the initial meeting.</summary>
public class ProbationObjective
{
    public int Id { get; set; }
    public int ProbationRecordId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string SuccessIndicators { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
}

/// <summary>One line of the development plan agreed at the initial meeting.</summary>
public class ProbationDevelopmentPlan
{
    public int Id { get; set; }
    public int ProbationRecordId { get; set; }
    public string? TrainingNeed { get; set; }
    public string? DevelopmentNeed { get; set; }
    public string? Timeline { get; set; }
}

/// <summary>A first (3-month) or final assessment by the HOD, with the six ratings.</summary>
public class ProbationAssessment : BaseEntity
{
    public int ProbationRecordId { get; set; }
    public ProbationAssessmentKind Kind { get; set; }
    public string AssessorUserId { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }

    public ProbationRating ApplicationOfSkills { get; set; }
    public ProbationRating QualityOfWork { get; set; }
    public ProbationRating Productivity { get; set; }
    public ProbationRating Cooperation { get; set; }
    public ProbationRating Dependability { get; set; }

    /// <summary>Null when the job has no supervisory duties.</summary>
    public ProbationRating? AbilityToSupervise { get; set; }

    public string Comments { get; set; } = string.Empty;
    public string? ImprovementPlan { get; set; }

    // ---- Final assessment only
    public ProbationOutcome? Outcome { get; set; }

    /// <summary>For "Extend": how many months (1 to 3, within the total limit) and why.</summary>
    public int? ExtensionMonths { get; set; }
    public string? ExtensionReason { get; set; }

    /// <summary>The HR Manager's decision on a final assessment: null = waiting, true = approved, false = returned.</summary>
    public bool? HrApproved { get; set; }
    public string? HrComments { get; set; }
}

/// <summary>One line of a probation's history. Never edited.</summary>
public class ProbationActionEntry : BaseEntity
{
    public int ProbationRecordId { get; set; }
    public ProbationAction Action { get; set; }
    public ProbationStatus FromStatus { get; set; }
    public ProbationStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionAt { get; set; }
}

/// <summary>
/// Employment confirmation (the spec's ConfirmationRecord): created when the HR Manager approves confirmation. The letter is
/// filed in the personnel file (Generated Letters); <see cref="PersonnelFileDocumentId"/> is where it is ("LetterPath").
/// </summary>
public class ConfirmationRecord : BaseEntity
{
    public string EmployeeUserId { get; set; } = string.Empty;

    /// <summary>One confirmation per probation (unique).</summary>
    public int ProbationRecordId { get; set; }

    /// <summary>Letter reference, e.g. "CNF-2026-0001". Unique.</summary>
    public string LetterNumber { get; set; } = string.Empty;
    public DateOnly ConfirmationDate { get; set; }
    public string ApprovedByUserId { get; set; } = string.Empty;

    /// <summary>The letter in the personnel file (PersonnelFileDocuments.Id).</summary>
    public int? PersonnelFileDocumentId { get; set; }
}
