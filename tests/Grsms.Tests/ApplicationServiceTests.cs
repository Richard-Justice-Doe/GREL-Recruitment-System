using System.Text;
using Grsms.Application.Applications;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Applications;
using Grsms.Domain.Documents;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Candidate profiles and applications (Module 5): profile completeness, applying through a live advert (with a frozen
/// copy of the profile and CV), one application per vacancy, internal vacancies, withdrawal, the staff review steps,
/// privacy of internal comments, visibility and report filters.
/// </summary>
public class ApplicationServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro = "", _ro2 = "", _candidate = "", _candidate2 = "", _employee = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _ro2 = await _db.CreateUserAsync("ro2@grel.test", 1, Roles.RecruitmentOfficer);
        _candidate = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _candidate2 = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);
        _employee = await _db.CreateUserAsync("emp@grel.test", 26, Roles.Employee);

        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsCandidate(string? id = null) => As(id ?? _candidate, Roles.Candidate);
    private void AsRecruiter() => As(_ro, Roles.RecruitmentOfficer);

    private async Task<T> Run<T>(Func<ApplicationService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationService>());
    }

    private async Task<T> Profile<T>(Func<CandidateProfileService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<CandidateProfileService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static MemoryStream FakePdf(string text = "cv") => new(Encoding.ASCII.GetBytes($"%PDF-1.4\n% {text}\n%%EOF"));

    /// <summary>A published Factory APM vacancy with a live advert on the given channels. Returns the advert number.</summary>
    private async Task<(int VacancyId, string AdvertNumber)> LiveAdvertAsync(VacancyType type = VacancyType.InternalAndExternal,
        params AdvertChannel[] channels)
    {
        if (channels.Length == 0) channels = [AdvertChannel.CompanyWebsite];
        return await Db(async db =>
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var request = new RecruitmentRequest
            {
                RequestNumber = $"RR-T-{tag}", DivisionId = 2, DepartmentId = Apm, SiteId = 1, PositionTitle = "Safety Officer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = $"VAC-T-{tag}", RecruitmentRequestId = request.Id, JobTitle = "Safety Officer", DivisionId = 2,
                DepartmentId = Apm, SiteId = 1, RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = type, JobDescription = "Inspections.", MinimumEducation = EducationLevel.Diploma,
                ClosingDate = Today.AddDays(10), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            var a = new JobAdvert
            {
                AdvertNumber = $"ADV-T-{tag}", VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
                Title = "Safety Officer", Body = "Text", HowToApply = "Online", StartDate = Today, EndDate = Today.AddDays(10),
                Status = AdvertStatus.Live, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, CreatedByUserId = _ro
            };
            foreach (var c in channels) a.Channels.Add(new AdvertChannelPost { Channel = c, Status = ChannelPostStatus.Posted, PostedAt = DateTime.UtcNow });
            db.JobAdverts.Add(a);
            await db.SaveChangesAsync();
            return (v.Id, a.AdvertNumber);
        });
    }

    /// <summary>Fills in the current user's profile and uploads a CV.</summary>
    private async Task CompleteProfileAsync(string cvName = "My CV.pdf")
    {
        Assert.True((await Profile(s => s.SaveMineAsync(new ProfileFormDto
        {
            Phone = "024 123 4567", Location = "Takoradi", HighestEducation = EducationLevel.Bachelors, FieldOfStudy = "Safety",
            YearsExperience = 4, Skills = "Risk assessment\n- risk assessment\nFirst aid", Certifications = "NEBOSH IGC"
        }))).Succeeded);
        Assert.True((await Profile(s => s.AddDocumentAsync(AttachmentCategory.Cv, cvName, FakePdf()))).Succeeded);
    }

    private async Task<int> ApplyAsync(string advertNumber, ApplicationSource source = ApplicationSource.LinkedIn)
    {
        var result = await Run(s => s.ApplyAsync(new ApplyFormDto { AdvertNumber = advertNumber, Source = source, Confirm = true, CoverLetter = "I am keen." }));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Value;
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task A_complete_profile_with_a_cv_is_needed_and_a_new_cv_replaces_the_old_one()
    {
        var (_, advert) = await LiveAdvertAsync();
        AsCandidate();
        Assert.Equal(["your phone number", "your highest education", "your CV"], (await Profile(s => s.GetMineAsync())).Missing);
        var refused = await Run(s => s.ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.CareersPage, Confirm = true }));
        Assert.False(refused.Succeeded);
        Assert.Contains("your CV", refused.Errors[0]);

        await CompleteProfileAsync("Old CV.pdf");
        Assert.True((await Profile(s => s.AddDocumentAsync(AttachmentCategory.Cv, "New CV.pdf", FakePdf("new")))).Succeeded);
        Assert.True((await Profile(s => s.AddDocumentAsync(AttachmentCategory.Certificate, "NEBOSH.pdf", FakePdf("cert")))).Succeeded);
        var profile = await Profile(s => s.GetMineAsync());
        Assert.Empty(profile.Missing);
        Assert.Equal(["NEBOSH.pdf", "New CV.pdf"], profile.Documents.Select(d => d.FileName).Order().ToArray());
        Assert.Equal("Risk assessment\nFirst aid", profile.Skills); // cleaned list: bullets and duplicates removed

        // Not confirming the declaration is refused.
        Assert.False((await Run(s => s.ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.CareersPage, Confirm = false }))).Succeeded);
    }

    [Fact]
    public async Task Applying_freezes_a_copy_of_the_profile_and_documents_and_emails_both_sides()
    {
        var (vacancyId, advert) = await LiveAdvertAsync();
        AsCandidate();
        await CompleteProfileAsync();
        Assert.True((await Run(s => s.GetApplyStatusAsync(advert))).CanApply);

        var id = await ApplyAsync(advert);
        var stored = await Db(db => db.JobApplications.SingleAsync(a => a.Id == id));
        Assert.Matches(@"^APP-\d{4}-00001$", stored.ApplicationNumber);
        Assert.Equal((vacancyId, 2, Apm, _hod, false), (stored.VacancyId, stored.DivisionId, stored.DepartmentId, stored.RequestingHodUserId, stored.IsInternal));
        Assert.Equal(("024 123 4567", EducationLevel.Bachelors, 4, ApplicationSource.LinkedIn), (stored.Phone, stored.HighestEducation, stored.YearsExperience, stored.Source));

        // Later profile changes don't touch the application, and its CV copy survives the profile CV being removed.
        Assert.True((await Profile(s => s.SaveMineAsync(new ProfileFormDto { Phone = "020 000 0000", HighestEducation = EducationLevel.Masters }))).Succeeded);
        var profileCv = (await Profile(s => s.GetMineAsync())).Documents.Single();
        Assert.True((await Profile(s => s.RemoveDocumentAsync(profileCv.Id))).Succeeded);
        var mine = (await Run(s => s.GetMineAsync(id)))!;
        Assert.Equal("My CV.pdf", Assert.Single(mine.Documents).FileName);
        Assert.Equal("024 123 4567", (await Db(db => db.JobApplications.SingleAsync(a => a.Id == id))).Phone);
        Assert.NotNull(await Run(s => s.OpenMyDocumentAsync(id, mine.Documents[0].Id)));

        var emails = _db.SentEmails();
        Assert.Contains(emails, m => m.Contains("To: ama@mail.test") && m.Contains("We received your application"));
        Assert.Contains(emails, m => m.Contains("To: ro@grel.test") && m.Contains("New application"));

        // Only once per vacancy.
        var again = await Run(s => s.ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.CareersPage, Confirm = true }));
        Assert.False(again.Succeeded);
        var status = await Run(s => s.GetApplyStatusAsync(advert));
        Assert.Equal((false, id), (status.CanApply, status.ExistingApplicationId));
    }

    [Fact]
    public async Task Internal_vacancies_accept_staff_only_and_closed_ones_accept_nobody()
    {
        var (_, internalAdvert) = await LiveAdvertAsync(VacancyType.Internal, AdvertChannel.InternalPortal);
        AsCandidate();
        await CompleteProfileAsync();
        Assert.False((await Run(s => s.GetApplyStatusAsync(internalAdvert))).CanApply);
        Assert.False((await Run(s => s.GetApplyFormAsync(internalAdvert))).Succeeded);

        As(_employee, Roles.Employee);
        await CompleteProfileAsync();
        var id = await ApplyAsync(internalAdvert, ApplicationSource.InternalPortal);
        Assert.True((await Db(db => db.JobApplications.SingleAsync(a => a.Id == id))).IsInternal);

        // Once the vacancy closes, nobody can apply.
        var (vacancyId, advert) = await LiveAdvertAsync();
        await Db(async db => { (await db.Vacancies.FindAsync(vacancyId))!.Status = VacancyStatus.Closed; return await db.SaveChangesAsync(); });
        AsCandidate();
        Assert.False((await Run(s => s.GetApplyStatusAsync(advert))).CanApply);
        Assert.False((await Run(s => s.ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.CareersPage, Confirm = true }))).Succeeded);
    }

    [Fact]
    public async Task Applicants_see_and_withdraw_only_their_own_applications()
    {
        var (_, advert) = await LiveAdvertAsync();
        AsCandidate();
        await CompleteProfileAsync();
        var id = await ApplyAsync(advert);

        AsCandidate(_candidate2);
        Assert.Null(await Run(s => s.GetMineAsync(id)));
        Assert.Empty(await Run(s => s.MyApplicationsAsync()));
        Assert.False((await Run(s => s.WithdrawAsync(id))).Succeeded);
        Assert.Null(await Run(s => s.GetDetailsAsync(id))); // candidates never reach the staff view

        AsCandidate();
        Assert.Single(await Run(s => s.MyApplicationsAsync()));
        Assert.True((await Run(s => s.WithdrawAsync(id))).Succeeded);
        var mine = (await Run(s => s.GetMineAsync(id)))!;
        Assert.Equal(ApplicationStatus.Withdrawn, mine.Status);
        Assert.False(mine.CanWithdraw);
        Assert.False((await Run(s => s.WithdrawAsync(id))).Succeeded);
    }

    [Fact]
    public async Task Recruiters_review_and_the_internal_reason_never_reaches_the_applicant()
    {
        var (_, advert) = await LiveAdvertAsync();
        AsCandidate();
        await CompleteProfileAsync();
        var id = await ApplyAsync(advert);

        // A HOD of another department can't see it; the department's own HOD can, but can't act.
        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Run(s => s.GetDetailsAsync(id)));
        As(_hod, Roles.HeadOfDepartment);
        var hodView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.False(hodView.CanStartReview || hodView.CanMarkNotSuccessful);
        Assert.False((await Run(s => s.StartReviewAsync(id))).Succeeded);
        Assert.Equal(["First aid", "Risk assessment"], hodView.Skills.Order().ToArray());

        AsRecruiter();
        Assert.True((await Run(s => s.StartReviewAsync(id))).Succeeded);
        Assert.False((await Run(s => s.MarkNotSuccessfulAsync(id, ""))).Succeeded);   // a reason is required
        Assert.True((await Run(s => s.MarkNotSuccessfulAsync(id, "Does not meet the NEBOSH requirement"))).Succeeded);
        Assert.Equal(ApplicationStatus.NotSuccessful, (await Run(s => s.GetDetailsAsync(id)))!.Status);

        // The applicant sees the outcome, but not the reason, on the tracker or in the email.
        AsCandidate();
        var mine = (await Run(s => s.GetMineAsync(id)))!;
        Assert.Equal(ApplicationStatus.NotSuccessful, mine.Status);
        Assert.Equal(ApplicationStatus.UnderReview, mine.LastStageReached);
        Assert.DoesNotContain(mine.Timeline, t => t.What.Contains("NEBOSH"));
        var outcomeEmail = _db.SentEmails().Single(m => m.Contains("To: ama@mail.test") && m.Contains("Your application for"));
        Assert.DoesNotContain("NEBOSH", outcomeEmail);
    }

    [Fact]
    public async Task Nobody_reviews_their_own_application()
    {
        var (_, advert) = await LiveAdvertAsync();
        As(_ro2, Roles.RecruitmentOfficer);
        await CompleteProfileAsync();
        var id = await ApplyAsync(advert);

        var own = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.False(own.CanStartReview);
        Assert.NotNull(own.ActionBlockedReason);
        Assert.False((await Run(s => s.StartReviewAsync(id))).Succeeded);

        AsRecruiter();
        Assert.True((await Run(s => s.StartReviewAsync(id))).Succeeded);
    }

    [Fact]
    public async Task Staff_list_filters_by_division_department_vacancy_source_applicant_type_and_period()
    {
        var (vacancyId, advert) = await LiveAdvertAsync();
        AsCandidate();
        await CompleteProfileAsync();
        await ApplyAsync(advert, ApplicationSource.Jobberman);
        As(_employee, Roles.Employee);
        await CompleteProfileAsync();
        await ApplyAsync(advert, ApplicationSource.CareersPage);

        AsRecruiter();
        async Task<int> Count(ApplicationFilter f) => (await Run(s => s.SearchAsync(f))).TotalCount;
        Assert.Equal(2, await Count(new() { Tab = "new" }));
        Assert.Equal(0, await Count(new() { Tab = "review" }));
        Assert.Equal(2, await Count(new() { Tab = "all", DivisionId = 2, DepartmentId = Apm, VacancyId = vacancyId }));
        Assert.Equal(0, await Count(new() { Tab = "all", DivisionId = 1 }));
        Assert.Equal(1, await Count(new() { Tab = "all", Source = ApplicationSource.Jobberman }));
        Assert.Equal(1, await Count(new() { Tab = "all", Internal = true }));
        Assert.Equal(1, await Count(new() { Tab = "all", Internal = false, Search = "ama@" }));
        Assert.Equal(2, await Count(new() { Tab = "all", From = Today, To = Today }));
        Assert.Equal(0, await Count(new() { Tab = "all", From = Today.AddDays(1) }));
        Assert.Equal(new ApplicationCounts(2, 0, 2), await Run(s => s.CountsAsync()));
    }
}
