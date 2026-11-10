using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

/// <param name="CanVerify">Phase 20: HR may confirm that a task someone else marked done really is done.</param>
public record OnboardingTaskDto(int Id, OnboardingTeam Team, string Title, DateOnly? DueDate, string? AssignedToUserId, string? AssignedTo,
    OnboardingTaskStatus Status, string? Notes, DateTime? CompletedAt, string? CompletedBy, bool CanUpdate, bool IsOverdue)
{
    // Phase 20: departmental induction
    public InductionArea? Area { get; init; }
    public string? Description { get; init; }
    public DateTime? AssignedAt { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public string? VerifiedBy { get; init; }
    public bool CanVerify { get; init; }

    /// <summary>The heading the task is listed under: its induction area, or the older team.</summary>
    public string Group => Area is { } a ? Common.EnumText.Label(a) : Team == OnboardingTeam.Department ? "Own department (HOD)" : Common.EnumText.Label(Team);
}

/// <summary>Phase 20: an induction area to tick (Phase 27: when activating), with who would receive its tasks.</summary>
public record InductionAreaChoiceDto(InductionArea Area, bool Ticked, string Owner, IReadOnlyList<string> Tasks);

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

    /// <summary>The RO (onboarding.manage): change first-day details, notes and the checklist; cancel.</summary>
    public bool CanManage { get; init; }
    public bool CanEditStartDate { get; init; }
    public bool CanIssueLetter { get; init; }

    /// <summary>Phase 27: the RO may ask the new employee to correct something that is not a document (e.g. the personal record).</summary>
    public bool CanRequestCorrection { get; init; }
    public string? CorrectionMessage { get; init; }

    /// <summary>Phase 27: the RO may change the expatriate flag and the education certificates required.</summary>
    public bool CanSetRequirements { get; init; }
    public bool IsExpatriate { get; init; }
    public IReadOnlyList<DocumentChoiceDto> EducationChoices { get; init; } = [];

    /// <summary>Phase 27: when the documents are due, and whether the one extension was used.</summary>
    public DateOnly? DocumentsDueDate { get; init; }
    public bool DocumentsDueDateExtended { get; init; }

    /// <summary>Phase 27: what is still outstanding for the new employee (empty = nothing), and the overall progress in %.</summary>
    public IReadOnlyList<string> Outstanding { get; init; } = [];
    public int Percent { get; init; }

    public bool CanCompleteInduction { get; init; }
    public string? InductionBlockedReason { get; init; }

    /// <summary>Phase 27: the RO or HR Manager may activate now (ready for activation).</summary>
    public bool CanActivate { get; init; }

    /// <summary>Phase 20: the induction areas to tick (Phase 27: at activation; empty unless the user can activate now).</summary>
    public IReadOnlyList<InductionAreaChoiceDto> InductionChoices { get; init; } = [];

    /// <summary>Phase 20: the employee's personnel file, once opened (for people who may open it).</summary>
    public int? PersonnelFileId { get; init; }
}

/// <summary>Phase 27: a document type the RO can tick (education certificates), with whether it is ticked now.</summary>
public record DocumentChoiceDto(string Code, string Name, bool Ticked);

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

    /// <summary>Phase 27: a non-Ghanaian (the expatriate documents apply, the Ghana Card does not).</summary>
    public bool IsExpatriate { get; set; }

    /// <summary>Phase 27: the codes of the education certificates this new hire must provide.</summary>
    public List<string> RequiredEducation { get; set; } = [];

    // Shown on the form only
    public IReadOnlyList<DocumentChoiceDto> EducationOptions { get; set; } = [];
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

/// <summary>Phase 27: one line of a document's history.</summary>
public record DocumentEventDto(DocumentEventAction Action, EmployeeDocumentStatus FromStatus, EmployeeDocumentStatus ToStatus, string? FileName,
    DocumentRejectionReason? RejectionReason, string? Comments, string ActorName, DateTime ActionAt);

/// <summary>One document of a new employee's checklist (Phase 18; Phase 27: configurable types, review, history).</summary>
/// <param name="CanEdit">The current user may upload, replace or remove files, or declare it not applicable.</param>
/// <param name="CanReview">The current user (RO) may verify or reject it now.</param>
/// <param name="CanStartReview">The current user (RO) may mark it "Under review" (locks it for the new employee).</param>
/// <param name="ReplacedFiles">Earlier files that were replaced or removed (history; still downloadable).</param>
public record EmployeeDocumentDto(
    int Id, string Code, string Name, string? Description, DocumentGroup Group, bool IsRequired, bool IsApplicable, EmployeeDocumentStatus Status,
    DateTime? SubmittedAt, string? NotApplicableReason, DateTime? ReviewStartedAt, string? ReviewStartedBy, DateTime? VerifiedAt, string? VerifiedBy,
    DocumentRejectionReason? RejectionReasonCode, string? RejectionReason, int RejectionCount, int MinFiles, IReadOnlyList<string> FileTypes,
    IReadOnlyList<EmployeeDocumentFileDto> Files, IReadOnlyList<EmployeeDocumentFileDto> ReplacedFiles, IReadOnlyList<DocumentEventDto> History,
    bool CanEdit, bool CanReview, bool CanStartReview)
{
    /// <summary>"Not applicable" that the RO has accepted.</summary>
    public bool NotApplicableAccepted => Status == EmployeeDocumentStatus.NotApplicable && VerifiedAt is not null;

    public bool CanAddFile => CanEdit && Files.Count < OnboardingWorkflow.MaxFilesPerDocument;
    public bool CanMarkNotApplicable => CanEdit && !IsRequired && Files.Count == 0 && Status != EmployeeDocumentStatus.NotApplicable;
    public string Accept => string.Join(",", FileTypes);
}

/// <summary>The whole checklist of one onboarding, with its progress (for the new employee's portal and for the RO).</summary>
/// <param name="Outstanding">What the new employee must still do (empty = nothing).</param>
/// <param name="Percent">Overall progress: required documents verified plus the letter and the personal record signed.</param>
public record DocumentChecklistDto(
    int OnboardingId, string OnboardingNumber, int ApplicationId, string EmployeeName, string JobTitle, OnboardingStatus Stage,
    IReadOnlyList<EmployeeDocumentDto> Documents, bool LetterIssued, bool LetterSigned, bool PersonalRecordSigned, bool IsExpatriate,
    DateOnly? DueDate, string? CorrectionMessage, IReadOnlyList<string> Outstanding, int Percent)
{
    /// <summary>The documents the new employee sees (those that apply to them, plus any with files).</summary>
    public IReadOnlyList<EmployeeDocumentDto> Visible => Documents.Where(d => d.IsApplicable || d.Files.Count > 0).ToList();

    public int RequiredTotal => Visible.Count(d => d.IsRequired);
    public int Uploaded => Visible.Count(d => d.Status is not (EmployeeDocumentStatus.Pending or EmployeeDocumentStatus.NotApplicable));
    public int RequiredUploaded => Visible.Count(d => d.IsRequired && d.Status != EmployeeDocumentStatus.Pending);
    public int NotSubmitted => Visible.Count(d => d.IsRequired && d.Status == EmployeeDocumentStatus.Pending);
    public int Settled => Visible.Count(d => d.Status == EmployeeDocumentStatus.Verified || d.NotApplicableAccepted);
    public int Verified => Visible.Count(d => d.Status == EmployeeDocumentStatus.Verified);
    public int Rejected => Visible.Count(d => d.Status == EmployeeDocumentStatus.Rejected);
    public int WaitingForHr => Visible.Count(d => OnboardingWorkflow.AwaitsReview(d.Status));
    public bool ReadyForActivation => Stage == OnboardingStatus.AwaitingActivation;
}

/// <summary>Usability U1 (10 Nov 2026): kinds of onboarding work that appear on the home to-dos.</summary>
public enum OnboardingWorkKind { IssueLetter, VerifyDocuments, Activate, CloseInduction }

/// <summary>One piece of onboarding work waiting for the current user (letter to issue, documents to verify, ...).</summary>
/// <param name="Count">How many items (e.g. documents waiting for verification).</param>
public record OnboardingWorkItemDto(int OnboardingId, string Number, string EmployeeName, string JobTitle, OnboardingWorkKind Kind, int Count, DateTime Since);
