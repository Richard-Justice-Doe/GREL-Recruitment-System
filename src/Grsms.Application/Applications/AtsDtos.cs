using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Applications;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

/// <summary>Phase 15: the ATS settings screen (Super Admin).</summary>
public class AtsSettingsFormDto
{
    [Range(0, 100)] [Display(Name = "Education")] public int EducationWeight { get; set; } = 25;
    [Range(0, 100)] [Display(Name = "Experience")] public int ExperienceWeight { get; set; } = 25;
    [Range(0, 100)] [Display(Name = "Skills")] public int SkillsWeight { get; set; } = 25;
    [Range(0, 100)] [Display(Name = "Certifications")] public int CertificationWeight { get; set; } = 25;

    [Range(1, 100)] [Display(Name = "Highly recommended from")] public int HighlyRecommendedFrom { get; set; } = 90;
    [Range(1, 100)] [Display(Name = "Recommended from")] public int RecommendedFrom { get; set; } = 80;
    [Range(1, 100)] [Display(Name = "Review required from")] public int ReviewRequiredFrom { get; set; } = 70;
    [Range(0, 100)] [Display(Name = "Low match from (below this: Auto reject)")] public int LowMatchFrom { get; set; } = 50;

    [Display(Name = "A mandatory knockout question is answered \"No\"")] public bool RejectOnKnockout { get; set; } = true;
    [Display(Name = "The overall match is in the Auto reject band")] public bool RejectBelowLowMatch { get; set; } = true;
    [Display(Name = "Education is below the vacancy's minimum")] public bool RejectBelowMinimumEducation { get; set; } = true;
    [Display(Name = "Experience is below the vacancy's minimum")] public bool RejectBelowMinimumExperience { get; set; } = true;
    [Display(Name = "A required certification is not found")] public bool RejectMissingRequiredCertification { get; set; } = true;
    [Display(Name = "A required skill is not found")] public bool RejectMissingRequiredSkill { get; set; }

    [Display(Name = "Read CVs with the Claude AI reader (CV text is sent to Anthropic)")] public bool UseAiReader { get; set; }

    // ---- Read-only, filled by the service
    public bool AiConfigured { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>One vacancy on the ATS overview.</summary>
public record AtsVacancySummaryDto(
    int VacancyId, string VacancyNumber, string JobTitle, string DepartmentName, VacancyStatus Status,
    int Applications, int Screened, int HighlyRecommended, int Recommended, int ReviewRequired, int LowMatch, int AutoRejectBand,
    int AwaitingHr, int? TopScore);

/// <summary>One application in a vacancy's ATS ranking.</summary>
public record AtsRowDto(
    int ApplicationId, string ApplicationNumber, string FullName, bool IsInternal, ApplicationStatus Status,
    int? Overall, AtsClassification? Classification, int? Education, int? Experience, int? Skills, int? Certifications,
    bool? MeetsMinimum, IReadOnlyList<string> MissingRequired, IReadOnlyList<string> FailedKnockouts,
    AtsDecision Decision, string? RejectionReason, string? DecisionNote, string CvReader, bool IsOwn, bool CanDecide);

/// <summary>How many applicants have, and lack, one of the vacancy's requirements ("missing skill analysis").</summary>
public record RequirementCoverageDto(string Name, RequirementType Type, bool IsRequired, int Have, int Missing);

/// <summary>Phase 15: a vacancy's ATS dashboard.</summary>
public record AtsBoardDto
{
    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public VacancyStatus VacancyStatus { get; init; }
    public EducationLevel MinimumEducation { get; init; }
    public int MinYearsExperience { get; init; }
    public IReadOnlyList<string> KnockoutQuestions { get; init; } = [];

    /// <summary>Ranked by overall match (highest first), then earliest application.</summary>
    public IReadOnlyList<AtsRowDto> Rows { get; init; } = [];
    public IReadOnlyList<RequirementCoverageDto> Coverage { get; init; } = [];
    public IReadOnlyDictionary<AtsClassification, int> ByBand { get; init; } = new Dictionary<AtsClassification, int>();

    public int AwaitingHr { get; init; }
    public int NotScreened { get; init; }
    public bool IsReviewer { get; init; }

    /// <summary>The settings now in force (for the legend).</summary>
    public AtsSettings Settings { get; init; } = new();
}
