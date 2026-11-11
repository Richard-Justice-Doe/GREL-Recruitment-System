using Grsms.Domain.Common;

namespace Grsms.Domain.Applications;

/// <summary>
/// Phase 15: the ATS settings for the whole system (table "AtsSettings", one row). Only a Super Admin may change them
/// (permission "ats.configure"); every change is in the audit trail. A new installation uses the defaults below until
/// the Super Admin saves the screen for the first time.
/// </summary>
public class AtsSettings : BaseEntity
{
    // ---------------------------------------------------------------- Weights of the overall match (must add up to 100)
    public int EducationWeight { get; set; } = 25;
    public int ExperienceWeight { get; set; } = 25;
    public int SkillsWeight { get; set; } = 25;
    public int CertificationWeight { get; set; } = 25;

    // ---------------------------------------------------------------- Classification bands (lowest overall % of each band)
    public int HighlyRecommendedFrom { get; set; } = 90;
    public int RecommendedFrom { get; set; } = 80;
    public int ReviewRequiredFrom { get; set; } = 70;

    /// <summary>Below this, the band is "Auto reject".</summary>
    public int LowMatchFrom { get; set; } = 50;

    // ---------------------------------------------------------------- Automatic rejection rules
    /// <summary>Reject when a mandatory knockout question is answered "No".</summary>
    public bool RejectOnKnockout { get; set; } = true;

    /// <summary>Reject when the overall match is in the "Auto reject" band (below <see cref="LowMatchFrom"/>).</summary>
    public bool RejectBelowLowMatch { get; set; } = true;

    /// <summary>Reject when the highest education is below the vacancy's minimum.</summary>
    public bool RejectBelowMinimumEducation { get; set; } = true;

    /// <summary>Reject when the years of experience are below the vacancy's minimum.</summary>
    public bool RejectBelowMinimumExperience { get; set; } = true;

    /// <summary>Reject when an essential (required) certification is not found.</summary>
    public bool RejectMissingRequiredCertification { get; set; } = true;

    /// <summary>
    /// Reject when an essential (required) skill is not found. Off by default: people describe the same skill in many
    /// words, so a missing skill is a reason to look, not to reject.
    /// </summary>
    public bool RejectMissingRequiredSkill { get; set; }

    // ---------------------------------------------------------------- CV reading
    /// <summary>
    /// Use the optional Claude AI CV reader (also needs Ats:Ai:ApiKey in the server settings). Off by default: when on,
    /// the text of each CV is sent to Anthropic. Only switch it on once GREL has approved that under its data-protection rules.
    /// </summary>
    public bool UseAiReader { get; set; }

    /// <summary>Checks the numbers make sense. Returns the problems (empty = fine).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        int[] weights = [EducationWeight, ExperienceWeight, SkillsWeight, CertificationWeight];
        if (weights.Any(w => w is < 0 or > 100)) errors.Add("Each weight must be between 0 and 100.");
        if (weights.Sum() != 100) errors.Add($"The four weights must add up to 100 (they add up to {weights.Sum()}).");
        if (!(HighlyRecommendedFrom <= 100 && HighlyRecommendedFrom > RecommendedFrom && RecommendedFrom > ReviewRequiredFrom &&
              ReviewRequiredFrom > LowMatchFrom && LowMatchFrom >= 0))
            errors.Add("The bands must go down in order: Highly recommended > Recommended > Review required > Low match ≥ 0, all at most 100.");
        return errors;
    }
}
