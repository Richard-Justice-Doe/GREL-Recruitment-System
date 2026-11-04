using Grsms.Domain.Applications;
using Grsms.Domain.Security;

namespace Grsms.Domain.Interviews;

/// <summary>
/// The rules of interviews and assessments, in one place:
///
///   Shortlisted application ──schedule──► Interview (Scheduled) ──► Completed / Cancelled / Did not attend
///   The application moves to "Interview" at the first scheduled session. Further rounds can be added.
///
///   * Recruitment Officers and HR Managers schedule, complete, cancel and record no-shows.
///   * The panel has at least 2 people, one chair, and always the department's Head of Department; the applicant
///     can never sit on their own panel.
///   * Each panel member submits one scorecard (from the start time until the session is marked completed).
///     Panel members see the others' scorecards only after submitting their own, so nobody is swayed.
///   * Reviewers, the department's HOD, the MD and HR Administrators see every scorecard and the combined result.
/// </summary>
public static class InterviewWorkflow
{
    public static readonly IReadOnlyList<string> SchedulerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    public const int MinPanelSize = 2, MaxPanelSize = 8, MinRating = 1, MaxRating = 5;

    /// <summary>Default scorecard criteria (weights add up to 100). The scheduler can change them per session.</summary>
    public static readonly IReadOnlyList<(string Name, int Weight)> DefaultCriteria =
    [
        ("Technical knowledge and skills", 30),
        ("Relevant experience", 25),
        ("Communication", 15),
        ("Problem solving and judgement", 15),
        ("Attitude, safety awareness and fit with GREL", 15)
    ];

    /// <summary>Applications can be scheduled once shortlisted, and get further rounds while at the interview stage.</summary>
    public static bool CanSchedule(ApplicationStatus s) => s is ApplicationStatus.Shortlisted or ApplicationStatus.Interview;

    public static bool CanScore(Interview i, DateTime now) => i.Status == InterviewStatus.Scheduled && now >= i.StartsAt;
    public static bool CanClose(InterviewStatus s) => s == InterviewStatus.Scheduled;

    /// <summary>Σ (rating ÷ 5 × weight), rounded to one decimal place: a score out of 100.</summary>
    public static decimal WeightedScore(IEnumerable<(int Rating, int Weight)> ratings) =>
        Math.Round(ratings.Sum(r => (decimal)r.Rating / MaxRating * r.Weight), 1, MidpointRounding.AwayFromZero);
}
