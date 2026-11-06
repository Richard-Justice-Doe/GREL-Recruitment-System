using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Onboarding;

// Module 9: appointment letters and onboarding. Phase 18 (6 November 2026): the eight-stage onboarding workflow.

/// <summary>
/// The stage an onboarding has reached (its "current stage"). The order and the allowed moves are in
/// <see cref="OnboardingWorkflow"/>. Stored as text, e.g. "AwaitingDocumentVerification".
/// </summary>
public enum OnboardingStatus
{
    /// <summary>HR is preparing the onboarding (first-day details, appointment letter).</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>The letter is issued: the new employee signs it and uploads the new-hire documents.</summary>
    [Display(Name = "Awaiting employee submission")] AwaitingEmployeeSubmission,

    /// <summary>The new employee has submitted; HR verifies each document.</summary>
    [Display(Name = "Awaiting document verification")] AwaitingDocumentVerification,

    /// <summary>Documents verified; the HR Manager approves the new hire's records.</summary>
    [Display(Name = "Awaiting HR approval")] AwaitingHrApproval,

    /// <summary>Approved; the departments complete their induction tasks.</summary>
    [Display(Name = "Awaiting departmental induction")] AwaitingDepartmentalInduction,

    /// <summary>Induction done; HR records the staff number and activates the employee's staff account.</summary>
    [Display(Name = "Awaiting activation")] AwaitingActivation,

    /// <summary>The employee is active. Final.</summary>
    [Display(Name = "Completed")] Completed,

    /// <summary>Stopped (e.g. the new employee did not report). Final.</summary>
    [Display(Name = "Cancelled")] Cancelled
}

/// <summary>One line of an onboarding's history.</summary>
public enum OnboardingAction
{
    [Display(Name = "Started")] Started,
    [Display(Name = "Appointment letter issued")] LetterIssued,
    [Display(Name = "Submitted by the new employee")] Submitted,
    [Display(Name = "Returned to the new employee")] ReturnedToEmployee,
    [Display(Name = "Documents verified")] DocumentsVerified,
    [Display(Name = "Approved by HR")] HrApproved,
    [Display(Name = "Returned for verification")] ReturnedForVerification,
    [Display(Name = "Induction completed")] InductionCompleted,
    [Display(Name = "Employee activated")] Activated,
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

    // ---- Workflow (Phase 18)
    /// <summary>The current stage of the onboarding workflow (see <see cref="OnboardingWorkflow"/>).</summary>
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Draft;
    public DateTime StatusChangedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>When the onboarding should be finished: by default <see cref="OnboardingWorkflow.ExpectedCompletionDays"/> after the start date.</summary>
    public DateOnly? ExpectedCompletionDate { get; set; }

    /// <summary>When the employee was activated (the onboarding reached Completed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>HR's internal notes on this onboarding (not shown to the new employee).</summary>
    public string? Notes { get; set; }

    /// <summary>Why the onboarding was cancelled (e.g. the new employee did not report).</summary>
    public string? CancelReason { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>Every stage change, oldest first.</summary>
    public ICollection<OnboardingActionEntry> Actions { get; set; } = new List<OnboardingActionEntry>();

    public bool LetterIssued => LetterIssuedAt is not null;
    public bool LetterAcknowledged => LetterAcknowledgedAt is not null;

    /// <summary>Moves to <paramref name="newStatus"/> and records the step in the history.</summary>
    public void RecordStep(OnboardingAction action, OnboardingStatus newStatus, string actorUserId, string actorName, string? comments, DateTime now)
    {
        Actions.Add(new OnboardingActionEntry
        {
            Action = action, FromStatus = Status, ToStatus = newStatus, ActorUserId = actorUserId, ActorName = actorName,
            Comments = comments, ActionAt = now
        });
        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>One line of an onboarding's history. Never edited.</summary>
public class OnboardingActionEntry : BaseEntity
{
    public int OnboardingRecordId { get; set; }
    public OnboardingAction Action { get; set; }
    public OnboardingStatus FromStatus { get; set; }
    public OnboardingStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionAt { get; set; }
}

/// <summary>
/// Phase 20: the twelve induction areas of the GREL departmental induction. Each has an owner and default tasks on the
/// Induction areas screen (<see cref="InductionAreaSetting"/>); an area without an owner falls to HR.
/// </summary>
public enum InductionArea
{
    [Display(Name = "Human Resource")] HumanResource,
    [Display(Name = "Corporate Information Technology")] CorporateIt,
    [Display(Name = "Corporate Procurement")] CorporateProcurement,
    [Display(Name = "Corporate Supply Chain")] CorporateSupplyChain,
    [Display(Name = "Corporate Finance")] CorporateFinance,
    [Display(Name = "Factory Operations")] FactoryOperations,
    [Display(Name = "TBU Operations")] TbuOperations,
    [Display(Name = "Stores Operations")] StoresOperations,
    [Display(Name = "Engineering")] Engineering,
    [Display(Name = "Security")] Security,
    [Display(Name = "Health & Safety")] HealthAndSafety,
    [Display(Name = "Administration")] Administration
}

/// <summary>
/// One onboarding / departmental induction task (the spec's "DepartmentalOnboardingTask"): who does it (the area's owner,
/// the HOD for the new employee's own department, or HR), by when, when it was done and who verified it.
/// </summary>
public class OnboardingTask : BaseEntity
{
    public int OnboardingRecordId { get; set; }

    /// <summary>The older grouping (HR / department / IT / new employee); kept for every task, and derived from the area for new ones.</summary>
    public OnboardingTeam Team { get; set; }

    /// <summary>Phase 20: the induction area, or null for the new employee's own department (its HOD's tasks) and older tasks.</summary>
    public InductionArea? Area { get; set; }

    /// <summary>"Task name".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Phase 20: what the task involves, if more than the title.</summary>
    public string? Description { get; set; }

    /// <summary>Phase 20: when the task was given to its current owner ("assigned date").</summary>
    public DateTime? AssignedAt { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>The staff member responsible (null for the new employee's items, which HR ticks off).</summary>
    public string? AssignedToUserId { get; set; }

    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Open;
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }

    /// <summary>Phase 20: HR's check that a task marked done really is done. A task done by HR itself counts as verified.</summary>
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUserId { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// Phase 20: the settings of one induction area (one row per <see cref="InductionArea"/>, created on first use): who owns its
/// tasks, whether HR ticks it by default when approving an onboarding, and its default tasks (one per line).
/// </summary>
public class InductionAreaSetting : BaseEntity
{
    public InductionArea Area { get; set; }

    /// <summary>The person who receives this area's tasks; null = HR (the vacancy's recruiter).</summary>
    public string? OwnerUserId { get; set; }

    /// <summary>Ticked by default when the HR Manager approves an onboarding.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The area's tasks, one per line; null = the built-in list in <see cref="OnboardingWorkflow.InductionTemplates"/>.</summary>
    public string? Tasks { get; set; }
}
