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
    /// <summary>Planned, or taking place; panel members submit their scorecards from the start time.</summary>
    [Display(Name = "Scheduled")] Scheduled,

    /// <summary>The scores are validated by the Recruitment Officer: final, and visible to the HOD, MD and in the results.</summary>
    [Display(Name = "Completed (scores validated)")] Completed,
    [Display(Name = "Cancelled")] Cancelled,
    [Display(Name = "Did not attend")] NoShow,

    /// <summary>Every panel member who took part has submitted; the vacancy's Recruitment Officer checks and validates the scores.</summary>
    [Display(Name = "Awaiting score validation")] AwaitingValidation
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

    /// <summary>Who validated the scores, when, and their optional note (status Completed). Null for sessions completed before 9 Nov 2026.</summary>
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedByUserId { get; set; }
    public string? ValidationNote { get; set; }

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

    /// <summary>
    /// Whether the member took part (score validation, 9 Nov 2026). A member marked Absent or Excused does not have to submit a
    /// scorecard before the scores can be validated. Submitting a scorecard counts as taking part.
    /// </summary>
    public PanelAttendance Attendance { get; set; } = PanelAttendance.NotRecorded;
    public string? AttendanceNote { get; set; }
    public string? AttendanceRecordedByUserId { get; set; }
    public DateTime? AttendanceRecordedAt { get; set; }
}

/// <summary>Whether a panel member took part in the session.</summary>
public enum PanelAttendance
{
    [Display(Name = "Not recorded")] NotRecorded,
    [Display(Name = "Present")] Present,
    [Display(Name = "Absent")] Absent,
    [Display(Name = "Excused")] Excused
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

    /// <summary>
    /// Set when the validating Recruitment Officer sends the scorecard back to its panel member to correct (with the reason).
    /// A returned scorecard does not count until the member submits it again, which clears these fields.
    /// </summary>
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnReason { get; set; }
    public string? ReturnedByUserId { get; set; }

    /// <summary>True when the scorecard counts: submitted and not sent back for correction.</summary>
    public bool IsValid => ReturnedAt is null;

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
