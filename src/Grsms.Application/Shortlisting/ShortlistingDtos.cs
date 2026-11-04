using Grsms.Domain.Applications;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Shortlisting;

/// <summary>One application on a vacancy's shortlisting board.</summary>
public record ShortlistRowDto(
    int ApplicationId,
    string ApplicationNumber,
    string FullName,
    bool IsInternal,
    ApplicationStatus Status,
    int? Score,
    bool? MeetsMinimum,
    IReadOnlyList<string> MissingEssential,
    EducationLevel HighestEducation,
    int YearsExperience,
    ShortlistRecommendation RecruiterRecommendation,
    string? RecruiterNote,
    ShortlistRecommendation HodRecommendation,
    string? HodNote,
    bool IsOwn,
    bool CanRecommend,
    bool CanHodReview,
    bool CanShortlist,
    bool NeedsOverride);

/// <summary>A vacancy's shortlisting board: requirements, ranked applications and what the user may do.</summary>
public record ShortlistBoardDto
{
    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public VacancyStatus VacancyStatus { get; init; }
    public DateOnly ClosingDate { get; init; }
    public int NumberOfPositions { get; init; }
    public EducationLevel MinimumEducation { get; init; }
    public int MinYearsExperience { get; init; }
    public IReadOnlyList<string> EssentialItems { get; init; } = [];
    public IReadOnlyList<string> DesirableItems { get; init; } = [];

    /// <summary>Ranked: meets minimum first, then by score, then earliest application.</summary>
    public IReadOnlyList<ShortlistRowDto> Rows { get; init; } = [];

    public bool IsReviewer { get; init; }
    public bool IsHod { get; init; }
    public bool CanOverride { get; init; }

    /// <summary>Applications with a recruiter recommendation still waiting for the HOD's view.</summary>
    public int AwaitingHod { get; init; }
    public int Shortlisted { get; init; }
}

/// <summary>A vacancy where the current HOD has recommendations waiting for their view (for "My tasks").</summary>
public record HodShortlistTaskDto(int VacancyId, string VacancyNumber, string JobTitle, int Waiting, DateTime Since);
