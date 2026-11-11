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

    /// <summary>How early (days before the due date) an assessment may be recorded.</summary>
    public const int AssessmentWindowDays = 14;

    /// <summary>True while something can still happen.</summary>
    public static bool IsActive(ProbationStatus s) => s is not (ProbationStatus.Confirmed or ProbationStatus.NotConfirmed);

    /// <summary>The HOD can assess (first or final) in these statuses.</summary>
    public static bool CanBeAssessed(ProbationStatus s) => s is ProbationStatus.InProgress or ProbationStatus.Extended;
}
