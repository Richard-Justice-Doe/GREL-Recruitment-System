using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Onboarding;

// Phase 18: the GREL New Hire Administrative Records Form, as a checklist of documents the new employee uploads and HR verifies.

/// <summary>The documents on the New Hire Administrative Records Form, in the form's order.</summary>
public enum EmployeeDocumentType
{
    [Display(Name = "Application letter")] ApplicationLetter,
    [Display(Name = "CV")] Cv,
    [Display(Name = "Passport picture")] PassportPicture,
    [Display(Name = "Recommendation letters")] RecommendationLetters,
    [Display(Name = "Medical report")] MedicalReport,
    [Display(Name = "Police clearance")] PoliceClearance,
    [Display(Name = "School certificates")] SchoolCertificates,
    [Display(Name = "Birth certificate")] BirthCertificate,
    [Display(Name = "Ghana Card")] GhanaCard,
    [Display(Name = "SSNIT card")] SsnitCard,
    [Display(Name = "Marriage certificate")] MarriageCertificate,
    [Display(Name = "Children's birth certificates")] ChildBirthCertificates,
    [Display(Name = "Dependants' photographs")] DependantPhotographs,
    [Display(Name = "Bank confirmation letter")] BankConfirmationLetter,
    [Display(Name = "Passport biodata page")] PassportBiodataPage,
    [Display(Name = "Non-citizen card")] NonCitizenCard
}

/// <summary>Where one document is in its verification.</summary>
public enum EmployeeDocumentStatus
{
    /// <summary>Nothing uploaded yet.</summary>
    [Display(Name = "Pending")] Pending,

    /// <summary>Uploaded, waiting for HR to verify it.</summary>
    [Display(Name = "Submitted")] Submitted,

    /// <summary>HR checked it against the original. Locked.</summary>
    [Display(Name = "Verified")] Verified,

    /// <summary>HR refused it (reason recorded); the new employee uploads it again.</summary>
    [Display(Name = "Rejected")] Rejected,

    /// <summary>
    /// Only for documents that do not apply to everyone (marriage certificate, children's birth certificates, dependants'
    /// photographs, passport biodata page, non-citizen card): the new employee says it does not apply and why. HR can
    /// accept that or reject it.
    /// </summary>
    [Display(Name = "Not applicable")] NotApplicable
}

/// <summary>
/// One document on a new employee's administrative-records checklist. Created (Pending) for every
/// <see cref="EmployeeDocumentType"/> when onboarding starts. The files themselves are <c>Attachments</c> rows
/// (OwnerType "EmployeeDocument", OwnerId = this Id): stored on the server under random names outside the web root, with
/// the original file name, the stored path, the upload date and a SHA-256 fingerprint. A document can have several files
/// (e.g. several school certificates).
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public int OnboardingRecordId { get; set; }

    /// <summary>The new employee (Users.Id), copied from the onboarding for quick look-ups.</summary>
    public string EmployeeUserId { get; set; } = string.Empty;

    public EmployeeDocumentType DocumentType { get; set; }

    /// <summary>False for the documents that can be marked "not applicable" (see <see cref="OnboardingWorkflow.OptionalDocuments"/>).</summary>
    public bool IsRequired { get; set; }

    public EmployeeDocumentStatus Status { get; set; } = EmployeeDocumentStatus.Pending;

    /// <summary>When a file was last uploaded (or "not applicable" declared).</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Why the new employee says the document does not apply.</summary>
    public string? NotApplicableReason { get; set; }

    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUserId { get; set; }

    /// <summary>Why HR rejected it (shown to the new employee).</summary>
    public string? RejectionReason { get; set; }
}
