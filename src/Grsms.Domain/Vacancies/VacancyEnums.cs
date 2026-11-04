using System.ComponentModel.DataAnnotations;

namespace Grsms.Domain.Vacancies;

// Fixed lists used by vacancies (Module 3). Stored in MySQL as readable text.

/// <summary>Life cycle of a vacancy.</summary>
public enum VacancyStatus
{
    /// <summary>Being prepared by the Recruitment Officer (also where a returned vacancy goes back to).</summary>
    [Display(Name = "Draft")] Draft,

    /// <summary>Waiting for the HR Manager to approve the wording, requirements and salary.</summary>
    [Display(Name = "Pending approval")] PendingApproval,

    /// <summary>Approved and open for applications until the closing date.</summary>
    [Display(Name = "Published")] Published,

    /// <summary>No longer accepting applications (closing date passed, or closed by hand). Selection continues.</summary>
    [Display(Name = "Closed")] Closed,

    /// <summary>Finished with, or abandoned. Kept for history and reports only.</summary>
    [Display(Name = "Archived")] Archived
}

/// <summary>Who may apply.</summary>
public enum VacancyType
{
    [Display(Name = "Internal (GREL staff only)")] Internal,
    [Display(Name = "External (public)")] External,
    [Display(Name = "Internal and external")] InternalAndExternal
}

/// <summary>Minimum education level, ordered from lowest to highest so levels can be compared ("at least Diploma").</summary>
public enum EducationLevel
{
    [Display(Name = "No minimum")] None,
    [Display(Name = "Basic education (BECE)")] Basic,
    [Display(Name = "Secondary (WASSCE / SSSCE)")] Secondary,
    [Display(Name = "Certificate")] Certificate,
    [Display(Name = "Diploma")] Diploma,
    [Display(Name = "HND")] Hnd,
    [Display(Name = "Bachelor's degree")] Bachelors,
    [Display(Name = "Master's degree")] Masters,
    [Display(Name = "Doctorate")] Doctorate
}

/// <summary>Kind of listed requirement.</summary>
public enum RequirementType
{
    [Display(Name = "Skill")] Skill,
    [Display(Name = "Certification")] Certification
}

/// <summary>One line of a vacancy's history.</summary>
public enum VacancyAction
{
    [Display(Name = "Created")] Created,
    [Display(Name = "Submitted for approval")] Submitted,
    [Display(Name = "Approved and published")] Published,
    [Display(Name = "Returned for changes")] Returned,
    [Display(Name = "Closed")] Closed,
    [Display(Name = "Closed automatically (closing date passed)")] AutoClosed,
    [Display(Name = "Reopened")] Reopened,
    [Display(Name = "Archived")] Archived
}
