using System.Text.RegularExpressions;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>Phase 15: the outcome of screening one application (stored as JSON on the application, ScreeningDetails).</summary>
public record AtsResult
{
    /// <summary>2 = the Phase 15 ATS. Older (Phase 6) results have no version and are ignored; re-run screening to replace them.</summary>
    public int Version { get; init; } = 2;

    // ---- Scores, each 0-100 %
    public int Overall { get; init; }
    public AtsClassification Classification { get; init; }
    public int Education { get; init; }
    public int Experience { get; init; }
    public int Skills { get; init; }
    public int Certifications { get; init; }

    /// <summary>The weights used, so the breakdown can be explained later even if the Super Admin changes them.</summary>
    public int EducationWeight { get; init; }
    public int ExperienceWeight { get; init; }
    public int SkillsWeight { get; init; }
    public int CertificationWeight { get; init; }

    // ---- What was compared
    public EducationLevel MinimumEducation { get; init; }
    public EducationLevel EducationFound { get; init; }
    public bool EducationMet { get; init; }
    public bool? FieldOfStudyMatched { get; init; }
    public int MinYearsExperience { get; init; }
    public int YearsFound { get; init; }
    public bool ExperienceMet { get; init; }

    /// <summary>Years worked out from the CV's employment history, when it differs from what the applicant typed.</summary>
    public int? CvYears { get; init; }

    public IReadOnlyList<string> MatchedRequiredSkills { get; init; } = [];
    public IReadOnlyList<string> MissingRequiredSkills { get; init; } = [];
    public IReadOnlyList<string> MatchedPreferredSkills { get; init; } = [];
    public IReadOnlyList<string> MissingPreferredSkills { get; init; } = [];
    public IReadOnlyList<string> MatchedRequiredCertifications { get; init; } = [];
    public IReadOnlyList<string> MissingRequiredCertifications { get; init; } = [];
    public IReadOnlyList<string> MatchedPreferredCertifications { get; init; } = [];
    public IReadOnlyList<string> MissingPreferredCertifications { get; init; } = [];

    /// <summary>Knockout questions answered "No".</summary>
    public IReadOnlyList<string> FailedKnockouts { get; init; } = [];

    /// <summary>Why the ATS rejects it (empty = not rejected). Only rules the Super Admin has switched on count.</summary>
    public IReadOnlyList<string> RejectionReasons { get; init; } = [];

    public bool AutoReject => RejectionReasons.Count > 0;

    /// <summary>Every requirement met: minimum education and experience, every required item, every knockout passed.</summary>
    public bool MeetsMinimum => EducationMet && ExperienceMet && MissingRequiredSkills.Count == 0 &&
                                MissingRequiredCertifications.Count == 0 && FailedKnockouts.Count == 0;

    public IReadOnlyList<string> AllMissingRequired => [.. MissingRequiredCertifications, .. MissingRequiredSkills];
}

/// <summary>One knockout question and the applicant's answer.</summary>
public record KnockoutAnswerInput(string Question, bool Answer);

/// <summary>
/// Phase 15: the ATS scoring engine (replaces the Phase 6 points score). Transparent on purpose: every percentage can be
/// explained to a panel or an applicant.
///
///   Education      100 % at or above the vacancy's minimum level; 60 % one level below; 30 % two below; else 0.
///                  If the vacancy names fields of study and none is found, 20 points off.
///   Experience     years ÷ minimum years (100 % at or above the minimum; 100 % when no minimum).
///   Skills         required skills count double, preferred once: (2 × required found + preferred found) ÷
///                  (2 × required + preferred). 100 % when the vacancy lists none.
///   Certifications the same, for certifications.
///   Overall        the weighted average of the four (weights set by the Super Admin, 25 % each by default),
///                  e.g. 90, 100, 75 and 80 → 86 %.
///   Band           Highly recommended / Recommended / Review required / Low match / Auto reject, by the overall %.
///
/// WHAT IS COMPARED: the profile the applicant typed (education level, years, skills, certifications, field of study,
/// job title) PLUS the text of their CV. The CV can only add matches, never take them away: a requirement counts as
/// found when all its words appear in the profile or the CV (case and punctuation ignored), e.g. "NEBOSH IGC" is found
/// in "NEBOSH International General Certificate (IGC)". Education uses the higher of the profile and the CV's level;
/// experience uses the profile's years (the CV's own figure is shown beside it when it differs).
///
/// Automatic rejection only applies the rules the Super Admin switched on (see <see cref="AtsSettings"/>).
/// </summary>
public static partial class AtsEngine
{
    public static AtsResult Score(Vacancy v, JobApplication a, ParsedCv? cv, string? cvText,
        IReadOnlyList<KnockoutAnswerInput> knockouts, AtsSettings settings)
    {
        var applicantWords = Words(string.Join(" ", a.Skills, a.Certifications, a.FieldOfStudy, a.CurrentPosition, cvText,
            string.Join(" ", cv?.Skills ?? []), string.Join(" ", cv?.Certifications ?? [])));

        // ---- Education
        var level = cv is not null && cv.HighestEducation > a.HighestEducation ? cv.HighestEducation : a.HighestEducation;
        var gap = (int)v.MinimumEducation - (int)level;
        var education = gap <= 0 ? 100 : gap == 1 ? 60 : gap == 2 ? 30 : 0;
        bool? fieldMatched = null;
        var fields = FieldsOfStudy(v.FieldOfStudy);
        if (fields.Count > 0)
        {
            var fieldWords = Words(string.Join(" ", a.FieldOfStudy, cvText, string.Join(" ", cv?.Education ?? [])));
            fieldMatched = fields.Any(f => IsFound(f, fieldWords));
            if (fieldMatched == false) education = Math.Max(0, education - 20);
        }

        // ---- Experience
        var years = a.YearsExperience;
        var experience = v.MinYearsExperience <= 0 || years >= v.MinYearsExperience
            ? 100
            : Pct(years, v.MinYearsExperience);

        // ---- Skills and certifications
        var (reqSkillsHit, reqSkillsMiss) = Split(v, RequirementType.Skill, true, applicantWords);
        var (prefSkillsHit, prefSkillsMiss) = Split(v, RequirementType.Skill, false, applicantWords);
        var (reqCertsHit, reqCertsMiss) = Split(v, RequirementType.Certification, true, applicantWords);
        var (prefCertsHit, prefCertsMiss) = Split(v, RequirementType.Certification, false, applicantWords);
        var skills = DoubleWeighted(reqSkillsHit.Count, reqSkillsMiss.Count, prefSkillsHit.Count, prefSkillsMiss.Count);
        var certifications = DoubleWeighted(reqCertsHit.Count, reqCertsMiss.Count, prefCertsHit.Count, prefCertsMiss.Count);

        // ---- Overall and band
        var overall = (int)Math.Round((education * settings.EducationWeight + experience * settings.ExperienceWeight +
                                       skills * settings.SkillsWeight + certifications * settings.CertificationWeight) / 100.0,
                                      MidpointRounding.AwayFromZero);
        var band = Classify(overall, settings);

        // ---- Automatic rejection (only the rules that are switched on)
        var failedKnockouts = knockouts.Where(k => !k.Answer).Select(k => k.Question).ToList();
        var reasons = new List<string>();
        if (settings.RejectOnKnockout && failedKnockouts.Count > 0)
            reasons.Add($"Answered \"No\" to a mandatory question: {string.Join("; ", failedKnockouts)}");
        if (settings.RejectBelowMinimumEducation && gap > 0)
            reasons.Add($"Education below the minimum ({Label(level)}; the vacancy needs at least {Label(v.MinimumEducation)})");
        if (settings.RejectBelowMinimumExperience && v.MinYearsExperience > 0 && years < v.MinYearsExperience)
            reasons.Add($"{years} year(s) of experience; the vacancy needs at least {v.MinYearsExperience}");
        if (settings.RejectMissingRequiredCertification && reqCertsMiss.Count > 0)
            reasons.Add($"Required certification(s) not found: {string.Join(", ", reqCertsMiss)}");
        if (settings.RejectMissingRequiredSkill && reqSkillsMiss.Count > 0)
            reasons.Add($"Required skill(s) not found: {string.Join(", ", reqSkillsMiss)}");
        if (settings.RejectBelowLowMatch && band == AtsClassification.AutoReject)
            reasons.Add($"Overall match {overall}% is below {settings.LowMatchFrom}%");

        return new AtsResult
        {
            Overall = overall,
            Classification = band,
            Education = education,
            Experience = experience,
            Skills = skills,
            Certifications = certifications,
            EducationWeight = settings.EducationWeight,
            ExperienceWeight = settings.ExperienceWeight,
            SkillsWeight = settings.SkillsWeight,
            CertificationWeight = settings.CertificationWeight,
            MinimumEducation = v.MinimumEducation,
            EducationFound = level,
            EducationMet = gap <= 0,
            FieldOfStudyMatched = fieldMatched,
            MinYearsExperience = v.MinYearsExperience,
            YearsFound = years,
            ExperienceMet = years >= v.MinYearsExperience,
            CvYears = cv?.YearsExperience is int cy && cy != years ? cy : null,
            MatchedRequiredSkills = reqSkillsHit,
            MissingRequiredSkills = reqSkillsMiss,
            MatchedPreferredSkills = prefSkillsHit,
            MissingPreferredSkills = prefSkillsMiss,
            MatchedRequiredCertifications = reqCertsHit,
            MissingRequiredCertifications = reqCertsMiss,
            MatchedPreferredCertifications = prefCertsHit,
            MissingPreferredCertifications = prefCertsMiss,
            FailedKnockouts = failedKnockouts,
            RejectionReasons = reasons
        };
    }

    /// <summary>The band an overall percentage falls in.</summary>
    public static AtsClassification Classify(int overall, AtsSettings s) =>
        overall >= s.HighlyRecommendedFrom ? AtsClassification.HighlyRecommended
        : overall >= s.RecommendedFrom ? AtsClassification.Recommended
        : overall >= s.ReviewRequiredFrom ? AtsClassification.ReviewRequired
        : overall >= s.LowMatchFrom ? AtsClassification.LowMatch
        : AtsClassification.AutoReject;

    /// <summary>True when every word of <paramref name="requirement"/> appears among the applicant's words.</summary>
    public static bool IsFound(string requirement, IReadOnlySet<string> applicantWords)
    {
        var words = Words(requirement);
        return words.Count > 0 && words.All(applicantWords.Contains);
    }

    private static (List<string> Hit, List<string> Miss) Split(Vacancy v, RequirementType type, bool essential, IReadOnlySet<string> words)
    {
        var items = v.Requirements.Where(r => r.Type == type && r.IsEssential == essential).OrderBy(r => r.Id).Select(r => r.Name).ToList();
        var hit = items.Where(i => IsFound(i, words)).ToList();
        return (hit, items.Except(hit).ToList());
    }

    private static int DoubleWeighted(int reqHit, int reqMiss, int prefHit, int prefMiss)
    {
        var total = 2 * (reqHit + reqMiss) + prefHit + prefMiss;
        return total == 0 ? 100 : Pct(2 * reqHit + prefHit, total);
    }

    private static int Pct(int part, int whole) =>
        whole <= 0 ? 100 : Math.Min(100, (int)Math.Round(100.0 * part / whole, MidpointRounding.AwayFromZero));

    /// <summary>"Occupational Health and Safety, Engineering or related field" → ["Occupational Health and Safety", "Engineering"].</summary>
    private static List<string> FieldsOfStudy(string? text) =>
        FieldSplit().Split(text ?? "")
            .Select(f => f.Trim())
            .Where(f => f.Length > 1 && !f.Contains("related", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains("similar", StringComparison.OrdinalIgnoreCase) && !f.Contains("equivalent", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static string Label(EducationLevel l) => l switch
    {
        EducationLevel.None => "none stated",
        EducationLevel.Hnd => "HND",
        _ => System.Text.RegularExpressions.Regex.Replace(l.ToString(), "(?<=[a-z])(?=[A-Z])", " ")
    };

    /// <summary>Lower-case words of letters and digits, e.g. "NEBOSH (IGC)" → {"nebosh", "igc"}. Common filler words are ignored.</summary>
    private static HashSet<string> Words(string? text) =>
        WordRegex().Matches((text ?? "").ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => !Ignored.Contains(w))
            .ToHashSet();

    private static readonly HashSet<string> Ignored = ["and", "or", "of", "in", "the", "a", "an", "for", "with", "to", "on"];

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@",|;|/|\bor\b|\band/or\b", RegexOptions.IgnoreCase)]
    private static partial Regex FieldSplit();
}
