using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Interviews;

/// <summary>What kind of session it is.</summary>
public enum InterviewType
{
    [Display(Name = "Interview")] Interview,
    [Display(Name = "Written test")] WrittenTest,
    [Display(Name = "Practical assessment")] PracticalTest
}

/// <summary>How the applicant attends.</summary>
public enum InterviewMode
{
    [Display(Name = "In person")] InPerson,
    [Display(Name = "Video call")] Video,
    [Display(Name = "Phone call")] Phone
}

public enum InterviewStatus
{
    [Display(Name = "Scheduled")] Scheduled,
    [Display(Name = "Completed")] Completed,
    [Display(Name = "Cancelled")] Cancelled,
    [Display(Name = "Did not attend")] NoShow
}

/// <summary>A panel member's overall recommendation on their scorecard.</summary>
public enum PanelRecommendation
{
    [Display(Name = "Strongly recommend")] StronglyRecommend,
    [Display(Name = "Recommend")] Recommend,
    [Display(Name = "Recommend with reservations")] WithReservations,
    [Display(Name = "Do not recommend")] DoNotRecommend
}

/// <summary>
/// An interview or assessment of one shortlisted applicant (Module 7). An application can have several rounds
/// (e.g. a written test, then an interview). See <see cref="InterviewWorkflow"/> for the rules.
///
/// The panel always includes the vacancy's department Head of Department (GREL requirement: HODs take part in
/// interview evaluation and scorecard reviews). Each panel member rates the weighted criteria on a scorecard.
/// Like every recruitment record, it stores the division, department and requesting HOD of its vacancy.
/// </summary>
public class Interview : BaseEntity
{
    public int JobApplicationId { get; set; }
    public int VacancyId { get; set; }

    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    /// <summary>1 for the first session of this application, 2 for the next, ...</summary>
    public int Round { get; set; } = 1;

    public InterviewType Type { get; set; }
    public InterviewMode Mode { get; set; }

    /// <summary>Start time (UTC; Ghana is on GMT all year, so UTC = local time).</summary>
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 45;

    /// <summary>Room and building, or the video link / phone arrangement.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>What the applicant should bring or prepare (shown to them and in their invitation).</summary>
    public string? CandidateInstructions { get; set; }

    public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;
    public string? StatusReason { get; set; }
    public string ScheduledByUserId { get; set; } = string.Empty;

    /// <summary>Concurrency stamp.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<InterviewPanelMember> Panel { get; set; } = new List<InterviewPanelMember>();
    public ICollection<InterviewCriterion> Criteria { get; set; } = new List<InterviewCriterion>();
    public ICollection<Scorecard> Scorecards { get; set; } = new List<Scorecard>();
}

/// <summary>One person on an interview panel.</summary>
public class InterviewPanelMember
{
    public int Id { get; set; }
    public int InterviewId { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The chair leads the session. One per panel.</summary>
    public bool IsChair { get; set; }
}

/// <summary>One thing the panel scores, e.g. "Technical knowledge", with its weight (all weights add up to 100).</summary>
public class InterviewCriterion
{
    public int Id { get; set; }
    public int InterviewId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A panel member's scorecard: a 1-5 rating per criterion, a recommendation and comments.</summary>
public class Scorecard : BaseEntity
{
    public int InterviewId { get; set; }
    public string PanelUserId { get; set; } = string.Empty;
    public PanelRecommendation Recommendation { get; set; }
    public string? Strengths { get; set; }
    public string? Concerns { get; set; }
    public DateTime SubmittedAt { get; set; }

    /// <summary>The weighted total out of 100: Σ (rating ÷ 5 × weight).</summary>
    public decimal WeightedScore { get; set; }

    public ICollection<ScorecardRating> Ratings { get; set; } = new List<ScorecardRating>();
}

public class ScorecardRating
{
    public int Id { get; set; }
    public int ScorecardId { get; set; }
    public int CriterionId { get; set; }

    /// <summary>1 = poor, 2 = below expectations, 3 = meets, 4 = above, 5 = outstanding.</summary>
    public int Score { get; set; }
}
