using Grsms.Application.Ai;
using Grsms.Application.Applications;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-5: AI summaries with personal data. Each needs its own switch; only validated scores are summarised (panel
/// members anonymised); only a Recruitment Officer or HR Manager asks for them; the HOD of the department can read them; the
/// candidate cannot; the CV reader goes through the gateway when "CV reading" is on (with soft skills) and falls back otherwise.
/// </summary>
public class AiSummaryTests : IAsyncLifetime
{
    private const int Apm = 31;
    private readonly TestDatabase _db = new();
    private string _ro = "", _hod = "", _cand = "", _panel = "";
    private int _appId, _vacancyId;

    public async Task InitializeAsync()
    {
        _ro = await _db.CreateUserAsync("s.ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hod = await _db.CreateUserAsync("s.hod@grel.test", Apm, Roles.HeadOfDepartment);
        _panel = await _db.CreateUserAsync("s.panel@grel.test", Apm, Roles.Employee);
        _cand = await _db.CreateUserAsync("s.cand@mail.test", null, Roles.Candidate);
        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-S-1", DivisionId = 2, DepartmentId = Apm, SiteId = 3, PositionTitle = "Process Engineer", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "S",
            EstimatedAnnualCost = 1, RequestedByUserId = _hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-S-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm, SiteId = 3,
            RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1, VacancyType = VacancyType.External,
            JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors, ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        _vacancyId = v.Id;
        var app = new JobApplication
        {
            ApplicationNumber = "APP-S-1", VacancyId = v.Id, ApplicantUserId = _cand, Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = _hod, FullName = "Esi Owusu", Email = "esi@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors,
            Status = ApplicationStatus.Interview, SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow,
            CvText = "Esi Owusu. BSc Chemical Engineering. 5 years at Tema Oil Refinery. Team leader."
        };
        db.JobApplications.Add(app);
        await db.SaveChangesAsync();
        _appId = app.Id;

        var i = new Interview
        {
            JobApplicationId = app.Id, VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, StartsAt = DateTime.UtcNow.AddDays(-2),
            Location = "Board Room", ScheduledByUserId = _ro, Status = InterviewStatus.Scheduled
        };
        i.Panel.Add(new InterviewPanelMember { UserId = _panel, IsChair = true });
        i.Criteria.Add(new InterviewCriterion { Name = "Process knowledge", Weight = 100, SortOrder = 1 });
        db.Interviews.Add(i);
        await db.SaveChangesAsync();
        var card = new Scorecard
        {
            InterviewId = i.Id, PanelUserId = _panel, Recommendation = PanelRecommendation.Recommend, Strengths = "Clear on coagulation.",
            Concerns = "Little supervision experience.", SubmittedAt = DateTime.UtcNow, WeightedScore = 80
        };
        card.Ratings.Add(new ScorecardRating { CriterionId = i.Criteria.First().Id, Score = 4 });
        db.Scorecards.Add(card);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<T> Run<T>(Func<AiSummaryService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<AiSummaryService>());
    }

    private async Task SwitchOnAsync(params AiFeature[] features)
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
            { [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k" }
            .Concat(features.Select(f => KeyValuePair.Create(SettingCatalogue.AiFeatureKey(f), (string?)"true"))).ToDictionary());
    }

    private Task ValidateAsync() => Db(async db =>
    {
        (await db.Interviews.SingleAsync()).Status = InterviewStatus.Completed;
        await db.SaveChangesAsync();
    });

    private async Task Db(Func<ApplicationDbContext, Task> work)
    {
        using var scope = _db.NewScope();
        await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    [Fact]
    public async Task Interview_summary_uses_validated_scores_only_and_is_advisory_staff_only()
    {
        _db.CurrentUser.SignInAs(_ro, "s.ro@grel.test", Roles.RecruitmentOfficer);
        var panel = (await Run(s => s.GetInterviewPanelAsync(_appId)))!;
        Assert.False(panel.FeatureOn);
        Assert.Contains("switched off", (await Run(s => s.GenerateInterviewSummaryAsync(_appId))).Errors[0]);

        await SwitchOnAsync(AiFeature.InterviewSummary);
        panel = (await Run(s => s.GetInterviewPanelAsync(_appId)))!;
        Assert.False(panel.CanGenerate); // scores not validated yet
        Assert.Contains("validated", panel.Unavailable);

        await ValidateAsync();
        _db.Ai.Reply = _ => new AiReply("""
            {"overview":"Strong technical candidate.","strengths":["Process knowledge"],"weaknesses":["Supervision"],"recommendation":"HireWithReservations",
             "recommendation_reason":"Good scores, limited supervision.","risks":["Check references on supervision."]}
            """, 500, 200, "m");
        Assert.True((await Run(s => s.GenerateInterviewSummaryAsync(_appId))).Succeeded);
        var input = _db.Ai.Calls[^1].Call.Input;
        Assert.Contains("Panel member 1: total 80.0/100; Process knowledge 4/5", input);
        Assert.Contains("concerns: Little supervision experience.", input);
        Assert.DoesNotContain("s.panel", input); // panel members are anonymised
        Assert.Contains("do not use or infer age, gender", _db.Ai.Calls[^1].Call.Instructions);

        // Stored with the record; the HOD reads it but can't ask for a new one; the candidate sees nothing.
        _db.CurrentUser.SignInAs(_hod, "s.hod@grel.test", Roles.HeadOfDepartment);
        var hodView = (await Run(s => s.GetInterviewPanelAsync(_appId)))!;
        Assert.Equal(SummaryRecommendation.HireWithReservations, hodView.Latest!.Value.Recommendation);
        Assert.False(hodView.CanGenerate);
        Assert.False((await Run(s => s.GenerateInterviewSummaryAsync(_appId))).Succeeded);
        _db.CurrentUser.SignInAs(_cand, "s.cand@mail.test", Roles.Candidate);
        Assert.Null(await Run(s => s.GetInterviewPanelAsync(_appId)));

        // Personal data: never cached, so "Write again" calls the provider again.
        _db.CurrentUser.SignInAs(_ro, "s.ro@grel.test", Roles.RecruitmentOfficer);
        Assert.True((await Run(s => s.GenerateInterviewSummaryAsync(_appId))).Succeeded);
        Assert.Equal(2, _db.Ai.Calls.Count);
    }

    [Fact]
    public async Task Cv_summary_reads_the_cv_against_the_vacancy_and_the_cv_reader_goes_through_the_gateway()
    {
        _db.CurrentUser.SignInAs(_ro, "s.ro@grel.test", Roles.RecruitmentOfficer);
        await SwitchOnAsync(AiFeature.CvSummary);
        _db.Ai.Reply = _ => new AiReply("""
            {"summary":"Experienced engineer.","fit":"Strong","strengths":["5 years refinery"],"weaknesses":[],"missing_requirements":[],"soft_skills":["Leadership"]}
            """, 400, 100, "m");
        Assert.True((await Run(s => s.GenerateCvSummaryAsync(_appId))).Succeeded);
        Assert.Contains("BSc Chemical Engineering", _db.Ai.Calls[^1].Call.Input);
        Assert.Contains("Education: Bachelor", _db.Ai.Calls[^1].Call.Input);
        Assert.Equal(CvFit.Strong, (await Run(s => s.GetCvPanelAsync(_appId)))!.Latest!.Value.Fit);

        // CV reading: off → the earlier reader (no key here) is not configured; on → through the gateway, with soft skills.
        using (var scope = _db.NewScope())
            Assert.False(scope.ServiceProvider.GetRequiredService<IAiCvReader>().IsConfigured);
        await SwitchOnAsync(AiFeature.CvReading);
        _db.Ai.Reply = _ => new AiReply("""
            {"full_name":"Esi Owusu","email":"esi@mail.test","phone":"","highest_education":"Bachelors","education":["BSc Chemical Engineering"],
             "employment_history":[{"description":"Engineer, Tema Oil Refinery","from":"2020-01","to":"","is_current":true}],
             "skills":["Process control"],"soft_skills":["Leadership"],"certifications":[]}
            """, 300, 150, "m");
        using (var scope = _db.NewScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IAiCvReader>();
            Assert.True(reader.IsSwitchedOn);
            var parsed = await reader.ReadAsync("Esi Owusu ...", new DateOnly(2026, 11, 13));
            Assert.Equal(("ai", EducationLevel.Bachelors, 6), (parsed!.Reader, parsed.HighestEducation, parsed.YearsExperience!.Value));
            Assert.Equal(["Leadership"], parsed.SoftSkills);
        }
        using (var scope = _db.NewScope())
            Assert.Contains(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AiRequestLogs.ToListAsync(),
                l => l.Feature == AiFeature.CvReading && l.Outcome == AiOutcome.Success);
    }
}
