using Grsms.Application.Recruitment;
using Grsms.Application.Vacancies;
using Grsms.Domain.Organization;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Vacancy Management rules (Module 3): created only from approved requests, structured requirements,
/// HR Manager approval, close / reopen / archive, automatic closing and visibility.
/// </summary>
public class VacancyServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3, HeadOffice = 1; // 31 = seeded "Factory APM" (Operations), 3 = Finance (Corporate)
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro1 = "", _ro2 = "", _hrm1 = "", _hrm2 = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro1 = await _db.CreateUserAsync("ro1@grel.test", 1, Roles.RecruitmentOfficer);
        _ro2 = await _db.CreateUserAsync("ro2@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm1 = await _db.CreateUserAsync("hrm1@grel.test", 1, Roles.HrManager);
        _hrm2 = await _db.CreateUserAsync("hrm2@grel.test", 1, Roles.HrManager);

        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        db.Units.Add(new Unit { DepartmentId = Apm, Name = "Fire Safety", Code = "FIRE" });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsRecruiter() => As(_ro1, Roles.RecruitmentOfficer);

    private async Task<T> Run<T>(Func<VacancyService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<VacancyService>());
    }

    /// <summary>Puts a recruitment request straight into the database with the given status (skipping its own workflow).</summary>
    private async Task<int> RequestAsync(RecruitmentRequestStatus status = RecruitmentRequestStatus.Approved, int dept = Apm)
    {
        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = new RecruitmentRequest
        {
            RequestNumber = "RR-TEST-" + Guid.NewGuid().ToString("N")[..8],
            DivisionId = (await db.Departments.FindAsync(dept))!.DivisionId,
            DepartmentId = dept,
            SiteId = HeadOffice,
            PositionTitle = "Safety Officer",
            NumberOfPositions = 2,
            EmploymentType = EmploymentType.FixedTermContract,
            VacancyReason = VacancyReason.NewPosition,
            Justification = "Needed for the new processing plant.",
            BudgetCode = "HSE-26",
            EstimatedAnnualCost = 90000,
            RequestedByUserId = dept == Apm ? _hod : _otherHod,
            AssignedRecruiterUserId = _ro2,
            Status = status,
            StatusChangedAt = DateTime.UtcNow,
            ApprovedAt = status == RecruitmentRequestStatus.Approved ? DateTime.UtcNow : null
        };
        db.RecruitmentRequests.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private static VacancyFormDto Form(int requestId) => new()
    {
        RecruitmentRequestId = requestId,
        JobTitle = "Safety Officer",
        SiteId = HeadOffice,
        VacancyType = VacancyType.InternalAndExternal,
        JobDescription = "Leads daily safety inspections, investigates incidents and trains staff on safe work procedures.",
        MinimumEducation = EducationLevel.Bachelors,
        FieldOfStudy = "Occupational Health and Safety",
        MinYearsExperience = 3,
        EssentialSkills = "Risk assessment\n- Incident investigation\n\nrisk assessment",
        DesirableSkills = "Report writing\nIncident Investigation",
        EssentialCertifications = "NEBOSH IGC",
        SalaryMin = 6000,
        SalaryMax = 8000,
        ShowSalary = true,
        ClosingDate = Today.AddDays(14)
    };

    /// <summary>A draft vacancy created by Recruitment Officer 1 from a fresh approved request.</summary>
    private async Task<int> DraftAsync()
    {
        var requestId = await RequestAsync();
        AsRecruiter();
        var result = await Run(s => s.CreateAsync(Form(requestId)));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Value;
    }

    /// <summary>A vacancy submitted by RO1 and approved by HR Manager 1.</summary>
    private async Task<int> PublishedAsync()
    {
        var id = await DraftAsync();
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        As(_hrm1, Roles.HrManager);
        Assert.True((await Run(s => s.DecideAsync(id, new VacancyDecisionDto { Decision = VacancyDecision.Approve, ExpectedStatus = VacancyStatus.PendingApproval }))).Succeeded);
        return id;
    }

    private Task<VacancyDetailsDto?> Details(int id) => Run(s => s.GetDetailsAsync(id));

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task New_form_is_prefilled_from_the_approved_request()
    {
        var requestId = await RequestAsync();
        AsRecruiter();
        var form = (await Run(s => s.GetNewFormAsync(requestId))).Value!;

        Assert.Equal("Safety Officer", form.JobTitle);
        Assert.Equal(Apm, form.DepartmentId);
        Assert.Equal(2, form.NumberOfPositions);
        Assert.Equal(EmploymentType.FixedTermContract, form.EmploymentType);
        Assert.Equal(_hod, form.ReportingManagerUserId);       // the department's HOD by default
        Assert.Equal(Today.AddDays(14), form.ClosingDate);
    }

    [Fact]
    public async Task Vacancies_come_only_from_approved_requests_one_each_and_only_from_recruiters()
    {
        var notApproved = await RequestAsync(RecruitmentRequestStatus.MdApproval);
        var approved = await RequestAsync();

        AsRecruiter();
        Assert.False((await Run(s => s.CreateAsync(Form(notApproved)))).Succeeded);
        Assert.True((await Run(s => s.CreateAsync(Form(approved)))).Succeeded);
        var again = await Run(s => s.CreateAsync(Form(approved)));
        Assert.False(again.Succeeded);
        Assert.Contains(again.Errors, e => e.Contains("already exists"));

        var third = await RequestAsync();
        As(_hrm1, Roles.HrManager);
        Assert.False((await Run(s => s.CreateAsync(Form(third)))).Succeeded);
    }

    [Fact]
    public async Task Created_vacancy_takes_fixed_fields_from_the_request_and_parses_requirements()
    {
        var id = await DraftAsync();
        var v = (await Details(id))!;

        Assert.Equal($"VAC-{DateTime.UtcNow.Year}-0001", v.VacancyNumber);
        Assert.Equal(VacancyStatus.Draft, v.Status);
        Assert.Equal(2, v.NumberOfPositions);
        Assert.Equal("ro2", v.RecruiterName); // the request's assigned recruiter, not whoever typed it

        // Duplicates and bullets removed; listed as both essential and desirable → essential.
        var skills = v.Requirements.Where(r => r.Type == RequirementType.Skill).ToList();
        Assert.Equal(["Risk assessment", "Incident investigation", "Report writing"], skills.Select(s => s.Name));
        Assert.Equal([true, true, false], skills.Select(s => s.IsEssential));
        Assert.Single(v.Requirements, r => r.Type == RequirementType.Certification && r.Name == "NEBOSH IGC" && r.IsEssential);
    }

    [Fact]
    public async Task Editing_replaces_the_requirement_lists()
    {
        var id = await DraftAsync();
        var form = (await Run(s => s.GetForEditAsync(id)))!;
        form.EssentialSkills = "First aid";
        form.DesirableSkills = null;
        form.EssentialCertifications = null;
        Assert.True((await Run(s => s.UpdateAsync(form))).Succeeded);

        Assert.Equal(["First aid"], (await Details(id))!.Requirements.Select(r => r.Name));
        using var scope = _db.NewScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().VacancyRequirements.CountAsync());
    }

    [Fact]
    public async Task Closing_date_and_salary_are_validated()
    {
        var requestId = await RequestAsync();
        AsRecruiter();

        var past = Form(requestId); past.ClosingDate = Today;
        Assert.False((await Run(s => s.CreateAsync(past))).Succeeded);

        var badSalary = Form(requestId); badSalary.SalaryMin = 9000; badSalary.SalaryMax = 5000;
        Assert.False((await Run(s => s.CreateAsync(badSalary))).Succeeded);

        var noSalary = Form(requestId); noSalary.SalaryMin = noSalary.SalaryMax = null; noSalary.ShowSalary = true;
        Assert.False((await Run(s => s.CreateAsync(noSalary))).Succeeded);
    }

    [Fact]
    public async Task Submit_then_hr_approval_publishes_and_notifies()
    {
        var id = await DraftAsync();
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
        Assert.Equal(VacancyStatus.PendingApproval, (await Details(id))!.Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("hrm1@grel.test") && m.Contains("needs your approval"));

        // A Recruitment Officer cannot approve.
        Assert.False((await Run(s => s.DecideAsync(id, new VacancyDecisionDto { Decision = VacancyDecision.Approve, ExpectedStatus = VacancyStatus.PendingApproval }))).Succeeded);

        As(_hrm1, Roles.HrManager);
        Assert.True((await Run(s => s.DecideAsync(id, new VacancyDecisionDto { Decision = VacancyDecision.Approve, ExpectedStatus = VacancyStatus.PendingApproval }))).Succeeded);

        var v = (await Details(id))!;
        Assert.Equal(VacancyStatus.Published, v.Status);
        Assert.True(v.IsOpenForApplications);
        Assert.NotNull(v.PublishedAt);
        Assert.Contains(_db.SentEmails(), m => m.Contains("hod@grel.test") && m.Contains("published"));
        Assert.Equal(1, (await Run(s => s.CountsAsync())).Open);
    }

    [Fact]
    public async Task Returned_vacancies_go_back_to_draft_with_the_comments()
    {
        var id = await DraftAsync();
        await Run(s => s.SubmitAsync(id));

        As(_hrm1, Roles.HrManager);
        Assert.False((await Run(s => s.DecideAsync(id, new VacancyDecisionDto { Decision = VacancyDecision.Return, ExpectedStatus = VacancyStatus.PendingApproval }))).Succeeded);
        Assert.True((await Run(s => s.DecideAsync(id, new VacancyDecisionDto
        {
            Decision = VacancyDecision.Return, ExpectedStatus = VacancyStatus.PendingApproval, Comments = "Salary band is too high."
        }))).Succeeded);

        AsRecruiter();
        var v = (await Details(id))!;
        Assert.Equal(VacancyStatus.Draft, v.Status);
        Assert.Equal("Salary band is too high.", v.ReturnComments);
        Assert.True(v.CanEdit && v.CanSubmit);
        Assert.True((await Run(s => s.SubmitAsync(id))).Succeeded);
    }

    [Fact]
    public async Task Whoever_submitted_cannot_approve_and_stale_decisions_are_refused()
    {
        var requestId = await RequestAsync();
        var dual = await _db.CreateUserAsync("dual@grel.test", 1, Roles.RecruitmentOfficer, Roles.HrManager);
        As(dual, Roles.RecruitmentOfficer, Roles.HrManager);
        var id = (await Run(s => s.CreateAsync(Form(requestId)))).Value;
        await Run(s => s.SubmitAsync(id));

        var approve = new VacancyDecisionDto { Decision = VacancyDecision.Approve, ExpectedStatus = VacancyStatus.PendingApproval };
        Assert.False((await Run(s => s.DecideAsync(id, approve))).Succeeded);
        Assert.NotNull((await Details(id))!.DecisionBlockedReason);

        As(_hrm1, Roles.HrManager);
        Assert.True((await Run(s => s.DecideAsync(id, approve))).Succeeded);

        As(_hrm2, Roles.HrManager); // still had the old page open
        var stale = await Run(s => s.DecideAsync(id, approve));
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Errors, e => e.Contains("already moved on"));
    }

    [Fact]
    public async Task Close_reopen_and_archive_follow_the_life_cycle()
    {
        var id = await PublishedAsync();
        AsRecruiter();

        Assert.False((await Run(s => s.ArchiveAsync(id, null))).Succeeded);            // must close first
        Assert.True((await Run(s => s.CloseAsync(id, "Enough applicants"))).Succeeded);
        Assert.False((await Run(s => s.ReopenAsync(id, Today))).Succeeded);           // date must be in the future
        Assert.True((await Run(s => s.ReopenAsync(id, Today.AddDays(7)))).Succeeded);

        var reopened = (await Details(id))!;
        Assert.Equal(VacancyStatus.Published, reopened.Status);
        Assert.Equal(Today.AddDays(7), reopened.ClosingDate);

        Assert.True((await Run(s => s.CloseAsync(id, null))).Succeeded);
        Assert.True((await Run(s => s.ArchiveAsync(id, "Position filled"))).Succeeded);
        var archived = (await Details(id))!;
        Assert.Equal(VacancyStatus.Archived, archived.Status);
        Assert.False(archived.CanClose || archived.CanReopen || archived.CanArchive || archived.CanEdit);
    }

    [Fact]
    public async Task Vacancies_close_automatically_after_the_closing_date()
    {
        var expiring = await PublishedAsync();
        var stillOpen = await PublishedAsync();

        // Pretend the first one's closing date was yesterday and the second's is today.
        using (var scope = _db.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Vacancies.FindAsync(expiring))!.ClosingDate = Today.AddDays(-1);
            (await db.Vacancies.FindAsync(stillOpen))!.ClosingDate = Today;
            await db.SaveChangesAsync();
        }

        _db.CurrentUser.SignInAs(null!, null!); // the background job has no signed-in user
        Assert.Equal(1, await Run(s => s.CloseExpiredAsync(Today)));

        AsRecruiter();
        var closed = (await Details(expiring))!;
        Assert.Equal(VacancyStatus.Closed, closed.Status);
        Assert.Equal(VacancyAction.AutoClosed, closed.History.Last().Action);
        Assert.Equal("System", closed.History.Last().ActorName);
        Assert.Equal(VacancyStatus.Published, (await Details(stillOpen))!.Status); // the closing day itself still counts
    }

    [Fact]
    public async Task Hods_see_their_own_departments_vacancies_but_cannot_manage_them()
    {
        var id = await PublishedAsync();

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Details(id));
        Assert.Equal(0, (await Run(s => s.SearchAsync(new VacancyFilter { Tab = "all" }))).TotalCount);

        As(_hod, Roles.HeadOfDepartment);
        var v = (await Details(id))!;
        Assert.False(v.CanEdit || v.CanClose || v.CanDecide);
        Assert.False((await Run(s => s.CloseAsync(id, null))).Succeeded);
    }

    [Fact]
    public async Task The_request_page_links_to_its_vacancy()
    {
        var requestId = await RequestAsync();

        AsRecruiter();
        using (var scope = _db.NewScope())
        {
            var before = (await scope.ServiceProvider.GetRequiredService<RecruitmentRequestService>().GetDetailsAsync(requestId))!;
            Assert.True(before.CanCreateVacancy);
        }

        var vacancyId = (await Run(s => s.CreateAsync(Form(requestId)))).Value;
        using (var scope = _db.NewScope())
        {
            var after = (await scope.ServiceProvider.GetRequiredService<RecruitmentRequestService>().GetDetailsAsync(requestId))!;
            Assert.False(after.CanCreateVacancy);
            Assert.Equal(vacancyId, after.VacancyId);
        }
        Assert.Empty(await Run(s => s.GetApprovedRequestsWithoutVacancyAsync()));
    }

    [Fact]
    public async Task A_vacancy_copies_the_division_and_requesting_hod_and_can_be_filtered_by_them()
    {
        var id = await DraftAsync();   // from a Factory APM request (Operations)

        using (var scope = _db.NewScope())
        {
            var v = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Vacancies.SingleAsync(x => x.Id == id);
            Assert.Equal(2, v.DivisionId);
            Assert.Equal(_hod, v.RequestingHodUserId);
        }
        var details = (await Details(id))!;
        Assert.Equal("Operations", details.DivisionName);
        Assert.Equal("hod", details.RequestingHodName);

        AsRecruiter();
        async Task<int> Count(VacancyFilter f) => (await Run(s => s.SearchAsync(f))).TotalCount;
        Assert.Equal(1, await Count(new() { Tab = "all", DivisionId = 2 }));
        Assert.Equal(0, await Count(new() { Tab = "all", DivisionId = 1 }));
        Assert.Equal(1, await Count(new() { Tab = "all", DepartmentId = Apm, Status = VacancyStatus.Draft }));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(1, await Count(new() { Tab = "all", From = today, To = today }));
        Assert.Equal(0, await Count(new() { Tab = "all", From = today.AddDays(1) }));
    }

    // ------------------------------------------------------------------ converting a QF-81-11 request into a vacancy

    /// <summary>An approved request with every QF-81-11 field filled in, raised by an APM staff member (not the HOD).</summary>
    private async Task<int> FullRequestAsync()
    {
        var staff = await _db.CreateUserAsync("staff@grel.test", Apm, Roles.Employee);
        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = new RecruitmentRequest
        {
            RequestNumber = "RR-APM-QF-0001", DivisionId = 2, DepartmentId = Apm, SiteId = HeadOffice,
            PositionTitle = "Fleet Supervisor", NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent,
            StaffCategory = StaffCategory.Senior, VacancyReason = VacancyReason.Expansion,
            RequestDate = Today, ProposedStartDate = Today.AddDays(60),
            PostDescription = "<h3>Purpose</h3><p>Runs the <strong>estate fleet</strong>.</p><ul><li>Plans routes</li><li>Supervises drivers</li></ul>",
            Justification = "Fleet doubled with the new estates.", EducationLevel = EducationLevel.Hnd,
            QualificationCategory = "Automotive engineering", TrainingLevel = "Advanced", CertificateRequired = "Defensive driving\nFirst aid",
            MinYearsExperience = 4, ProfessionalExperience = "Supervising a fleet of 20+ vehicles",
            DrivingLicence = DrivingLicenceRequirement.Mandatory, DrivingLicenceClass = "C",
            BudgetCode = "TR-26", EstimatedAnnualCost = 70000, RequestedByUserId = staff,
            Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    [Fact]
    public async Task Converting_a_request_prefills_the_vacancy_from_the_form()
    {
        var requestId = await FullRequestAsync();
        AsRecruiter();
        var form = (await Run(s => s.GetNewFormAsync(requestId))).Value!;

        Assert.Equal("Fleet Supervisor", form.JobTitle);
        Assert.Equal("Purpose\nRuns the estate fleet.\n• Plans routes\n• Supervises drivers", form.JobDescription);
        Assert.Equal(EducationLevel.Hnd, form.MinimumEducation);
        Assert.Equal("Automotive engineering", form.FieldOfStudy);
        Assert.Equal("Level of training: Advanced", form.QualificationNotes);
        Assert.Equal(4, form.MinYearsExperience);
        Assert.Equal("Supervising a fleet of 20+ vehicles", form.ExperienceNotes);
        Assert.Equal("Defensive driving\nFirst aid\nDriving licence (class C)", form.EssentialCertifications);
        Assert.Contains("Do you hold a valid class C driving licence?", form.KnockoutQuestions);
        Assert.Equal(_ro1, form.OwnerUserId);   // the person converting it, by default
        Assert.Null(form.RequisitionNumber);   // given on saving
    }

    [Fact]
    public async Task Only_a_mandatory_driving_licence_becomes_a_knockout_question()
    {
        var requestId = await FullRequestAsync();
        async Task<VacancyFormDto> FormWith(DrivingLicenceRequirement licence)
        {
            using (var scope = _db.NewScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.RecruitmentRequests.FindAsync(requestId))!.DrivingLicence = licence;
                await db.SaveChangesAsync();
            }
            AsRecruiter();
            return (await Run(s => s.GetNewFormAsync(requestId))).Value!;
        }

        var desirable = await FormWith(DrivingLicenceRequirement.Desirable);
        Assert.Equal("Driving licence (class C)", desirable.DesirableCertifications);
        Assert.DoesNotContain("Driving licence", desirable.EssentialCertifications);
        Assert.DoesNotContain("driving licence", desirable.KnockoutQuestions ?? "");

        var notNeeded = await FormWith(DrivingLicenceRequirement.NotNeeded);
        Assert.Null(notNeeded.DesirableCertifications);
        Assert.DoesNotContain("Driving licence", notNeeded.EssentialCertifications);
        Assert.DoesNotContain("driving licence", notNeeded.KnockoutQuestions ?? "");
    }

    [Fact]
    public async Task A_converted_vacancy_gets_a_requisition_number_an_owner_and_the_departments_hod()
    {
        var requestId = await FullRequestAsync();
        AsRecruiter();
        var form = (await Run(s => s.GetNewFormAsync(requestId))).Value!;
        form.OwnerUserId = _ro2;
        form.VacancyType = VacancyType.Internal;
        form.ClosingDate = Today.AddDays(21);
        var created = await Run(s => s.CreateAsync(form));
        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var v = (await Details(created.Value))!;
        Assert.Equal($"JRN-{DateTime.UtcNow.Year}-0001", v.RequisitionNumber);
        Assert.Equal("ro2", v.RecruiterName);                // the chosen vacancy owner
        Assert.Equal("hod", v.RequestingHodName);            // the department's HOD, not the staff member who raised it
        Assert.Equal(VacancyType.Internal, v.VacancyType);
        Assert.Equal(Today.AddDays(21), v.ClosingDate);

        // The owner must be an active Recruitment Officer.
        var second = await SecondRequestAsync();
        var bad = (await Run(s => s.GetNewFormAsync(second))).Value!;
        bad.OwnerUserId = _hrm1;
        Assert.Contains((await Run(s => s.CreateAsync(bad))).Errors, e => e.Contains("vacancy owner"));

        // Requisition numbers are sequential.
        bad.OwnerUserId = _ro1;
        var next = (await Run(s => s.CreateAsync(bad))).Value;
        Assert.Equal($"JRN-{DateTime.UtcNow.Year}-0002", (await Details(next))!.RequisitionNumber);
    }

    /// <summary>A second approved request (plain, from <see cref="RequestAsync"/>).</summary>
    private Task<int> SecondRequestAsync() => RequestAsync();
}
