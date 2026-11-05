using Grsms.Domain.Common;
using Grsms.Domain.Recruitment;

namespace Grsms.Domain.Vacancies;

/// <summary>
/// An open position that candidates can apply for (Module 3).
/// Every vacancy comes from exactly one APPROVED recruitment request, so nothing is advertised without
/// the HOD → Recruitment → HR → MD approval chain. See <see cref="VacancyWorkflow"/> for its life cycle.
///
/// Requirements are stored in a structured way (education level, years, skill and certification lists)
/// rather than only as free text, so the CV screening module (Phase 6) can score candidates against them.
/// </summary>
public class Vacancy : BaseEntity
{
    /// <summary>Reference, e.g. "VAC-2026-0001". Unique.</summary>
    public string VacancyNumber { get; set; } = string.Empty;

    /// <summary>The approved request this vacancy fills (RecruitmentRequests.Id). One vacancy per request.</summary>
    public int RecruitmentRequestId { get; set; }

    // ---------------------------------------------------------------- The job

    public string JobTitle { get; set; } = string.Empty;
    /// <summary>Division (Divisions.Id), copied from the request.</summary>
    public int DivisionId { get; set; }

    public int DepartmentId { get; set; }
    public int? UnitId { get; set; }
    public int SiteId { get; set; }

    /// <summary>The Head of Department who raised the request (Users.Id), copied from the request.</summary>
    public string RequestingHodUserId { get; set; } = string.Empty;

    /// <summary>The person the new hire will report to (Users.Id). Defaults to the department's HOD.</summary>
    public string? ReportingManagerUserId { get; set; }

    public EmploymentType EmploymentType { get; set; }
    public int NumberOfPositions { get; set; } = 1;
    public VacancyType VacancyType { get; set; } = VacancyType.InternalAndExternal;

    /// <summary>Full job description shown to applicants: purpose, duties, responsibilities.</summary>
    public string JobDescription { get; set; } = string.Empty;

    // ---------------------------------------------------------------- Requirements

    public EducationLevel MinimumEducation { get; set; }

    /// <summary>e.g. "Occupational Health and Safety, Engineering or related field".</summary>
    public string? FieldOfStudy { get; set; }

    /// <summary>Any other qualification wording for the advert.</summary>
    public string? QualificationNotes { get; set; }

    public int MinYearsExperience { get; set; }

    /// <summary>What kind of experience, e.g. "in a manufacturing or plantation environment".</summary>
    public string? ExperienceNotes { get; set; }

    /// <summary>Skills and certifications, each marked essential or desirable.</summary>
    public ICollection<VacancyRequirement> Requirements { get; set; } = new List<VacancyRequirement>();

    /// <summary>
    /// Phase 15: mandatory knockout questions, e.g. "Do you have a valid driver's licence?". Applicants must answer each
    /// "Yes"; a "No" makes the ATS reject the application (held for HR to confirm). Replaced as a set on every edit.
    /// </summary>
    public ICollection<VacancyKnockoutQuestion> KnockoutQuestions { get; set; } = new List<VacancyKnockoutQuestion>();

    // ---------------------------------------------------------------- Salary and dates

    /// <summary>Monthly salary range in GHS. Both optional, but if both are set, min ≤ max.</summary>
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }

    /// <summary>Whether the salary range may be shown on adverts.</summary>
    public bool ShowSalary { get; set; }

    /// <summary>Last day applications are accepted (inclusive). After it passes, the vacancy closes automatically.</summary>
    public DateOnly ClosingDate { get; set; }

    // ---------------------------------------------------------------- Workflow state

    /// <summary>Recruitment Officer responsible for this vacancy (Users.Id). Copied from the request's assigned recruiter.</summary>
    public string RecruiterUserId { get; set; } = string.Empty;

    public VacancyStatus Status { get; set; } = VacancyStatus.Draft;
    public DateTime StatusChangedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>Concurrency stamp; changes on every step (see RecruitmentRequest.Version).</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<VacancyActionEntry> Actions { get; set; } = new List<VacancyActionEntry>();

    /// <summary>Moves to <paramref name="newStatus"/> and records the step in the history.</summary>
    /// <param name="actorUserId">Null when the system acts on its own (automatic closing).</param>
    public void RecordStep(VacancyAction action, VacancyStatus newStatus, string? actorUserId, string actorName, string? comments, DateTime now)
    {
        Actions.Add(new VacancyActionEntry
        {
            Action = action,
            FromStatus = Status,
            ToStatus = newStatus,
            ActorUserId = actorUserId,
            ActorName = actorName,
            Comments = comments,
            ActionAt = now
        });
        if (newStatus != Status) StatusChangedAt = now;
        Status = newStatus;
        Version = Guid.NewGuid();
    }
}

/// <summary>
/// One listed skill or certification, e.g. (Skill, "Incident investigation", essential).
/// These rows are replaced as a set whenever the vacancy is edited, so they are not soft-deleted.
/// </summary>
public class VacancyRequirement
{
    public int Id { get; set; }
    public int VacancyId { get; set; }
    public RequirementType Type { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True = must have; false = nice to have.</summary>
    public bool IsEssential { get; set; }
}

/// <summary>Phase 15: one knockout question of a vacancy (the passing answer is always "Yes").</summary>
public class VacancyKnockoutQuestion
{
    public int Id { get; set; }
    public int VacancyId { get; set; }

    /// <summary>The question as applicants see it, e.g. "Are you willing to relocate to Apemanim?".</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>One line of a vacancy's history. Never edited.</summary>
public class VacancyActionEntry : BaseEntity
{
    public int VacancyId { get; set; }
    public VacancyAction Action { get; set; }
    public VacancyStatus FromStatus { get; set; }
    public VacancyStatus ToStatus { get; set; }

    /// <summary>Who acted (Users.Id), or null for the system's automatic closing.</summary>
    public string? ActorUserId { get; set; }

    public string ActorName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionAt { get; set; }
}
