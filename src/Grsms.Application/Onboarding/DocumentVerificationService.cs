using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Users;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using static Grsms.Application.Common.Text;

namespace Grsms.Application.Onboarding;

/// <summary>
/// The new-hire document checklist (<see cref="EmployeeDocument"/>; Phase 18, reworked in Phase 27): the new employee uploads each
/// document as soon as they have it (or declares an optional one not applicable); the Recruitment Officer reviews each one on its
/// own. After every change the onboarding's stage is worked out again (<see cref="OnboardingProgressService.RefreshAsync"/>).
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * Upload / replace / remove a file, declare "not applicable" : the new employee until activation, never on a verified document
///                                                                 or one under review; the RO (onboarding.manage) on their behalf.
///   * Start review / verify / reject                             : onboarding.verify-documents (Recruitment Officers by default),
///                                                                 until activation, never on their own onboarding.
///   * Change the requirements (expatriate, education certificates): onboarding.manage.
///   * See the documents and open the files                       : the new employee (their own) and onboarding.view (RO and HR
///                                                                 Manager by default). Not the HOD: these are personal documents.
/// Replaced and removed files are never deleted: they move to the document's history (OwnerType "EmployeeDocumentReplaced").
/// Every step, and every time someone opens a file, is an <see cref="EmployeeDocumentEvent"/> (user, time, IP, status before and
/// after); the audit trail records the row changes too.
/// </summary>
public class DocumentVerificationService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    IRepository<EmployeeDocumentEvent> events,
    IRepository<OnboardingDocumentType> types,
    AttachmentService files,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    AccessScope scope,
    OnboardingProgressService progress,
    WorkflowMailer mailer,
    IAppLinks links)
{
    private const string ConflictMessage = "Someone else changed this document at the same moment. Please reload the page and try again.";

    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool MayView => me.HasPermission(Permissions.ViewOnboarding);
    private bool MayManage => me.HasPermission(Permissions.ManageOnboarding);
    private bool MayVerify => me.HasPermission(Permissions.VerifyOnboardingDocuments);

    /// <summary>The checklist for a new onboarding: one "Not submitted" row per active document type, with its requirement applied.</summary>
    /// <param name="requiredEducation">Codes of the "chosen per hire" types the RO ticked.</param>
    public static IEnumerable<EmployeeDocument> NewChecklist(OnboardingRecord o, IEnumerable<OnboardingDocumentType> all, IReadOnlyCollection<string> requiredEducation)
    {
        foreach (var t in all.Where(t => t.IsActive && !t.IsLegacy).OrderBy(t => t.SortOrder))
        {
            var d = new EmployeeDocument
            {
                OnboardingRecordId = o.Id, EmployeeUserId = o.EmployeeUserId, DocumentTypeCode = t.Code,
                IsRequired = t.Requirement == DocumentRequirement.ChosenPerHire && requiredEducation.Contains(t.Code, StringComparer.OrdinalIgnoreCase)
            };
            OnboardingWorkflow.ApplyRequirement(t, o, d, hasFiles: false);
            yield return d;
        }
    }

    // ===================================================================== Reading

    /// <summary>The new employee's own checklist, for one of their applications (null if there is none or it is not theirs).</summary>
    public async Task<DocumentChecklistDto?> GetMineAsync(int applicationId, CancellationToken ct = default)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId || o.Status == OnboardingStatus.Cancelled) return null;
        return await ChecklistAsync(o, ct);
    }

    /// <summary>The checklist for staff with onboarding.view (null for anyone else, and for their own onboarding).</summary>
    public async Task<DocumentChecklistDto?> GetForHrAsync(int onboardingId, CancellationToken ct = default)
    {
        if (!MayView) return null;
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        return o is null || o.EmployeeUserId == me.UserId ? null : await ChecklistAsync(o, ct);
    }

    /// <summary>The same, from a snapshot the caller already built (the onboarding page builds it once).</summary>
    public async Task<DocumentChecklistDto?> GetForHrAsync(OnboardingProgressService.Snapshot snap, CancellationToken ct = default) =>
        !MayView || snap.Onboarding.EmployeeUserId == me.UserId ? null : await ChecklistAsync(snap.Onboarding, ct, snap);

    private async Task<DocumentChecklistDto> ChecklistAsync(OnboardingRecord o, CancellationToken ct, OnboardingProgressService.Snapshot? snap = null)
    {
        snap ??= await progress.SnapshotAsync(o, ct);
        var ids = snap.Documents.Select(x => x.Document.Id).ToList();
        var history = (await events.ListAsync(e => ids.Contains(e.EmployeeDocumentId), ct))
            .Where(e => e.Action != DocumentEventAction.Viewed || o.EmployeeUserId != me.UserId) // the new employee does not see who opened their files
            .GroupBy(e => e.EmployeeDocumentId).ToDictionary(g => g.Key, g => g.OrderBy(e => e.ActionAt).ThenBy(e => e.Id).ToList());
        var names = await users.GetDisplayNamesAsync(snap.Documents.SelectMany(x => new[] { x.Document.VerifiedByUserId, x.Document.ReviewStartedByUserId })
            .OfType<string>().Distinct(), ct);
        string? Name(string? uid) => uid is null ? null : names.GetValueOrDefault(uid);
        var canEdit = CanEdit(o);
        var canReview = CanReview(o);
        var own = o.EmployeeUserId == me.UserId;

        var dtos = new List<EmployeeDocumentDto>();
        foreach (var (d, t, _) in snap.Documents)
        {
            var current = snap.CurrentFiles[d.Id].Select(ToFile).ToList();
            var replaced = snap.ReplacedFiles[d.Id].Select(ToFile).ToList();
            var reviewable = canReview && (OnboardingWorkflow.AwaitsReview(d.Status) || d.Status == EmployeeDocumentStatus.NotApplicable && d.VerifiedAt is null);
            dtos.Add(new EmployeeDocumentDto(d.Id, t.Code, t.Name, t.Description, t.Group, d.IsRequired, d.IsApplicable, d.Status, d.SubmittedAt,
                d.NotApplicableReason, d.ReviewStartedAt, own ? null : Name(d.ReviewStartedByUserId), d.VerifiedAt, own ? null : Name(d.VerifiedByUserId),
                d.RejectionReasonCode, d.RejectionReason, d.RejectionCount, t.MinFiles, OnboardingWorkflow.FileTypesFor(t), current, replaced,
                (history.GetValueOrDefault(d.Id) ?? []).Select(e => new DocumentEventDto(e.Action, e.FromStatus, e.ToStatus, e.FileName, e.RejectionReason,
                    e.Comments, own && e.ActorUserId != o.EmployeeUserId ? "HR" : e.ActorName, e.ActionAt)).ToList(),
                canEdit && OnboardingWorkflow.EmployeeMayChange(d.Status),
                reviewable, canReview && d.Status is EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.Resubmitted));
        }
        return new DocumentChecklistDto(o.Id, o.Number, o.JobApplicationId, o.EmployeeName, o.JobTitle, o.Status, dtos, o.LetterIssued,
            o.LetterAcknowledged, snap.PersonalRecordSigned, o.IsExpatriate, o.DocumentsDueDate, o.CorrectionMessage, snap.Readiness.ForEmployee, snap.Percent);
    }

    // ===================================================================== The new employee (or the RO on their behalf)

    /// <summary>
    /// Adds a file to a document, or replaces one of its files (<paramref name="replacesAttachmentId"/>; the old file moves to the
    /// history). The document goes to the RO at once: "Submitted", or "Resubmitted, awaiting verification" after a rejection.
    /// </summary>
    public async Task<Result<int>> UploadAsync(int documentId, string fileName, Stream content, CancellationToken ct = default, int? replacesAttachmentId = null)
    {
        var (d, o, t, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure<int>(error!);
        var existing = await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct);
        if (replacesAttachmentId is int old && existing.All(f => f.Id != old)) return Result.Failure<int>("The file to replace was not found.");
        if (replacesAttachmentId is null && existing.Count >= OnboardingWorkflow.MaxFilesPerDocument)
            return Result.Failure<int>($"A document can have at most {OnboardingWorkflow.MaxFilesPerDocument} files. Replace or remove one first.");

        var added = await files.AddAsync(AttachmentOwners.EmployeeDocument, d.Id, AttachmentCategory.NewHireDocument, fileName, content, ct,
            OnboardingWorkflow.FileTypesFor(t!));
        if (!added.Succeeded) return added;

        var from = d.Status;
        string? replacedName = null;
        if (replacesAttachmentId is int replaced)
        {
            replacedName = existing.First(f => f.Id == replaced).FileName;
            await files.MoveAsync(AttachmentOwners.EmployeeDocument, d.Id, replaced, AttachmentOwners.EmployeeDocumentReplaced, ct);
        }
        d.Status = from is EmployeeDocumentStatus.Rejected or EmployeeDocumentStatus.Resubmitted ? EmployeeDocumentStatus.Resubmitted : EmployeeDocumentStatus.Submitted;
        d.SubmittedAt = DateTime.UtcNow;
        d.NotApplicableReason = null;
        d.VerifiedAt = null;
        d.VerifiedByUserId = null;
        d.ReviewStartedAt = null;
        d.ReviewStartedByUserId = null;
        var action = from == EmployeeDocumentStatus.Rejected ? DocumentEventAction.Resubmitted
            : replacesAttachmentId is null ? DocumentEventAction.Uploaded : DocumentEventAction.Replaced;
        Log(d, action, from, added.Value, Path.GetFileName(fileName), comments: replacedName is null ? null : $"Replaces {replacedName}");
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return Result.Failure<int>([.. saved.Errors]);
        await progress.RefreshAsync(o!.Id, ct);
        return added;
    }

    /// <summary>Removes a file (it moves to the history). A document left without files goes back to "Not submitted".</summary>
    public async Task<Result> RemoveFileAsync(int documentId, int attachmentId, CancellationToken ct = default)
    {
        var (d, o, _, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        var current = await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct);
        var file = current.FirstOrDefault(f => f.Id == attachmentId);
        if (file is null) return Result.Failure("File not found.");
        await files.MoveAsync(AttachmentOwners.EmployeeDocument, d.Id, attachmentId, AttachmentOwners.EmployeeDocumentReplaced, ct);
        var from = d.Status;
        if (current.Count == 1 && OnboardingWorkflow.AwaitsReview(d.Status))
        {
            d.Status = EmployeeDocumentStatus.Pending;
            d.SubmittedAt = null;
        }
        Log(d, DocumentEventAction.FileRemoved, from, attachmentId, file.FileName);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o!.Id, ct);
        return saved;
    }

    /// <summary>Declares a document that is not required (e.g. marriage certificate) not applicable, with the reason.</summary>
    public async Task<Result> MarkNotApplicableAsync(int documentId, string? reason, CancellationToken ct = default)
    {
        var (d, o, t, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (d.IsRequired) return Result.Failure($"The {t!.Name.ToLowerInvariant()} is required; it cannot be marked not applicable.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 3) return Result.Failure("Say briefly why it does not apply (e.g. \"Not married\", \"Ghanaian citizen\").");
        if (reason.Length > 500) return Result.Failure("Keep the reason under 500 characters.");
        if ((await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct)).Count > 0)
            return Result.Failure("Remove the uploaded file(s) first, or keep the document as uploaded.");

        var from = d.Status;
        d.Status = EmployeeDocumentStatus.NotApplicable;
        d.NotApplicableReason = reason;
        d.SubmittedAt = DateTime.UtcNow;
        d.RejectionReason = null;
        d.RejectionReasonCode = null;
        d.VerifiedAt = null;
        d.VerifiedByUserId = null;
        Log(d, DocumentEventAction.MarkedNotApplicable, from, comments: reason);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o!.Id, ct);
        return saved;
    }

    // ===================================================================== Verification (Recruitment Officer)

    /// <summary>The RO starts checking a document: "Under review". The new employee can no longer change it until it is decided.</summary>
    public async Task<Result> StartReviewAsync(int documentId, CancellationToken ct = default)
    {
        var (d, _, _, error) = await LoadForReviewAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (d.Status is not (EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.Resubmitted))
            return Result.Failure(d.Status == EmployeeDocumentStatus.UnderReview ? "This document is already under review." : "Only uploaded documents can be reviewed.");
        var from = d.Status;
        d.Status = EmployeeDocumentStatus.UnderReview;
        d.ReviewStartedAt = DateTime.UtcNow;
        d.ReviewStartedByUserId = MyId;
        Log(d, DocumentEventAction.ReviewStarted, from);
        return await SaveAsync(ct);
    }

    /// <summary>The RO verifies an uploaded document against the original, or accepts a "not applicable" declaration.</summary>
    public async Task<Result> VerifyAsync(int documentId, CancellationToken ct = default)
    {
        var (d, o, t, error) = await LoadForReviewAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        var from = d.Status;
        if (OnboardingWorkflow.AwaitsReview(d.Status))
        {
            var count = (await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct)).Count;
            if (count < t!.MinFiles)
                return Result.Failure($"The {t.Name.ToLowerInvariant()} needs {t.MinFiles} files ({count} uploaded). Reject it with \"Missing pages\" or wait for the rest.");
            d.Status = EmployeeDocumentStatus.Verified;
        }
        else if (d.Status != EmployeeDocumentStatus.NotApplicable || d.VerifiedAt is not null)
            return Result.Failure("Only uploaded documents (or \"not applicable\" declarations) can be verified.");
        d.VerifiedAt = DateTime.UtcNow;
        d.VerifiedByUserId = MyId;
        d.RejectionReason = null;
        d.RejectionReasonCode = null;
        Log(d, DocumentEventAction.Verified, from);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o!.Id, ct);
        return saved;
    }

    /// <summary>
    /// The RO rejects a document (or a "not applicable" declaration) with a reason from the list (Other needs the RO's own words).
    /// The new employee is emailed at once and can upload a replacement.
    /// </summary>
    public async Task<Result> RejectAsync(int documentId, DocumentRejectionReason? reasonCode, string? note, CancellationToken ct = default)
    {
        var (d, o, t, error) = await LoadForReviewAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (!OnboardingWorkflow.AwaitsReview(d.Status) && d.Status != EmployeeDocumentStatus.NotApplicable)
            return Result.Failure("Only uploaded documents (or \"not applicable\" declarations) can be rejected.");
        note = Clean(note);
        if (reasonCode is not { } code || !Enum.IsDefined(code)) return Result.Failure("Choose the reason for rejecting the document.");
        if (code == DocumentRejectionReason.Other && (note is null || note.Length < 5))
            return Result.Failure("Say why the document is rejected, so the new employee knows what to upload.");
        if (note?.Length > 900) return Result.Failure("Keep the note under 900 characters.");

        var from = d.Status;
        d.Status = EmployeeDocumentStatus.Rejected;
        d.RejectionReasonCode = code;
        d.RejectionReason = code == DocumentRejectionReason.Other ? note : note is null ? EnumText.Label(code) : $"{EnumText.Label(code)}: {note}";
        d.RejectionCount++;
        d.VerifiedAt = DateTime.UtcNow;
        d.VerifiedByUserId = MyId;
        d.ReviewStartedAt = null;
        d.ReviewStartedByUserId = null;
        Log(d, DocumentEventAction.Rejected, from, reason: code, comments: note);
        var saved = await SaveAsync(ct);
        if (!saved.Succeeded) return saved;

        await mailer.TemplateToUserAsync(o!.EmployeeUserId, EmailTemplateCatalogue.OnboardingDocumentRejected,
            new Dictionary<string, string?> { ["Document"] = t!.Name.ToLowerInvariant(), ["Reason"] = d.RejectionReason },
            links.MyApplication(o.JobApplicationId), ct: ct);
        await progress.RefreshAsync(o.Id, ct);
        return saved;
    }

    // ===================================================================== Requirements (Recruitment Officer)

    /// <summary>
    /// The RO changes whether the new hire is an expatriate and which education certificates they must provide. Documents already
    /// verified stay verified; a document that no longer applies stays visible while it has files.
    /// </summary>
    public async Task<Result> SetRequirementsAsync(int onboardingId, bool isExpatriate, IReadOnlyCollection<string>? requiredEducation, CancellationToken ct = default)
    {
        if (!MayManage) return Result.Failure("Only a Recruitment Officer can change the documents required.");
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        if (o is null || o.EmployeeUserId == MyId) return Result.Failure("Onboarding not found.");
        if (!OnboardingWorkflow.BeforeActivation(o.Status)) return Result.Failure("The documents can no longer be changed: the employee is activated.");
        var chosen = (requiredEducation ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byCode = (await types.ListAsync(ct: ct)).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        if (chosen.Any(c => !byCode.TryGetValue(c, out var t) || t.Requirement != DocumentRequirement.ChosenPerHire || !t.IsActive))
            return Result.Failure("Choose education certificates from the list.");

        var snap = await progress.SnapshotAsync(o, ct); // one query for every file (audit P2)
        var docs = snap.Documents.Select(x => x.Document).ToList();
        var withFiles = docs.Where(d => snap.CurrentFiles[d.Id].Any()).Select(d => d.Id).ToHashSet();

        // Education certificates ticked now but missing from an older checklist are added.
        foreach (var code in chosen.Where(c => docs.All(d => !string.Equals(d.DocumentTypeCode, c, StringComparison.OrdinalIgnoreCase))))
            documents.Add(new EmployeeDocument { OnboardingRecordId = o.Id, EmployeeUserId = o.EmployeeUserId, DocumentTypeCode = byCode[code].Code, IsRequired = true });

        var changes = new List<string>();
        if (o.IsExpatriate != isExpatriate) changes.Add(isExpatriate ? "Marked as an expatriate" : "Marked as Ghanaian");
        o.IsExpatriate = isExpatriate;
        foreach (var d in docs)
        {
            if (!byCode.TryGetValue(d.DocumentTypeCode, out var t)) continue;
            var before = (d.IsRequired, d.IsApplicable);
            if (t.Requirement == DocumentRequirement.ChosenPerHire && !t.IsLegacy) d.IsRequired = chosen.Contains(t.Code);
            OnboardingWorkflow.ApplyRequirement(t, o, d, withFiles.Contains(d.Id));
            if (before == (d.IsRequired, d.IsApplicable)) continue;
            d.Version = Guid.NewGuid();
            if (t.Requirement == DocumentRequirement.ChosenPerHire) changes.Add($"{t.Name}: {(d.IsRequired ? "required" : "not required")}");
            Log(d, DocumentEventAction.RequirementChanged, d.Status,
                comments: !d.IsApplicable ? "No longer applies" : d.IsRequired ? "Now required" : "No longer required");
        }
        changes.AddRange(chosen.Where(c => docs.All(d => !string.Equals(d.DocumentTypeCode, c, StringComparison.OrdinalIgnoreCase)))
            .Select(c => $"{byCode[c].Name}: required"));
        if (changes.Count == 0) return Result.Success();
        o.RecordStep(OnboardingAction.RequirementsChanged, o.Status, MyId, scope.UserName, string.Join("; ", changes), DateTime.UtcNow);
        var saved = await SaveAsync(ct);
        if (saved.Succeeded) await progress.RefreshAsync(o.Id, ct);
        return saved;
    }

    /// <summary>The "chosen per hire" types (education certificates) to tick on the start form, with the ones already ticked.</summary>
    public async Task<IReadOnlyList<DocumentChoiceDto>> EducationChoicesAsync(IReadOnlyCollection<string>? ticked, CancellationToken ct = default)
    {
        var required = (ticked ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Choices(await types.ListAsync(t => t.IsActive && !t.IsLegacy && t.Requirement == DocumentRequirement.ChosenPerHire, ct), required);
    }

    /// <summary>The same for an existing onboarding, from a snapshot the caller already built (no query).</summary>
    public static IReadOnlyList<DocumentChoiceDto> EducationChoices(OnboardingProgressService.Snapshot snap) =>
        Choices(snap.Types.Values.Where(t => t.IsActive && !t.IsLegacy && t.Requirement == DocumentRequirement.ChosenPerHire),
            snap.Documents.Where(x => x.Document.IsRequired).Select(x => x.Document.DocumentTypeCode).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<DocumentChoiceDto> Choices(IEnumerable<OnboardingDocumentType> types, IReadOnlySet<string> required) =>
        types.OrderBy(t => t.SortOrder).Select(t => new DocumentChoiceDto(t.Code, t.Name, required.Contains(t.Code))).ToList();

    // ===================================================================== Files

    /// <summary>
    /// Opens a current or replaced file for the new employee (their own) or staff with onboarding.view; null for anyone else.
    /// Every opening is recorded (who, when, IP address).
    /// </summary>
    public async Task<FileDownload?> OpenFileAsync(int documentId, int attachmentId, CancellationToken ct = default)
    {
        var d = await documents.GetByIdAsync(documentId, ct);
        if (d is null || me.UserId is null) return null;
        var own = d.EmployeeUserId == me.UserId;
        if (!own && !MayView) return null;
        var file = await files.OpenAsync(AttachmentOwners.EmployeeDocument, d.Id, attachmentId, ct)
                   ?? await files.OpenAsync(AttachmentOwners.EmployeeDocumentReplaced, d.Id, attachmentId, ct);
        if (file is null) return null;
        Log(d, DocumentEventAction.Viewed, d.Status, attachmentId, file.FileName);
        await unitOfWork.SaveChangesAsync(ct);
        return file;
    }

    // ===================================================================== Helpers

    /// <summary>The new employee until activation; the RO (onboarding.manage, not on their own onboarding) on their behalf.</summary>
    private bool CanEdit(OnboardingRecord o)
    {
        if (o.EmployeeUserId == me.UserId) return OnboardingWorkflow.EmployeeMayEditDocuments(o.Status);
        return MayManage && OnboardingWorkflow.HrMayEditDocuments(o.Status);
    }

    private bool CanReview(OnboardingRecord o) =>
        MayVerify && o.EmployeeUserId != me.UserId && OnboardingWorkflow.DocumentsMayBeReviewed(o.Status);

    private async Task<(EmployeeDocument? Document, OnboardingRecord? Onboarding, OnboardingDocumentType? Type, string? Error)> LoadForEditAsync(int documentId, CancellationToken ct)
    {
        var d = await documents.GetByIdAsync(documentId, ct);
        var o = d is null ? null : await onboardings.GetByIdAsync(d.OnboardingRecordId, ct);
        if (d is null || o is null || (o.EmployeeUserId != me.UserId && !MayManage)) return (null, null, null, "Document not found.");
        if (!CanEdit(o))
            return (null, null, null, o.EmployeeUserId == me.UserId
                ? "Your onboarding is complete, so your documents can no longer be changed."
                : "Documents can no longer be changed at this stage.");
        if (d.Status == EmployeeDocumentStatus.Verified) return (null, null, null, "This document has been verified and is locked.");
        if (d.Status == EmployeeDocumentStatus.UnderReview) return (null, null, null, "HR is reviewing this document right now; you can change it if HR rejects it.");
        var t = (await types.ListAsync(x => x.Code == d.DocumentTypeCode, ct)).FirstOrDefault();
        if (t is null) return (null, null, null, "This document type no longer exists.");
        return (d, o, t, null);
    }

    private async Task<(EmployeeDocument? Document, OnboardingRecord? Onboarding, OnboardingDocumentType? Type, string? Error)> LoadForReviewAsync(int documentId, CancellationToken ct)
    {
        if (!MayVerify) return (null, null, null, "Only a Recruitment Officer can verify documents.");
        var d = await documents.GetByIdAsync(documentId, ct);
        var o = d is null ? null : await onboardings.GetByIdAsync(d.OnboardingRecordId, ct);
        if (d is null || o is null) return (null, null, null, "Document not found.");
        if (o.EmployeeUserId == MyId) return (null, null, null, "These are your own documents.");
        if (!OnboardingWorkflow.DocumentsMayBeReviewed(o.Status)) return (null, null, null, "The employee is activated; documents can no longer be reviewed.");
        var t = (await types.ListAsync(x => x.Code == d.DocumentTypeCode, ct)).FirstOrDefault()
                ?? new OnboardingDocumentType { Code = d.DocumentTypeCode, Name = d.DocumentTypeCode, MinFiles = 1 };
        return (d, o, t, null);
    }

    private void Log(EmployeeDocument d, DocumentEventAction action, EmployeeDocumentStatus from, int? attachmentId = null, string? fileName = null,
        DocumentRejectionReason? reason = null, string? comments = null)
    {
        if (action != DocumentEventAction.Viewed) d.Version = Guid.NewGuid();
        events.Add(new EmployeeDocumentEvent
        {
            EmployeeDocumentId = d.Id, OnboardingRecordId = d.OnboardingRecordId, Action = action, FromStatus = from, ToStatus = d.Status,
            AttachmentId = attachmentId, FileName = fileName?.Length > 200 ? fileName[..200] : fileName, RejectionReason = reason, Comments = comments,
            ActorUserId = MyId, ActorName = scope.UserName, IpAddress = me.IpAddress, ActionAt = DateTime.UtcNow
        });
    }

    private static EmployeeDocumentFileDto ToFile(Attachment a) => new(a.Id, a.OriginalFileName, a.SizeBytes, a.CreatedAt);

    private Task<Result> SaveAsync(CancellationToken ct) => unitOfWork.TrySaveAsync(ConflictMessage, ct);

}
