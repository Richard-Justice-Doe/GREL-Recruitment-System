using System.Text;
using Grsms.Application.Applications;
using Grsms.Application.Shortlisting;
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
/// CV screening and shortlisting (Module 6): the scoring rules, scoring on submission, the ranked board, and the
/// recommend → HOD view → shortlist steps, including the HR Manager override, notifications and permissions.
/// </summary>
public class ShortlistingServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro = "", _hrm = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);

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
    private void AsRecruiter() => As(_ro, Roles.RecruitmentOfficer);
    private void AsHod() => As(_hod, Roles.HeadOfDepartment);

    private async Task<T> Run<T>(Func<ShortlistingService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ShortlistingService>());
    }

    private async Task<T> Scoped<T>(Func<IServiceProvider, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider);
    }

    private Task<JobApplication> Application(int id) =>
        Scoped(sp => sp.GetRequiredService<ApplicationDbContext>().JobApplications.SingleAsync(a => a.Id == id));

    /// <summary>
    /// A published Factory APM vacancy (Diploma, 3 years; essential "NEBOSH IGC" and "Risk assessment", desirable
    /// "Report writing") with a live careers-page advert. Returns the vacancy id and advert number.
    /// </summary>
    private Task<(int VacancyId, string AdvertNumber)> VacancyAsync() => Scoped(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-S-1", DivisionId = 2, DepartmentId = Apm, SiteId = 1, PositionTitle = "Safety Officer",
            NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
            Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
            Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-S-1", RecruitmentRequestId = request.Id, JobTitle = "Safety Officer", DivisionId = 2, DepartmentId = Apm,
            SiteId = 1, RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent,
            VacancyType = VacancyType.InternalAndExternal, JobDescription = "Inspections.", MinimumEducation = EducationLevel.Diploma,
            MinYearsExperience = 3, ClosingDate = Today.AddDays(10), Status = VacancyStatus.Published,
            StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Certification, Name = "NEBOSH IGC", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Risk assessment", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Report writing", IsEssential = false });
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        var a = new JobAdvert
        {
            AdvertNumber = "ADV-S-1", VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, Title = "Safety Officer",
            Body = "Text", HowToApply = "Online", StartDate = Today, EndDate = Today.AddDays(10), Status = AdvertStatus.Live,
            StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, CreatedByUserId = _ro
        };
        a.Channels.Add(new AdvertChannelPost { Channel = AdvertChannel.CompanyWebsite, Status = ChannelPostStatus.Posted, PostedAt = DateTime.UtcNow });
        db.JobAdverts.Add(a);
        await db.SaveChangesAsync();
        return (v.Id, a.AdvertNumber);
    });

    /// <summary>A new candidate with the given profile applies through the advert. Returns the application id.</summary>
    private async Task<int> ApplicantAsync(string advert, string email, EducationLevel education, int years, string skills, string? certifications = null)
    {
        var id = await _db.CreateUserAsync(email, null, Roles.Candidate);
        As(id, Roles.Candidate);
        await Scoped(async sp =>
        {
            var profiles = sp.GetRequiredService<CandidateProfileService>();
            await profiles.SaveMineAsync(new ProfileFormDto
            {
                Phone = "0240000000", HighestEducation = education, YearsExperience = years, Skills = skills, Certifications = certifications
            });
            return await profiles.AddDocumentAsync(AttachmentCategory.Cv, "cv.pdf", new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\n%x\n%%EOF")));
        });
        var applied = await Scoped(sp => sp.GetRequiredService<ApplicationService>()
            .ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.CareersPage, Confirm = true }));
        Assert.True(applied.Succeeded, string.Join(" ", applied.Errors));
        return applied.Value;
    }

    // ------------------------------------------------------------------ the scoring rules

    [Fact]
    public void Ats_scores_the_four_parts_classifies_and_applies_only_the_rules_switched_on()
    {
        var v = new Vacancy { MinimumEducation = EducationLevel.Diploma, MinYearsExperience = 4 };
        v.Requirements.Add(new VacancyRequirement { Id = 1, Type = RequirementType.Certification, Name = "NEBOSH IGC", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Id = 2, Type = RequirementType.Skill, Name = "Risk assessment", IsEssential = true });
        v.Requirements.Add(new VacancyRequirement { Id = 3, Type = RequirementType.Skill, Name = "Report writing", IsEssential = false });
        v.Requirements.Add(new VacancyRequirement { Id = 4, Type = RequirementType.Certification, Name = "First aid", IsEssential = false });
        var settings = new AtsSettings();

        // Everything met: words found in any order, case and punctuation ignored.
        var full = AtsEngine.Score(v, new JobApplication
        {
            HighestEducation = EducationLevel.Bachelors, YearsExperience = 6,
            Skills = "Risk Assessment\nReport-writing", Certifications = "NEBOSH International General Certificate (IGC)\nFirst Aid"
        }, null, null, [], settings);
        Assert.Equal((100, AtsClassification.HighlyRecommended, true, false), (full.Overall, full.Classification, full.MeetsMinimum, full.AutoReject));

        // One level below (60), half the years (50), skills: required found, preferred not = 2/3 (67).
        // Certifications: the profile has none, but the CV text mentions "First Aid" → the preferred one is found = 1/3 (33).
        var app = new JobApplication { HighestEducation = EducationLevel.Certificate, YearsExperience = 2, Skills = "risk assessment" };
        var partial = AtsEngine.Score(v, app, null, "Courses: First Aid at Work (2021)", [new("Do you have a valid driver's licence?", false)], settings);
        Assert.Equal((60, 50, 67, 33), (partial.Education, partial.Experience, partial.Skills, partial.Certifications));
        Assert.Equal(53, partial.Overall); // (60 + 50 + 67 + 33) / 4 = 52.5 → 53
        Assert.Equal(AtsClassification.LowMatch, partial.Classification);
        Assert.Equal(["NEBOSH IGC"], partial.MissingRequiredCertifications);
        Assert.False(partial.MeetsMinimum);
        // Default rules: knockout, education, experience and a missing required certification (the score is above 50).
        Assert.Equal(4, partial.RejectionReasons.Count);
        Assert.Contains(partial.RejectionReasons, r => r.Contains("driver's licence"));

        // With every rule switched off, nothing is rejected.
        var lenient = new AtsSettings
        {
            RejectOnKnockout = false, RejectBelowMinimumEducation = false, RejectBelowMinimumExperience = false,
            RejectMissingRequiredCertification = false, RejectBelowLowMatch = false
        };
        Assert.False(AtsEngine.Score(v, app, null, null, [new("Q?", false)], lenient).AutoReject);

        // The example from the request: 90, 100, 75, 80 with equal weights → 86 %, "Recommended".
        Assert.Equal(AtsClassification.Recommended, AtsEngine.Classify(86, settings));
        Assert.Equal(AtsClassification.AutoReject, AtsEngine.Classify(49, settings));

        // A vacancy with no requirements gives full marks.
        Assert.Equal(100, AtsEngine.Score(new Vacancy(), new JobApplication(), null, null, [], settings).Overall);

        // Weights and bands are checked before they are saved.
        Assert.NotEmpty(new AtsSettings { SkillsWeight = 30 }.Validate());
        Assert.NotEmpty(new AtsSettings { RecommendedFrom = 95 }.Validate());
        Assert.Empty(settings.Validate());
    }

    // ------------------------------------------------------------------ the workflow

    [Fact]
    public async Task Applications_are_scored_on_submission_and_ranked_on_the_board()
    {
        var (vacancyId, advert) = await VacancyAsync();
        var weak = await ApplicantAsync(advert, "weak@mail.test", EducationLevel.Secondary, 1, "Driving");
        var strong = await ApplicantAsync(advert, "strong@mail.test", EducationLevel.Hnd, 5, "Risk assessment\nReport writing", "NEBOSH IGC");
        var close = await ApplicantAsync(advert, "close@mail.test", EducationLevel.Diploma, 3, "Risk assessment");

        var s = await Application(strong);
        Assert.Equal((100, true), (s.ScreeningScore, s.MeetsMinimum));

        AsRecruiter();
        var board = (await Run(x => x.GetBoardAsync(vacancyId)))!;
        Assert.Equal([strong, close, weak], board.Rows.Select(r => r.ApplicationId).ToArray());
        Assert.Equal(["NEBOSH IGC"], board.Rows[1].MissingEssential);
        Assert.Equal(["NEBOSH IGC", "Risk assessment"], board.EssentialItems.Order().ToArray());
        Assert.Equal(3, (await Run(x => x.ScreenVacancyAsync(vacancyId))).Value);

        // Other departments' HODs can't open the board; the department's HOD can.
        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Run(x => x.GetBoardAsync(vacancyId)));
        AsHod();
        Assert.True((await Run(x => x.GetBoardAsync(vacancyId)))!.IsHod);
        Assert.False((await Run(x => x.ScreenVacancyAsync(vacancyId))).Succeeded);
    }

    [Fact]
    public async Task Recruiter_recommends_the_hod_agrees_and_the_applicant_is_shortlisted()
    {
        var (vacancyId, advert) = await VacancyAsync();
        var id = await ApplicantAsync(advert, "ama@mail.test", EducationLevel.Hnd, 5, "Risk assessment", "NEBOSH IGC");

        // The HOD can't give a view before the recruiter recommends.
        AsHod();
        Assert.False((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);

        AsRecruiter();
        Assert.False((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.DoNotShortlist, null))).Succeeded); // note required
        Assert.True((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.Shortlist, "Strong safety background"))).Succeeded);
        Assert.Equal(ApplicationStatus.UnderReview, (await Application(id)).Status); // moved into review automatically
        Assert.Equal(1, (await Run(x => x.SendToHodAsync(vacancyId))).Value);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("needs your view"));

        // The HOD sees it in their tasks; another HOD can't respond; disagreeing needs a comment.
        AsHod();
        var task = Assert.Single(await Run(x => x.HodTasksAsync()));
        Assert.Equal((vacancyId, 1), (task.VacancyId, task.Waiting));
        As(_otherHod, Roles.HeadOfDepartment);
        Assert.False((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        AsHod();
        Assert.False((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.DoNotShortlist, null))).Succeeded);
        Assert.True((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        Assert.Empty(await Run(x => x.HodTasksAsync()));
        Assert.False((await Run(x => x.ShortlistAsync(id, null))).Succeeded); // the HOD doesn't shortlist

        AsRecruiter();
        Assert.True((await Run(x => x.ShortlistAsync(id, null))).Succeeded);
        var a = await Application(id);
        Assert.Equal(ApplicationStatus.Shortlisted, a.Status);
        Assert.NotNull(a.ShortlistedAt);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("shortlisted"));
        Assert.Equal(1, (await Run(x => x.GetBoardAsync(vacancyId)))!.Shortlisted);
    }

    [Fact]
    public async Task Without_the_hods_agreement_only_an_hr_manager_can_shortlist_with_a_reason()
    {
        var (_, advert) = await VacancyAsync();
        var id = await ApplicantAsync(advert, "kofi@mail.test", EducationLevel.Diploma, 3, "Risk assessment");

        // Phase 15: NEBOSH is required, so the ATS holds the application as rejected; the recruiter overrides it to review it.
        var held = await Scoped(sp => sp.GetRequiredService<ApplicationDbContext>().JobApplications.SingleAsync(x => x.Id == id));
        Assert.Equal((ApplicationStatus.AutoRejected, AtsDecision.AutoRejected), (held.Status, held.AtsDecision));
        Assert.Contains("NEBOSH IGC", held.AtsRejectionReason);
        AsRecruiter();
        Assert.False((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        Assert.True((await Scoped(sp => sp.GetRequiredService<AtsService>().OverrideAsync(id, "Strong field record; NEBOSH can follow"))).Succeeded);

        Assert.True((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        AsHod();
        Assert.True((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.DoNotShortlist, "No NEBOSH yet"))).Succeeded);

        AsRecruiter();
        Assert.False((await Run(x => x.ShortlistAsync(id, "We are short of candidates"))).Succeeded);

        As(_hrm, Roles.HrManager);
        Assert.False((await Run(x => x.ShortlistAsync(id, null))).Succeeded); // the reason is required
        Assert.True((await Run(x => x.ShortlistAsync(id, "Only two qualified applicants; NEBOSH can be obtained in probation"))).Succeeded);
        var a = await Scoped(sp => sp.GetRequiredService<ApplicationDbContext>().JobApplications.Include(x => x.Actions).SingleAsync(x => x.Id == id));
        Assert.Equal(ApplicationStatus.Shortlisted, a.Status);
        Assert.Contains(a.Actions, x => x.Action == ApplicationAction.ShortlistedOverride && x.Comments!.Contains("probation"));
    }

    [Fact]
    public async Task Changing_the_recommendation_asks_the_hod_again_and_nobody_handles_their_own_application()
    {
        var (_, advert) = await VacancyAsync();
        var id = await ApplicantAsync(advert, "efua@mail.test", EducationLevel.Hnd, 4, "Risk assessment", "NEBOSH IGC");

        AsRecruiter();
        Assert.True((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        AsHod();
        Assert.True((await Run(x => x.HodReviewAsync(id, ShortlistRecommendation.Shortlist, null))).Succeeded);
        AsRecruiter();
        Assert.True((await Run(x => x.RecommendAsync(id, ShortlistRecommendation.DoNotShortlist, "References came back poor"))).Succeeded);
        var a = await Application(id);
        Assert.Equal(ShortlistRecommendation.None, a.HodRecommendation);

        // A recruiter who applies can't recommend or shortlist themselves.
        As(_ro, Roles.RecruitmentOfficer);
        var own = await Scoped(async sp =>
        {
            var profiles = sp.GetRequiredService<CandidateProfileService>();
            await profiles.SaveMineAsync(new ProfileFormDto { Phone = "0200000000", HighestEducation = EducationLevel.Masters });
            await profiles.AddDocumentAsync(AttachmentCategory.Cv, "cv.pdf", new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\n%x\n%%EOF")));
            return (await sp.GetRequiredService<ApplicationService>()
                .ApplyAsync(new ApplyFormDto { AdvertNumber = advert, Source = ApplicationSource.InternalPortal, Confirm = true })).Value;
        });
        Assert.False((await Run(x => x.RecommendAsync(own, ShortlistRecommendation.Shortlist, null))).Succeeded);
        var ownVacancy = (await Application(own)).VacancyId;
        Assert.True((await Run(x => x.GetBoardAsync(ownVacancy)))!.Rows.Single(r => r.ApplicationId == own).IsOwn);
    }
}
