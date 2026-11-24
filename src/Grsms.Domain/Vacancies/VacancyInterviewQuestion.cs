using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Vacancies;

/// <summary>
/// 13 November 2026 (docs/36 step AI-2): one question of a vacancy's interview guide, written by hand or drafted with AI and edited
/// by the Recruitment Officer or HR Manager. Panel members see the guide on the interview page; candidates never do.
/// </summary>
public class VacancyInterviewQuestion : BaseEntity
{
    public int VacancyId { get; set; }
    public InterviewQuestionCategory Category { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>What a good answer shows (for the panel).</summary>
    public string? Guidance { get; set; }

    public int SortOrder { get; set; }

    public const int MaxQuestions = 40, MaxQuestionLength = 1000, MaxGuidanceLength = 1000;
}

public enum InterviewQuestionCategory
{
    [Display(Name = "Technical")] Technical,
    [Display(Name = "Behavioural")] Behavioural,
    [Display(Name = "Situational")] Situational,
    [Display(Name = "Competency")] Competency,
    [Display(Name = "Practical assessment")] Practical
}
