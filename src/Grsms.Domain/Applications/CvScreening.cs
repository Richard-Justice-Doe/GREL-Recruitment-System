using System.Text.RegularExpressions;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>The outcome of screening one application against its vacancy (stored as JSON on the application).</summary>
public record ScreeningResult(
    int Score,
    bool MeetsMinimum,
    int EducationPoints,
    int ExperiencePoints,
    int EssentialPoints,
    int DesirablePoints,
    bool EducationMet,
    bool ExperienceMet,
    IReadOnlyList<string> MatchedEssential,
    IReadOnlyList<string> MissingEssential,
    IReadOnlyList<string> MatchedDesirable,
    IReadOnlyList<string> MissingDesirable);

/// <summary>
/// Rule-based CV screening (Phase 6): scores an application's structured details against the vacancy's structured
/// requirements. It is transparent on purpose: every point can be explained to a panel or an applicant.
///
///   Education         20 points: meets the minimum level = 20; one level below = 10; lower = 0
///   Experience        20 points: in proportion to the minimum years (full marks at or above the minimum)
///   Essential items   45 points: share of essential skills and certifications the applicant lists
///   Desirable items   15 points: share of desirable skills and certifications the applicant lists
///   (A section with nothing required gives full marks.)
///
/// "Meets minimum" = education and experience minimums met and EVERY essential item found.
///
/// A requirement counts as found when all its words appear in the applicant's skills, certifications, field of study
/// or job title (case and punctuation ignored), e.g. "NEBOSH IGC" is found in "NEBOSH International General Certificate (IGC)".
/// The text inside CV files is not read: screening uses what the applicant entered in their profile.
/// </summary>
public static partial class CvScreening
{
    public const int EducationWeight = 20, ExperienceWeight = 20, EssentialWeight = 45, DesirableWeight = 15;

    public static ScreeningResult Score(Vacancy v, JobApplication a)
    {
        var haystack = Words(string.Join(" ", a.Skills, a.Certifications, a.FieldOfStudy, a.CurrentPosition));

        // Education: the enum is ordered from lowest to highest, so levels can be compared.
        var educationMet = a.HighestEducation >= v.MinimumEducation;
        var educationPoints = educationMet ? EducationWeight
            : (int)a.HighestEducation == (int)v.MinimumEducation - 1 ? EducationWeight / 2 : 0;

        var experienceMet = a.YearsExperience >= v.MinYearsExperience;
        var experiencePoints = v.MinYearsExperience == 0 || experienceMet
            ? ExperienceWeight
            : (int)Math.Round(ExperienceWeight * (double)a.YearsExperience / v.MinYearsExperience, MidpointRounding.AwayFromZero);

        var essential = v.Requirements.Where(r => r.IsEssential).Select(r => r.Name).ToList();
        var desirable = v.Requirements.Where(r => !r.IsEssential).Select(r => r.Name).ToList();
        var (matchedEssential, missingEssential) = Split(essential, haystack);
        var (matchedDesirable, missingDesirable) = Split(desirable, haystack);

        var essentialPoints = Share(EssentialWeight, matchedEssential.Count, essential.Count);
        var desirablePoints = Share(DesirableWeight, matchedDesirable.Count, desirable.Count);

        return new ScreeningResult(
            educationPoints + experiencePoints + essentialPoints + desirablePoints,
            educationMet && experienceMet && missingEssential.Count == 0,
            educationPoints, experiencePoints, essentialPoints, desirablePoints,
            educationMet, experienceMet,
            matchedEssential, missingEssential, matchedDesirable, missingDesirable);
    }

    /// <summary>True when every word of <paramref name="requirement"/> appears among the applicant's words.</summary>
    public static bool IsFound(string requirement, IReadOnlySet<string> applicantWords)
    {
        var words = Words(requirement);
        return words.Count > 0 && words.All(applicantWords.Contains);
    }

    private static (List<string> Matched, List<string> Missing) Split(List<string> items, IReadOnlySet<string> haystack)
    {
        var matched = items.Where(i => IsFound(i, haystack)).ToList();
        return (matched, items.Except(matched).ToList());
    }

    private static int Share(int weight, int matched, int total) =>
        total == 0 ? weight : (int)Math.Round(weight * (double)matched / total, MidpointRounding.AwayFromZero); // halves round up, e.g. 22.5 → 23

    /// <summary>Lower-case words of letters and digits, e.g. "NEBOSH (IGC)" → {"nebosh", "igc"}. Common filler words are ignored.</summary>
    private static HashSet<string> Words(string? text) =>
        WordRegex().Matches((text ?? "").ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => !Ignored.Contains(w))
            .ToHashSet();

    private static readonly HashSet<string> Ignored = ["and", "or", "of", "in", "the", "a", "an", "for", "with", "to", "on"];

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
