using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Employees;
using Grsms.Domain.Probation;
using Grsms.Domain.Recruitment;

namespace Grsms.Application.Employees;

// Phase 28 (docs/30): what the employee screens show and send.

/// <summary>The employee list's filter (all optional).</summary>
public class EmployeeFilter
{
    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public EmployeeStatus? Status { get; set; }
    public int? GradeId { get; set; }
}

public record EmployeeListItemDto(int Id, string EmployeeNumber, string Name, string PositionTitle, string DepartmentName, string SiteName,
    string? GradeName, EmploymentType EmploymentType, EmployeeStatus Status, DateOnly HireDate);

public record EmployeeCountsDto(int All, int OnProbation, int Active, int Separated);

/// <summary>One line of the job history, newest first on screen.</summary>
public record EmployeeAssignmentDto(DateOnly EffectiveFrom, DateOnly? EffectiveTo, string PositionTitle, string DepartmentName, string SiteName,
    string? GradeName, string? ReportsTo, EmploymentType EmploymentType, DateOnly? ContractEndDate, AssignmentSource Source, string? Note, string? RecordedBy);

public record EmployeeDetailsDto(
    int Id, string EmployeeNumber, string Name, string? Email, string? Phone, bool AccountActive,
    string PositionTitle, int DepartmentId, string DepartmentName, string DivisionName, int SiteId, string SiteName, int? GradeId, string? GradeName,
    string? ReportsToUserId, string? ReportsTo, string LineManagerNote, EmploymentType EmploymentType, DateOnly? ContractEndDate,
    DateOnly HireDate, DateOnly? ConfirmationDate, EmployeeStatus Status,
    int? OnboardingRecordId, string? OnboardingNumber, int? PersonnelFileId, string? PersonnelFileNumber,
    int? ProbationId, string? ProbationNumber, ProbationStatus? ProbationStatus,
    IReadOnlyList<EmployeeAssignmentDto> History, bool CanManage, Guid Version)
{
    /// <summary>Years and months at GREL, e.g. "3 years 2 months".</summary>
    public string ServiceLength(DateOnly today)
    {
        var months = (today.Year - HireDate.Year) * 12 + today.Month - HireDate.Month - (today.Day < HireDate.Day ? 1 : 0);
        if (months < 0) return "Not started yet";
        var (y, m) = (months / 12, months % 12);
        return y == 0 && m == 0 ? "Less than a month"
            : string.Join(" ", new[] { y > 0 ? $"{y} year{(y == 1 ? "" : "s")}" : null, m > 0 ? $"{m} month{(m == 1 ? "" : "s")}" : null }.OfType<string>());
    }
}

/// <summary>Registering an existing member of staff (hired before GRSMS).</summary>
public class RegisterEmployeeForm
{
    [Required, StringLength(150, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email address", Description = "Their sign-in. An existing staff account with this address is used.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Staff number", Description = "From payroll, e.g. GREL-0456.")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, StringLength(150)]
    [Display(Name = "Position")]
    public string PositionTitle { get; set; } = string.Empty;

    [Required, Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required, Display(Name = "Place of work (site)")]
    public int? SiteId { get; set; }

    [Display(Name = "Grade")]
    public int? GradeId { get; set; }

    [Display(Name = "Reports to", Description = "The line manager. Leave empty and the Head of Department acts as line manager.")]
    public string? ReportsToUserId { get; set; }

    [Display(Name = "Employment type")]
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    [Display(Name = "Contract end date", Description = "For contract and temporary staff.")]
    public DateOnly? ContractEndDate { get; set; }

    [Required, Display(Name = "Date joined GREL")]
    public DateOnly? HireDate { get; set; }

    [Display(Name = "Status")]
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    [Display(Name = "Confirmation date", Description = "When their appointment was confirmed, if known.")]
    public DateOnly? ConfirmationDate { get; set; }
}

/// <summary>Updating an employee's job details: adds a job history line from the effective date.</summary>
public class UpdateJobForm
{
    public int EmployeeId { get; set; }
    public Guid Version { get; set; }

    [Required, Display(Name = "With effect from")]
    public DateOnly? EffectiveFrom { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Position")]
    public string PositionTitle { get; set; } = string.Empty;

    [Required, Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Required, Display(Name = "Place of work (site)")]
    public int? SiteId { get; set; }

    [Display(Name = "Grade")]
    public int? GradeId { get; set; }

    [Display(Name = "Reports to")]
    public string? ReportsToUserId { get; set; }

    [Display(Name = "Employment type")]
    public EmploymentType EmploymentType { get; set; }

    [Display(Name = "Contract end date")]
    public DateOnly? ContractEndDate { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    [Display(Name = "Reason for the change", Description = "Kept in the job history, e.g. \"Grade given after the 2026 grading exercise\".")]
    public string Note { get; set; } = string.Empty;
}

/// <summary>The outcome of a CSV import: how many were imported, or what is wrong (by row) when nothing was.</summary>
public record EmployeeImportResult(int Imported, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

// ---------------------------------------------------------------- Grades

public record GradeListItemDto(int Id, string Code, string Name, int SortOrder, decimal? MinMonthlySalary, decimal? MaxMonthlySalary, bool IsActive, int Employees);

public class GradeFormDto
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    [RegularExpression("^[A-Za-z0-9&./-]+$", ErrorMessage = "Use letters, numbers, '&', '.', '/' or '-' only (no spaces).")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 999)]
    [Display(Name = "Order on screens")]
    public int SortOrder { get; set; }

    [Range(0, 10_000_000)]
    [Display(Name = "Minimum monthly salary (GHS)")]
    public decimal? MinMonthlySalary { get; set; }

    [Range(0, 10_000_000)]
    [Display(Name = "Maximum monthly salary (GHS)")]
    public decimal? MaxMonthlySalary { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
