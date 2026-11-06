using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Users;
using Grsms.Domain.Documents;
using Grsms.Domain.Onboarding;

namespace Grsms.Application.Onboarding;

/// <summary>
/// Phase 18: the New Hire Administrative Records checklist (<see cref="EmployeeDocument"/>): the new employee uploads each
/// document (or declares an optional one not applicable), HR verifies or rejects it. The stage moves themselves
/// (submit, return to the employee, documents verified) are in <see cref="OnboardingService"/>.
///
/// WHO MAY DO WHAT (checked here, on the server):
///   * Upload / remove a file, declare "not applicable" : the new employee (while it is their turn: Draft or Awaiting
///                                                         employee submission), or HR on their behalf (until HR approval).
///                                                         Never on a verified document.
///   * Verify / reject a document                        : HR (Recruitment Officer or HR Manager) at "Awaiting document
///                                                         verification", never on their own onboarding.
///   * See the documents and download the files          : the new employee (their own) and HR. Not the HOD: these are
///                                                         personal documents (Ghana Card, birth and medical records).
/// Files are stored by <see cref="AttachmentService"/>: extension and real content checked, random name outside the web
/// root, SHA-256 recorded. Every change is in the audit trail.
/// </summary>
public class DocumentVerificationService(
    IRepository<OnboardingRecord> onboardings,
    IRepository<EmployeeDocument> documents,
    AttachmentService files,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    ICurrentUserService me,
    IRepository<EmployeePersonalRecord> personalRecords)
{
    private string MyId => me.UserId ?? throw new InvalidOperationException("No signed-in user.");
    private bool IsHr => OnboardingWorkflow.HrRoles.Any(me.IsInRole);

    /// <summary>True when HR has finished with the document: verified, or "not applicable" accepted.</summary>
    public static bool IsSettled(EmployeeDocument d) =>
        d.Status == EmployeeDocumentStatus.Verified || (d.Status == EmployeeDocumentStatus.NotApplicable && d.VerifiedAt is not null);

    /// <summary>The empty checklist for a new onboarding: one Pending row per document type.</summary>
    public static IEnumerable<EmployeeDocument> NewChecklist(OnboardingRecord o) =>
        Enum.GetValues<EmployeeDocumentType>().Select(t => new EmployeeDocument
        {
            OnboardingRecordId = o.Id, EmployeeUserId = o.EmployeeUserId, DocumentType = t,
            IsRequired = !OnboardingWorkflow.OptionalDocuments.Contains(t)
        });

    // ===================================================================== Reading

    /// <summary>The new employee's own checklist, for one of their applications (null if there is none or it is not theirs).</summary>
    public async Task<DocumentChecklistDto?> GetMineAsync(int applicationId, CancellationToken ct = default)
    {
        var o = (await onboardings.ListAsync(x => x.JobApplicationId == applicationId, ct)).FirstOrDefault();
        if (o is null || o.EmployeeUserId != me.UserId || o.Status == OnboardingStatus.Cancelled) return null;
        return await ChecklistAsync(o, ct);
    }

    /// <summary>The checklist for HR (null for anyone else).</summary>
    public async Task<DocumentChecklistDto?> GetForHrAsync(int onboardingId, CancellationToken ct = default)
    {
        if (!IsHr) return null;
        var o = await onboardings.GetByIdAsync(onboardingId, ct);
        return o is null || o.EmployeeUserId == me.UserId ? null : await ChecklistAsync(o, ct);
    }

    private async Task<DocumentChecklistDto> ChecklistAsync(OnboardingRecord o, CancellationToken ct)
    {
        var list = await LoadAsync(o.Id, ct);
        var names = await users.GetDisplayNamesAsync(list.Select(d => d.VerifiedByUserId).OfType<string>().Distinct(), ct);
        var canEdit = CanEdit(o);
        var canReview = CanReview(o);
        var dtos = new List<EmployeeDocumentDto>();
        foreach (var d in list)
        {
            var docFiles = (await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct))
                .OrderBy(f => f.UploadedAt).Select(f => new EmployeeDocumentFileDto(f.Id, f.FileName, f.SizeBytes, f.UploadedAt)).ToList();
            dtos.Add(new EmployeeDocumentDto(d.Id, d.DocumentType, d.IsRequired, d.Status, d.SubmittedAt, d.NotApplicableReason, d.VerifiedAt,
                d.VerifiedByUserId is null ? null : names.GetValueOrDefault(d.VerifiedByUserId), d.RejectionReason, docFiles,
                canEdit && d.Status != EmployeeDocumentStatus.Verified,
                canReview && d.Status is EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.NotApplicable && !IsSettled(d)));
        }
        return new DocumentChecklistDto(o.Id, o.Number, o.JobApplicationId, o.EmployeeName, o.JobTitle, o.Status, dtos, o.LetterAcknowledged,
            SubmitProblem(o, list, await PersonalRecordSignedAsync(o.Id, ct)));
    }

    /// <summary>Why the onboarding cannot be submitted to HR yet, or null when it can (used by <see cref="OnboardingService"/>).</summary>
    public static string? SubmitProblem(OnboardingRecord o, IReadOnlyCollection<EmployeeDocument> list, bool personalRecordSigned)
    {
        if (o.Status != OnboardingStatus.AwaitingEmployeeSubmission)
            return o.Status == OnboardingStatus.Draft ? "HR has not issued the appointment letter yet." : "The documents have already been submitted.";
        if (!o.LetterAcknowledged) return "Sign the appointment letter first.";
        if (!personalRecordSigned) return "Fill in your personal record and sign the declaration.";
        var missing = list.Where(d => !OnboardingWorkflow.ReadyToSubmit(d.Status)).Select(d => EnumText.Label(d.DocumentType)).ToList();
        return missing.Count == 0 ? null
            : $"Upload {(missing.Count == 1 ? "this document" : "these documents")} (or, where allowed, mark it not applicable): {string.Join(", ", missing)}.";
    }

    /// <summary>Why HR cannot mark the documents verified yet, or null when everything is settled.</summary>
    public static string? VerifiedProblem(IReadOnlyCollection<EmployeeDocument> list)
    {
        var rejected = list.Count(d => d.Status == EmployeeDocumentStatus.Rejected);
        if (rejected > 0) return $"{rejected} document(s) were rejected. Send them back to the new employee first.";
        var open = list.Count(d => !IsSettled(d));
        return open > 0 ? $"{open} document(s) still need to be verified." : null;
    }

    /// <summary>Phase 19: true when the personal record exists and its current version is signed.</summary>
    public async Task<bool> PersonalRecordSignedAsync(int onboardingId, CancellationToken ct = default) =>
        (await personalRecords.ListAsync(r => r.OnboardingRecordId == onboardingId, ct)).FirstOrDefault()?.DeclaredAt is not null;

    public Task<IReadOnlyList<EmployeeDocument>> LoadAsync(int onboardingId, CancellationToken ct = default) =>
        LoadSortedAsync(onboardingId, ct);

    private async Task<IReadOnlyList<EmployeeDocument>> LoadSortedAsync(int onboardingId, CancellationToken ct) =>
        (await documents.ListAsync(d => d.OnboardingRecordId == onboardingId, ct)).OrderBy(d => d.DocumentType).ToList();

    // ===================================================================== The new employee (or HR on their behalf)

    /// <summary>Adds a file (PDF, JPG or PNG) to a document. The document becomes "Submitted" (waiting for HR).</summary>
    public async Task<Result<int>> UploadAsync(int documentId, string fileName, Stream content, CancellationToken ct = default)
    {
        var (d, o, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure<int>(error!);
        var existing = await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct);
        if (existing.Count >= OnboardingWorkflow.MaxFilesPerDocument)
            return Result.Failure<int>($"A document can have at most {OnboardingWorkflow.MaxFilesPerDocument} files. Remove one first.");

        var added = await files.AddAsync(AttachmentOwners.EmployeeDocument, d.Id, AttachmentCategory.NewHireDocument, fileName, content, ct,
            OnboardingWorkflow.DocumentFileTypes);
        if (!added.Succeeded) return added;

        d.Status = EmployeeDocumentStatus.Submitted;
        d.SubmittedAt = DateTime.UtcNow;
        d.NotApplicableReason = null;
        d.RejectionReason = null;
        d.VerifiedAt = null;
        d.VerifiedByUserId = null;
        await unitOfWork.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Removes a file. A document left without files goes back to "Pending".</summary>
    public async Task<Result> RemoveFileAsync(int documentId, int attachmentId, CancellationToken ct = default)
    {
        var (d, _, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        var removed = await files.RemoveAsync(AttachmentOwners.EmployeeDocument, d.Id, attachmentId, ct);
        if (!removed.Succeeded) return removed;
        if ((await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct)).Count == 0 && d.Status == EmployeeDocumentStatus.Submitted)
        {
            d.Status = EmployeeDocumentStatus.Pending;
            d.SubmittedAt = null;
            await unitOfWork.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    /// <summary>Declares an optional document (e.g. marriage certificate) not applicable, with the reason.</summary>
    public async Task<Result> MarkNotApplicableAsync(int documentId, string? reason, CancellationToken ct = default)
    {
        var (d, _, error) = await LoadForEditAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (d.IsRequired) return Result.Failure($"The {EnumText.Label(d.DocumentType).ToLowerInvariant()} is required; it cannot be marked not applicable.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 3) return Result.Failure("Say briefly why it does not apply (e.g. \"Not married\", \"Ghanaian citizen\").");
        if (reason.Length > 500) return Result.Failure("Keep the reason under 500 characters.");
        if ((await files.ListAsync(AttachmentOwners.EmployeeDocument, d.Id, ct)).Count > 0)
            return Result.Failure("Remove the uploaded file(s) first, or keep the document as uploaded.");

        d.Status = EmployeeDocumentStatus.NotApplicable;
        d.NotApplicableReason = reason;
        d.SubmittedAt = DateTime.UtcNow;
        d.RejectionReason = null;
        d.VerifiedAt = null;
        d.VerifiedByUserId = null;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ===================================================================== HR verification

    /// <summary>HR verifies an uploaded document against the original, or accepts a "not applicable" declaration.</summary>
    public async Task<Result> VerifyAsync(int documentId, CancellationToken ct = default)
    {
        var (d, _, error) = await LoadForReviewAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (d.Status == EmployeeDocumentStatus.Submitted)
            d.Status = EmployeeDocumentStatus.Verified;
        else if (d.Status != EmployeeDocumentStatus.NotApplicable)
            return Result.Failure("Only uploaded documents (or \"not applicable\" declarations) can be verified.");
        d.VerifiedAt = DateTime.UtcNow;
        d.VerifiedByUserId = MyId;
        d.RejectionReason = null;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>HR rejects a document (or a "not applicable" declaration). The reason is shown to the new employee.</summary>
    public async Task<Result> RejectAsync(int documentId, string? reason, CancellationToken ct = default)
    {
        var (d, _, error) = await LoadForReviewAsync(documentId, ct);
        if (d is null) return Result.Failure(error!);
        if (d.Status is not (EmployeeDocumentStatus.Submitted or EmployeeDocumentStatus.NotApplicable))
            return Result.Failure("Only uploaded documents (or \"not applicable\" declarations) can be rejected.");
        reason = Clean(reason);
        if (reason is null || reason.Length < 5) return Result.Failure("Say why the document is rejected, so the new employee knows what to upload.");
        if (reason.Length > 1000) return Result.Failure("Keep the reason under 1,000 characters.");

        d.Status = EmployeeDocumentStatus.Rejected;
        d.RejectionReason = reason;
        d.VerifiedAt = DateTime.UtcNow;
        d.VerifiedByUserId = MyId;
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ===================================================================== Files

    /// <summary>Opens a file for the new employee (their own) or HR; null for anyone else.</summary>
    public async Task<FileDownload?> OpenFileAsync(int documentId, int attachmentId, CancellationToken ct = default)
    {
        var d = await documents.GetByIdAsync(documentId, ct);
        if (d is null) return null;
        var own = d.EmployeeUserId == me.UserId;
        if (!own && !IsHr) return null;
        return await files.OpenAsync(AttachmentOwners.EmployeeDocument, d.Id, attachmentId, ct);
    }

    // ===================================================================== Helpers

    /// <summary>The new employee while it is their turn; HR (not on their own onboarding) until HR approval.</summary>
    private bool CanEdit(OnboardingRecord o)
    {
        if (o.EmployeeUserId == me.UserId) return OnboardingWorkflow.EmployeeMayEditDocuments(o.Status);
        return IsHr && OnboardingWorkflow.HrMayEditDocuments(o.Status);
    }

    private bool CanReview(OnboardingRecord o) =>
        IsHr && o.EmployeeUserId != me.UserId && o.Status == OnboardingStatus.AwaitingDocumentVerification;

    private async Task<(EmployeeDocument? Document, OnboardingRecord? Onboarding, string? Error)> LoadForEditAsync(int documentId, CancellationToken ct)
    {
        var d = await documents.GetByIdAsync(documentId, ct);
        var o = d is null ? null : await onboardings.GetByIdAsync(d.OnboardingRecordId, ct);
        if (d is null || o is null || (o.EmployeeUserId != me.UserId && !IsHr)) return (null, null, "Document not found.");
        if (!CanEdit(o))
            return (null, null, o.EmployeeUserId == me.UserId
                ? "Your documents are with HR now; you can change them only if HR sends them back to you."
                : "Documents can no longer be changed at this stage.");
        if (d.Status == EmployeeDocumentStatus.Verified) return (null, null, "This document has been verified and is locked.");
        return (d, o, null);
    }

    private async Task<(EmployeeDocument? Document, OnboardingRecord? Onboarding, string? Error)> LoadForReviewAsync(int documentId, CancellationToken ct)
    {
        if (!IsHr) return (null, null, "Only a Recruitment Officer or HR Manager can verify documents.");
        var d = await documents.GetByIdAsync(documentId, ct);
        var o = d is null ? null : await onboardings.GetByIdAsync(d.OnboardingRecordId, ct);
        if (d is null || o is null) return (null, null, "Document not found.");
        if (o.EmployeeUserId == MyId) return (null, null, "These are your own documents.");
        if (o.Status != OnboardingStatus.AwaitingDocumentVerification)
            return (null, null, "Documents are verified once the new employee has submitted them (stage \"Awaiting document verification\").");
        return (d, o, null);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
