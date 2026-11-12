using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Organization;
using Grsms.Application.Recruitment;
using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;

namespace Grsms.Application.Interviews;

/// <summary>The "schedule an interview / assessment" form.</summary>
public class ScheduleFormDto
{
    [Required]
    public int ApplicationId { get; set; }

    [Required]
    [Display(Name = "Type")]
    public InterviewType? Type { get; set; } = InterviewType.Interview;

    [Required]
    [Display(Name = "How")]
    public InterviewMode? Mode { get; set; } = InterviewMode.InPerson;

    [Required(ErrorMessage = "Choose the date and time.")]
    [Display(Name = "Date and time (GMT)")]
    public DateTime? StartsAt { get; set; }

    [Range(10, 480, ErrorMessage = "Between 10 minutes and 8 hours.")]
    [Display(Name = "Duration (minutes)")]
    public int DurationMinutes { get; set; } = 45;

    [Required, StringLength(500)]
    [Display(Name = "Where (room, building) or video link")]
    public string Location { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Instructions for the applicant (optional)")]
    public string? CandidateInstructions { get; set; }

    [Display(Name = "Panel")]
    public List<string> PanelUserIds { get; set; } = [];

    [Display(Name = "Chair")]
    public string? ChairUserId { get; set; }

    /// <summary>12 November 2026: schedule even though a panel member or the candidate already has a session at that time.</summary>
    [Display(Name = "Schedule anyway")]
    public bool IgnoreClashes { get; set; }

    /// <summary>Criteria, one per line as "Name | weight". Weights must add up to 100.</summary>
    [Required, StringLength(2000)]
    [Display(Name = "Scorecard criteria (one per line: name | weight)")]
    public string Criteria { get; set; } = string.Empty;

    // ---- Read-only, filled by the service (nullable, so model validation never asks for them)
    public string? ApplicantName { get; set; }
    public string? ApplicationNumber { get; set; }
    public string? JobTitle { get; set; }
    public int Round { get; set; }

    /// <summary>The department's Head(s) of Department, who must be on the panel.</summary>
    public IReadOnlyList<string> RequiredPanelUserIds { get; set; } = [];
    public IReadOnlyList<LookupItem> StaffList { get; set; } = [];
}

/// <summary>Filters on the Interviews list.</summary>
public class InterviewFilter
{
    /// <summary>"mine" (I am on the panel), "upcoming", "scorecards" (mine still to score) or "all".</summary>
    public string Tab { get; set; } = "mine";
    public InterviewStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? VacancyId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public record InterviewListItemDto(
    int Id,
    int ApplicationId,
    string ApplicantName,
    string VacancyNumber,
    string JobTitle,
    string DepartmentName,
    int Round,
    InterviewType Type,
    InterviewMode Mode,
    DateTime StartsAt,
    string Location,
    InterviewStatus Status,
    int PanelSize,
    int ScorecardsIn,
    decimal? AverageScore);

/// <summary>A panel member. HasSubmitted = a valid scorecard; ScorecardReturned = theirs was sent back for correction.</summary>
public record PanelMemberDto(string UserId, string Name, bool IsChair, bool HasSubmitted, bool ScorecardReturned = false,
    PanelAttendance Attendance = PanelAttendance.NotRecorded, string? AttendanceNote = null);

public record CriterionDto(int Id, string Name, int Weight);

public record ScorecardDto(string PanelUserId, string PanelName, PanelRecommendation Recommendation, decimal WeightedScore,
    IReadOnlyDictionary<int, int> Ratings, string? Strengths, string? Concerns, DateTime SubmittedAt,
    DateTime? ReturnedAt = null, string? ReturnReason = null, string? ReturnedByName = null);

/// <summary>The interview page: details, panel, criteria, my scorecard, the scorecards I may see, and actions.</summary>
public record InterviewDetailsDto
{
    public int Id { get; init; }
    public int ApplicationId { get; init; }
    public string ApplicationNumber { get; init; } = "";
    public string ApplicantName { get; init; } = "";
    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public int Round { get; init; }
    public InterviewType Type { get; init; }
    public InterviewMode Mode { get; init; }
    public DateTime StartsAt { get; init; }
    public int DurationMinutes { get; init; }
    public string Location { get; init; } = "";
    public string? CandidateInstructions { get; init; }
    public InterviewStatus Status { get; init; }
    public string? StatusReason { get; init; }

    public IReadOnlyList<PanelMemberDto> Panel { get; init; } = [];
    public IReadOnlyList<CriterionDto> Criteria { get; init; } = [];

    /// <summary>The current user's own scorecard, if submitted.</summary>
    public ScorecardDto? MyScorecard { get; init; }

    /// <summary>Scorecards the user may see (all of them once allowed; otherwise none).</summary>
    public IReadOnlyList<ScorecardDto> Scorecards { get; init; } = [];
    public bool ScorecardsHidden { get; init; }
    public decimal? AverageScore { get; init; }

    public bool IsPanelMember { get; init; }
    public bool CanScore { get; init; }
    public bool CanManage { get; init; }
    public string? ScoreNotYetReason { get; init; }

    // ---- Score validation (9 Nov 2026)

    /// <summary>The vacancy's Recruitment Officer, who validates the scores (another Recruitment Officer may stand in).</summary>
    public string? ValidatorName { get; init; }

    /// <summary>Panel members still owing a valid scorecard, by name.</summary>
    public IReadOnlyList<string> MissingScorers { get; init; } = [];

    /// <summary>The current user may validate, return scorecards and mark absences (a Recruitment Officer not on this panel).</summary>
    public bool IsValidator { get; init; }

    /// <summary>Validate is possible now; otherwise <see cref="ValidationBlockedReason"/> says why.</summary>
    public bool CanValidate { get; init; }
    public string? ValidationBlockedReason { get; init; }

    /// <summary>The current user's scorecard was returned and must be corrected and submitted again.</summary>
    public bool MyScorecardReturned { get; init; }

    public string? ValidatedByName { get; init; }
    public DateTime? ValidatedAt { get; init; }
    public string? ValidationNote { get; init; }
}

/// <summary>One applicant's interview results for a vacancy (the results board).</summary>
public record InterviewResultDto(int ApplicationId, string ApplicationNumber, string ApplicantName, ApplicationStatus Status,
    int? ScreeningScore, IReadOnlyList<(int InterviewId, int Round, InterviewType Type, InterviewStatus Status, decimal? Average, int Scorecards, int Panel)> Sessions,
    decimal? OverallAverage, IReadOnlyDictionary<PanelRecommendation, int> Recommendations);

/// <summary>An upcoming interview, as the applicant sees it.</summary>
public record ApplicantInterviewDto(InterviewType Type, InterviewMode Mode, DateTime StartsAt, int DurationMinutes, string Location,
    string? Instructions, InterviewStatus Status);

/// <summary>Data access for interviews.</summary>
public interface IInterviewRepository : IRepository<Interview>
{
    /// <summary>An interview with its panel, criteria and scorecards (with ratings).</summary>
    Task<Interview?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>All interviews of these applications, with panel and scorecards.</summary>
    Task<IReadOnlyList<Interview>> ListForApplicationsAsync(IReadOnlyCollection<int> applicationIds, CancellationToken ct = default);

    /// <summary>12 November 2026: scheduled sessions starting between the two times, with their panel (for the clash check).</summary>
    Task<IReadOnlyList<Interview>> ListScheduledBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>The Interviews list. <paramref name="panelUserId"/> limits it to sessions with that person on the panel.</summary>
    Task<PagedResult<InterviewListItemDto>> SearchAsync(InterviewFilter filter, RequestVisibility visibility, string? panelUserId,
        bool onlyAwaitingMyScorecard, DateTime now, CancellationToken ct = default);
}
