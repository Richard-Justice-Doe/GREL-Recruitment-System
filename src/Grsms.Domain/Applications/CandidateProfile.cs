using Grsms.Domain.Common;
using Grsms.Domain.Vacancies;

namespace Grsms.Domain.Applications;

/// <summary>
/// A person's job-seeker profile (Module 5): contact details, education, experience, skills and certifications.
/// Every account can have one: external candidates, and GREL staff applying for internal vacancies.
///
/// The profile is the person's "current" information. When they apply, a copy of it (and of their CV) is frozen
/// on the application, so later edits never change what an applicant actually sent.
/// The structured fields (education level, years, skill and certification lists) feed CV screening in Phase 6.
/// </summary>
public class CandidateProfile : BaseEntity
{
    /// <summary>The account this profile belongs to (Users.Id). One profile per account.</summary>
    public string UserId { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Town or region where the person lives, e.g. "Takoradi".</summary>
    public string? Location { get; set; }

    public EducationLevel HighestEducation { get; set; } = EducationLevel.None;
    public string? FieldOfStudy { get; set; }
    public int YearsExperience { get; set; }
    public string? CurrentEmployer { get; set; }
    public string? CurrentPosition { get; set; }

    /// <summary>Skills, one per line.</summary>
    public string? Skills { get; set; }

    /// <summary>Certifications, one per line.</summary>
    public string? Certifications { get; set; }

    /// <summary>Optional LinkedIn or portfolio address.</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>A short personal statement.</summary>
    public string? Summary { get; set; }
}
