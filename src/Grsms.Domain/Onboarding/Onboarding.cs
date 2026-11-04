using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Onboarding;

// Module 9: appointment letters and onboarding.

public enum OnboardingStatus
{
    [Display(Name = "In progress")] InProgress,
    [Display(Name = "Completed")] Completed,
    [Display(Name = "Cancelled")] Cancelled
}

/// <summary>Who an onboarding task belongs to.</summary>
public enum OnboardingTeam
{
    [Display(Name = "Human Resources")] HumanResources,
    [Display(Name = "Department")] Department,
    [Display(Name = "IT")] InformationTechnology,

    /// <summary>Things the new employee brings or provides; HR ticks them off when received.</summary>
    [Display(Name = "New employee")] NewEmployee
}

public enum OnboardingTaskStatus
{
    [Display(Name = "To do")] Open,
    [Display(Name = "Done")] Done,
    [Display(Name = "Not needed")] NotNeeded
}

/// <summary>
/// The onboarding of one new employee: started by HR once the candidate has accepted the offer. Holds the
/// appointment letter, the first-day information and (in <see cref="OnboardingTask"/>) the checklist.
/// Stores the division, department and requesting HOD of its vacancy, like every recruitment record.
/// </summary>
public class OnboardingRecord : BaseEntity
{
    /// <summary>Reference, e.g. "ONB-2026-0001". Unique.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>One onboarding per application (unique).</summary>
    public int JobApplicationId { get; set; }

    /// <summary>The accepted offer (unique).</summary>
    public int OfferId { get; set; }

    public int VacancyId { get; set; }
    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;
    public int SiteId { get; set; }

    /// <summary>The new employee's GRSMS account (the applicant).</summary>
    public string EmployeeUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>Monthly salary in GHS, from the accepted offer.</summary>
    public decimal MonthlySalary { get; set; }

    /// <summary>Staff number, once payroll has assigned one.</summary>
    public string? EmployeeNumber { get; set; }

    // ---- First day
    public DateOnly StartDate { get; set; }
    public TimeOnly ReportTime { get; set; } = new(8, 0);
    public string ReportLocation { get; set; } = string.Empty;

    /// <summary>The person who meets the new employee on the first day (supervisor or buddy).</summary>
    public string? ContactPersonUserId { get; set; }

    /// <summary>Anything else for the first day (dress code, transport, ...).</summary>
    public string? FirstDayNotes { get; set; }

    // ---- Appointment letter
    /// <summary>Letter reference, e.g. "APT-2026-0001", given when the letter is first issued.</summary>
    public string? LetterNumber { get; set; }

    /// <summary>Conditions written in the letter (probation, hours, leave, benefits, notice period, ...).</summary>
    public string? LetterTerms { get; set; }
    public DateTime? LetterIssuedAt { get; set; }
    public string? LetterIssuedByUserId { get; set; }

    /// <summary>When the new employee accepted the letter in their portal (their electronic signature).</summary>
    public DateTime? LetterAcknowledgedAt { get; set; }

    /// <summary>The full name the new employee typed to sign.</summary>
    public string? LetterAcknowledgedName { get; set; }
    public string? LetterAcknowledgedIp { get; set; }

    // ---- Status
    public OnboardingStatus Status { get; set; } = OnboardingStatus.InProgress;
    public DateTime StatusChangedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>Why the onboarding was cancelled (e.g. the new employee did not report).</summary>
    public string? CancelReason { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();

    public bool LetterIssued => LetterIssuedAt is not null;
    public bool LetterAcknowledged => LetterAcknowledgedAt is not null;
}

/// <summary>One item on an onboarding checklist.</summary>
public class OnboardingTask : BaseEntity
{
    public int OnboardingRecordId { get; set; }
    public OnboardingTeam Team { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }

    /// <summary>The staff member responsible (null for the new employee's items, which HR ticks off).</summary>
    public string? AssignedToUserId { get; set; }

    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Open;
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
    public int SortOrder { get; set; }
}
