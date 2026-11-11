using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;
using Grsms.Domain.Recruitment;

namespace Grsms.Domain.Employees;

// Phase 28 (11 November 2026, docs/30): the employee master record. It starts where recruitment ends (the onboarding's
// activation) or, for staff hired before GRSMS, when HR registers them. Personal details are NOT copied here: the name,
// email and phone stay on the user account (Users) and the personal details on the Employee Personal Record.

/// <summary>Where the employee is in their employment.</summary>
public enum EmployeeStatus
{
    /// <summary>Activated (or registered) and still on probation.</summary>
    [Display(Name = "On probation")] OnProbation,

    /// <summary>Confirmed, or registered as an established member of staff.</summary>
    [Display(Name = "Active")] Active,

    /// <summary>Left GREL (Phase 33, separation). The record and its history are kept for good.</summary>
    [Display(Name = "Separated")] Separated
}

/// <summary>Why a job history row was added.</summary>
public enum AssignmentSource
{
    /// <summary>Hired through GRSMS: the onboarding was activated.</summary>
    [Display(Name = "Hired")] Hire,

    /// <summary>An existing member of staff registered by HR (on the form or by CSV import).</summary>
    [Display(Name = "Registered")] Registration,

    /// <summary>HR updated the job details (grade, reporting line, contract, position, place of work).</summary>
    [Display(Name = "Job details updated")] Update
}

/// <summary>
/// One GREL employee: the master record that leave, movements, performance, payroll and separation build on.
/// One per person (<see cref="UserId"/> is unique). The "current" job fields below are always a copy of the open
/// <see cref="EmployeeAssignment"/> (the history row with no end date), kept here only so lists are quick.
/// </summary>
public class Employee : BaseEntity
{
    /// <summary>The staff number from payroll (e.g. "GREL-0456"). Unique; the same value as Users.StaffNumber.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>The employee's GRSMS account (Users.Id): name, email, phone and sign-in. Unique.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The onboarding they were hired through, which links back to the offer, application and vacancy. Null for staff registered directly.</summary>
    public int? OnboardingRecordId { get; set; }

    // ---------------------------------------------------------------- current job (copy of the open assignment)
    public string PositionTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int SiteId { get; set; }
    public int? GradeId { get; set; }

    /// <summary>The line manager (Users.Id). When empty, the Head of Department acts as line manager.</summary>
    public string? ReportsToUserId { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    /// <summary>For contracts and temporary staff: when the current contract ends.</summary>
    public DateOnly? ContractEndDate { get; set; }

    // ---------------------------------------------------------------- employment
    /// <summary>First day at GREL (the onboarding's start date, or the date HR registered).</summary>
    public DateOnly HireDate { get; set; }

    /// <summary>When the appointment was confirmed after probation.</summary>
    public DateOnly? ConfirmationDate { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.OnProbation;
    public DateTime StatusChangedAt { get; set; }

    /// <summary>Changes on every save; two people editing the same employee cannot overwrite each other.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>The job history. Add new lines here so the employee and its line are saved together.</summary>
    public ICollection<EmployeeAssignment> Assignments { get; set; } = new List<EmployeeAssignment>();
}

/// <summary>
/// One line of an employee's job history. A change never edits a row: it closes the open row (sets
/// <see cref="EffectiveTo"/>) and adds a new one, so the full history is kept.
/// </summary>
public class EmployeeAssignment : BaseEntity
{
    public int EmployeeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>The last day of this assignment; null while it is the current one.</summary>
    public DateOnly? EffectiveTo { get; set; }

    public string PositionTitle { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public int SiteId { get; set; }
    public int? GradeId { get; set; }
    public string? ReportsToUserId { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public DateOnly? ContractEndDate { get; set; }

    public AssignmentSource Source { get; set; }

    /// <summary>The record that caused it, e.g. the onboarding for <see cref="AssignmentSource.Hire"/>.</summary>
    public int? SourceId { get; set; }

    /// <summary>Why it changed (required for an update).</summary>
    public string? Note { get; set; }
    public string? RecordedByUserId { get; set; }
}

/// <summary>A GREL salary grade, e.g. "G5: Senior Officer". Kept by HR on the Grades page.</summary>
public class Grade : BaseEntity
{
    /// <summary>Short code, e.g. "G5". Unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Name, e.g. "Senior Officer". Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Order on screens (lowest first).</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional monthly salary range in GHS, for reference.</summary>
    public decimal? MinMonthlySalary { get; set; }
    public decimal? MaxMonthlySalary { get; set; }

    /// <summary>Inactive grades are kept for history but cannot be given to anyone.</summary>
    public bool IsActive { get; set; } = true;
}
