using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Recruitment;

/// <summary>
/// The digital QF-81-11 Recruitment Request Form, filled in by a department staff member or the Head of Department.
/// Sections: Employment details, Request information, Description of the post, Qualification requirements,
/// Additional information, and Budget (checked by the HR Manager). Supporting documents are uploaded on the next page.
/// </summary>
public class RecruitmentRequestFormDto
{
    /// <summary>Most characters (of text, not formatting) allowed in the Description of the post.</summary>
    public const int MaxPostDescriptionChars = 10000;

    /// <summary>Fewest characters needed in the Description of the post.</summary>
    public const int MinPostDescriptionChars = 50;

    /// <summary>0 when creating; the record id when editing.</summary>
    public int Id { get; set; }

    // ---------------------------------------------------------------- Where

    [Required(ErrorMessage = "Choose the department.")]
    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Unit (optional)")]
    public int? UnitId { get; set; }

    [Required(ErrorMessage = "Choose where the new hire will work.")]
    [Display(Name = "Site / work location")]
    public int? SiteId { get; set; }

    // ---------------------------------------------------------------- Employment details

    [Required(ErrorMessage = "Choose the engagement type.")]
    [Display(Name = "Engagement type")]
    public EmploymentType? EmploymentType { get; set; }

    [Required(ErrorMessage = "Choose the category.")]
    [Display(Name = "Category")]
    public StaffCategory? StaffCategory { get; set; }

    [Required(ErrorMessage = "Choose the purpose of the request.")]
    [Display(Name = "Purpose")]
    public VacancyReason? VacancyReason { get; set; }

    [StringLength(200)]
    [Display(Name = "Other purpose (please specify)")]
    public string? PurposeOther { get; set; }

    [StringLength(150)]
    [Display(Name = "Employee being replaced")]
    public string? ReplacedEmployee { get; set; }

    // ---------------------------------------------------------------- Request information

    [Required(ErrorMessage = "Enter the request date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Request date")]
    public DateOnly? RequestDate { get; set; }

    [Required(ErrorMessage = "Enter the specific job title."), StringLength(150)]
    [Display(Name = "Specific job title")]
    public string PositionTitle { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Enter between 1 and 100 staff.")]
    [Display(Name = "Number of staff required")]
    public int NumberOfPositions { get; set; } = 1;

    [Required(ErrorMessage = "Enter the proposed starting date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Proposed starting date")]
    public DateOnly? ProposedStartDate { get; set; }

    // ---------------------------------------------------------------- Description of the post

    /// <summary>Formatted text from the rich-text editor; cleaned by <see cref="IRichTextSanitizer"/> before saving.</summary>
    [Required(ErrorMessage = "Describe the post.")]
    [StringLength(100_000, ErrorMessage = "The description of the post is too long.")]
    [Display(Name = "Description of the post")]
    public string? PostDescription { get; set; }

    [Required, StringLength(4000, MinimumLength = 20, ErrorMessage = "Explain the need in at least 20 characters.")]
    [Display(Name = "Justification")]
    public string Justification { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Qualification requirements

    [Required(ErrorMessage = "Choose the educational level.")]
    [Display(Name = "Educational level")]
    public EducationLevel? EducationLevel { get; set; }

    [StringLength(200)]
    [Display(Name = "Category (field of study)")]
    public string? QualificationCategory { get; set; }

    [StringLength(200)]
    [Display(Name = "Level of training")]
    public string? TrainingLevel { get; set; }

    [StringLength(500)]
    [Display(Name = "Certificate(s) required (one per line)")]
    public string? CertificateRequired { get; set; }

    [Range(0, 40, ErrorMessage = "Enter between 0 and 40 years.")]
    [Display(Name = "Years of professional experience")]
    public int? MinYearsExperience { get; set; }

    [StringLength(2000)]
    [Display(Name = "Professional experience")]
    public string? ProfessionalExperience { get; set; }

    /// <summary>Optional: "Not needed" unless the department chooses otherwise.</summary>
    [Display(Name = "Driving licence")]
    public DrivingLicenceRequirement DrivingLicence { get; set; } = DrivingLicenceRequirement.NotNeeded;

    [StringLength(50)]
    [Display(Name = "Licence class (optional)")]
    public string? DrivingLicenceClass { get; set; }

    // ---------------------------------------------------------------- Additional information

    [StringLength(2000)]
    [Display(Name = "Observations / remarks")]
    public string? Remarks { get; set; }

    // No budget fields: QF-81-11 has none. The budget is evidenced by the "Budget approval document" upload, which the
    // HR Manager confirms at stage 2. (Requests raised before the form was digitised keep their budget code and cost.)
}

/// <summary>Filters on the Recruitment Requests list. Every filter is optional.</summary>
public class RecruitmentRequestFilter
{
    /// <summary>True = only requests waiting for the current user's decision.</summary>
    public bool AwaitingMe { get; set; }

    public RecruitmentRequestStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }

    /// <summary>Recruitment period: requests submitted (or, for drafts, created) on or after this day.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Recruitment period: requests submitted (or, for drafts, created) on or before this day.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Part of the request number or position title.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>One row on the Recruitment Requests list.</summary>
public record RecruitmentRequestListItemDto(
    int Id,
    string RequestNumber,
    string PositionTitle,
    int NumberOfPositions,
    string DivisionName,
    string DepartmentName,
    string RequestedByName,
    RecruitmentRequestStatus Status,
    DateTime? DateRequested,
    DateTime StatusChangedAt,
    decimal EstimatedAnnualCost);

/// <summary>One line of a request's history timeline.</summary>
public record RecruitmentRequestHistoryDto(
    WorkflowAction Action,
    RecruitmentRequestStatus FromStatus,
    RecruitmentRequestStatus ToStatus,
    string ActorName,
    string? Comments,
    DateTime ActionAt);

/// <summary>Everything on the request's details page, including what the current user may do with it.</summary>
public record RecruitmentRequestDetailsDto
{
    public int Id { get; init; }
    public string RequestNumber { get; init; } = "";
    public RecruitmentRequestStatus Status { get; init; }
    public DateTime StatusChangedAt { get; init; }

    public int DepartmentId { get; init; }
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string? UnitName { get; init; }
    public string SiteName { get; init; } = "";

    public string PositionTitle { get; init; } = "";
    public int NumberOfPositions { get; init; }
    public EmploymentType EmploymentType { get; init; }
    public StaffCategory? StaffCategory { get; init; }
    public VacancyReason VacancyReason { get; init; }
    public string? PurposeOther { get; init; }
    public string? ReplacedEmployee { get; init; }
    public string Justification { get; init; } = "";

    public DateOnly? RequestDate { get; init; }
    public DateOnly? ProposedStartDate { get; init; }

    /// <summary>Sanitised HTML, safe to output as-is.</summary>
    public string? PostDescriptionHtml { get; init; }

    // ---- Qualification requirements
    public EducationLevel? EducationLevel { get; init; }
    public string? QualificationCategory { get; init; }
    public string? TrainingLevel { get; init; }
    public string? CertificateRequired { get; init; }
    public int? MinYearsExperience { get; init; }
    public string? ProfessionalExperience { get; init; }
    public DrivingLicenceRequirement DrivingLicence { get; init; }
    public string? DrivingLicenceClass { get; init; }

    public string? Remarks { get; init; }

    public string BudgetCode { get; init; } = "";
    public decimal EstimatedAnnualCost { get; init; }
    public bool BudgetValidated { get; init; }
    public string? BudgetValidatedByName { get; init; }

    public string RequestedByUserId { get; init; } = "";
    public string RequestedByName { get; init; } = "";
    public string? HeadOfDepartmentName { get; init; }
    public string? AssignedRecruiterName { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? DateRequested { get; init; }
    public DateTime? ApprovedAt { get; init; }

    public IReadOnlyList<RecruitmentRequestHistoryDto> History { get; init; } = [];
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = [];

    // ---- What the current user may do (worked out by the service, so views stay simple)
    public bool CanEdit { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanDecide { get; init; }
    public bool CanCancel { get; init; }

    /// <summary>Why the current user cannot decide even though it is their stage (e.g. they raised the request).</summary>
    public string? DecisionBlockedReason { get; init; }

    /// <summary>True if a job description is attached (required before submitting).</summary>
    public bool HasJobDescription { get; init; }

    /// <summary>The person (or role) the request is waiting for, e.g. "Ama Mensah (Head of Department)".</summary>
    public string? WaitingFor { get; init; }

    // ---- Phase 3: the vacancy created from this request (if any)
    public int? VacancyId { get; init; }
    public string? VacancyNumber { get; init; }
    public string? RequisitionNumber { get; init; }

    /// <summary>True for a Recruitment Officer looking at an approved request that has no vacancy yet.</summary>
    public bool CanCreateVacancy { get; init; }
}

/// <summary>
/// Lets the recruitment request module ask "is there a vacancy for this request?" without depending on
/// the whole vacancy module. Implemented by the vacancy repository.
/// </summary>
public interface IVacancyLookup
{
    Task<(int Id, string Number, string? RequisitionNumber)?> FindForRequestAsync(int recruitmentRequestId, CancellationToken ct = default);
}

/// <summary>The three choices a reviewer has at each approval stage.</summary>
public enum ReviewDecision
{
    Approve,
    Return,
    Reject
}

/// <summary>What a reviewer posts from the decision panel.</summary>
public class ReviewDecisionDto
{
    [Required]
    public ReviewDecision? Decision { get; set; }

    [StringLength(2000)]
    public string? Comments { get; set; }

    /// <summary>The stage the reviewer saw on screen. If the request has moved on since, the decision is refused.</summary>
    [Required]
    public RecruitmentRequestStatus? ExpectedStatus { get; set; }

}

/// <summary>
/// Who may see which requests. Built by the service from the current user's roles.
/// </summary>
/// <param name="SeeAll">Recruitment Officers, HR Manager, MD and HR Administrator see every request.</param>
/// <param name="UserId">The current user; they always see requests they raised.</param>
/// <param name="HodDepartmentIds">Departments the user heads; they see all of those departments' requests.</param>
public record RequestVisibility(bool SeeAll, string UserId, IReadOnlyCollection<int> HodDepartmentIds);

/// <summary>
/// Data access for recruitment requests, beyond the generic repository: loading a request with its history,
/// and searching with names of departments and people filled in.
/// </summary>
public interface IRecruitmentRequestRepository : IRepository<RecruitmentRequest>
{
    /// <summary>Loads a request with its history (Actions), or null.</summary>
    Task<RecruitmentRequest?> GetWithHistoryAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Searches the requests the user may see. <paramref name="awaiting"/> (if not null) limits the result to the
    /// requests waiting for the user's decision (used for "Awaiting my action").
    /// </summary>
    Task<PagedResult<RecruitmentRequestListItemDto>> SearchAsync(RecruitmentRequestFilter filter, RequestVisibility visibility,
        AwaitingDecision? awaiting, CancellationToken ct = default);
}

/// <summary>What "waiting for my decision" means for one user.</summary>
/// <param name="RoleStages">Stages decided by a role the user holds (HR Manager review, MD approval, ...).</param>
/// <param name="HeadReviewDepartmentIds">Departments whose Department Head review the user does (the departments they head).</param>
/// <param name="ExcludeRequestedBy">The user: requests they raised are left out, because nobody decides on their own request.</param>
public record AwaitingDecision(
    IReadOnlyCollection<RecruitmentRequestStatus> RoleStages,
    IReadOnlyCollection<int> HeadReviewDepartmentIds,
    string ExcludeRequestedBy);
