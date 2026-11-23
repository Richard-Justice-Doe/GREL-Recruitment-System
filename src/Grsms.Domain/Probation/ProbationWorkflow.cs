namespace Grsms.Domain.Probation;

/// <summary>
/// The rules of probation (Phase 21), in one place:
///
///   Onboarding completed ─► Initial meeting ─(objectives + plan, signed by employee and HOD)─► In progress
///        In progress ─(3 months: first assessment)─► In progress ─(end date: final assessment)─► Awaiting HR decision
///        Awaiting HR decision ─HR Manager approves─► Confirmed (letter filed) | Extended (new end date) | Not confirmed
///                             ─HR Manager returns─► back to the HOD (In progress / Extended)
///        Extended ─(new end date: final assessment again)─► Awaiting HR decision
///
///   * 6 months by default; the initial meeting is due within 7 days of the start; the first assessment at 3 months.
///   * The Head of the employee's department holds the meeting and the assessments; the employee signs the initial meeting.
///   * Extensions add up to at most 3 months in total.
///   * An assessment can be recorded from 14 days before its due date.
///   * Nobody assesses or decides on their own probation.
/// </summary>
public static class ProbationWorkflow
{
    public const int DurationMonths = 6;
    public const int InitialMeetingDays = 7;
    public const int FirstAssessmentMonths = 3;
    public const int MaxExtensionMonths = 3;

    /// <summary>The initial meeting may be held from two weeks before the start (17 Nov 2026).</summary>
    public const int MeetingDaysBeforeStart = 14;

    /// <summary>
    /// 23 Nov 2026: the dates the initial meeting may be set to: from two weeks before the start up to the due date, so the HOD can
    /// SCHEDULE it before the employee starts (the employee signs on or after that day), or up to today when it is held late.
    /// </summary>
    public static (DateOnly Earliest, DateOnly Latest) MeetingDateWindow(DateOnly start, DateOnly due, DateOnly today) =>
        (start.AddDays(-MeetingDaysBeforeStart), today > due ? today : due);

    /// <summary>
    /// 23 Nov 2026: the date GRSMS suggests for the initial meeting: the first working day (Monday to Friday) from the employee's
    /// start, or from today if they have already started, no later than the due date (when overdue: today).
    /// </summary>
    public static DateOnly SuggestedMeetingDate(DateOnly start, DateOnly due, DateOnly today)
    {
        var d = today > start ? today : start;
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        if (d > due) d = today > due ? today : due;
        return d;
    }

    /// <summary>The last due date an objective may have: the end of the probation plus the longest extension.</summary>
    public static DateOnly LatestObjectiveDue(DateOnly end) => end.AddMonths(MaxExtensionMonths);

    /// <summary>How early (days before the due date) an assessment may be recorded.</summary>
    public const int AssessmentWindowDays = 14;

    /// <summary>True while something can still happen.</summary>
    public static bool IsActive(ProbationStatus s) => s is not (ProbationStatus.Confirmed or ProbationStatus.NotConfirmed);

    /// <summary>The HOD can assess (first or final) in these statuses.</summary>
    public static bool CanBeAssessed(ProbationStatus s) => s is ProbationStatus.InProgress or ProbationStatus.Extended;
}
