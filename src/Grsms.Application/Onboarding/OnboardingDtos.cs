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

    /// <summary>HR: change first-day details, the staff number and the checklist; complete or cancel.</summary>
    public bool CanManage { get; init; }
    public bool CanEditStartDate { get; init; }
    public bool CanIssueLetter { get; init; }
    public bool CanComplete { get; init; }
    public string? CompleteBlockedReason { get; init; }
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
    public bool CanSign => LetterReady && LetterSignedAt is null && Status == OnboardingStatus.InProgress;
}
