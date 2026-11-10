using Grsms.Domain.Applications;
using Grsms.Domain.Security;

namespace Grsms.Domain.Interviews;

/// <summary>
/// The rules of interviews and assessments, in one place:
///
///   Shortlisted application ──schedule──► Interview (Scheduled) ──all scorecards in──► Awaiting score validation
///        ──Recruitment Officer validates──► Completed          (or Cancelled / Did not attend while not completed)
///   The application moves to "Interview" at the first scheduled session. Further rounds can be added.
///
///   * Recruitment Officers and HR Managers schedule, reschedule, cancel and record no-shows.
///   * The panel has at least 2 people, one chair, and always the department's Head of Department; the applicant
///     can never sit on their own panel.
///   * Each panel member submits one scorecard (from the start time until the scores are validated).
///     Panel members see the others' scorecards only after submitting their own, so nobody is swayed.
///   * Score validation (9 Nov 2026): when every panel member who took part (not marked Absent or Excused) has a valid
///     scorecard, the session waits for the vacancy's Recruitment Officer (any Recruitment Officer may stand in). They
///     validate the scores, or return one scorecard to its panel member with a reason. They never change scores, and never
///     return their own; one who sat on the panel may still validate. The Head of Department's scorecard is required.
///   * Until validated, the scores are seen only by the panel members who submitted theirs and by Recruitment Officers;
///     the HOD (unless on the panel), MD, HR and the interview results see validated scores only.
/// </summary>
public static class InterviewWorkflow
{
    public static readonly IReadOnlyList<string> SchedulerRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Who validates the scores: Recruitment Officers (the vacancy's own, or another standing in).</summary>
    public const string ValidatorRole = Roles.RecruitmentOfficer;

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

    /// <summary>Open = not completed, cancelled or no-show: scorecards can still change and the session can be closed.</summary>
    public static bool IsOpen(InterviewStatus s) => s is InterviewStatus.Scheduled or InterviewStatus.AwaitingValidation;

    public static bool CanScore(Interview i, DateTime now) => IsOpen(i.Status) && now >= i.StartsAt;
    public static bool CanClose(InterviewStatus s) => IsOpen(s);

    /// <summary>Panel members who must submit before validation: everyone not marked Absent or Excused.</summary>
    public static IEnumerable<InterviewPanelMember> ExpectedScorers(Interview i) =>
        i.Panel.Where(p => p.Attendance is not (PanelAttendance.Absent or PanelAttendance.Excused));

    /// <summary>Panel members still owing a valid scorecard (none submitted, or theirs was returned).</summary>
    public static IReadOnlyList<InterviewPanelMember> MissingScorers(Interview i) =>
        ExpectedScorers(i).Where(p => !i.Scorecards.Any(s => s.PanelUserId == p.UserId && s.IsValid)).ToList();

    /// <summary>True when every expected panel member has a valid scorecard (and there is at least one).</summary>
    public static bool AllScoresIn(Interview i) =>
        i.Scorecards.Any(s => s.IsValid) && MissingScorers(i).Count == 0;

    /// <summary>Moves an open session between Scheduled and Awaiting validation as scorecards come in or are returned.</summary>
    public static void RefreshValidationStatus(Interview i, DateTime now)
    {
        if (!IsOpen(i.Status)) return;
        i.Status = now >= i.StartsAt && AllScoresIn(i) ? InterviewStatus.AwaitingValidation : InterviewStatus.Scheduled;
    }

    /// <summary>The scorecards that count: valid ones of a completed (validated) session.</summary>
    public static IEnumerable<Scorecard> ValidatedScorecards(Interview i) =>
        i.Status == InterviewStatus.Completed ? i.Scorecards.Where(s => s.IsValid) : [];

    /// <summary>Σ (rating ÷ 5 × weight), rounded to one decimal place: a score out of 100.</summary>
    public static decimal WeightedScore(IEnumerable<(int Rating, int Weight)> ratings) =>
        Math.Round(ratings.Sum(r => (decimal)r.Rating / MaxRating * r.Weight), 1, MidpointRounding.AwayFromZero);
}
