using Grsms.Domain.Common;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>
/// One person's application for one vacancy (Module 5). See <see cref="ApplicationWorkflow"/> for the rules.
///
/// The applicant's details are copied from their profile at the moment of applying (a "snapshot"), and their CV and
/// certificates are copied as attachments of the application, so the record shows exactly what was sent.
/// Like every recruitment record, it stores the division, department and requesting Head of Department of its vacancy.
/// </summary>
public class JobApplication : BaseEntity
{
    /// <summary>Reference, e.g. "APP-2026-00001". Unique.</summary>
    public string ApplicationNumber { get; set; } = string.Empty;

    public int VacancyId { get; set; }

    /// <summary>The advert the person applied through (JobAdverts.Id).</summary>
    public int? JobAdvertId { get; set; }

    /// <summary>The applicant (Users.Id). One application per person per vacancy.</summary>
    public string ApplicantUserId { get; set; } = string.Empty;

    /// <summary>True when the applicant is GREL staff (an internal applicant).</summary>
    public bool IsInternal { get; set; }

    public ApplicationSource Source { get; set; }

    // ---------------------------------------------------------------- Links copied from the vacancy
    public int DivisionId { get; set; }
    public int DepartmentId { get; set; }
    public string RequestingHodUserId { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Snapshot of the applicant when they applied
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Location { get; set; }
    public EducationLevel HighestEducation { get; set; }
    public string? FieldOfStudy { get; set; }
    public int YearsExperience { get; set; }
    public string? CurrentEmployer { get; set; }
    public string? CurrentPosition { get; set; }
    public string? Skills { get; set; }
    public string? Certifications { get; set; }
    public string? LinkedInUrl { get; set; }

    /// <summary>The applicant's covering message for this vacancy.</summary>
    public string? CoverLetter { get; set; }

    // ---------------------------------------------------------------- State
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime SubmittedAt { get; set; }
    public DateTime StatusChangedAt { get; set; }

    /// <summary>Internal reason for "Not successful" (not shown to the applicant).</summary>
    public string? OutcomeReason { get; set; }

    // ---------------------------------------------------------------- Phase 6: screening
    /// <summary>Match score 0-100 against the vacancy's requirements (see <see cref="CvScreening"/>). Null = not screened yet.</summary>
    public int? ScreeningScore { get; set; }

    /// <summary>True when the applicant meets the minimum education and experience and every essential requirement.</summary>
    public bool? MeetsMinimum { get; set; }

    /// <summary>The score breakdown (JSON of <see cref="ScreeningResult"/>).</summary>
    public string? ScreeningDetails { get; set; }

    public DateTime? ScreenedAt { get; set; }

    // ---------------------------------------------------------------- Phase 6: shortlisting
    public ShortlistRecommendation RecruiterRecommendation { get; set; }
    public string? RecruiterNote { get; set; }
    public string? RecruiterRecommendedByUserId { get; set; }
    public DateTime? RecruiterRecommendedAt { get; set; }

    /// <summary>The department Head of Department's view on the recruiter's recommendation.</summary>
    public ShortlistRecommendation HodRecommendation { get; set; }
    public string? HodNote { get; set; }
    public string? HodRecommendedByUserId { get; set; }
    public DateTime? HodRecommendedAt { get; set; }

    public DateTime? ShortlistedAt { get; set; }

    /// <summary>Concurrency stamp; changes on every step.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<ApplicationActionEntry> Actions { get; set; } = new List<ApplicationActionEntry>();

    /// <summary>Stores a screening result (score, minimum check and the JSON breakdown).</summary>
    public void ApplyScreening(ScreeningResult result, DateTime now)
    {
        ScreeningScore = result.Score;
        MeetsMinimum = result.MeetsMinimum;
        ScreeningDetails = System.Text.Json.JsonSerializer.Serialize(result);
        ScreenedAt = now;
    }

    /// <summary>The stored screening breakdown, or null if not screened.</summary>
    public ScreeningResult? ReadScreening() =>
        ScreeningDetails is null ? null : System.Text.Json.JsonSerializer.Deserialize<ScreeningResult>(ScreeningDetails);

    /// <summary>Moves to <paramref name="newStatus"/> and records the step in the history.</summary>
    public void RecordStep(ApplicationAction action, ApplicationStatus newStatus, string? actorUserId, string actorName, string? comments, DateTime now)
    {
        Actions.Add(new ApplicationActionEntry
        {
            Action = action,
            FromStatus = Status,
            ToStatus = newStatus,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Comments = comments is { Length: > 2000 } ? comments[..1997] + "..." : comments, // the history column holds 2,000 characters
            ActionAt = now
        });
        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>One line of an application's history. Never edited.</summary>
public class ApplicationActionEntry : BaseEntity
{
    public int JobApplicationId { get; set; }
    public ApplicationAction Action { get; set; }
    public ApplicationStatus FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }

    /// <summary>Who acted (Users.Id), or null for the system.</summary>
    public string? ActorUserId { get; set; }

    public string ActorName { get; set; } = string.Empty;

    /// <summary>Staff-only comments (e.g. the reason for "Not successful"). Never shown to the applicant.</summary>
    public string? Comments { get; set; }

    public DateTime ActionAt { get; set; }
}
