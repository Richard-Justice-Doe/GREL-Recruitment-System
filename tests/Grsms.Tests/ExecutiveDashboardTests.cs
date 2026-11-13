using Grsms.Application.Ai;
using Grsms.Application.Dashboard;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Applications;
using Grsms.Domain.Employees;
using Grsms.Domain.Hiring;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-3: the executive dashboard's figures (worked out by GRSMS), the like-for-like quarter comparison, department
/// vacancy rates, and the AI summary, which is sent totals only (no names) and told to use only those figures.
/// </summary>
public class ExecutiveDashboardTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;
    private readonly TestDatabase _db = new();
    private static readonly DateOnly Today = new(2026, 11, 13); // Q4 2026, day 44 → compared with 1 Jul – 13 Aug

    public async Task InitializeAsync()
    {
        var md = await _db.CreateUserAsync("x.md@grel.test", 1, Roles.ManagingDirector);
        _db.CurrentUser.SignInAs(md, "x.md@grel.test", Roles.ManagingDirector);
        var hod = await _db.CreateUserAsync("x.hod@grel.test", Apm, Roles.HeadOfDepartment);
        var cands = new List<string>();
        for (var i = 1; i <= 5; i++) cands.Add(await _db.CreateUserAsync($"x.cand{i}@mail.test", null, Roles.Candidate));
        using var scope = _db.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var request = new RecruitmentRequest
        {
            RequestNumber = "RR-X-1", DivisionId = 2, DepartmentId = Apm, SiteId = 3, PositionTitle = "Tapper", NumberOfPositions = 3,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
            EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow,
            DateRequested = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        db.RecruitmentRequests.Add(request);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-X-1", RecruitmentRequestId = request.Id, JobTitle = "Tapper", DivisionId = 2, DepartmentId = Apm, SiteId = 3,
            RequestingHodUserId = hod, RecruiterUserId = hod, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 3, VacancyType = VacancyType.External,
            JobDescription = "Taps rubber.", ClosingDate = Today.AddDays(5), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();

        JobApplication App(string n, DateTime at) => new()
        {
            ApplicationNumber = "APP-X-" + n, VacancyId = v.Id, ApplicantUserId = cands[int.Parse(n) - 1], Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = hod, FullName = "Kwame Secret-Name", Email = "k@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Basic,
            Status = ApplicationStatus.Submitted, SubmittedAt = at, StatusChangedAt = at
        };
        db.JobApplications.AddRange(App("1", new DateTime(2026, 10, 5)), App("2", new DateTime(2026, 11, 2)), App("3", new DateTime(2026, 11, 12)),
            App("4", new DateTime(2026, 7, 10)),                // previous quarter, before the same point → counted
            App("5", new DateTime(2026, 9, 20)));               // previous quarter, after the same point → not counted
        await db.SaveChangesAsync();
        var appId = db.JobApplications.First().Id;
        var rec = new HiringRecommendation
        {
            Number = "HIR-X-1", JobApplicationId = appId, VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = hod, ProposedSalary = 2000,
            ProposedStartDate = Today.AddDays(10), Justification = "Good.", CreatedByUserId = hod, Status = HiringStatus.Approved
        };
        db.HiringRecommendations.Add(rec);
        await db.SaveChangesAsync();
        db.Offers.Add(new Offer
        {
            Number = "OFF-X-1", JobApplicationId = appId, HiringRecommendationId = rec.Id, VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = hod,
            JobTitle = "Tapper", MonthlySalary = 2000, StartDate = Today.AddDays(10), ExpiresOn = Today, Status = OfferStatus.Accepted,
            SentAt = new DateTime(2026, 10, 25), SentByUserId = hod, RespondedAt = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc)
        });
        db.Employees.AddRange(
            new Employee { EmployeeNumber = "E1", UserId = hod, PositionTitle = "HOD", DepartmentId = Apm, SiteId = 3, HireDate = new DateOnly(2020, 1, 1), Status = EmployeeStatus.Active },
            new Employee { EmployeeNumber = "E2", UserId = md, PositionTitle = "Accountant", DepartmentId = Finance, SiteId = 1, HireDate = new DateOnly(2026, 10, 20),
                Status = EmployeeStatus.OnProbation, ContractEndDate = Today.AddDays(60) });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<T> Run<T>(Func<ExecutiveDashboardService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ExecutiveDashboardService>());
    }

    private static ExecutiveKpi K(IEnumerable<ExecutiveKpi> list, string key) => list.Single(k => k.Key == key);

    [Fact]
    public void Quarters_compare_like_for_like()
    {
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 13)), ExecutiveDashboardService.Quarter(Today));
        // Late in a long quarter, the comparison stops at the end of the previous quarter.
        Assert.Equal(new DateOnly(2026, 6, 30), ExecutiveDashboardService.Quarter(new DateOnly(2026, 9, 30)).PrevSamePoint);
    }

    [Fact]
    public async Task Figures_come_from_the_records_and_the_ai_summary_gets_totals_only()
    {
        var d = await Run(s => s.GetAsync(Today));
        Assert.Equal("Q4 2026", d.QuarterLabel);
        Assert.Equal((3m, 1m), (K(d.Recruitment, "applications").Value, K(d.Recruitment, "applications").Previous!.Value));
        Assert.Equal(200m, K(d.Recruitment, "applications").ChangePercent);
        Assert.Equal(1m, K(d.Recruitment, "hires").Value);
        Assert.Equal(30m, K(d.Recruitment, "time-to-hire").Value); // 1 Oct → 31 Oct
        Assert.Equal(2m, K(d.Recruitment, "open-positions").Value); // 3 positions, 1 accepted
        Assert.Equal((2m, 1m, 1m), (K(d.Workforce, "headcount").Value, K(d.Workforce, "new-hires").Value, K(d.Workforce, "contracts").Value));
        var apm = d.Departments.Single(f => f.OpenPositions > 0);
        Assert.Equal((1, 2, 66.7m), (apm.Headcount, apm.OpenPositions, apm.VacancyRate));
        Assert.False(d.AiOn);

        using (var scope = _db.NewScope())
            await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?>
                { [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiApiKey] = "k", [SettingCatalogue.AiFeatureKey(AiFeature.ExecutiveSummary)] = "true" });
        _db.Ai.Reply = _ => new AiReply("""{"headline":"Applications tripled.","highlights":["3 applications."],"risks":[],"actions":["Fill 2 positions."]}""", 400, 80, "m");
        var r = await Run(s => s.SummaryAsync(Today, false));
        Assert.True(r.Succeeded);
        Assert.Equal("Applications tripled.", r.Value!.Headline);
        var input = _db.Ai.Calls[^1].Call.Input;
        Assert.Contains("- Applications received: 3 (previous: 1, change +200 %)", input);
        Assert.DoesNotContain("Kwame", input);         // no names
        Assert.Contains("Use ONLY these figures", _db.Ai.Calls[^1].Call.Instructions);

        // The same figures: answered from the cache, no second call.
        var again = await Run(s => s.SummaryAsync(Today, false));
        Assert.True(again.FromCache);
        Assert.Single(_db.Ai.Calls);
    }
}
