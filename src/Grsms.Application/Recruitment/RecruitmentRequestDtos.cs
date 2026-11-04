using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Domain.Recruitment;

namespace Grsms.Application.Recruitment;

/// <summary>The create/edit form a Head of Department fills in.</summary>
public class RecruitmentRequestFormDto
{
    /// <summary>0 when creating; the record id when editing.</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Choose the department.")]
    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Unit (optional)")]
    public int? UnitId { get; set; }

    [Required(ErrorMessage = "Choose where the new hire will work.")]
    [Display(Name = "Site / work location")]
    public int? SiteId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Position (job title)")]
    public string PositionTitle { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Enter between 1 and 100 positions.")]
    [Display(Name = "Number of positions")]
    public int NumberOfPositions { get; set; } = 1;

    [Required]
    [Display(Name = "Employment type")]
    public EmploymentType? EmploymentType { get; set; }

    [Required]
    [Display(Name = "Reason for the vacancy")]
    public VacancyReason? VacancyReason { get; set; }

    [StringLength(150)]
    [Display(Name = "Employee being replaced")]
    public string? ReplacedEmployee { get; set; }

    [Required, StringLength(4000, MinimumLength = 20, ErrorMessage = "Explain the need in at least 20 characters.")]
    [Display(Name = "Justification")]
    public string Justification { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Display(Name = "Budget code")]
    public string BudgetCode { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0.01", "100000000", ErrorMessage = "Enter the estimated annual cost in GHS.")]
    [Display(Name = "Estimated annual cost (GHS)")]
    public decimal? EstimatedAnnualCost { get; set; }
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
    public VacancyReason VacancyReason { get; init; }
    public string? ReplacedEmployee { get; init; }
    public string Justification { get; init; } = "";

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

    // ---- Phase 3: the vacancy created from this request (if any)
    public int? VacancyId { get; init; }
    public string? VacancyNumber { get; init; }

    /// <summary>True for a Recruitment Officer looking at an approved request that has no vacancy yet.</summary>
    public bool CanCreateVacancy { get; init; }
}

/// <summary>
/// Lets the recruitment request module ask "is there a vacancy for this request?" without depending on
/// the whole vacancy module. Implemented by the vacancy repository.
/// </summary>
public interface IVacancyLookup
{
    Task<(int Id, string Number)?> FindForRequestAsync(int recruitmentRequestId, CancellationToken ct = default);
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

    /// <summary>Recruitment Review stage only: who will handle the recruitment.</summary>
    public string? AssignedRecruiterUserId { get; set; }

    /// <summary>HR Approval stage only: the HR Manager confirms the budget code and cost are correct.</summary>
    public bool BudgetValidated { get; set; }
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
    /// Searches the requests the user may see. <paramref name="awaitingStatuses"/> (if not null) limits the
    /// result to requests sitting at one of those stages (used for "Awaiting my action").
    /// <paramref name="excludeRequestedBy"/> leaves out requests raised by that user (nobody approves their own).
    /// </summary>
    Task<PagedResult<RecruitmentRequestListItemDto>> SearchAsync(RecruitmentRequestFilter filter, RequestVisibility visibility,
        IReadOnlyCollection<RecruitmentRequestStatus>? awaitingStatuses, string? excludeRequestedBy, CancellationToken ct = default);
}
