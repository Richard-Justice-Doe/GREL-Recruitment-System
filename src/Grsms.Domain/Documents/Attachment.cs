using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Documents;

/// <summary>
/// A file uploaded to the system (job description, CV, certificate, ...). One table serves every module:
/// <see cref="OwnerType"/> + <see cref="OwnerId"/> say which record the file belongs to.
///
/// The file itself is stored on disk (or a file server) under a random name (<see cref="StoredFileName"/>);
/// only this description is in the database. Files are never served directly: every download goes
/// through a page that checks the user may see the owning record.
/// </summary>
public class Attachment : BaseEntity
{
    /// <summary>Kind of record the file belongs to. See <see cref="AttachmentOwners"/>.</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>Primary key of that record.</summary>
    public int OwnerId { get; set; }

    public AttachmentCategory Category { get; set; }

    /// <summary>The file name as uploaded, e.g. "Safety Officer JD.pdf" (shown to users, never used as a path).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Random name on disk, e.g. "2026/11/3f2a...e9.pdf". Users never see or choose it.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. "application/pdf".</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 fingerprint of the file, to prove later that it has not been altered.</summary>
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>What a file is. New modules add their own categories here.</summary>
public enum AttachmentCategory
{
    // Recruitment request supporting documents (form QF-81-11). Induction programme and Other remain for older requests.
    [Display(Name = "Job description")] JobDescription,
    [Display(Name = "Organisational structure")] OrganisationalChart,
    [Display(Name = "Induction programme")] InductionProgramme,
    [Display(Name = "Other supporting document")] Other,
    [Display(Name = "Main accountabilities")] MainAccountabilities,
    [Display(Name = "Department justification")] DepartmentJustification,
    [Display(Name = "Budget approval document")] BudgetApproval,

    // Phase 5: candidate documents
    [Display(Name = "CV")] Cv,
    [Display(Name = "Certificate")] Certificate,
    [Display(Name = "Cover letter")] CoverLetter,

    // Phase 18: a new employee's administrative-records document (the document type is on EmployeeDocument)
    [Display(Name = "New-hire document")] NewHireDocument
}

/// <summary>The values used in <see cref="Attachment.OwnerType"/>.</summary>
public static class AttachmentOwners
{
    public const string RecruitmentRequest = "RecruitmentRequest";

    /// <summary>A person's candidate profile (their current CV and certificates).</summary>
    public const string CandidateProfile = "CandidateProfile";

    /// <summary>A job application (copies of the documents sent with it, frozen at the time of applying).</summary>
    public const string JobApplication = "JobApplication";

    /// <summary>Phase 18: a file of one new-hire document (EmployeeDocument).</summary>
    public const string EmployeeDocument = "EmployeeDocument";
}
