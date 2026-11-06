using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

public record OnboardingTaskDto(int Id, OnboardingTeam Team, string Title, DateOnly? DueDate, string? AssignedToUserId, string? AssignedTo,
    OnboardingTaskStatus Status, string? Notes, DateTime? CompletedAt, string? CompletedBy, bool CanUpdate, bool IsOverdue);

/// <summary>Everything about one onboarding, for the staff details page.</summary>
public record OnboardingDetailsDto
{
    public int Id { get; init; }
    public string Number { get; init; } = "";
    public OnboardingStatus Status { get; init; }
    public int ApplicationId { get; init; }
    public string ApplicationNumber { get; init; } = "";
    public string OfferNumber { get; init; } = "";
    public string EmployeeName { get; init; } = "";
    public string? EmployeeEmail { get; init; }
    public string? EmployeeNumber { get; init; }
    public string JobTitle { get; init; } = "";
    public string VacancyNumber { get; init; } = "";
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string SiteName { get; init; } = "";
    public decimal MonthlySalary { get; init; }

    public DateOnly StartDate { get; init; }
    public TimeOnly ReportTime { get; init; }
    public string ReportLocation { get; init; } = "";
    public string? ContactPersonUserId { get; init; }
    public string? ContactPerson { get; init; }
    public string? FirstDayNotes { get; init; }

    public string? LetterNumber { get; init; }
    public string? LetterTerms { get; init; }
    public DateTime? LetterIssuedAt { get; init; }
    public string? LetterIssuedBy { get; init; }
    public DateTime? LetterSignedAt { get; init; }
    public string? LetterSignedName { get; init; }

    public string CreatedBy { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string? CancelReason { get; init; }

    public IReadOnlyList<OnboardingTaskDto> Tasks { get; init; } = [];
    public int TasksFinished => Tasks.Count(t => t.Status != OnboardingTaskStatus.Open);

    // ---- Phase 18: workflow
    public DateOnly? ExpectedCompletionDate { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<OnboardingHistoryDto> History { get; init; } = [];

    /// <summary>The New Hire Administrative Records checklist; null for people who may not see personal documents (e.g. the HOD).</summary>
    public DocumentChecklistDto? Documents { get; init; }

    /// <summary>Phase 19: the personal record is signed (HR only; null for others).</summary>
    public bool? PersonalRecordSigned { get; init; }

    /// <summary>HR: change first-day details, notes and the checklist; cancel.</summary>
    public bool CanManage { get; init; }
    public bool CanEditStartDate { get; init; }
    public bool CanIssueLetter { get; init; }

    /// <summary>HR may submit for the new employee (e.g. documents brought in on paper and scanned by HR).</summary>
    public bool CanSubmitForEmployee { get; init; }
    public bool CanReturnToEmployee { get; init; }
    public bool CanMarkVerified { get; init; }
    public string? VerifiedBlockedReason { get; init; }
    public bool CanHrDecide { get; init; }
    public string? HrDecisionBlockedReason { get; init; }
    public bool CanCompleteInduction { get; init; }
    public string? InductionBlockedReason { get; init; }
    public bool CanActivate { get; init; }
}

/// <summary>An accepted offer that has no onboarding yet (the "Waiting to start" tab).</summary>
public record OnboardingToStartDto(int ApplicationId, string ApplicationNumber, string OfferNumber, string EmployeeName, string JobTitle,
    string DivisionName, string DepartmentName, DateOnly StartDate, DateTime AcceptedAt);

/// <summary>The form to start onboarding, pre-filled from the accepted offer.</summary>
public class StartOnboardingForm
{
    public int ApplicationId { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? ReportTime { get; set; }
    public string? ReportLocation { get; set; }
    public string? ContactPersonUserId { get; set; }
    public string? FirstDayNotes { get; set; }

    // Shown on the form only
    public string EmployeeName { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string OfferNumber { get; set; } = "";
    public IReadOnlyList<Organization.LookupItem> Staff { get; set; } = [];
}

/// <summary>Filters on the Onboarding list.</summary>
public class OnboardingFilter
{
    /// <summary>"tasks" (assigned to me), "progress" (in progress), "start" (accepted offers waiting) or "all".</summary>
    public string Tab { get; set; } = "progress";
    public OnboardingStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }

    /// <summary>Start date from / to.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record OnboardingListItemDto(int Id, string Number, int ApplicationId, string EmployeeName, string JobTitle, string DivisionName,
    string DepartmentName, DateOnly StartDate, OnboardingStatus Status, bool LetterIssued, bool LetterSigned, int TasksDone, int TasksTotal);

/// <summary>An open checklist item that the current user is responsible for ("My tasks").</summary>
public record MyOnboardingTaskDto(int TaskId, int OnboardingId, string Number, string EmployeeName, string JobTitle, OnboardingTeam Team,
    string Title, DateOnly? DueDate, DateOnly StartDate, DateTime Since);

/// <summary>The appointment as the new employee sees it: the letter and the first-day information.</summary>
public record MyAppointmentDto(
    int OnboardingId, string? LetterNumber, string EmployeeName, string JobTitle, string DepartmentName, string SiteName, string? ReportingTo,
    decimal MonthlySalary, DateOnly StartDate, TimeOnly ReportTime, string ReportLocation, string? ContactPerson, string? ContactEmail,
    string? FirstDayNotes, string? LetterTerms, DateTime? LetterIssuedAt, DateTime? LetterSignedAt, string? LetterSignedName,
    OnboardingStatus Status, IReadOnlyList<(string Item, bool Received)> ToBring)
{
    public bool LetterReady => LetterIssuedAt is not null;
    public bool CanSign => LetterReady && LetterSignedAt is null && OnboardingWorkflow.IsActive(Status);
}

// ===================================================================== Phase 18: workflow history and new-hire documents

public record OnboardingHistoryDto(OnboardingAction Action, OnboardingStatus FromStatus, OnboardingStatus ToStatus, string ActorName,
    string? Comments, DateTime ActionAt);

public record EmployeeDocumentFileDto(int AttachmentId, string FileName, long SizeBytes, DateTime UploadedAt);

/// <summary>One line of the New Hire Administrative Records checklist.</summary>
/// <param name="CanEdit">The current user may upload / remove files or declare it not applicable.</param>
/// <param name="CanReview">The current user (HR) may verify or reject it now.</param>
public record EmployeeDocumentDto(int Id, EmployeeDocumentType Type, bool IsRequired, EmployeeDocumentStatus Status, DateTime? SubmittedAt,
    string? NotApplicableReason, DateTime? VerifiedAt, string? VerifiedBy, string? RejectionReason, IReadOnlyList<EmployeeDocumentFileDto> Files,
    bool CanEdit, bool CanReview)
{
    /// <summary>"Not applicable" that HR has accepted.</summary>
    public bool NotApplicableAccepted => Status == EmployeeDocumentStatus.NotApplicable && VerifiedAt is not null;
}

/// <summary>The whole checklist of one onboarding (for the new employee's portal and for HR).</summary>
/// <param name="SubmitBlockedReason">Why it cannot be submitted to HR yet (null = it can).</param>
public record DocumentChecklistDto(int OnboardingId, string OnboardingNumber, int ApplicationId, string EmployeeName, string JobTitle,
    OnboardingStatus Stage, IReadOnlyList<EmployeeDocumentDto> Documents, bool LetterSigned, string? SubmitBlockedReason)
{
    public int Settled => Documents.Count(d => d.Status == EmployeeDocumentStatus.Verified || d.NotApplicableAccepted);
    public int Rejected => Documents.Count(d => d.Status == EmployeeDocumentStatus.Rejected);
    public int WaitingForHr => Documents.Count(d => d.Status == EmployeeDocumentStatus.Submitted ||
                                                    (d.Status == EmployeeDocumentStatus.NotApplicable && d.VerifiedAt is null));
}
