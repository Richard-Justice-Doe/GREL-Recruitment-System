using System.ComponentModel.DataAnnotations;
using Grsms.Domain.Common;

namespace Grsms.Domain.Onboarding;

// Phase 18: the GREL New Hire Administrative Records Form, as a checklist of documents the new employee uploads and HR verifies.
// Phase 27 (10 November 2026): the document types became configurable (OnboardingDocumentType), each document is reviewed on
// its own as soon as it is uploaded, replaced files are kept as history, and every step is recorded (EmployeeDocumentEvent).

/// <summary>Heading a document type is listed under, on the candidate's checklist and the settings screen.</summary>
public enum DocumentGroup
{
    [Display(Name = "Personal documents")] Personal,
    [Display(Name = "Education documents")] Education,
    [Display(Name = "Employment and verification")] Employment,
    [Display(Name = "Banking information")] Banking,
    [Display(Name = "Family documents")] Family,
    [Display(Name = "Expatriate documents")] Expatriate
}

/// <summary>When a document type is required. Applied to every onboarding by <see cref="OnboardingWorkflow.ApplyRequirement"/>.</summary>
public enum DocumentRequirement
{
    /// <summary>Every new employee provides it.</summary>
    [Display(Name = "Required for everyone")] Required,

    /// <summary>Listed for everyone, never required (the new employee may mark it not applicable).</summary>
    [Display(Name = "Optional")] Optional,

    /// <summary>Education certificates: the Recruitment Officer ticks, for each new hire, the ones that person must provide.</summary>
    [Display(Name = "Chosen by the RO for each new hire")] ChosenPerHire,

    /// <summary>Required only when the RO ticks "Expatriate" (non-Ghanaian); not shown to Ghanaians.</summary>
    [Display(Name = "Expatriates only")] ExpatriateOnly,

    /// <summary>Required for Ghanaians; not shown to expatriates (e.g. the Ghana Card, replaced by the non-citizen card).</summary>
    [Display(Name = "Ghanaians only")] GhanaianOnly
}

/// <summary>
/// Phase 27: one kind of onboarding document (the spec's "OnboardingDocumentType"). The HR Administrator maintains the list on
/// the Onboarding documents screen; a change applies at once to the onboardings in progress (see OnboardingDocumentTypeService).
/// </summary>
public class OnboardingDocumentType : BaseEntity
{
    /// <summary>Stable key stored on each <see cref="EmployeeDocument"/>, e.g. "GhanaCard". Never changes once created.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>What exactly the new employee must provide ("requirement description"), shown under the name.</summary>
    public string? Description { get; set; }

    public DocumentGroup Group { get; set; }
    public DocumentRequirement Requirement { get; set; }

    /// <summary>True when Word (.docx) files are accepted as well as PDF and pictures (letters and the CV).</summary>
    public bool AllowsWordFiles { get; set; }

    /// <summary>Fewest files needed (e.g. 2 recommendation letters). A document cannot be verified with fewer.</summary>
    public int MinFiles { get; set; } = 1;

    /// <summary>Personnel file category the verified files are filed in (<see cref="PersonnelFileCategories"/>).</summary>
    public int PersonnelFileCategoryId { get; set; } = PersonnelFileCategories.EmployeeRecords;

    public int SortOrder { get; set; }

    /// <summary>False = retired: no longer asked for; files already uploaded are kept.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// A type from before Phase 27 (the single "School certificates" document). Kept only for the onboardings that already had it,
    /// which finish on their old checklist; never added to new onboardings and not editable.
    /// </summary>
    public bool IsLegacy { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();
}

/// <summary>Where one document is in its verification.</summary>
public enum EmployeeDocumentStatus
{
    /// <summary>Nothing uploaded yet.</summary>
    [Display(Name = "Not submitted")] Pending,

    /// <summary>Uploaded, waiting for the Recruitment Officer. The new employee can still replace the file.</summary>
    [Display(Name = "Submitted")] Submitted,

    /// <summary>The RO has started checking it: the new employee can no longer change it until it is verified or rejected.</summary>
    [Display(Name = "Under review")] UnderReview,

    /// <summary>Checked against the original. Locked.</summary>
    [Display(Name = "Verified")] Verified,

    /// <summary>Refused (reason recorded and emailed); the new employee uploads a replacement.</summary>
    [Display(Name = "Rejected")] Rejected,

    /// <summary>Only for documents that are not required: the new employee says it does not apply and why.</summary>
    [Display(Name = "Not applicable")] NotApplicable,

    /// <summary>A replacement for a rejected document, awaiting verification.</summary>
    [Display(Name = "Resubmitted, awaiting verification")] Resubmitted
}

/// <summary>Why the RO rejected a document (a fixed list, plus "Other" with the RO's own words).</summary>
public enum DocumentRejectionReason
{
    [Display(Name = "Document unreadable")] Unreadable,
    [Display(Name = "Incorrect document uploaded")] WrongDocument,
    [Display(Name = "Expired document")] Expired,
    [Display(Name = "Missing pages")] MissingPages,
    [Display(Name = "Other")] Other
}

/// <summary>
/// One document on a new employee's checklist (the spec's "OnboardingDocument"). Created for every active document type when
/// onboarding starts. The current files are <c>Attachments</c> rows (OwnerType "EmployeeDocument", OwnerId = this Id), stored under
/// random names outside the web root with a SHA-256 fingerprint; replaced files move to OwnerType "EmployeeDocumentReplaced" and
/// stay downloadable as history.
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public int OnboardingRecordId { get; set; }

    /// <summary>The new employee (Users.Id), copied from the onboarding for quick look-ups.</summary>
    public string EmployeeUserId { get; set; } = string.Empty;

    /// <summary>The <see cref="OnboardingDocumentType.Code"/> (column "DocumentType", which held the Phase 18 type names).</summary>
    public string DocumentTypeCode { get; set; } = string.Empty;

    /// <summary>True when the document counts towards "ready for activation".</summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// False when the type does not apply to this new employee (expatriate documents for a Ghanaian, a retired type): hidden from
    /// the new employee unless files were already uploaded, and never counted.
    /// </summary>
    public bool IsApplicable { get; set; } = true;

    public EmployeeDocumentStatus Status { get; set; } = EmployeeDocumentStatus.Pending;

    /// <summary>When a file was last uploaded (or "not applicable" declared).</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Why the new employee says the document does not apply.</summary>
    public string? NotApplicableReason { get; set; }

    /// <summary>When the RO clicked "Start review" (status Under review).</summary>
    public DateTime? ReviewStartedAt { get; set; }
    public string? ReviewStartedByUserId { get; set; }

    /// <summary>When it was verified or rejected, and by whom.</summary>
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUserId { get; set; }

    /// <summary>docs/36 AI-4: "valid until" of an ID-type document (Ghana Card, passport, permits), given by the new employee or the RO.</summary>
    public DateOnly? ExpiresOn { get; set; }

    public DocumentRejectionReason? RejectionReasonCode { get; set; }

    /// <summary>The rejection reason shown to the new employee (the reason's name, plus the RO's note).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>How many times it has been rejected (shown to the RO; there is no limit).</summary>
    public int RejectionCount { get; set; }

    public Guid Version { get; set; } = Guid.NewGuid();
}

/// <summary>What happened to a document (one line of its history).</summary>
public enum DocumentEventAction
{
    [Display(Name = "Uploaded")] Uploaded,
    [Display(Name = "File replaced")] Replaced,
    [Display(Name = "File removed")] FileRemoved,
    [Display(Name = "Marked not applicable")] MarkedNotApplicable,
    [Display(Name = "Review started")] ReviewStarted,
    [Display(Name = "Verified")] Verified,
    [Display(Name = "Rejected")] Rejected,
    [Display(Name = "Resubmitted")] Resubmitted,
    [Display(Name = "Opened")] Viewed,
    [Display(Name = "Requirement changed")] RequirementChanged
}

/// <summary>
/// Phase 27: one line of a document's history (the spec's "OnboardingDocumentVerification"): uploads, replacements, review,
/// verification, rejection, resubmission and every time someone opened a file, with who, when, the IP address and the status
/// before and after. Never edited.
/// </summary>
public class EmployeeDocumentEvent : BaseEntity
{
    public int EmployeeDocumentId { get; set; }
    public int OnboardingRecordId { get; set; }
    public DocumentEventAction Action { get; set; }
    public EmployeeDocumentStatus FromStatus { get; set; }
    public EmployeeDocumentStatus ToStatus { get; set; }

    /// <summary>The file concerned (uploaded, replaced, opened), if any.</summary>
    public int? AttachmentId { get; set; }
    public string? FileName { get; set; }

    public DocumentRejectionReason? RejectionReason { get; set; }
    public string? Comments { get; set; }

    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime ActionAt { get; set; }
}

/// <summary>
/// The default checklist (seeded with fixed ids 1..N; the HR Administrator can change it afterwards). The first sixteen codes are
/// the Phase 18 document names, so existing checklists keep working; "SchoolCertificates" is the legacy type.
/// </summary>
public static class DefaultDocumentTypes
{
    public const string SchoolCertificates = "SchoolCertificates";
    public const string RecommendationLetters = "RecommendationLetters";

    /// <summary>Retired 11 Nov 2026 (docs/29, change 4): bank details are entered as data in the personal record.</summary>
    public const string BankConfirmationLetter = "BankConfirmationLetter";

    private const int Recruitment = PersonnelFileCategories.RecruitmentDocuments, Employee = PersonnelFileCategories.EmployeeRecords,
        Identification = PersonnelFileCategories.IdentificationRecords, Compliance = PersonnelFileCategories.ComplianceRecords;

    /// <summary>(Id, Code, Name, Description, Group, Requirement, Word files allowed, minimum files, personnel file category).</summary>
    public static readonly IReadOnlyList<OnboardingDocumentType> All = Build(
    [
        (1, "ApplicationLetter", "Application letter", "The letter you sent when applying.", DocumentGroup.Personal, DocumentRequirement.Required, true, 1, Recruitment),
        (2, "Cv", "Updated CV", "Your current CV.", DocumentGroup.Personal, DocumentRequirement.Required, true, 1, Recruitment),
        (3, "PassportPicture", "Passport-size photograph", "A recent passport-size photograph with a plain background.", DocumentGroup.Personal, DocumentRequirement.Required, false, 1, Identification),
        (4, "GhanaCard", "Ghana Card / National ID", "Both sides of your Ghana Card.", DocumentGroup.Personal, DocumentRequirement.GhanaianOnly, false, 1, Identification),
        (5, "SsnitCard", "SSNIT card", "Your SSNIT card or biometric registration slip.", DocumentGroup.Personal, DocumentRequirement.Required, false, 1, Identification),
        (6, "BirthCertificate", "Birth certificate", "Your birth certificate.", DocumentGroup.Personal, DocumentRequirement.Required, false, 1, Identification),

        (7, "BeceCertificate", "BECE certificate", "Basic Education Certificate Examination results.", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (8, "WassceCertificate", "WASSCE / SSSCE certificate", "West African Senior School Certificate Examination results.", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (9, "DiplomaCertificate", "Diploma certificate", "Diploma certificate (and transcript, if you have it).", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (10, "HndCertificate", "HND certificate", "Higher National Diploma certificate.", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (11, "DegreeCertificate", "Degree certificate", "Bachelor's degree certificate (and transcript, if you have it).", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (12, "MastersCertificate", "Master's certificate", "Master's degree certificate.", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),
        (13, "ProfessionalCertificates", "Professional certifications", "Professional or trade certificates (one file each).", DocumentGroup.Education, DocumentRequirement.ChosenPerHire, false, 1, Employee),

        (14, RecommendationLetters, "Recommendation letters", "Two recommendation letters (one file each).", DocumentGroup.Employment, DocumentRequirement.Required, true, 2, Recruitment),
        (15, "PoliceClearance", "Police clearance report", "Police clearance report from the Ghana Police Service.", DocumentGroup.Employment, DocumentRequirement.Required, false, 1, Compliance),
        (16, "MedicalReport", "Medical examination report", "Medical examination report from a recognised hospital.", DocumentGroup.Employment, DocumentRequirement.Required, false, 1, Compliance),

        (17, BankConfirmationLetter, "Bank confirmation letter", "A letter from your bank confirming your account name and number.", DocumentGroup.Banking, DocumentRequirement.Required, false, 1, Compliance),

        (18, "MarriageCertificate", "Marriage certificate", "If you are married.", DocumentGroup.Family, DocumentRequirement.Optional, false, 1, Employee),
        (19, "ChildBirthCertificates", "Children's birth certificates", "One file per child, if you have children.", DocumentGroup.Family, DocumentRequirement.Optional, false, 1, Employee),
        (20, "DependantPhotographs", "Dependants' passport photographs", "Passport-size photographs of your dependants, if any.", DocumentGroup.Family, DocumentRequirement.Optional, false, 1, Employee),

        (21, "PassportBiodataPage", "Passport biodata page", "The photo page of your passport.", DocumentGroup.Expatriate, DocumentRequirement.ExpatriateOnly, false, 1, Identification),
        (22, "NonCitizenCard", "Non-citizen card", "Your Ghana non-citizen identification card.", DocumentGroup.Expatriate, DocumentRequirement.ExpatriateOnly, false, 1, Identification),
        (23, "WorkPermit", "Work permit", "Your Ghana Immigration Service work permit.", DocumentGroup.Expatriate, DocumentRequirement.ExpatriateOnly, false, 1, Compliance),
        (24, "ResidencePermit", "Residence permit", "Your residence permit.", DocumentGroup.Expatriate, DocumentRequirement.ExpatriateOnly, false, 1, Compliance),

        (25, SchoolCertificates, "School certificates (old checklist)", "Kept for onboardings started before 10 November 2026.", DocumentGroup.Education, DocumentRequirement.Required, false, 1, Employee)
    ]);

    private static IReadOnlyList<OnboardingDocumentType> Build(
        IEnumerable<(int Id, string Code, string Name, string Description, DocumentGroup Group, DocumentRequirement Requirement, bool Word, int MinFiles, int Category)> rows) =>
        rows.Select(r => new OnboardingDocumentType
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description, Group = r.Group, Requirement = r.Requirement, AllowsWordFiles = r.Word,
            MinFiles = r.MinFiles, PersonnelFileCategoryId = r.Category, SortOrder = r.Id * 10,
            IsActive = r.Code != SchoolCertificates && r.Code != BankConfirmationLetter, IsLegacy = r.Code == SchoolCertificates
        }).ToList();
}
