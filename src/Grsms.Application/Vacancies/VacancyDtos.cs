using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Vacancies;

/// <summary>
/// The vacancy create/edit form (Recruitment Officer). Department, number of positions and employment type
/// come from the approved request and are shown read-only, so a vacancy cannot drift from what was approved.
/// </summary>
public class VacancyFormDto
{
    /// <summary>0 when creating.</summary>
    public int Id { get; set; }

    /// <summary>The approved recruitment request this vacancy is created from.</summary>
    [Required]
    public int RecruitmentRequestId { get; set; }

    // ---- shown, not edited (filled in by the service)
    public string? RequestNumber { get; set; }
    public string? DepartmentName { get; set; }
    public int DepartmentId { get; set; }
    public int NumberOfPositions { get; set; }
    public EmploymentType EmploymentType { get; set; }

    // ---- the job
    [Required, StringLength(150)]
    [Display(Name = "Job title")]
    public string JobTitle { get; set; } = string.Empty;

    [Display(Name = "Unit")]
    public int? UnitId { get; set; }

    [Required(ErrorMessage = "Choose the work location.")]
    [Display(Name = "Site / work location")]
    public int? SiteId { get; set; }

    [Display(Name = "Reporting manager")]
    public string? ReportingManagerUserId { get; set; }

    [Required]
    [Display(Name = "Who may apply")]
    public VacancyType? VacancyType { get; set; } = Domain.Vacancies.VacancyType.InternalAndExternal;

    [Required, StringLength(8000, MinimumLength = 50, ErrorMessage = "Write a job description of at least 50 characters.")]
    [Display(Name = "Job description (purpose, duties and responsibilities)")]
    public string JobDescription { get; set; } = string.Empty;

    // ---- requirements
    [Required]
    [Display(Name = "Minimum education")]
    public EducationLevel? MinimumEducation { get; set; } = EducationLevel.None;

    [StringLength(200)]
    [Display(Name = "Field of study")]
    public string? FieldOfStudy { get; set; }

    [StringLength(1000)]
    [Display(Name = "Other qualification requirements")]
    public string? QualificationNotes { get; set; }

    [Range(0, 40)]
    [Display(Name = "Minimum years of experience")]
    public int MinYearsExperience { get; set; }

    [StringLength(1000)]
    [Display(Name = "Type of experience")]
    public string? ExperienceNotes { get; set; }

    /// <summary>One per line. Parsed into VacancyRequirement rows.</summary>
    [Display(Name = "Essential skills (one per line)")]
    public string? EssentialSkills { get; set; }

    [Display(Name = "Desirable skills (one per line)")]
    public string? DesirableSkills { get; set; }

    [Display(Name = "Essential certifications (one per line)")]
    public string? EssentialCertifications { get; set; }

    [Display(Name = "Desirable certifications (one per line)")]
    public string? DesirableCertifications { get; set; }

    // ---- salary and dates
    [Range(typeof(decimal), "0", "10000000")]
    [Display(Name = "Monthly salary from (GHS)")]
    public decimal? SalaryMin { get; set; }

    [Range(typeof(decimal), "0", "10000000")]
    [Display(Name = "Monthly salary to (GHS)")]
    public decimal? SalaryMax { get; set; }

    [Display(Name = "Show the salary range on adverts")]
    public bool ShowSalary { get; set; }

    [Required(ErrorMessage = "Choose the closing date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Closing date (last day to apply)")]
    public DateOnly? ClosingDate { get; set; }
}

/// <summary>Filters on the Vacancies list.</summary>
public class VacancyFilter
{
    /// <summary>"open", "pending", "awaiting" (approved requests without a vacancy) or "all".</summary>
    public string Tab { get; set; } = "open";
    public VacancyStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }

    /// <summary>Recruitment period: vacancies published (or, if never published, created) on or after this day.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Recruitment period: vacancies published (or, if never published, created) on or before this day.</summary>
    public DateOnly? To { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>One row on the Vacancies list.</summary>
public record VacancyListItemDto(
    int Id,
    string VacancyNumber,
    string JobTitle,
    string DivisionName,
    string DepartmentName,
    int NumberOfPositions,
    VacancyType VacancyType,
    VacancyStatus Status,
    DateOnly ClosingDate,
    string RecruiterName,
    DateTime StatusChangedAt);

/// <summary>An approved recruitment request that has no vacancy yet ("Awaiting a vacancy" tab).</summary>
public record ApprovedRequestDto(
    int Id,
    string RequestNumber,
    string PositionTitle,
    int NumberOfPositions,
    string DepartmentName,
    DateTime? ApprovedAt,
    string? AssignedRecruiterName);

public record VacancyHistoryDto(VacancyAction Action, VacancyStatus FromStatus, VacancyStatus ToStatus, string ActorName, string? Comments, DateTime ActionAt);

public record VacancyRequirementDto(RequirementType Type, string Name, bool IsEssential);

/// <summary>Everything on the vacancy details page, plus what the current user may do.</summary>
public record VacancyDetailsDto
{
    public int Id { get; init; }
    public string VacancyNumber { get; init; } = "";
    public VacancyStatus Status { get; init; }
    public DateTime StatusChangedAt { get; init; }

    public int RecruitmentRequestId { get; init; }
    public string RequestNumber { get; init; } = "";

    public string JobTitle { get; init; } = "";
    public int DepartmentId { get; init; }
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string RequestingHodName { get; init; } = "";
    public string? UnitName { get; init; }
    public string SiteName { get; init; } = "";
    public string? ReportingManagerName { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public int NumberOfPositions { get; init; }
    public VacancyType VacancyType { get; init; }
    public string JobDescription { get; init; } = "";

    public EducationLevel MinimumEducation { get; init; }
    public string? FieldOfStudy { get; init; }
    public string? QualificationNotes { get; init; }
    public int MinYearsExperience { get; init; }
    public string? ExperienceNotes { get; init; }
    public IReadOnlyList<VacancyRequirementDto> Requirements { get; init; } = [];

    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    public bool ShowSalary { get; init; }
    public DateOnly ClosingDate { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ClosedAt { get; init; }

    public string RecruiterName { get; init; } = "";
    public IReadOnlyList<VacancyHistoryDto> History { get; init; } = [];

    /// <summary>Accepting applications right now (published and closing date not passed).</summary>
    public bool IsOpenForApplications { get; init; }

    /// <summary>Days until the closing date (0 = closes today; negative = passed).</summary>
    public int DaysToClosing { get; init; }

    /// <summary>The comments of the last "return for changes", shown to the Recruitment Officer while in Draft.</summary>
    public string? ReturnComments { get; init; }

    // ---- permissions
    public bool CanEdit { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanDecide { get; init; }
    public string? DecisionBlockedReason { get; init; }
    public bool CanClose { get; init; }
    public bool CanReopen { get; init; }
    public bool CanArchive { get; init; }
}

public enum VacancyDecision { Approve, Return }

/// <summary>The HR Manager's decision on a vacancy pending approval.</summary>
public class VacancyDecisionDto
{
    [Required] public VacancyDecision? Decision { get; set; }
    [StringLength(2000)] public string? Comments { get; set; }

    /// <summary>The status the HR Manager saw; a stale page is refused.</summary>
    [Required] public VacancyStatus? ExpectedStatus { get; set; }
}

/// <summary>Vacancy data access beyond the generic repository.</summary>
public interface IVacancyRepository : IRepository<Vacancy>, IVacancyLookup
{
    /// <summary>A vacancy with its requirements and history, or null.</summary>
    Task<Vacancy?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>The vacancy list, limited to what the user may see and to <paramref name="statuses"/> if given.</summary>
    Task<PagedResult<VacancyListItemDto>> SearchAsync(VacancyFilter filter, RequestVisibility visibility,
        IReadOnlyCollection<VacancyStatus>? statuses, CancellationToken ct = default);

    /// <summary>Approved recruitment requests that do not have a vacancy yet.</summary>
    Task<IReadOnlyList<ApprovedRequestDto>> GetApprovedRequestsWithoutVacancyAsync(RequestVisibility visibility, CancellationToken ct = default);

    /// <summary>Published vacancies whose closing date is before <paramref name="today"/> (with history loaded, ready to close).</summary>
    Task<IReadOnlyList<Vacancy>> GetExpiredPublishedAsync(DateOnly today, CancellationToken ct = default);
}
