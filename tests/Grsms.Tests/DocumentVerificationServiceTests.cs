using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>
/// Phase 18: the New Hire Administrative Records checklist: who may upload, file checks, "not applicable" only for the
/// optional documents, submitting only when everything is there, HR verification and rejection, sending rejected documents
/// back, and who may see the files (never the HOD).
/// </summary>
public class DocumentVerificationServiceTests : OnboardingTestBase
{
    private async Task<(int Id, int App)> LetterSignedAsync()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        await IssueAndSignAsync(id, app);
        return (id, app);
    }

    private async Task<EmployeeDocument> DocAsync(int id, EmployeeDocumentType type) => (await DocumentsAsync(id)).Single(d => d.DocumentType == type);

    [Fact]
    public async Task Only_the_new_employee_or_hr_may_upload_and_files_are_checked()
    {
        var (id, _) = await LetterSignedAsync();
        var ghanaCard = await DocAsync(id, EmployeeDocumentType.GhanaCard);

        // Another candidate, the HOD and an unrelated staff member cannot.
        foreach (var (user, role) in new[] { (_candidate2, Roles.Candidate), (_hod, Roles.HeadOfDepartment), (_staff, Roles.Employee) })
        {
            As(user, role);
            Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.pdf", FakePdf()))).Succeeded);
            Assert.Null(await RunDocs(s => s.GetForHrAsync(id)));
        }

        As(_candidate, Roles.Candidate);
        // Only PDF, JPG or PNG, and the content must match the extension.
        Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.docx", new MemoryStream([0x50, 0x4B, 0x03, 0x04])))).Succeeded);
        Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.pdf", new MemoryStream([0x4D, 0x5A, 0x90, 0x00])))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.png", new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A])))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card-back.pdf", FakePdf()))).Succeeded); // several files allowed

        var mine = (await RunDocs(s => s.GetMineAsync(_appOf(id))))!;
        var card = mine.Documents.Single(d => d.Type == EmployeeDocumentType.GhanaCard);
        Assert.Equal((EmployeeDocumentStatus.Submitted, 2), (card.Status, card.Files.Count));

        // Files are stored under random names and can be downloaded by the employee and HR only.
        var attachmentId = card.Files[0].AttachmentId;
        Assert.NotNull(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        AsRecruiter();
        Assert.NotNull(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Null(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        var stored = await Db(db => Task.FromResult(db.Attachments.Single(a => a.Id == attachmentId)));
        Assert.NotEqual("card.png", stored.StoredFileName);
        Assert.Equal("card.png", stored.OriginalFileName);
        Assert.Equal(64, stored.Sha256.Length);

        // Removing the last file puts the document back to Pending.
        As(_candidate, Roles.Candidate);
        foreach (var f in card.Files)
            Assert.True((await RunDocs(s => s.RemoveFileAsync(ghanaCard.Id, f.AttachmentId))).Succeeded);
        Assert.Equal(EmployeeDocumentStatus.Pending, (await DocAsync(id, EmployeeDocumentType.GhanaCard)).Status);
    }

    [Fact]
    public async Task Only_optional_documents_can_be_not_applicable_and_submitting_needs_everything()
    {
        var (id, app) = await LetterSignedAsync();
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.MarkNotApplicableAsync((DocAsync(id, EmployeeDocumentType.GhanaCard)).Result.Id, "Lost it"))).Succeeded);
        var marriage = await DocAsync(id, EmployeeDocumentType.MarriageCertificate);
        Assert.False((await RunDocs(s => s.MarkNotApplicableAsync(marriage.Id, null))).Succeeded); // reason needed
        Assert.True((await RunDocs(s => s.MarkNotApplicableAsync(marriage.Id, "Not married"))).Succeeded);

        var blocked = await Run(s => s.SubmitAsync(id));
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Errors, e => e.Contains("personal record")); // Phase 19: the letter, then the personal record, then the documents
        Assert.NotNull((await RunDocs(s => s.GetMineAsync(app)))!.SubmitBlockedReason);

        await FillDocumentsAsync(id);
        Assert.Contains("personal record", (await RunDocs(s => s.GetMineAsync(app)))!.SubmitBlockedReason); // Phase 19
        await FillPersonalRecordAsync(app);
        As(_candidate, Roles.Candidate);
        Assert.Null((await RunDocs(s => s.GetMineAsync(app)))!.SubmitBlockedReason);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);

        // Once submitted, the new employee can no longer change anything; HR can until approval.
        var cv = await DocAsync(id, EmployeeDocumentType.Cv);
        Assert.False((await RunDocs(s => s.UploadAsync(cv.Id, "cv2.pdf", FakePdf()))).Succeeded);
        AsRecruiter();
        Assert.True((await RunDocs(s => s.UploadAsync(cv.Id, "cv-scanned-by-hr.pdf", FakePdf()))).Succeeded);
    }

    [Fact]
    public async Task Hr_verifies_or_rejects_and_rejected_documents_go_back_to_the_new_employee()
    {
        var (id, app) = await SubmittedAsync();
        var police = await DocAsync(id, EmployeeDocumentType.PoliceClearance);
        var marriage = await DocAsync(id, EmployeeDocumentType.MarriageCertificate);

        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.VerifyAsync(police.Id))).Succeeded); // HR only
        AsRecruiter();
        Assert.False((await RunDocs(s => s.RejectAsync(police.Id, null))).Succeeded); // reason needed
        Assert.True((await RunDocs(s => s.RejectAsync(police.Id, "Older than 6 months: please get a new one."))).Succeeded);
        Assert.True((await RunDocs(s => s.RejectAsync(marriage.Id, "Your CV says you are married: please upload the certificate."))).Succeeded);
        Assert.False((await Run(s => s.MarkDocumentsVerifiedAsync(id))).Succeeded); // rejected ones first

        Assert.True((await Run(s => s.ReturnToEmployeeAsync(id, null))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("need attention"));

        // The new employee fixes them and submits again; the other documents keep their status.
        As(_candidate, Roles.Candidate);
        var mine = (await RunDocs(s => s.GetMineAsync(app)))!;
        Assert.Equal(2, mine.Rejected);
        Assert.Equal("Older than 6 months: please get a new one.", mine.Documents.Single(d => d.Type == EmployeeDocumentType.PoliceClearance).RejectionReason);
        Assert.True((await RunDocs(s => s.UploadAsync(police.Id, "police-new.pdf", FakePdf()))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(marriage.Id, "marriage.pdf", FakePdf()))).Succeeded);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);

        await VerifyAllAsync(id);
        var docs = await DocumentsAsync(id);
        Assert.All(docs, d => Assert.True(Application.Onboarding.DocumentVerificationService.IsSettled(d)));
        Assert.All(docs.Where(d => d.Status == EmployeeDocumentStatus.Verified), d => Assert.Equal(_ro, d.VerifiedByUserId));
        Assert.True((await Run(s => s.MarkDocumentsVerifiedAsync(id))).Succeeded);

        // Verified documents are locked, even for HR.
        AsRecruiter();
        Assert.False((await RunDocs(s => s.UploadAsync(police.Id, "another.pdf", FakePdf()))).Succeeded);
    }

    private int _appOf(int onboardingId) => RecordAsync(onboardingId).Result.JobApplicationId;
}
