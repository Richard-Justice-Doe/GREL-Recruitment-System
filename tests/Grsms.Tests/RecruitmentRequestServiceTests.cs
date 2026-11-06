using System.Text;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Recruitment;
using Grsms.Domain.Documents;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// The recruitment request (QF-81-11) rules: who may raise, the form's fields and checks, the three approval stages
/// (Department Head → HR Manager → Managing Director) with approve / reject / return for correction, budget validation,
/// visibility, supporting documents, the rich-text description and notifications.
/// </summary>
public class RecruitmentRequestServiceTests : IAsyncLifetime
{
    private const int Apm = 31;      // seeded "Factory APM" (code APM, Operations division)
    private const int Finance = 3;   // seeded "Finance" (no Head of Department set)
    private const int HeadOffice = 1;

    private readonly TestDatabase _db = new(maxUploadMb: 1);
    private string _staff = "", _hod = "", _otherHod = "", _ro1 = "", _ro2 = "", _hrm = "", _md = "";

    /// <summary>The cast: an APM staff member and their HOD, a Finance HOD, two recruiters, an HR Manager and an MD.</summary>
    public async Task InitializeAsync()
    {
        _staff = await _db.CreateUserAsync("staff@grel.test", Apm, Roles.Employee);
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
    private void AsStaff() => As(_staff, Roles.Employee);
    private void AsHod() => As(_hod, Roles.HeadOfDepartment, Roles.Employee);

    /// <summary>Runs one call as a fresh "web request" (new scope = new DbContext).</summary>
    private async Task<T> Run<T>(Func<RecruitmentRequestService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<RecruitmentRequestService>());
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private const string Description =
        "<h3>Purpose</h3><p>Lead <strong>health and safety</strong> on the plantation.</p><ul><li>Inspect sites weekly</li><li>Train supervisors</li></ul>";

    private static RecruitmentRequestFormDto Form(int dept = Apm) => new()
    {
        DepartmentId = dept,
        SiteId = HeadOffice,
        EmploymentType = EmploymentType.Permanent,
        StaffCategory = StaffCategory.Senior,
        VacancyReason = VacancyReason.NewPosition,
        RequestDate = Today,
        PositionTitle = "Safety Officer",
        NumberOfPositions = 1,
        ProposedStartDate = Today.AddDays(60),
        PostDescription = Description,
        Justification = "New sites need on-site safety supervision every shift.",
        EducationLevel = EducationLevel.Bachelors,
        QualificationCategory = "Occupational health and safety",
        TrainingLevel = "Advanced",
        CertificateRequired = "NEBOSH IGC",
        MinYearsExperience = 3,
        ProfessionalExperience = "Safety supervision in agriculture or manufacturing",
        DrivingLicence = DrivingLicenceRequirement.Mandatory,
        DrivingLicenceClass = "b",
        Remarks = "Urgent before the next harvest."
    };

    private static MemoryStream FakePdf() => new(Encoding.ASCII.GetBytes("%PDF-1.4\n% fake test document\n%%EOF"));

    /// <summary>The signed-in user creates a draft, attaches a job description and submits it. Returns the request id.</summary>
    private async Task<int> SubmitAsCurrentUserAsync(RecruitmentRequestFormDto form)
    {
        var created = await Run(s => s.CreateAsync(form));
        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        var id = created.Value;
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "JD.pdf", FakePdf()))).Succeeded);
        var submitted = await Run(s => s.SubmitAsync(id));
        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));
        return id;
    }

    /// <summary>A staff member raises and submits a request: it waits for the APM HOD (stage 1).</summary>
    private Task<int> SubmittedRequestAsync()
    {
        AsStaff();
        return SubmitAsCurrentUserAsync(Form());
    }

    /// <summary>Submitted, then approved by the HOD: waiting for the HR Manager (stage 2).</summary>
    private async Task<int> AtHrStageAsync()
    {
        var id = await SubmittedRequestAsync();
        AsHod();
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.DepartmentHeadReview)).Succeeded);
        return id;
    }

    private Task<Result> Decide(int id, ReviewDecision decision, RecruitmentRequestStatus expected,
        string? comments = null, bool budget = false) =>
        Run(s => s.DecideAsync(id, new ReviewDecisionDto
        {
            Decision = decision, ExpectedStatus = expected, Comments = comments, BudgetValidated = budget
        }));

    private Task<RecruitmentRequestDetailsDto?> Details(int id) => Run(s => s.GetDetailsAsync(id));

    // ------------------------------------------------------------------ raising

    [Fact]
    public async Task Drafts_get_sequential_numbers_per_department_and_year()
    {
        AsStaff();
        var first = (await Run(s => s.CreateAsync(Form()))).Value;
        var second = (await Run(s => s.CreateAsync(Form()))).Value;

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"RR-APM-{year}-0001", (await Details(first))!.RequestNumber);
        Assert.Equal($"RR-APM-{year}-0002", (await Details(second))!.RequestNumber);
        Assert.Equal(RecruitmentRequestStatus.Draft, (await Details(first))!.Status);
    }

    [Fact]
    public async Task Department_staff_and_hods_raise_requests_only_for_their_own_department()
    {
        As(_ro1, Roles.RecruitmentOfficer); // no "Raise recruitment requests" permission
        Assert.False((await Run(s => s.CreateAsync(Form()))).Succeeded);

        AsStaff();
        Assert.False((await Run(s => s.CreateAsync(Form(dept: Finance)))).Succeeded);
        Assert.True((await Run(s => s.CreateAsync(Form()))).Succeeded);

        AsHod();
        Assert.True((await Run(s => s.CreateAsync(Form()))).Succeeded);

        // A staff member without a department cannot raise one.
        var homeless = await _db.CreateUserAsync("nodept@grel.test", null, Roles.Employee);
        As(homeless, Roles.Employee);
        Assert.Empty(await Run(s => s.GetCreatableDepartmentsAsync()));
        Assert.False((await Run(s => s.CreateAsync(Form()))).Succeeded);
    }

    [Fact]
    public async Task All_qf_81_11_fields_are_saved_and_shown()
    {
        AsStaff();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;
        var d = (await Details(id))!;

        Assert.Equal(EmploymentType.Permanent, d.EmploymentType);
        Assert.Equal(StaffCategory.Senior, d.StaffCategory);
        Assert.Equal(VacancyReason.NewPosition, d.VacancyReason);
        Assert.Equal(Today, d.RequestDate);
        Assert.Equal(Today.AddDays(60), d.ProposedStartDate);
        Assert.Equal(EducationLevel.Bachelors, d.EducationLevel);
        Assert.Equal("Occupational health and safety", d.QualificationCategory);
        Assert.Equal("Advanced", d.TrainingLevel);
        Assert.Equal("NEBOSH IGC", d.CertificateRequired);
        Assert.Equal(3, d.MinYearsExperience);
        Assert.Equal("Safety supervision in agriculture or manufacturing", d.ProfessionalExperience);
        Assert.Equal(DrivingLicenceRequirement.Mandatory, d.DrivingLicence);
        Assert.Equal("B", d.DrivingLicenceClass);
        Assert.Equal("Urgent before the next harvest.", d.Remarks);
        Assert.Contains("<strong>health and safety</strong>", d.PostDescriptionHtml);
        Assert.Contains("<li>Inspect sites weekly</li>", d.PostDescriptionHtml);
    }

    [Fact]
    public async Task The_form_offers_only_the_qf_81_11_choices_and_checks_dates_and_details()
    {
        AsStaff();
        async Task<IReadOnlyList<string>> Errors(Action<RecruitmentRequestFormDto> change)
        {
            var f = Form();
            change(f);
            return (await Run(s => s.CreateAsync(f))).Errors;
        }

        Assert.Contains(await Errors(f => f.EmploymentType = EmploymentType.Casual), e => e.Contains("Permanent or Contract"));
        Assert.Contains(await Errors(f => f.VacancyReason = VacancyReason.TemporaryCover), e => e.Contains("purposes"));
        Assert.Contains(await Errors(f => f.VacancyReason = VacancyReason.Other), e => e.Contains("'Other'"));
        Assert.Contains(await Errors(f => f.VacancyReason = VacancyReason.Replacement), e => e.Contains("replaced"));
        Assert.Contains(await Errors(f => f.RequestDate = Today.AddDays(1)), e => e.Contains("request date"));
        Assert.Contains(await Errors(f => f.ProposedStartDate = Today), e => e.Contains("after today"));
        Assert.Empty(await Errors(f => f.DrivingLicenceClass = " "));   // the licence class is optional
        Assert.Contains(await Errors(f => f.PostDescription = "<p>Too short.</p>"), e => e.Contains("at least 50"));
        Assert.Contains(await Errors(f => f.PostDescription = $"<p>{new string('x', RecruitmentRequestFormDto.MaxPostDescriptionChars + 1)}</p>"),
            e => e.Contains("limit"));

        // Contract and "Other" with its explanation are fine; the licence class is dropped when no licence is needed.
        var ok = Form();
        ok.EmploymentType = EmploymentType.FixedTermContract;
        ok.VacancyReason = VacancyReason.Other;
        ok.PurposeOther = "Pilot project";
        ok.DrivingLicence = DrivingLicenceRequirement.NotNeeded;
        var id = (await Run(s => s.CreateAsync(ok))).Value;
        var d = (await Details(id))!;
        Assert.Equal("Pilot project", d.PurposeOther);
        Assert.Null(d.DrivingLicenceClass);
    }

    [Fact]
    public async Task The_description_keeps_basic_formatting_but_nothing_dangerous()
    {
        AsStaff();
        var f = Form();
        f.PostDescription = Description +
            "<script>alert('x')</script><p onclick=\"steal()\" style=\"color:red\">Clean <a href=\"javascript:evil()\">link</a> " +
            "<img src=x onerror=alert(1)><span style=\"font-size:40px\">kept text</span></p><iframe src=\"https://evil.test\"></iframe>";
        var id = (await Run(s => s.CreateAsync(f))).Value;
        var html = (await Details(id))!.PostDescriptionHtml!;

        Assert.Contains("<h3>Purpose</h3>", html);
        Assert.Contains("<ul><li>", html);
        Assert.Contains("kept text", html);
        foreach (var bad in new[] { "<script", "alert(", "onclick", "style=", "javascript:", "<a", "<img", "onerror", "<iframe", "<span" })
            Assert.DoesNotContain(bad, html, StringComparison.OrdinalIgnoreCase);

        // Text typed without the editor keeps its line breaks.
        f.PostDescription = "First line of the post description, long enough.\nSecond line, with no tags at all here.";
        var plain = (await Run(s => s.CreateAsync(f))).Value;
        Assert.Equal("<p>First line of the post description, long enough.</p><p>Second line, with no tags at all here.</p>",
            (await Details(plain))!.PostDescriptionHtml);
    }

    [Fact]
    public void The_character_count_ignores_formatting()
    {
        Assert.Equal(11, RichText.CharacterCount("<p><strong>Hello</strong> world</p>"));
        Assert.Equal(3, RichText.CharacterCount("<p>a&amp;b</p>"));
        Assert.Equal("Purpose\nLead health and safety on the plantation.\n• Inspect sites weekly\n• Train supervisors",
            RichText.ToPlainText(Description));
    }

    // ------------------------------------------------------------------ the three approval stages

    [Fact]
    public async Task A_job_description_is_required_before_submitting()
    {
        AsStaff();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;

        var result = await Run(s => s.SubmitAsync(id));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("job description"));
    }

    [Fact]
    public async Task Full_approval_path_department_head_then_hr_manager_then_md()
    {
        var id = await SubmittedRequestAsync();
        var submitted = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.DepartmentHeadReview, submitted.Status);
        Assert.NotNull(submitted.DateRequested);
        Assert.Contains("hod", submitted.WaitingFor);
        Assert.Contains(_db.SentEmails(), m => m.Contains("hod@grel.test") && m.Contains("needs your review"));
        Assert.DoesNotContain(_db.SentEmails(), m => m.Contains("ro1@grel.test")); // no Recruitment Officer stage any more

        // Stage 1: the Head of the department.
        AsHod();
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.DepartmentHeadReview)).Succeeded);
        Assert.Contains(_db.SentEmails(), m => m.Contains("hrm@grel.test") && m.Contains("needs your approval"));

        // Stage 2: the HR Manager must confirm the budget.
        As(_hrm, Roles.HrManager);
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval)).Succeeded);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true)).Succeeded);

        // Stage 3: the Managing Director gives final approval.
        As(_md, Roles.ManagingDirector);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.MdApproval, comments: "Go ahead")).Succeeded);

        AsStaff();
        var done = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Approved, done.Status);
        Assert.NotNull(done.ApprovedAt);
        Assert.True(done.BudgetValidated);
        Assert.Equal(
            [WorkflowAction.Created, WorkflowAction.Submitted, WorkflowAction.Approved, WorkflowAction.Approved, WorkflowAction.Approved],
            done.History.Select(h => h.Action));
        Assert.Contains(_db.SentEmails(), m => m.Contains("staff@grel.test") && m.Contains("approved"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("ro1@grel.test") && m.Contains("convert it into a vacancy"));
        Assert.False(done.CanCancel); // final
    }

    [Fact]
    public async Task A_request_raised_by_the_hod_is_signed_off_by_them_and_goes_straight_to_hr()
    {
        AsHod();
        var id = await SubmitAsCurrentUserAsync(Form());

        var d = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.HrApproval, d.Status);
        var signOff = d.History.Last();
        Assert.Equal(WorkflowAction.Approved, signOff.Action);
        Assert.Equal(RecruitmentRequestStatus.DepartmentHeadReview, signOff.FromStatus);
        Assert.Contains("signed off by the Head of Department", signOff.Comments);
        Assert.Contains(_db.SentEmails(), m => m.Contains("hrm@grel.test") && m.Contains("needs your review"));
    }

    [Fact]
    public async Task Only_the_hod_of_that_department_decides_stage_one()
    {
        var id = await SubmittedRequestAsync();

        As(_otherHod, Roles.HeadOfDepartment); // heads Finance, not APM
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.DepartmentHeadReview)).Succeeded);
        Assert.Equal(0, await Run(s => s.CountAwaitingMeAsync()));

        As(_hrm, Roles.HrManager); // HR acts only after the HOD
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.DepartmentHeadReview)).Succeeded);

        AsHod();
        Assert.Equal(1, await Run(s => s.CountAwaitingMeAsync()));
        Assert.True((await Details(id))!.CanDecide);
    }

    [Fact]
    public async Task Only_the_stage_role_can_decide_and_stale_pages_are_refused()
    {
        var id = await AtHrStageAsync();

        As(_md, Roles.ManagingDirector); // MD tries to act at the HR stage
        Assert.False((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true)).Succeeded);

        As(_hrm, Roles.HrManager);
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true)).Succeeded);

        // A second HR Manager still has the old page open and clicks Approve again.
        var hrm2 = await _db.CreateUserAsync("hrm2@grel.test", 1, Roles.HrManager);
        As(hrm2, Roles.HrManager);
        var stale = await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true);
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Errors, e => e.Contains("already moved on"));
    }

    [Fact]
    public async Task Nobody_can_approve_their_own_request()
    {
        // This HOD is also an HR Manager: their own request skips stage 1 but another HR Manager must do stage 2.
        var hodHr = await _db.CreateUserAsync("dual@grel.test", Apm, Roles.HeadOfDepartment, Roles.HrManager);
        As(hodHr, Roles.HeadOfDepartment, Roles.HrManager);
        var id = await SubmitAsCurrentUserAsync(Form());

        var own = await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true);
        Assert.False(own.Succeeded);
        Assert.Equal(0, await Run(s => s.CountAwaitingMeAsync()));   // not in their own "awaiting" list
        Assert.NotNull((await Details(id))!.DecisionBlockedReason);

        As(_hrm, Roles.HrManager);
        Assert.Equal(1, await Run(s => s.CountAwaitingMeAsync()));
    }

    [Fact]
    public async Task Returned_requests_can_be_corrected_and_resubmitted_and_start_again_at_stage_one()
    {
        var id = await AtHrStageAsync();
        As(_hrm, Roles.HrManager);
        await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.HrApproval, budget: true);

        As(_md, Roles.ManagingDirector);
        Assert.False((await Decide(id, ReviewDecision.Return, RecruitmentRequestStatus.MdApproval)).Succeeded); // comment required
        Assert.True((await Decide(id, ReviewDecision.Return, RecruitmentRequestStatus.MdApproval, comments: "We need two inspectors, not one.")).Succeeded);
        Assert.Contains(_db.SentEmails(), m => m.Contains("staff@grel.test") && m.Contains("returned for correction"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("hod@grel.test") && m.Contains("returned for correction"));

        AsStaff();
        var returned = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Returned, returned.Status);
        Assert.True(returned.CanEdit);

        var form = (await Run(s => s.GetForEditAsync(id)))!;
        form.NumberOfPositions = 2;
        Assert.True((await Run(s => s.UpdateAsync(form))).Succeeded);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);

        var resubmitted = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.DepartmentHeadReview, resubmitted.Status);
        Assert.False(resubmitted.BudgetValidated);
        Assert.Equal(WorkflowAction.Resubmitted, resubmitted.History.Last().Action);
        Assert.Equal(2, resubmitted.NumberOfPositions);
    }

    [Fact]
    public async Task Each_stage_can_return_for_correction_or_reject()
    {
        foreach (var (stage, actor) in new[]
                 {
                     (RecruitmentRequestStatus.DepartmentHeadReview, (Action)AsHod),
                     (RecruitmentRequestStatus.HrApproval, () => As(_hrm, Roles.HrManager)),
                 })
        {
            var returnId = stage == RecruitmentRequestStatus.DepartmentHeadReview ? await SubmittedRequestAsync() : await AtHrStageAsync();
            actor();
            Assert.True((await Decide(returnId, ReviewDecision.Return, stage, comments: "Please correct the dates.")).Succeeded);
            AsStaff();
            Assert.Equal(RecruitmentRequestStatus.Returned, (await Details(returnId))!.Status);

            var rejectId = stage == RecruitmentRequestStatus.DepartmentHeadReview ? await SubmittedRequestAsync() : await AtHrStageAsync();
            actor();
            Assert.True((await Decide(rejectId, ReviewDecision.Reject, stage, comments: "Not in this year's plan.")).Succeeded);
            AsStaff();
            Assert.Equal(RecruitmentRequestStatus.Rejected, (await Details(rejectId))!.Status);
        }
    }

    [Fact]
    public async Task Submitting_needs_a_head_of_department_to_review_it()
    {
        // Finance has no HOD set, so a Finance staff member's request would wait for nobody.
        var finStaff = await _db.CreateUserAsync("finstaff@grel.test", Finance, Roles.Employee);
        As(finStaff, Roles.Employee);
        var id = (await Run(s => s.CreateAsync(Form(dept: Finance)))).Value;
        await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "JD.pdf", FakePdf()));

        var result = await Run(s => s.SubmitAsync(id));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("No Head of Department"));
    }

    [Fact]
    public async Task Rejected_requests_are_final()
    {
        var id = await SubmittedRequestAsync();
        AsHod();
        Assert.True((await Decide(id, ReviewDecision.Reject, RecruitmentRequestStatus.DepartmentHeadReview, comments: "Position already exists.")).Succeeded);

        AsStaff();
        var r = (await Details(id))!;
        Assert.Equal(RecruitmentRequestStatus.Rejected, r.Status);
        Assert.False(r.CanEdit || r.CanSubmit || r.CanCancel);
        Assert.False((await Run(s => s.SubmitAsync(id))).Succeeded);
        Assert.False((await Run(s => s.CancelAsync(id, null))).Succeeded);
    }

    [Fact]
    public async Task Requests_at_the_earlier_recruitment_review_stage_can_still_be_finished()
    {
        AsHod();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;
        using (var scope = _db.NewScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRecruitmentRequestRepository>();
            (await repo.GetByIdAsync(id))!.Status = RecruitmentRequestStatus.RecruitmentReview;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        As(_ro1, Roles.RecruitmentOfficer);
        Assert.Equal(1, await Run(s => s.CountAwaitingMeAsync()));
        Assert.True((await Decide(id, ReviewDecision.Approve, RecruitmentRequestStatus.RecruitmentReview)).Succeeded);
        Assert.Equal(RecruitmentRequestStatus.HrApproval, (await Details(id))!.Status);
    }

    // ------------------------------------------------------------------ visibility and documents

    [Fact]
    public async Task Other_departments_cannot_see_the_request_or_its_files()
    {
        var id = await SubmittedRequestAsync();
        var attachmentId = (await Details(id))!.Attachments.Single().Id;

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Details(id));
        Assert.Null(await Run(s => s.OpenAttachmentAsync(id, attachmentId)));
        Assert.Equal(0, (await Run(s => s.SearchAsync(new RecruitmentRequestFilter()))).TotalCount);

        // A colleague in the same department sees only their own requests.
        var colleague = await _db.CreateUserAsync("colleague@grel.test", Apm, Roles.Employee);
        As(colleague, Roles.Employee);
        Assert.Null(await Details(id));

        AsHod();
        Assert.NotNull(await Details(id));

        As(_md, Roles.ManagingDirector);
        Assert.NotNull(await Details(id));
        await using var file = (await Run(s => s.OpenAttachmentAsync(id, attachmentId)))!.Content;
        Assert.Equal("%PDF", Encoding.ASCII.GetString(new BinaryReader(file).ReadBytes(4)));
    }

    [Fact]
    public async Task Supporting_documents_accept_only_pdf_docx_and_xlsx_of_the_form_types()
    {
        AsStaff();
        var id = (await Run(s => s.CreateAsync(Form()))).Value;

        var exe = await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.DepartmentJustification, "tool.exe", new MemoryStream([0x4D, 0x5A, 0x90])));
        Assert.False(exe.Succeeded);

        // Images and old Word files are fine elsewhere in GRSMS, but not for QF-81-11.
        var png = await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.OrganisationalChart, "chart.png", new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D])));
        Assert.False(png.Succeeded);
        Assert.Contains(png.Errors, e => e.Contains(".pdf, .docx, .xlsx"));

        // A program renamed to .pdf does not start with "%PDF".
        var disguised = await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "jd.pdf", new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03])));
        Assert.False(disguised.Succeeded);

        var big = new MemoryStream();
        big.Write("%PDF"u8);
        big.Write(new byte[2 * 1024 * 1024]); // 2 MB, over this test's 1 MB limit
        big.Position = 0;
        Assert.False((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, "big.pdf", big))).Succeeded);

        // Categories that are not on the form are refused.
        Assert.False((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.InductionProgramme, "ind.pdf", FakePdf()))).Succeeded);

        // Folder names in the browser's file name are stripped; every form document type is accepted.
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.JobDescription, @"C:\fakepath\..\JD.pdf", FakePdf()))).Succeeded);
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.MainAccountabilities, "duties.docx", new MemoryStream(zip)))).Succeeded);
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.BudgetApproval, "budget.xlsx", new MemoryStream(zip)))).Succeeded);
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.OrganisationalChart, "structure.pdf", FakePdf()))).Succeeded);
        Assert.True((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.DepartmentJustification, "why.pdf", FakePdf()))).Succeeded);

        var docs = (await Details(id))!.Attachments;
        Assert.Equal(5, docs.Count);
        Assert.Contains(docs, a => a.FileName == "JD.pdf");
        Assert.Equal(RecruitmentRequestService.DocumentCategories.Order(), docs.Select(a => a.Category).Order());
    }

    [Fact]
    public async Task Files_cannot_be_changed_after_submission()
    {
        var id = await SubmittedRequestAsync();
        AsStaff();
        Assert.False((await Run(s => s.AddAttachmentAsync(id, AttachmentCategory.BudgetApproval, "more.pdf", FakePdf()))).Succeeded);
        var attachmentId = (await Details(id))!.Attachments.Single().Id;
        Assert.False((await Run(s => s.RemoveAttachmentAsync(id, attachmentId))).Succeeded);
    }

    [Fact]
    public async Task Simultaneous_saves_of_the_same_request_are_detected()
    {
        AsStaff();
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
        AsStaff();
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
        Assert.Equal([submitted], await Find(new() { DivisionId = 2, Status = RecruitmentRequestStatus.DepartmentHeadReview }));

        Assert.Equal([submitted, draft], await Find(new() { From = Today, To = Today }));
        Assert.Empty(await Find(new() { From = Today.AddDays(1) }));
        Assert.Empty(await Find(new() { To = Today.AddDays(-1) }));

        var row = (await Run(s => s.SearchAsync(new RecruitmentRequestFilter { DivisionId = 2 }))).Items.First();
        Assert.Equal("Operations", row.DivisionName);
    }
}
