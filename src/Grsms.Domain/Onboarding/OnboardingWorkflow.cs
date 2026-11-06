using Grsms.Domain.Security;

namespace Grsms.Domain.Onboarding;

/// <summary>
/// The rules of onboarding (Module 9, extended in Phase 18), in one place:
///
///   Draft ─letter issued─► Awaiting employee submission ─submit─► Awaiting document verification ─verified─► Awaiting HR approval
///                               ▲             │                         │                                       │
///                               └─ returned ──┴──── (documents rejected) ┘ ◄──────── returned for verification ──┘
///                                                                                                               │ approved
///   Completed ◄─activate─ Awaiting activation ◄─induction complete─ Awaiting departmental induction ◄────────────┘
///   (any stage before Completed) ─cancel─► Cancelled
///
///   Start      : a Recruitment Officer or HR Manager starts onboarding once the candidate has ACCEPTED the offer (one per
///                application): the record (Draft), the document checklist and the departmental checklist are created.
///   Letter     : an HR Manager issues the appointment letter (and may revise it until it is signed). The first issue moves
///                the onboarding to "Awaiting employee submission".
///   Submit     : the new employee (or HR on their behalf) submits once the letter is signed and every document is uploaded
///                (or, for the optional ones, declared not applicable).
///   Verify     : HR verifies or rejects each document. Rejected documents go back to the new employee; when everything is
///                verified, HR sends the onboarding to the HR Manager.
///   Approve    : an HR Manager who did not verify the documents approves (→ induction) or returns them for verification.
///   Induction  : each checklist task is done by its owner, the department's HOD (department tasks) or HR. HR closes the
///                induction once no task is still "To do" and the start date has come.
///   Activate   : HR records the staff number; the account becomes a GREL staff account. Completed.
///   Cancel     : HR cancels with a reason at any stage before Completed.
/// Nobody acts on their own onboarding.
/// </summary>
public static class OnboardingWorkflow
{
    /// <summary>Who runs onboarding: the same people who run recruitment.</summary>
    public static readonly IReadOnlyList<string> HrRoles = [Roles.RecruitmentOfficer, Roles.HrManager];

    /// <summary>Department code of the IT department, whose Head receives the IT tasks by default.</summary>
    public const string ItDepartmentCode = "IT";

    /// <summary>Days after the start date by which an onboarding is expected to be finished.</summary>
    public const int ExpectedCompletionDays = 30;

    /// <summary>The stages in order, for steppers and reports.</summary>
    public static readonly IReadOnlyList<OnboardingStatus> Stages =
    [
        OnboardingStatus.Draft,
        OnboardingStatus.AwaitingEmployeeSubmission,
        OnboardingStatus.AwaitingDocumentVerification,
        OnboardingStatus.AwaitingHrApproval,
        OnboardingStatus.AwaitingDepartmentalInduction,
        OnboardingStatus.AwaitingActivation,
        OnboardingStatus.Completed
    ];

    /// <summary>True while the onboarding can still move (not Completed or Cancelled).</summary>
    public static bool IsActive(OnboardingStatus s) => s is not (OnboardingStatus.Completed or OnboardingStatus.Cancelled);

    /// <summary>The new employee may upload documents and declare them not applicable only while it is their turn.</summary>
    public static bool EmployeeMayEditDocuments(OnboardingStatus s) =>
        s is OnboardingStatus.Draft or OnboardingStatus.AwaitingEmployeeSubmission;

    /// <summary>HR may upload documents for the new employee (e.g. originals brought in) until HR has approved.</summary>
    public static bool HrMayEditDocuments(OnboardingStatus s) =>
        s is OnboardingStatus.Draft or OnboardingStatus.AwaitingEmployeeSubmission or OnboardingStatus.AwaitingDocumentVerification;

    /// <summary>The appointment letter can be issued or revised (until it is signed) up to HR approval.</summary>
    public static bool LetterMayBeIssued(OnboardingStatus s) =>
        s is OnboardingStatus.Draft or OnboardingStatus.AwaitingEmployeeSubmission;

    /// <summary>The departmental checklist can be worked on from the start until activation.</summary>
    public static bool ChecklistOpen(OnboardingStatus s) => IsActive(s);

    // ---------------------------------------------------------------- New Hire Administrative Records

    /// <summary>
    /// Documents that do not apply to everyone, so the new employee may declare them "not applicable" (with a reason).
    /// Every other document on the form is required.
    /// </summary>
    public static readonly IReadOnlySet<EmployeeDocumentType> OptionalDocuments = new HashSet<EmployeeDocumentType>
    {
        EmployeeDocumentType.MarriageCertificate,
        EmployeeDocumentType.ChildBirthCertificates,
        EmployeeDocumentType.DependantPhotographs,
        EmployeeDocumentType.PassportBiodataPage,
        EmployeeDocumentType.NonCitizenCard
    };

    /// <summary>File types accepted for new-hire documents (scans and photographs).</summary>
    public static readonly IReadOnlyList<string> DocumentFileTypes = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>Most files one document may hold (e.g. several school certificates).</summary>
    public const int MaxFilesPerDocument = 10;

    /// <summary>True when the document counts as dealt with for the employee's submission.</summary>
    public static bool ReadyToSubmit(EmployeeDocumentStatus s) =>
        s is EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.Verified or EmployeeDocumentStatus.NotApplicable;

    /// <summary>True when HR has finished with the document (verified, or "not applicable" accepted).</summary>
    public static bool Settled(EmployeeDocumentStatus s) => s is EmployeeDocumentStatus.Verified or EmployeeDocumentStatus.NotApplicable;

    // ---------------------------------------------------------------- Departmental checklist

    /// <summary>One item of the default checklist. <paramref name="DueDays"/> is counted from the start date.</summary>
    public record DefaultTask(OnboardingTeam Team, string Title, int DueDays);

    /// <summary>
    /// The checklist every onboarding starts with. HR can add items, reassign them or mark them "not needed".
    /// (The new employee's own documents are no longer checklist items: they are the document checklist above.)
    /// </summary>
    public static readonly IReadOnlyList<DefaultTask> DefaultChecklist =
    [
        // Human Resources
        new(OnboardingTeam.HumanResources, "Open the personal file and check the original certificates", -3),
        new(OnboardingTeam.HumanResources, "Schedule the GREL induction programme", -5),
        new(OnboardingTeam.HumanResources, "Payroll set-up: bank account, SSNIT and GRA tax details", 0),
        new(OnboardingTeam.HumanResources, "Enrol in benefits (medical cover, provident fund)", 14),

        // Department
        new(OnboardingTeam.Department, "Workspace, tools and PPE ready", -1),
        new(OnboardingTeam.Department, "Department induction and introduction to the team", 0),
        new(OnboardingTeam.Department, "Agree probation objectives", 14),

        // IT
        new(OnboardingTeam.InformationTechnology, "Create the email account and system access", -2),
        new(OnboardingTeam.InformationTechnology, "Issue a computer / phone (mark \"not needed\" if none)", -1)
    ];
}
