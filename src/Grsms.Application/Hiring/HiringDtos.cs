using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;

namespace Grsms.Application.Hiring;

public record CheckDto(int Id, CheckType Type, string Subject, CheckStatus Status, string? Notes, DateTime? CompletedAt, string RecordedBy);

public record RecommendationDto(
    int Id, string Number, HiringStatus Status, decimal ProposedSalary, DateOnly ProposedStartDate, string Justification,
    string CreatedBy, DateTime CreatedAt,
    HiringDecision HodDecision, string? HodName, string? HodComment, DateTime? HodDecidedAt,
    HiringDecision HrDecision, string? HrName, string? HrComment, DateTime? HrDecidedAt,
    HiringDecision MdDecision, string? MdName, string? MdComment, DateTime? MdDecidedAt);

public record OfferDto(int Id, string Number, OfferStatus Status, string JobTitle, decimal MonthlySalary, DateOnly StartDate, DateOnly ExpiresOn,
    string? Terms, DateTime SentAt, DateTime? RespondedAt, string? Reason);

/// <summary>Everything about hiring one applicant: checks, the recommendation and its approvals, and offers.</summary>
public record HiringCaseDto
{
    public int ApplicationId { get; init; }
    public string ApplicationNumber { get; init; } = "";
    public string ApplicantName { get; init; } = "";
    public ApplicationStatus ApplicationStatus { get; init; }
    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public int Positions { get; init; }
    public int PositionsTaken { get; init; }
    public int? ScreeningScore { get; init; }
    public decimal? InterviewAverage { get; init; }
    public int CompletedInterviews { get; init; }
    public decimal? VacancySalaryMin { get; init; }
    public decimal? VacancySalaryMax { get; init; }

    public IReadOnlyList<CheckDto> Checks { get; init; } = [];
    public RecommendationDto? Recommendation { get; init; }
    public IReadOnlyList<RecommendationDto> PastRecommendations { get; init; } = [];
    public OfferDto? Offer { get; init; }
    public IReadOnlyList<OfferDto> PastOffers { get; init; } = [];

    public bool CanRecordChecks { get; init; }
    public bool CanRecommend { get; init; }
    public string? RecommendBlockedReason { get; init; }
    public bool CanHodDecide { get; init; }
    public bool CanHrDecide { get; init; }
    public bool HrNeedsOverride { get; init; }
    public bool CanMdDecide { get; init; }
    public bool CanWithdrawRecommendation { get; init; }
    public bool CanOffer { get; init; }
    public string? OfferBlockedReason { get; init; }
    public bool CanWithdrawOffer { get; init; }
}

/// <summary>Filters on the Hiring list.</summary>
public class HiringFilter
{
    /// <summary>"awaiting" (my decision), "approved" (ready for an offer), "offers" (sent) or "all".</summary>
    public string Tab { get; set; } = "awaiting";
    public HiringStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record HiringListItemDto(int RecommendationId, string Number, int ApplicationId, string ApplicantName, string VacancyNumber,
    string JobTitle, string DivisionName, string DepartmentName, HiringStatus Status, decimal ProposedSalary, DateOnly ProposedStartDate,
    DateTime CreatedAt, OfferStatus? OfferStatus);

/// <summary>The offer as the candidate sees it (the offer letter).</summary>
public record MyOfferDto(int Id, string Number, string CandidateName, string JobTitle, string DepartmentName, string SiteName, string? ReportingTo,
    decimal MonthlySalary, DateOnly StartDate, DateOnly ExpiresOn, string? Terms, OfferStatus Status, DateTime SentAt, DateTime? RespondedAt,
    bool CanRespond);
