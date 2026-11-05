using System.ComponentModel.DataAnnotations;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Recruitment;
using Grsms.Domain.Applications;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Vacancies;

namespace Grsms.Application.Applications;

// ===================================================================== Profile

/// <summary>The "My profile" form.</summary>
public class ProfileFormDto
{
    [Required(ErrorMessage = "Enter a phone number we can reach you on.")]
    [Phone, StringLength(30)]
    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    [StringLength(150)]
    [Display(Name = "Town / region where you live")]
    public string? Location { get; set; }

    [Required]
    [Display(Name = "Highest education")]
    public EducationLevel? HighestEducation { get; set; }

    [StringLength(200)]
    [Display(Name = "Field of study")]
    public string? FieldOfStudy { get; set; }

    [Range(0, 60, ErrorMessage = "Enter between 0 and 60 years.")]
    [Display(Name = "Years of work experience")]
    public int YearsExperience { get; set; }

    [StringLength(150)]
    [Display(Name = "Current or last employer")]
    public string? CurrentEmployer { get; set; }

    [StringLength(150)]
    [Display(Name = "Current or last job title")]
    public string? CurrentPosition { get; set; }

    [StringLength(3000)]
    [Display(Name = "Skills (one per line)")]
    public string? Skills { get; set; }

    [StringLength(3000)]
    [Display(Name = "Certifications (one per line)")]
    public string? Certifications { get; set; }

    [Url(ErrorMessage = "Enter a full web address starting with https://"), StringLength(300)]
    [Display(Name = "LinkedIn or portfolio link")]
    public string? LinkedInUrl { get; set; }

    [StringLength(2000)]
    [Display(Name = "About you")]
    public string? Summary { get; set; }

    // ---- Read-only, filled by the service (nullable, so model validation never asks for them)
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<AttachmentDto> Documents { get; set; } = [];

    /// <summary>What is still needed before the person can apply (empty = ready).</summary>
    public IReadOnlyList<string> Missing { get; set; } = [];
}

// ===================================================================== Applying

/// <summary>The apply page: what is sent, plus the few things asked per vacancy.</summary>
public class ApplyFormDto
{
    [Required]
    public string AdvertNumber { get; set; } = "";

    [Required(ErrorMessage = "Tell us where you saw this job.")]
    [Display(Name = "Where did you see this job?")]
    public ApplicationSource? Source { get; set; }

    [StringLength(4000)]
    [Display(Name = "Covering message (optional)")]
    public string? CoverLetter { get; set; }

    [Display(Name = "I confirm that the information in my profile and documents is true and complete")]
    public bool Confirm { get; set; }

    // ---- Read-only, filled by the service (nullable, so model validation never asks for them)
    public string? JobTitle { get; set; }
    public string? DepartmentName { get; set; }
    public string? VacancyNumber { get; set; }
    public DateOnly ClosingDate { get; set; }
    public ProfileFormDto? Profile { get; set; }

    /// <summary>Phase 15: the vacancy's knockout questions (id, text), shown with Yes / No buttons.</summary>
    public IReadOnlyList<(int Id, string Text)> Questions { get; set; } = [];

    /// <summary>Phase 15: the answers, by question id (true = Yes). Every question must be answered.</summary>
    public Dictionary<int, bool?> Answers { get; set; } = [];
}

// ===================================================================== The applicant's own view

/// <summary>One row of "My applications".</summary>
public record MyApplicationDto(int Id, string ApplicationNumber, string JobTitle, string DepartmentName, ApplicationStatus Status,
    DateTime SubmittedAt, DateOnly ClosingDate);

/// <summary>One of my applications, with its progress tracker. Staff-only comments are never included.</summary>
public record MyApplicationDetailsDto
{
    public int Id { get; init; }
    public string ApplicationNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string SiteName { get; init; } = "";
    public DateOnly ClosingDate { get; init; }
    public ApplicationStatus Status { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime StatusChangedAt { get; init; }
    public string? CoverLetter { get; init; }

    /// <summary>The furthest tracker stage the application reached (for ended applications: where it stopped).</summary>
    public ApplicationStatus LastStageReached { get; init; }
    public ApplicationSource Source { get; init; }
    public IReadOnlyList<AttachmentDto> Documents { get; init; } = [];

    /// <summary>The applicant-friendly history: what happened and when (no internal comments).</summary>
    public IReadOnlyList<(string What, DateTime When)> Timeline { get; init; } = [];

    public bool CanWithdraw { get; init; }
}

// ===================================================================== Staff view

/// <summary>Filters on the Applications list (staff). Every filter is optional.</summary>
public class ApplicationFilter
{
    /// <summary>"new" (received, not yet reviewed), "review" (under review) or "all".</summary>
    public string Tab { get; set; } = "new";
    public ApplicationStatus? Status { get; set; }
    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? VacancyId { get; set; }
    public ApplicationSource? Source { get; set; }

    /// <summary>True = GREL staff only, false = external only.</summary>
    public bool? Internal { get; set; }

    /// <summary>Recruitment period: applications submitted from / to these days.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Part of the application number, applicant name or email, or vacancy number.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>One row on the Applications list.</summary>
public record ApplicationListItemDto(
    int Id,
    string ApplicationNumber,
    string FullName,
    int VacancyId,
    string VacancyNumber,
    string JobTitle,
    string DivisionName,
    string DepartmentName,
    bool IsInternal,
    ApplicationSource Source,
    EducationLevel HighestEducation,
    int YearsExperience,
    ApplicationStatus Status,
    DateTime SubmittedAt);

public record ApplicationHistoryDto(ApplicationAction Action, ApplicationStatus FromStatus, ApplicationStatus ToStatus,
    string ActorName, string? Comments, DateTime ActionAt);

/// <summary>Everything on the staff application page, plus what the current user may do.</summary>
public record ApplicationDetailsDto
{
    public int Id { get; init; }
    public string ApplicationNumber { get; init; } = "";
    public ApplicationStatus Status { get; init; }
    public DateTime SubmittedAt { get; init; }
    public bool IsInternal { get; init; }
    public ApplicationSource Source { get; init; }
    public string? AdvertNumber { get; init; }
    public int? AdvertId { get; init; }

    public int VacancyId { get; init; }
    public string VacancyNumber { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string DivisionName { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public string RequestingHodName { get; init; } = "";

    // Snapshot of the applicant
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string? Location { get; init; }
    public EducationLevel HighestEducation { get; init; }
    public string? FieldOfStudy { get; init; }
    public int YearsExperience { get; init; }
    public string? CurrentEmployer { get; init; }
    public string? CurrentPosition { get; init; }
    public IReadOnlyList<string> Skills { get; init; } = [];
    public IReadOnlyList<string> Certifications { get; init; } = [];
    public string? LinkedInUrl { get; init; }
    public string? CoverLetter { get; init; }
    public string? OutcomeReason { get; init; }

    public IReadOnlyList<AttachmentDto> Documents { get; init; } = [];
    public IReadOnlyList<ApplicationHistoryDto> History { get; init; } = [];

    // Phase 6: shortlisting; Phase 15: the ATS result, what was read from the CV, knockout answers and the HR decision
    public AtsResult? Screening { get; init; }
    public ParsedCv? Cv { get; init; }
    public IReadOnlyList<KnockoutAnswerInput> KnockoutAnswers { get; init; } = [];
    public AtsDecision AtsDecision { get; init; }
    public string? AtsRejectionReason { get; init; }
    public string? AtsDecisionNote { get; init; }
    public string? AtsDecisionByName { get; init; }
    public DateTime? AtsDecisionAt { get; init; }
    public bool CanDecideAts { get; init; }
    public ShortlistRecommendation RecruiterRecommendation { get; init; }
    public string? RecruiterNote { get; init; }
    public ShortlistRecommendation HodRecommendation { get; init; }
    public string? HodNote { get; init; }

    public bool CanStartReview { get; init; }
    public bool CanMarkNotSuccessful { get; init; }

    /// <summary>Why the user cannot act (e.g. it is their own application), if so.</summary>
    public string? ActionBlockedReason { get; init; }
}

/// <summary>Counts per tab on the Applications list.</summary>
public record ApplicationCounts(int New, int UnderReview, int Total, int AutoRejected = 0);

/// <summary>Data access for applications beyond the generic repository.</summary>
public interface IApplicationRepository : IRepository<JobApplication>
{
    Task<JobApplication?> GetWithHistoryAsync(int id, CancellationToken ct = default);

    /// <summary>The staff list, limited to what the user may see.</summary>
    Task<PagedResult<ApplicationListItemDto>> SearchAsync(ApplicationFilter filter, RequestVisibility visibility, CancellationToken ct = default);

    Task<ApplicationCounts> CountAsync(RequestVisibility visibility, CancellationToken ct = default);

    /// <summary>The applicant's own applications, newest first.</summary>
    Task<IReadOnlyList<MyApplicationDto>> ListForApplicantAsync(string userId, CancellationToken ct = default);
}
