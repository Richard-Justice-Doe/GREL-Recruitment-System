using Grsms.Domain.Security;

namespace Grsms.Domain.Onboarding;

/// <summary>
/// The rules of onboarding (Module 9, extended in Phase 18 and changed in Phase 27, 10 November 2026), in one place:
///
///   Start (RO) ─► Draft ──letter issued──► Awaiting the new employee ◄──► Awaiting document verification ──► Ready for activation
///                   └──────────── (worked out automatically from the documents, the letter and the personal record) ───────┘
///   Ready for activation ─activate (RO or HR Manager)─► Induction in progress ─induction closed─► Completed
///   (any stage before Completed) ─cancel─► Cancelled
///
///   Start      : the Recruitment Officer clicks "Click to proceed to onboarding" once the candidate has ACCEPTED the offer (one
///                per application): the record, the document checklist, the departmental checklist and a provisional personnel
///                file are created, and the new employee can start uploading at once. The RO ticks the education certificates
///                this person must provide and whether they are an expatriate.
///   Letter     : the RO issues the appointment letter (and may revise it until it is signed).
///   Documents  : each document goes to the RO as soon as it is uploaded. The RO may "Start review" (the file is then locked),
///                verify it, or reject it with a reason (the new employee is emailed and uploads a replacement).
///   Stage      : before activation the stage follows the facts (<see cref="StageFor"/>): no letter yet → Draft; anything
///                outstanding for the new employee → Awaiting the new employee; documents still to verify → Awaiting document
///                verification; otherwise → Ready for activation. There is no HR Manager approval any more.
///   Activate   : the RO or an HR Manager records the staff number: the account becomes a GREL staff account, the probation
///                starts and the departmental induction tasks are created. The induction is then finished while the employee
///                is already active, and closing it completes the onboarding.
///   Cancel     : the RO cancels with a reason at any stage before Completed.
/// Who may do what is set by the permissions onboarding.view / .manage / .verify-documents / .complete (see
/// <see cref="Permissions"/>). Nobody acts on their own onboarding.
/// </summary>
public static class OnboardingWorkflow
{
    /// <summary>Department code of the IT department, whose Head receives the IT tasks by default.</summary>
    public const string ItDepartmentCode = "IT";

    /// <summary>Days after the start date by which an onboarding is expected to be finished.</summary>
    public const int ExpectedCompletionDays = 30;

    /// <summary>Phase 27: working days the new employee has to provide every document (changeable in System settings).</summary>
    public const int DocumentDeadlineWorkingDays = 10;

    /// <summary>Phase 27: longest extension of the document deadline the RO may give (once).</summary>
    public const int MaxDeadlineExtensionDays = 14;

    /// <summary>The stages in order, for steppers and reports.</summary>
    public static readonly IReadOnlyList<OnboardingStatus> Stages =
    [
        OnboardingStatus.Draft,
        OnboardingStatus.AwaitingEmployeeSubmission,
        OnboardingStatus.AwaitingDocumentVerification,
        OnboardingStatus.AwaitingActivation,
        OnboardingStatus.AwaitingDepartmentalInduction,
        OnboardingStatus.Completed
    ];

    /// <summary>True while the onboarding can still move (not Completed or Cancelled).</summary>
    public static bool IsActive(OnboardingStatus s) => s is not (OnboardingStatus.Completed or OnboardingStatus.Cancelled);

    /// <summary>The stages before activation, for database queries (same list as <see cref="BeforeActivation"/>).</summary>
    public static readonly IReadOnlyList<OnboardingStatus> BeforeActivationStatuses =
        [OnboardingStatus.Draft, OnboardingStatus.AwaitingEmployeeSubmission, OnboardingStatus.AwaitingDocumentVerification,
         OnboardingStatus.AwaitingHrApproval, OnboardingStatus.AwaitingActivation];

    /// <summary>Phase 27: before activation (the stages worked out by <see cref="StageFor"/>).</summary>
    public static bool BeforeActivation(OnboardingStatus s) => BeforeActivationStatuses.Contains(s);

    /// <summary>The new employee may upload documents and change the personal record until activation.</summary>
    public static bool EmployeeMayEditDocuments(OnboardingStatus s) => BeforeActivation(s);

    /// <summary>The RO may upload documents for the new employee (e.g. originals brought in) until activation.</summary>
    public static bool HrMayEditDocuments(OnboardingStatus s) => BeforeActivation(s);

    /// <summary>Documents are reviewed as they arrive, until activation.</summary>
    public static bool DocumentsMayBeReviewed(OnboardingStatus s) => BeforeActivation(s);

    /// <summary>The appointment letter can be issued or revised (until it is signed) before activation.</summary>
    public static bool LetterMayBeIssued(OnboardingStatus s) => BeforeActivation(s);

    /// <summary>The departmental checklist can be worked on from the start until the onboarding is completed.</summary>
    public static bool ChecklistOpen(OnboardingStatus s) => IsActive(s);

    // ---------------------------------------------------------------- New-hire documents

    /// <summary>File types accepted for every document (scans and photographs).</summary>
    public static readonly IReadOnlyList<string> DocumentFileTypes = [".pdf", ".jpg", ".jpeg", ".png"];

    /// <summary>File types accepted for the types that allow Word files (letters and the CV).</summary>
    public static readonly IReadOnlyList<string> WordDocumentFileTypes = [".pdf", ".docx", ".jpg", ".jpeg", ".png"];

    public static IReadOnlyList<string> FileTypesFor(OnboardingDocumentType type) => type.AllowsWordFiles ? WordDocumentFileTypes : DocumentFileTypes;

    /// <summary>Most files one document may hold (e.g. several professional certificates).</summary>
    public const int MaxFilesPerDocument = 10;

    /// <summary>The new employee may change the files of a document unless it is verified or the RO is reviewing it.</summary>
    public static bool EmployeeMayChange(EmployeeDocumentStatus s) => s is not (EmployeeDocumentStatus.Verified or EmployeeDocumentStatus.UnderReview);

    /// <summary>Uploaded and waiting for the RO.</summary>
    public static bool AwaitsReview(EmployeeDocumentStatus s) =>
        s is EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.Resubmitted or EmployeeDocumentStatus.UnderReview;

    /// <summary>
    /// Applies a document type's requirement to one onboarding's document: whether it is required and whether it applies at all.
    /// Legacy documents are left as they are; for "chosen per hire" types the RO's choice (<see cref="EmployeeDocument.IsRequired"/>)
    /// is kept. A document that no longer applies stays visible while it has files.
    /// </summary>
    public static void ApplyRequirement(OnboardingDocumentType type, OnboardingRecord o, EmployeeDocument d, bool hasFiles)
    {
        if (type.IsLegacy) return;
        (d.IsRequired, d.IsApplicable) = !type.IsActive ? (false, hasFiles) : type.Requirement switch
        {
            DocumentRequirement.Required => (true, true),
            DocumentRequirement.Optional => (false, true),
            DocumentRequirement.ChosenPerHire => (d.IsRequired, true),
            DocumentRequirement.ExpatriateOnly => (o.IsExpatriate, o.IsExpatriate || hasFiles),
            DocumentRequirement.GhanaianOnly => (!o.IsExpatriate, !o.IsExpatriate || hasFiles),
            _ => (d.IsRequired, d.IsApplicable)
        };
    }

    /// <summary>One document with what <see cref="CheckReadiness"/> needs to know about it.</summary>
    public record DocumentState(EmployeeDocument Document, string Name, int Files, int MinFiles);

    /// <summary>What is still outstanding before an onboarding is ready for activation.</summary>
    /// <param name="ForEmployee">What the new employee must still do, in plain words (empty = nothing).</param>
    /// <param name="AwaitingVerification">Documents uploaded and waiting for the RO.</param>
    public record Readiness(IReadOnlyList<string> ForEmployee, int AwaitingVerification, int Rejected, int RequiredTotal, int RequiredVerified)
    {
        public bool EmployeeDone => ForEmployee.Count == 0;
        public bool Ready => EmployeeDone && AwaitingVerification == 0;
    }

    /// <summary>
    /// The readiness rule: every required document uploaded (with enough files) and verified, the Personal Record Declaration and
    /// the appointment letter signed, no correction pending and no rejected document. Documents that do not apply are ignored.
    /// </summary>
    public static Readiness CheckReadiness(OnboardingRecord o, IReadOnlyCollection<DocumentState> docs, bool personalRecordSigned)
    {
        var todo = new List<string>();
        if (!o.LetterIssued) todo.Add("Wait for your appointment letter, then sign it");
        else if (!o.LetterAcknowledged) todo.Add("Sign your appointment letter");
        if (!personalRecordSigned) todo.Add("Fill in and sign your personal record");
        if (o.CorrectionRequestedAt is not null) todo.Add($"Make the correction HR asked for: {o.CorrectionMessage}");
        var applicable = docs.Where(x => x.Document.IsApplicable).ToList();
        foreach (var x in applicable)
        {
            var d = x.Document;
            if (d.Status == EmployeeDocumentStatus.Rejected)
                todo.Add($"Replace the {x.Name.ToLowerInvariant()} (rejected: {d.RejectionReason})");
            else if (d.IsRequired && d.Status != EmployeeDocumentStatus.Verified && x.Files < Math.Max(1, x.MinFiles))
                todo.Add(x.MinFiles > 1 && x.Files > 0
                    ? $"Upload {x.MinFiles} files for the {x.Name.ToLowerInvariant()} ({x.Files} so far)"
                    : $"Upload the {x.Name.ToLowerInvariant()}");
        }
        var required = applicable.Where(x => x.Document.IsRequired).ToList();
        return new Readiness(todo, applicable.Count(x => AwaitsReview(x.Document.Status)),
            applicable.Count(x => x.Document.Status == EmployeeDocumentStatus.Rejected),
            required.Count, required.Count(x => x.Document.Status == EmployeeDocumentStatus.Verified));
    }

    /// <summary>The stage an onboarding before activation should be at, from the facts (see the class summary).</summary>
    public static OnboardingStatus StageFor(OnboardingRecord o, Readiness r) =>
        !o.LetterIssued ? OnboardingStatus.Draft
        : !r.EmployeeDone ? OnboardingStatus.AwaitingEmployeeSubmission
        : r.AwaitingVerification > 0 ? OnboardingStatus.AwaitingDocumentVerification
        : OnboardingStatus.AwaitingActivation;

    /// <summary><paramref name="days"/> working days (Monday to Friday) after <paramref name="from"/>.</summary>
    public static DateOnly AddWorkingDays(DateOnly from, int days)
    {
        var d = from;
        while (days > 0)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days--;
        }
        return d;
    }

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

    // ---------------------------------------------------------------- Phase 20: departmental induction

    /// <summary>The induction area of a checklist team: HR → Human Resource, IT → Corporate IT; the own department has none.</summary>
    public static InductionArea? AreaOf(OnboardingTeam team) => team switch
    {
        OnboardingTeam.HumanResources => InductionArea.HumanResource,
        OnboardingTeam.InformationTechnology => InductionArea.CorporateIt,
        _ => null
    };

    /// <summary>The checklist team a new task of an area belongs to (HR, IT, or department for every other area).</summary>
    public static OnboardingTeam TeamOf(InductionArea area) => area switch
    {
        InductionArea.HumanResource => OnboardingTeam.HumanResources,
        InductionArea.CorporateIt => OnboardingTeam.InformationTechnology,
        _ => OnboardingTeam.Department
    };

    /// <summary>Areas ticked by default at activation (until changed on the Induction areas screen).</summary>
    public static readonly IReadOnlySet<InductionArea> DefaultAreas = new HashSet<InductionArea>
    {
        InductionArea.HumanResource, InductionArea.CorporateIt, InductionArea.Security, InductionArea.HealthAndSafety
    };

    /// <summary>Days after the induction starts (the later of the start date and the activation) by which area tasks are due.</summary>
    public const int InductionTaskDueDays = 7;

    /// <summary>The built-in tasks of each area (one per line on the Induction areas screen, where they can be changed).</summary>
    public static readonly IReadOnlyDictionary<InductionArea, IReadOnlyList<string>> InductionTemplates = new Dictionary<InductionArea, IReadOnlyList<string>>
    {
        [InductionArea.HumanResource] = ["Company orientation", "Policies review (code of conduct, leave, disciplinary and grievance procedures)"],
        [InductionArea.CorporateIt] = ["Email setup", "AD account creation", "System access (ERP, shared drives, printers)"],
        [InductionArea.CorporateProcurement] = ["Procurement procedures briefing"],
        [InductionArea.CorporateSupplyChain] = ["Supply chain procedures briefing"],
        [InductionArea.CorporateFinance] = ["Finance procedures briefing (expenses, petty cash, approvals)"],
        [InductionArea.FactoryOperations] = ["Factory floor induction and shift arrangements"],
        [InductionArea.TbuOperations] = ["TBU site induction"],
        [InductionArea.StoresOperations] = ["Stores procedures and stock-issue briefing"],
        [InductionArea.Engineering] = ["Engineering workshop induction and permit-to-work"],
        [InductionArea.Security] = ["Access card", "Security induction"],
        [InductionArea.HealthAndSafety] = ["Safety training", "PPE issue and emergency procedures"],
        [InductionArea.Administration] = ["Office, transport and canteen arrangements"]
    };

    /// <summary>"Email setup\n  AD account creation\n\n" → ["Email setup", "AD account creation"].</summary>
    public static IReadOnlyList<string> TaskLines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
