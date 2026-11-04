using System.Text;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// The recruitment request workflow rules (Module 2): numbering, who may do what at each stage,
/// return / resubmit / reject, budget validation, visibility, attachments and notifications.
/// </summary>
public class RecruitmentRequestServiceTests : IAsyncLifetime
{
    private const int Apm = 31;      // seeded "Factory APM" (code APM, Operations division)
    private const int Finance = 3;   // seeded "Finance"
    private const int HeadOffice = 1;

    private readonly TestDatabase _db = new(maxUploadMb: 1);
    private string _hod = "", _otherHod = "", _ro1 = "", _ro2 = "", _hrm = "", _md = "";

    /// <summary>Creates the cast: an HSE HOD, a Finance HOD, two recruiters, an HR Manager and an MD.</summary>
    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment, Roles.Employee);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro1 = await _db.CreateUserAsync("ro1@grel.test", 1, Roles.RecruitmentOfficer);
        _ro2 = await _db.CreateUserAsync("ro2@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _md = await _db.CreateUserAsync("md@grel.test", 1, Roles.ManagingDirector);

        using var scope = _db.NewScope();
        var depts = scope.ServiceProvider.GetRequiredService<IRepository<Department>>();
        (await depts.GetByIdAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsHod() => As(_hod, Roles.HeadOfDepartment, Roles.Employee);

    /// <summary>Runs one call as a fresh "web request" (new scope = new DbContext).</summary>
    private async Task<T> Run<T>(Func<RecruitmentRequestService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<RecruitmentRequestService>());
    }

    private static RecruitmentRequestFormDto Form(int dept = Apm) => new()
    {
        DepartmentId = dept,
        SiteId = HeadOffice,
        PositionTitle = "Safety Officer",
        NumberOfPositions = 1,
        EmploymentType = EmploymentType.Permanent,
        VacancyReason = VacancyReason.NewPosition,
        Justification = "New sites need on-site safety supervision every shift.",
        BudgetCode = "hse-2026-01",
        EstimatedAnnualCost = 85000m
    };

    private static MemoryStream FakePdf() => new(Encoding.ASCII.GetBytes("%PDF-1.4\n% fake test document\n%%EOF"));

    /// <summary>HOD creates a draft, attaches a job description and submits it. Returns the request id.</summary>
    private async Task<int> SubmittedRequestAsync()
    {
        AsHod();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "JD.pdf", FakePdf()))).Succeeded);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        return id;
    }

    private Task<Application.Common.Result> Decide(int id, ReviewDecision decision, RecruitmentRequestStatus expected,
        string? comments = null, bool budget = false, string? recruiter = null) =>
        Run(s => s.DecideAsync(id, new ReviewDecisionDto
        {
            Decision = decision, ExpectedStatus = expected, Comments = comments,
            BudgetValidated = budget, AssignedRecruiterUserId = recruiter
        }));

    private Task<RecruitmentRequestDetailsDto?> Details(int id) => Run(s => s.GetDetailsAsync(id));

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Drafts_get_sequential_numbers_per_department_and_year()
    {
        AsHod();
        var first = (await Run(s => s.CreateAsync(Form()))).Value;
        var second = (await Run(s => s.CreateAsync(Form()))).Value;

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"RR-APM-{year}-0001", (await Details(first))!.RequestNumber);
        Assert.Equal($"RR-APM-{year}-0002", (await Details(second))!.RequestNumber);
        Assert.Equal(RecruitmentRequestStatus.Draft, (await Details(first))!.Status);
    }

    [Fact]
    public async Task Only_a_hod_can_raise_and_only_for_a_department_they_head()
    {
        As(_ro1, Roles.RecruitmentOfficer);
        Assert.False((await Run(s => s.CreateAsync(Form()))).Succeeded);

        AsHod();
        Assert.False((await Run(s => s.CreateAsync(Form(dept: Finance)))).Succeeded);
        Assert.True((await Run(s => s.CreateAsync(Form()))).Succeeded);
    }

    [Fact]
    public async Task A_job_description_is_required_before_submitting()
    {
        AsHod();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;

        var result = await Run(s => s.SubmitAsync(id));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("job description"));
    }

    [Fact]
    public async Task Full_approval_path_ends_approved_with_complete_history_and_notifications()
    {
        var id = await SubmittedRequestAsync();
        var submitted = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.RecruitmentReview, submitted.Status);
        Assert.NotNull(submitted.DateRequested);
        Assert.Contains(_db.SentEmails(), m => m.Contains("ro1@grel.test") && m.Contains("needs your review"));

        // Recruitment Officer approves and assigns the other recruiter.
        As(_ro1, Roles.RecruitmentOfficer);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview, recruiter: _ro2)).Succeeded);

        // HR Manager must confirm the budget.
        As(_hrm, Roles.HrManager);
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval)).Succeeded);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true)).Succeeded);

        // Managing Director gives final approval.
        As(_md, Roles.ManagingDirector);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.MdApproval, comments: "Go ahead")).Succeeded);

        AsHod();
        var done = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Approved, done.Status);
        Assert.NotNull(done.ApprovedAt);
        Assert.True(done.BudgetValidated);
        Assert.Equal("ro2", done.AssignedRecruiterName);
        Assert.Equal(
            [WorkflowAction.Created, WorkflowAction.Submitted, WorkflowAction.Approved, WorkflowAction.Approved, WorkflowAction.Approved],
            done.History.Select(h => h.Action));
        Assert.Contains(_db.SentEmails(), m => m.Contains("hod@grel.test") && m.Contains("approved"));
        Assert.False(done.CanCancel); // final
    }

    [Fact]
    public async Task Only_the_stage_role_can_decide_and_stale_pages_are_refused()
    {
        var id = await SubmittedRequestAsync();

        As(_hrm, Roles.HrManager); // HR Manager tries to act at Recruitment Review
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview)).Succeeded);

        As(_ro1, Roles.RecruitmentOfficer);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview)).Succeeded);

        // A second recruiter still has the old page open and clicks Approve again.
        As(_ro2, Roles.RecruitmentOfficer);
        var stale = await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview);
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Errors, e => e.Contains("already moved on"));
    }

    [Fact]
    public async Task Nobody_can_approve_their_own_request()
    {
        // This HOD is also a Recruitment Officer.
        var hodRecruiter = await _db.CreateUserAsync("dual@grel.test", Finance, Roles.HeadOfDepartment, Roles.RecruitmentOfficer);
        As(hodRecruiter, Roles.HeadOfDepartment, Roles.RecruitmentOfficer);
        var form = Form(dept: Finance);
        var id = (await Run(s => s.CreateAsync(form))).Value;
        await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "JD.pdf", FakePdf()));
        await Run(s => s.SubmitAsync(id));

        var own = await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview);
        Assert.False(own.Succeeded);
        Assert.Equal(0, await Run(s => s.CountAwaitingMeAsync()));   // not in their own "awaiting" list
        Assert.NotNull((await Details(id))!.DecisionBlockedReason);

        As(_ro1, Roles.RecruitmentOfficer);
        Assert.Equal(1, await Run(s => s.CountAwaitingMeAsync()));
    }

    [Fact]
    public async Task Returned_requests_can_be_edited_and_resubmitted_and_need_a_fresh_budget_check()
    {
        var id = await SubmittedRequestAsync();
        As(_ro1, Roles.RecruitmentOfficer);
        await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview);
        As(_hrm, Roles.HrManager);
        await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true);

        As(_md, Roles.ManagingDirector);
        Assert.False((await Decide(id, ReviewDecision.Return, RecruitmentRequestStatus.MdApproval)).Succeeded); // comment required
        Assert.True((await Decide(id, ReviewDecision.Return, RecruitmentRequestStatus.MdApproval, comments: "Reduce the cost, please.")).Succeeded);

        AsHod();
        var returned = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Returned, returned.Status);
        Assert.True(returned.CanEdit);

        var form = (await Run(s => s.GetForEditAsync(id)))!;
        form.EstimatedAnnualCost = 70000m;
        Assert.True((await Run(s => s.UpdateAsync(form))).Succeeded);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);

        var resubmitted = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.RecruitmentReview, resubmitted.Status);
        Assert.False(resubmitted.BudgetValidated);
        Assert.Equal(WorkflowAction.Resubmitted, resubmitted.History.Last().Action);
        Assert.Equal(70000m, resubmitted.EstimatedAnnualCost);
    }

    [Fact]
    public async Task Rejected_requests_are_final()
    {
        var id = await SubmittedRequestAsync();
        As(_ro1, Roles.RecruitmentOfficer);
        Assert.True((await Decide(id, ReviewDecision.Reject, RecruitmentRequestStatus.RecruitmentReview, comments: "Position already exists.")).Succeeded);

        AsHod();
        var r = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Rejected, r.Status);
        Assert.False(r.CanEdit || r.CanSubmit || r.CanCancel);
        Assert.False((await Run(s => s.SubmitAsync(id))).Succeeded);
        Assert.False((await Run(s => s.CancelAsync(id, null))).Succeeded);
    }

    [Fact]
    public async Task Other_departments_hods_cannot_see_the_request_or_its_files()
    {
        var id = await SubmittedRequestAsync();
        var attachmentId = (await Details(id))!.Attachments.Single().Id;

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Details(id));
        Assert.Null(await Run(s => s.OpenAttachmentAsync(id, attachmentId)));
        Assert.Equal(0, (await Run(s => s.SearchAsync(new RecruitmentRequestFilter()))).TotalCount);

        As(_md, Roles.ManagingDirector);
        Assert.NotNull(await Details(id));
        await using var file = (await Run(s => s.OpenAttachmentAsync(id, attachmentId)))!.Content;
        Assert.Equal("%PDF", Encoding.ASCII.GetString(new BinaryReader(file).ReadBytes(4)));
    }

    [Fact]
    public async Task Uploads_are_checked_for_type_content_and_size()
    {
        AsHod();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;

        var exe = await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.Other, "tool.exe", new MemoryStream([0x4D, 0x5A, 0x90])));
        Assert.False(exe.Succeeded);

        // A program renamed to .pdf does not start with "%PDF".
        var disguised = await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "jd.pdf", new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03])));
        Assert.False(disguised.Succeeded);

        var big = new MemoryStream();
        big.Write("%PDF"u8);
        big.Write(new byte[2 * 1024 * 1024]); // 2 MB, over this test's 1 MB limit
        big.Position = 0;
        Assert.False((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "big.pdf", big))).Succeeded);

        // Folder names in the browser's file name are stripped.
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, @"C:\fakepath\..\JD.pdf", FakePdf()))).Succeeded);
        Assert.Equal("JD.pdf", (await Details(id))!.Attachments.Single().FileName);
    }

    [Fact]
    public async Task Files_cannot_be_changed_after_submission()
    {
        var id = await SubmittedRequestAsync();
        AsHod();
        Assert.False((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.Other, "more.pdf", FakePdf()))).Succeeded);
        var attachmentId = (await Details(id))!.Attachments.Single().Id;
        Assert.False((await Run(s => s.RemoveAttachmentAsync(id, attachmentId))).Succeeded);
    }

    [Fact]
    public async Task Simultaneous_saves_of_the_same_request_are_detected()
    {
        AsHod();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;

        using var scopeA = _db.NewScope();
        using var scopeB = _db.NewScope();
        var repoA = scopeA.ServiceProvider.GetRequiredService<IRecruitmentRequestRepository>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<IRecruitmentRequestRepository>();
        var a = (await repoA.GetByIdAsync(id))!;
        var b = (await repoB.GetByIdAsync(id))!;

        b.PositionTitle = "Changed by B";
        b.Version = Guid.NewGuid();
        await scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        a.PositionTitle = "Changed by A";
        a.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
    }

    [Fact]
    public async Task Requests_record_their_division_and_can_be_filtered_by_division_department_status_and_period()
    {
        var submitted = await SubmittedRequestAsync();   // Factory APM → Operations
        AsHod();
        var draft = (await Run(s => s.CreateAsync(Form()))).Value;

        var details = (await Details(submitted))!;
        Assert.Equal("Operations", details.DivisionName);
        Assert.Equal("Factory APM", details.DepartmentName);

        As(_hrm, Roles.HrManager);   // sees every request
        async Task<int[]> Find(RecruitmentRequestFilter f) =>
            (await Run(s => s.SearchAsync(f))).Items.Select(i => i.Id).Order().ToArray();

        Assert.Equal([submitted, draft], await Find(new() { DivisionId = 2 }));
        Assert.Empty(await Find(new() { DivisionId = 1 }));
        Assert.Equal([submitted, draft], await Find(new() { DepartmentId = Apm }));
        Assert.Equal([submitted], await Find(new() { DivisionId = 2, Status = RecruitmentRequestStatus.RecruitmentReview }));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal([submitted, draft], await Find(new() { From = today, To = today }));
        Assert.Empty(await Find(new() { From = today.AddDays(1) }));
        Assert.Empty(await Find(new() { To = today.AddDays(-1) }));

        var row = (await Run(s => s.SearchAsync(new RecruitmentRequestFilter { DivisionId = 2 }))).Items.First();
        Assert.Equal("Operations", row.DivisionName);
    }
}
