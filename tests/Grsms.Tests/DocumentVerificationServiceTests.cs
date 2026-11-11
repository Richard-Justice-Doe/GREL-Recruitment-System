using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Documents;
using Grsms.Application.Onboarding;
using Grsms.Domain.Documents;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grsms.Tests;

/// <summary>
/// Phase 18, reworked in Phase 27: the onboarding document checklist. Uploads open as soon as the RO starts the onboarding;
/// each document is reviewed on its own (start review, verify, reject with a reason, resubmit); replaced files are kept as
/// history; the stage follows the facts; only the RO verifies; every file opening is recorded; the requirements (expatriate,
/// education certificates) and the configurable document types apply to the running onboardings.
/// </summary>
public class DocumentVerificationServiceTests : OnboardingTestBase
{
    private async Task<(int Id, int App)> StartedAsync(Action<StartOnboardingForm>? change = null)
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        AsRecruiter();
        var form = (await Run(s => s.GetStartFormAsync(app))).Value!;
        change?.Invoke(form);
        var r = await Run(s => s.StartAsync(form));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        return (r.Value, app);
    }

    private async Task<T> RunTypes<T>(Func<OnboardingDocumentTypeService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<OnboardingDocumentTypeService>());
    }

    [Fact]
    public async Task Uploads_open_at_start_only_for_the_new_employee_and_files_are_checked()
    {
        var (id, app) = await StartedAsync();
        Assert.Equal(OnboardingStatus.Draft, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("onboarding has started"));
        var ghanaCard = await DocAsync(id, "GhanaCard");

        // Another candidate, the HOD, an unrelated staff member, the HR Manager and the Recruitment Officer cannot upload: only the
        // new employee does (11 Nov 2026, docs/29 change 3).
        foreach (var (user, role) in new[] { (_candidate2, Roles.Candidate), (_hod, Roles.HeadOfDepartment), (_staff, Roles.Employee), (_hrm, Roles.HrManager), (_ro, Roles.RecruitmentOfficer) })
        {
            As(user, role);
            Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.pdf", FakePdf()))).Succeeded);
        }
        As(_hod, Roles.HeadOfDepartment);
        Assert.Null(await RunDocs(s => s.GetForHrAsync(id)));
        AsHrManager();
        Assert.NotNull(await RunDocs(s => s.GetForHrAsync(id))); // onboarding.view

        // Before the letter is even issued, the new employee uploads. Only PDF / pictures for the Ghana Card; the content must match.
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.docx", new MemoryStream([0x50, 0x4B, 0x03, 0x04])))).Succeeded);
        Assert.False((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.pdf", new MemoryStream([0x4D, 0x5A, 0x90, 0x00])))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card.png", new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A])))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card-back.pdf", FakePdf()))).Succeeded); // several files allowed
        // The CV accepts Word files.
        var cv = await DocAsync(id, "Cv");
        Assert.True((await RunDocs(s => s.UploadAsync(cv.Id, "cv.docx", new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00])))).Succeeded);

        var mine = (await RunDocs(s => s.GetMineAsync(app)))!;
        var card = mine.Documents.Single(d => d.Code == "GhanaCard");
        Assert.Equal((EmployeeDocumentStatus.Submitted, 2), (card.Status, card.Files.Count));
        Assert.Equal("Ghana Card / National ID", card.Name);
        Assert.Equal(DocumentGroup.Personal, card.Group);

        // Files are stored under random names with a fingerprint, and open for the employee and onboarding.view only.
        var attachmentId = card.Files[0].AttachmentId;
        Assert.NotNull(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        AsHrManager();
        Assert.NotNull(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Null(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId)));
        var stored = await Db(db => db.Attachments.SingleAsync(a => a.Id == attachmentId));
        Assert.NotEqual("card.png", stored.StoredFileName);
        Assert.Equal(64, stored.Sha256.Length);

        // Every opening is recorded with the user and the IP address.
        var opened = await Db(db => db.EmployeeDocumentEvents.Where(e => e.EmployeeDocumentId == ghanaCard.Id && e.Action == DocumentEventAction.Viewed).ToListAsync());
        Assert.Equal(2, opened.Count);
        Assert.Contains(opened, e => e.ActorUserId == _hrm && e.IpAddress == "10.0.0.1");

        // Replacing a file keeps the old one in the history; removing the last file puts the document back to "Not submitted".
        As(_candidate, Roles.Candidate);
        Assert.True((await RunDocs(s => s.UploadAsync(ghanaCard.Id, "card-new.pdf", FakePdf(), replacesAttachmentId: attachmentId))).Succeeded);
        card = (await RunDocs(s => s.GetMineAsync(app)))!.Documents.Single(d => d.Code == "GhanaCard");
        Assert.Equal(2, card.Files.Count);
        Assert.Equal("card.png", Assert.Single(card.ReplacedFiles).FileName);
        Assert.Contains(card.History, h => h.Action == DocumentEventAction.Replaced);
        Assert.DoesNotContain(card.History, h => h.Action == DocumentEventAction.Viewed); // the new employee does not see who opened them
        Assert.NotNull(await RunDocs(s => s.OpenFileAsync(ghanaCard.Id, attachmentId))); // history files stay downloadable
        foreach (var f in card.Files)
            Assert.True((await RunDocs(s => s.RemoveFileAsync(ghanaCard.Id, f.AttachmentId))).Succeeded);
        Assert.Equal(EmployeeDocumentStatus.Pending, (await DocAsync(id, "GhanaCard")).Status);
        Assert.Equal(3, (await RunDocs(s => s.GetMineAsync(app)))!.Documents.Single(d => d.Code == "GhanaCard").ReplacedFiles.Count);
    }

    [Fact]
    public async Task The_stage_follows_the_facts_and_only_the_ro_verifies()
    {
        var (id, app) = await StartedAsync();
        AsRecruiter();
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 8:00 to 17:00."))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status);

        // Only optional documents can be "not applicable", with a reason.
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.MarkNotApplicableAsync(DocAsync(id, "GhanaCard").Result.Id, "Lost it"))).Succeeded);
        var marriage = await DocAsync(id, "MarriageCertificate");
        Assert.False((await RunDocs(s => s.MarkNotApplicableAsync(marriage.Id, null))).Succeeded);
        Assert.True((await RunDocs(s => s.MarkNotApplicableAsync(marriage.Id, "Not married"))).Succeeded);

        var mine = (await RunDocs(s => s.GetMineAsync(app)))!;
        Assert.Contains(mine.Outstanding, o => o.Contains("Sign your appointment letter"));
        Assert.Contains(mine.Outstanding, o => o.Contains("personal record"));
        Assert.Contains(mine.Outstanding, o => o.Contains("upload the ghana card", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mine.Outstanding, o => o.Contains("BECE", StringComparison.OrdinalIgnoreCase)); // not ticked = optional

        Assert.True((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded);
        await FillPersonalRecordAsync(app);
        await FillDocumentsAsync(id);
        // Everything in: the RO is told, and it moves by itself (no "Submit").
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("requirements submitted"));
        As(_candidate, Roles.Candidate);
        Assert.Empty((await RunDocs(s => s.GetMineAsync(app)))!.Outstanding);

        // The HR Manager can see but not verify; the RO verifies each one.
        var police = await DocAsync(id, "PoliceClearance");
        AsHrManager();
        Assert.False((await RunDocs(s => s.VerifyAsync(police.Id))).Succeeded);
        Assert.False((await RunDocs(s => s.StartReviewAsync(police.Id))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.VerifyAsync(police.Id))).Succeeded);

        await VerifyAllAsync(id);
        var record = await RecordAsync(id);
        Assert.Equal(OnboardingStatus.AwaitingActivation, record.Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("documents are complete"));
        Assert.All((await DocumentsAsync(id)).Where(d => d.Status == EmployeeDocumentStatus.Verified), d => Assert.Equal(_ro, d.VerifiedByUserId));
        As(_candidate, Roles.Candidate);
        Assert.Equal(100, (await RunDocs(s => s.GetMineAsync(app)))!.Percent);

        // Verified documents are locked, even for the RO; changing the personal record sends it back to the new employee.
        AsRecruiter();
        Assert.False((await RunDocs(s => s.UploadAsync(police.Id, "another.pdf", FakePdf()))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.True((await RunRecord(s => s.SaveAsync(app, ValidRecord()))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status);
        Assert.True((await RunRecord(s => s.SignAsync(app, "Ama Mensah", true))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingActivation, (await RecordAsync(id)).Status);
    }

    [Fact]
    public async Task Review_locks_a_document_and_rejected_documents_are_resubmitted()
    {
        var (id, app) = await SubmittedAsync();
        var police = await DocAsync(id, "PoliceClearance");

        // Start review: the new employee can no longer change it.
        AsRecruiter();
        Assert.True((await RunDocs(s => s.StartReviewAsync(police.Id))).Succeeded);
        Assert.Equal(EmployeeDocumentStatus.UnderReview, (await DocAsync(id, "PoliceClearance")).Status);
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.UploadAsync(police.Id, "police2.pdf", FakePdf()))).Succeeded);

        // Rejecting needs a reason from the list; "Other" needs the RO's own words.
        AsRecruiter();
        Assert.False((await RunDocs(s => s.RejectAsync(police.Id, null, null))).Succeeded);
        Assert.False((await RunDocs(s => s.RejectAsync(police.Id, DocumentRejectionReason.Other, null))).Succeeded);
        Assert.True((await RunDocs(s => s.RejectAsync(police.Id, DocumentRejectionReason.Expired, "Older than 6 months."))).Succeeded);
        var rejected = await DocAsync(id, "PoliceClearance");
        Assert.Equal((EmployeeDocumentStatus.Rejected, DocumentRejectionReason.Expired, "Expired document: Older than 6 months.", 1),
            (rejected.Status, rejected.RejectionReasonCode, rejected.RejectionReason, rejected.RejectionCount));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("police clearance report again"));
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status); // back with the new employee

        // The new employee sees why, and uploads a replacement: "Resubmitted, awaiting verification".
        As(_candidate, Roles.Candidate);
        var mine = (await RunDocs(s => s.GetMineAsync(app)))!;
        Assert.Equal(1, mine.Rejected);
        Assert.Contains(mine.Outstanding, o => o.Contains("Older than 6 months."));
        var old = mine.Documents.Single(d => d.Code == "PoliceClearance").Files.Single();
        Assert.True((await RunDocs(s => s.UploadAsync(police.Id, "police-new.pdf", FakePdf(), replacesAttachmentId: old.AttachmentId))).Succeeded);
        Assert.Equal(EmployeeDocumentStatus.Resubmitted, (await DocAsync(id, "PoliceClearance")).Status);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, (await RecordAsync(id)).Status);
        var history = (await RunDocs(s => s.GetMineAsync(app)))!.Documents.Single(d => d.Code == "PoliceClearance").History.Select(h => h.Action).ToList();
        Assert.Equal([DocumentEventAction.Uploaded, DocumentEventAction.ReviewStarted, DocumentEventAction.Rejected, DocumentEventAction.Resubmitted], history);

        await VerifyAllAsync(id);
        Assert.Equal(OnboardingStatus.AwaitingActivation, (await RecordAsync(id)).Status);
    }

    [Fact]
    public async Task Two_recommendation_letters_are_needed_before_verification()
    {
        var (id, app) = await StartedAsync();
        var letters = await DocAsync(id, DefaultDocumentTypes.RecommendationLetters);
        As(_candidate, Roles.Candidate);
        Assert.True((await RunDocs(s => s.UploadAsync(letters.Id, "letter-1.pdf", FakePdf()))).Succeeded);
        Assert.Contains((await RunDocs(s => s.GetMineAsync(app)))!.Outstanding, o => o.Contains("2 files for the recommendation letters (1 so far)"));
        AsRecruiter();
        var r = await RunDocs(s => s.VerifyAsync(letters.Id));
        Assert.False(r.Succeeded);
        Assert.Contains("needs 2 files", r.Errors.Single());
        As(_candidate, Roles.Candidate);
        Assert.True((await RunDocs(s => s.UploadAsync(letters.Id, "letter-2.pdf", FakePdf()))).Succeeded);
        AsRecruiter();
        Assert.True((await RunDocs(s => s.VerifyAsync(letters.Id))).Succeeded);
    }

    [Fact]
    public async Task The_ro_chooses_the_education_certificates_and_whether_the_hire_is_an_expatriate()
    {
        var (id, app) = await StartedAsync(f => f.RequiredEducation = ["WassceCertificate", "DegreeCertificate"]);
        var docs = await DocumentsAsync(id);
        Assert.True(docs.Single(d => d.DocumentTypeCode == "DegreeCertificate").IsRequired);
        Assert.False(docs.Single(d => d.DocumentTypeCode == "BeceCertificate").IsRequired);
        Assert.True(docs.Single(d => d.DocumentTypeCode == "GhanaCard").IsRequired);
        Assert.False(docs.Single(d => d.DocumentTypeCode == "WorkPermit").IsApplicable); // hidden from a Ghanaian
        Assert.DoesNotContain(docs, d => d.DocumentTypeCode == DefaultDocumentTypes.SchoolCertificates); // the old single document
        As(_candidate, Roles.Candidate);
        Assert.DoesNotContain((await RunDocs(s => s.GetMineAsync(app)))!.Visible, d => d.Code == "WorkPermit");

        // Only the RO changes the requirements.
        AsHrManager();
        Assert.False((await RunDocs(s => s.SetRequirementsAsync(id, true, ["DegreeCertificate"]))).Succeeded);
        AsRecruiter();
        Assert.False((await RunDocs(s => s.SetRequirementsAsync(id, true, ["GhanaCard"]))).Succeeded); // not an education certificate
        Assert.True((await RunDocs(s => s.SetRequirementsAsync(id, true, ["DegreeCertificate", "MastersCertificate"]))).Succeeded);
        docs = await DocumentsAsync(id);
        Assert.True((await RecordAsync(id)).IsExpatriate);
        Assert.True(docs.Single(d => d.DocumentTypeCode == "WorkPermit") is { IsRequired: true, IsApplicable: true });
        Assert.True(docs.Single(d => d.DocumentTypeCode == "GhanaCard") is { IsRequired: false, IsApplicable: false });
        Assert.False(docs.Single(d => d.DocumentTypeCode == "WassceCertificate").IsRequired);
        Assert.True(docs.Single(d => d.DocumentTypeCode == "MastersCertificate").IsRequired);
        Assert.Contains(await Db(db => db.OnboardingActions.Where(a => a.OnboardingRecordId == id).ToListAsync()),
            a => a.Action == OnboardingAction.RequirementsChanged && a.Comments!.Contains("Marked as an expatriate"));
    }

    [Fact]
    public async Task Changing_the_document_types_applies_to_the_onboardings_in_progress()
    {
        var (id, _) = await SubmittedAsync();
        var form = new OnboardingDocumentTypeForm
        {
            Name = "Driver's licence", Description = "Both sides.", Group = DocumentGroup.Personal, Requirement = DocumentRequirement.Required,
            MinFiles = 1, PersonnelFileCategoryId = PersonnelFileCategories.IdentificationRecords, SortOrder = 65
        };

        // Only "Maintain organisation data" (HR Administrator, Admin, Super Admin).
        AsRecruiter();
        Assert.False((await RunTypes(s => s.SaveAsync(form))).Succeeded);
        var admin = await _db.CreateUserAsync("hradmin@grel.test", 1, Roles.HrAdministrator);
        As(admin, Roles.HrAdministrator);
        var added = await RunTypes(s => s.SaveAsync(form));
        Assert.True(added.Succeeded, string.Join(" ", added.Errors));

        // A new required type is added to the running onboarding as "Not submitted", which sends it back to the new employee.
        var licence = await DocAsync(id, "DriversLicence");
        Assert.True(licence is { Status: EmployeeDocumentStatus.Pending, IsRequired: true, IsApplicable: true });
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status);

        // Retiring it stops it counting again.
        var saved = (await RunTypes(s => s.GetAsync(added.Value)))!;
        saved.IsActive = false;
        Assert.True((await RunTypes(s => s.SaveAsync(saved))).Succeeded);
        Assert.True(await DocAsync(id, "DriversLicence") is { IsRequired: false, IsApplicable: false });
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, (await RecordAsync(id)).Status);

        // The old checklist item cannot be changed.
        var legacy = (await RunTypes(s => s.GetAllAsync())).Single(t => t.IsLegacy);
        Assert.False((await RunTypes(s => s.SaveAsync(legacy))).Succeeded);
    }

    [Fact]
    public async Task A_file_refused_by_the_virus_scanner_is_never_stored()
    {
        using var db = new TestDatabase(configure: s =>
        {
            s.RemoveAll<IVirusScanner>();
            s.AddSingleton<IVirusScanner, RefuseEverything>();
        });
        using var scope = db.NewScope();
        var files = scope.ServiceProvider.GetRequiredService<AttachmentService>();
        var r = await files.AddAsync(AttachmentOwners.EmployeeDocument, 1, AttachmentCategory.NewHireDocument, "card.pdf", FakePdf());
        Assert.False(r.Succeeded);
        Assert.Contains("virus scan (EICAR test)", r.Errors.Single());
        Assert.False(await scope.ServiceProvider.GetRequiredService<Grsms.Infrastructure.Persistence.ApplicationDbContext>().Attachments.AnyAsync());
    }

    private sealed class RefuseEverything : IVirusScanner
    {
        public Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default) =>
            Task.FromResult(new VirusScanResult(false, "EICAR test"));
    }
}
