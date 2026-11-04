using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Applications;
using Grsms.Domain.Common;

namespace Grsms.Domain.Surveys;

// Module 10: feedback surveys.

public enum SurveyType
{
    /// <summary>Every applicant whose application has ended (not successful, offer declined, hired).</summary>
    [Display(Name = "Candidate experience")] CandidateExperience,

    /// <summary>New employees, after their onboarding is completed.</summary>
    [Display(Name = "New employee onboarding")] NewEmployee
}

/// <summary>
/// An invitation to answer one survey, about one application. Stores the division, department and requesting HOD of
/// the vacancy, like every recruitment record, so results can be filtered the same way.
/// </summary>
public class SurveyInvitation : BaseEntity
{
    public SurveyType Type { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int JobApplicationId { get; set; }
    public int? OnboardingRecordId { get; set; }
    public int VacancyId { get; set; }
    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    /// <summary>How the application ended when the invitation was sent (for comparing results).</summary>
    public ApplicationStatus Outcome { get; set; }

    public DateTime SentAt { get; set; }

    /// <summary>Last day to answer (inclusive).</summary>
    public DateOnly ExpiresOn { get; set; }

    public DateTime? CompletedAt { get; set; }
}

/// <summary>One answer. Confidential: never in the audit trail, and results never show who answered.</summary>
public class SurveyAnswer : BaseEntity, INotAudited
{
    public int SurveyInvitationId { get; set; }
    public string QuestionKey { get; set; } = string.Empty;

    /// <summary>1 (poor) to 5 (excellent); null = not applicable / not answered.</summary>
    public int? Rating { get; set; }

    public string? Text { get; set; }
}

/// <summary>One question. Rating questions are answered 1-5 (or "not applicable"); text questions are free text.</summary>
public record SurveyQuestion(string Key, string Text, bool IsRating, bool Required = false);

/// <summary>The questions of each survey. Keys are stored with the answers, so never change a key; add new ones instead.</summary>
public static class SurveyQuestions
{
    public static readonly IReadOnlyList<SurveyQuestion> CandidateExperience =
    [
        new("clarity", "The job advert and how to apply were clear.", true),
        new("communication", "We kept you informed about the progress of your application.", true),
        new("interview", "The interview or assessment was fair and well organised (leave blank if you were not interviewed).", true),
        new("time", "The process took a reasonable amount of time.", true),
        new("overall", "Overall, how would you rate your experience of applying to GREL?", true, Required: true),
        new("comments", "What could we do better?", false)
    ];

    public static readonly IReadOnlyList<SurveyQuestion> NewEmployee =
    [
        new("letter", "Your appointment letter and first-day information were clear.", true),
        new("welcome", "You were expected and welcomed on your first day.", true),
        new("equipment", "Your workspace, tools and system access were ready.", true),
        new("induction", "The induction helped you understand GREL and your role.", true),
        new("overall", "Overall, how would you rate your start at GREL?", true, Required: true),
        new("comments", "What could we do better for the next new employee?", false)
    ];

    public static IReadOnlyList<SurveyQuestion> For(SurveyType type) =>
        type == SurveyType.CandidateExperience ? CandidateExperience : NewEmployee;

    /// <summary>Applications ending in these statuses are invited to the candidate experience survey.</summary>
    public static readonly IReadOnlyList<ApplicationStatus> FinalOutcomes =
        [ApplicationStatus.NotSuccessful, ApplicationStatus.OfferDeclined, ApplicationStatus.Hired];

    /// <summary>Results (averages and comments) are shown only when at least this many responses match the filters.</summary>
    public const int MinimumResponsesToShow = 3;
}
