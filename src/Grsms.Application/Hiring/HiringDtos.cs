using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;

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

    /// <summary>
    /// Every interview or assessment round with its scores, so the recommendation, the approvals and the offer are made with the
    /// panel's scores in view (9 Nov 2026). Scores are filled only for rounds the Recruitment Officer has validated.
    /// </summary>
    public IReadOnlyList<InterviewRoundScoresDto> InterviewRounds { get; init; } = [];
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

    /// <summary>A draft recommendation (hiring process started with "Proceed") that the recruiter can review and submit to the HOD, or discard.</summary>
    public bool CanSubmitDraft { get; init; }
    public bool CanOffer { get; init; }
    public string? OfferBlockedReason { get; init; }
    public bool CanWithdrawOffer { get; init; }

    /// <summary>13 November 2026: the offer drafted when the hire was approved (null when none, or once sent).</summary>
    public OfferDto? OfferDraft { get; init; }

    /// <summary>The Recruitment Officer may edit the draft (or create one) now, even while checks still block sending.</summary>
    public bool CanEditOffer { get; init; }

    /// <summary>The values the offer form starts with: the draft's, or the approved hire's with the default reply-by date and terms.</summary>
    public decimal FormSalary { get; init; }
    public DateOnly FormStartDate { get; init; }
    public DateOnly FormExpiresOn { get; init; }
    public string? FormTerms { get; init; }
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

    /// <summary>"Ready to recommend" tab: only this vacancy's candidates (where score validation leads).</summary>
    public int? VacancyId { get; set; }
}

public record HiringListItemDto(int RecommendationId, string Number, int ApplicationId, string ApplicantName, string VacancyNumber,
    string JobTitle, string DivisionName, string DepartmentName, HiringStatus Status, decimal ProposedSalary, DateOnly ProposedStartDate,
    DateTime CreatedAt, OfferStatus? OfferStatus, decimal? InterviewAverage = null);

/// <summary>The offer as the candidate sees it (the offer letter).</summary>
public record MyOfferDto(int Id, string Number, string CandidateName, string JobTitle, string DepartmentName, string SiteName, string? ReportingTo,
    decimal MonthlySalary, DateOnly StartDate, DateOnly ExpiresOn, string? Terms, OfferStatus Status, DateTime SentAt, DateTime? RespondedAt,
    bool CanRespond);

/// <summary>One interview round on the hiring case. <see cref="Scores"/> is empty until the scores are validated.</summary>
public record InterviewRoundScoresDto(int InterviewId, int Round, InterviewType Type, DateTime StartsAt, InterviewStatus Status,
    int ScorecardsIn, int PanelSize, decimal? Average, string? ValidatedBy, DateTime? ValidatedAt, string? WaitingFor,
    IReadOnlyList<PanelScoreDto> Scores);

/// <summary>One panel member's validated score on the hiring case.</summary>
public record PanelScoreDto(string PanelName, bool IsChair, decimal Score, PanelRecommendation Recommendation, string? Strengths, string? Concerns);

/// <summary>A candidate whose interview scores are validated and who is ready for a hiring recommendation (9 Nov 2026).</summary>
public record ReadyToRecommendDto(int ApplicationId, string ApplicationNumber, string ApplicantName, int VacancyId, string VacancyNumber,
    string JobTitle, string DepartmentName, decimal? InterviewAverage, int ValidatedRounds, DateTime LastValidatedAt);
