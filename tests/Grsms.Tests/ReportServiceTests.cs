using Grsms.Application.Reports;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Grsms.Web.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Reports (Module 11): the figures, the funnel (furthest stage reached, from the history), time to hire, sources and
/// offers; the five filters (division, department, vacancy, status, period); HODs limited to their departments; and the
/// CSV export's protection against spreadsheet formulas.
/// </summary>
public class ReportServiceTests : IAsyncLifetime
{
    // APM factory (Operations, division 2) and Finance (Corporate, division 1).
    private const int Apm = 31, Finance = 3;
    private static readonly DateTime Base = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _db = new();
    private string _hod = "", _ro = "", _md = "";
    private int _apmVacancy, _finVacancy;

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _md = await _db.CreateUserAsync("md@grel.test", 1, Roles.ManagingDirector);
        var finHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);

        await Db(async db =>
        {
            (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
            (await db.Departments.FindAsync(Finance))!.HeadOfDepartmentUserId = finHod;
            await db.SaveChangesAsync();
            return 0;
        });

        // APM: requested 1 Sep, approved 11 Sep, published 15 Sep, 2 positions.
        _apmVacancy = await VacancyAsync("APM", 2, Apm, _hod, 2, requested: Base, approved: Base.AddDays(10), published: Base.AddDays(14));
        // Finance: requested 1 Oct, approved 6 Oct, published 8 Oct, 1 position.
        _finVacancy = await VacancyAsync("FIN", 1, Finance, finHod, 1, requested: Base.AddDays(30), approved: Base.AddDays(35), published: Base.AddDays(37));

        // APM applications: Ama (hired via LinkedIn), Kofi (interviewed, then not successful), Esi (received only).
        var ama = await ApplicationAsync(_apmVacancy, 2, Apm, "Ama", ApplicationSource.LinkedIn, ApplicationStatus.Hired, Base.AddDays(16),
            [ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Hired]);
        await ApplicationAsync(_apmVacancy, 2, Apm, "=HYPERLINK(\"x\")", ApplicationSource.CareersPage, ApplicationStatus.NotSuccessful, Base.AddDays(17),
            [ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.NotSuccessful]);
        await ApplicationAsync(_apmVacancy, 2, Apm, "Esi", ApplicationSource.CareersPage, ApplicationStatus.Submitted, Base.AddDays(18), []);
        // Finance: Yaw, offered and declined (October).
        var yaw = await ApplicationAsync(_finVacancy, 1, Finance, "Yaw", ApplicationSource.Jobberman, ApplicationStatus.OfferDeclined, Base.AddDays(40),
            [ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.OfferDeclined]);

        // Ama accepted on 21 Sep (20 days after the request); Yaw declined.
        await OfferAsync(ama, _apmVacancy, 2, Apm, _hod, OfferStatus.Accepted, sent: Base.AddDays(18), answered: Base.AddDays(20), salary: 8000);
        await OfferAsync(yaw, _finVacancy, 1, Finance, finHod, OfferStatus.Declined, sent: Base.AddDays(45), answered: Base.AddDays(47), salary: 6000);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<ReportResult> RunAs(string userId, string role, ReportFilter filter)
    {
        _db.CurrentUser.SignInAs(userId, userId, role);
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ReportService>().RunAsync(filter);
    }

    private Task<int> VacancyAsync(string code, int division, int department, string hod, int positions, DateTime requested, DateTime approved, DateTime published) =>
        Db(async db =>
        {
            var r = new RecruitmentRequest
            {
                RequestNumber = $"RR-{code}-2026-0001", DivisionId = division, DepartmentId = department, SiteId = 2, PositionTitle = $"{code} Officer",
                NumberOfPositions = positions, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.",
                BudgetCode = "B", EstimatedAnnualCost = 100_000, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved,
                DateRequested = requested, ApprovedAt = approved, StatusChangedAt = approved
            };
            db.RecruitmentRequests.Add(r);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = $"VAC-{code}-1", RecruitmentRequestId = r.Id, JobTitle = $"{code} Officer", DivisionId = division, DepartmentId = department,
                SiteId = 2, RequestingHodUserId = hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = positions,
                VacancyType = VacancyType.External, JobDescription = "Work.", MinimumEducation = EducationLevel.Bachelors,
                ClosingDate = DateOnly.FromDateTime(published.AddDays(14)), Status = VacancyStatus.Closed, StatusChangedAt = published.AddDays(14),
                PublishedAt = published
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            return v.Id;
        });

    private async Task<int> ApplicationAsync(int vacancyId, int division, int department, string name, ApplicationSource source, ApplicationStatus status,
        DateTime submitted, ApplicationStatus[] path)
    {
        var applicant = await _db.CreateUserAsync($"{Guid.NewGuid():N}"[..8] + "@mail.test", null, Roles.Candidate);
        return await Db(async db =>
        {
            var a = new JobApplication
            {
                ApplicationNumber = "APP-R-" + Guid.NewGuid().ToString("N")[..6], VacancyId = vacancyId, ApplicantUserId = applicant, Source = source,
                DivisionId = division, DepartmentId = department, RequestingHodUserId = _hod, FullName = name, Email = "a@mail.test", Phone = "0240000000",
                HighestEducation = EducationLevel.Bachelors, Status = ApplicationStatus.Submitted, SubmittedAt = submitted, StatusChangedAt = submitted
            };
            a.RecordStep(ApplicationAction.Submitted, ApplicationStatus.Submitted, applicant, name, null, submitted);
            var at = submitted;
            foreach (var step in path) a.RecordStep(ApplicationAction.ReviewStarted, step, _ro, "RO", null, at = at.AddHours(1));
            db.JobApplications.Add(a);
            await db.SaveChangesAsync();
            return a.Id;
        });
    }

    private Task OfferAsync(int app, int vacancyId, int division, int department, string hod, OfferStatus status, DateTime sent, DateTime answered, decimal salary) =>
        Db(async db =>
        {
            var rec = new HiringRecommendation
            {
                Number = "HIR-R-" + app, JobApplicationId = app, VacancyId = vacancyId, DivisionId = division, DepartmentId = department, RequestingHodUserId = hod,
                ProposedSalary = salary, ProposedStartDate = DateOnly.FromDateTime(answered.AddDays(14)), Justification = "Best.", CreatedByUserId = _ro,
                Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            db.Offers.Add(new Offer
            {
                Number = "OFF-R-" + app, JobApplicationId = app, HiringRecommendationId = rec.Id, VacancyId = vacancyId, DivisionId = division,
                DepartmentId = department, RequestingHodUserId = hod, JobTitle = "Officer", MonthlySalary = salary,
                StartDate = DateOnly.FromDateTime(answered.AddDays(14)), ExpiresOn = DateOnly.FromDateTime(sent.AddDays(7)), Status = status,
                SentAt = sent, SentByUserId = _ro, RespondedAt = answered
            });
            await db.SaveChangesAsync();
            return 0;
        });

    private static string Kpi(ReportResult r, string label) => r.Kpis.Single(k => k.Label == label).Value;

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task The_overview_counts_the_funnel_from_the_history_and_compares_divisions()
    {
        var r = await RunAs(_md, Roles.ManagingDirector, new ReportFilter());
        Assert.Equal(("2", "3", "4", "1"), (Kpi(r, "Requests raised"), Kpi(r, "Positions approved"), Kpi(r, "Applications"), Kpi(r, "Hires")));
        Assert.Equal("50%", Kpi(r, "Offer acceptance"));                // 1 accepted, 1 declined
        Assert.Equal("20 days", Kpi(r, "Average time to hire"));        // 1 Sep → 21 Sep
        Assert.Equal("2", Kpi(r, "Positions still open"));              // APM 2 − 1 filled, Finance 1 − 0
        Assert.False(r.LimitedToMyDepartments);

        // Kofi was interviewed before being turned down, so he counts at the interview stage.
        var funnel = r.Charts[0].Bars.Select(b => b.Value).ToList();
        Assert.Equal([4m, 3m, 3m, 3m, 2m, 1m], funnel);

        var operations = r.Table.Rows.Single(x => (string?)x.Cells[0] == "Operations");
        Assert.Equal((1, 2, 3, 1), ((int)operations.Cells[1]!, (int)operations.Cells[2]!, (int)operations.Cells[3]!, (int)operations.Cells[4]!));
    }

    [Fact]
    public async Task Every_report_applies_the_division_department_vacancy_status_and_period_filters()
    {
        // Division and department
        Assert.Equal("3", Kpi(await RunAs(_md, Roles.ManagingDirector, new ReportFilter { DivisionId = 2 }), "Applications"));
        Assert.Equal("1", Kpi(await RunAs(_md, Roles.ManagingDirector, new ReportFilter { DepartmentId = Finance }), "Applications"));

        // Vacancy: the request report follows the vacancy back to its request.
        var requests = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Requests, VacancyId = _finVacancy });
        Assert.Equal("RR-FIN-2026-0001", Assert.Single(requests.Table.Rows).Cells[0]);
        Assert.Equal("5 days", Kpi(requests, "Average time to approve"));

        // Status
        var hired = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Applications, Status = nameof(ApplicationStatus.Hired) });
        Assert.Equal("Ama", Assert.Single(hired.Table.Rows).Cells[1]);
        Assert.Contains(hired.StatusOptions, o => o.Value == nameof(ApplicationStatus.OfferDeclined));

        // Period (on each report's own date): only September's applications.
        var september = await RunAs(_md, Roles.ManagingDirector, new ReportFilter
            { Report = ReportKeys.Applications, From = new DateOnly(2026, 9, 1), To = new DateOnly(2026, 9, 30) });
        Assert.Equal(3, september.Table.Rows.Count);
        Assert.Equal("Interview", september.Table.Rows.Single(x => (string?)x.Cells[1] == "=HYPERLINK(\"x\")").Cells[8]); // furthest stage

        // Vacancies: funnel and open positions per vacancy.
        var vacancies = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Vacancies, DivisionId = 2 });
        var apm = Assert.Single(vacancies.Table.Rows).Cells;
        Assert.Equal((3, 2, 2, 1, 1, 1), ((int)apm[7]!, (int)apm[8]!, (int)apm[9]!, (int)apm[10]!, (int)apm[11]!, (int)apm[12]!));
    }

    [Fact]
    public async Task Time_to_hire_sources_and_offers_are_worked_out_per_record()
    {
        var tth = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.TimeToHire });
        var row = Assert.Single(tth.Table.Rows).Cells;
        Assert.Equal((10, 4, 6, 20), ((int?)row[8], (int?)row[9], (int?)row[10], (int?)row[11])); // request → approval → published → accepted
        Assert.Equal(20m, Assert.Single(tth.Charts[0].Bars).Value);

        var sources = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Sources });
        var careers = sources.Table.Rows.Single(x => (string?)x.Cells[0] == "GREL careers page").Cells;
        Assert.Equal((2, 1, 0), ((int)careers[1]!, (int)careers[2]!, (int)careers[4]!));
        Assert.Equal("100%", sources.Table.Rows.Single(x => (string?)x.Cells[0] == "LinkedIn").Cells[5]);

        var offers = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Offers });
        Assert.Equal(("2", "1", "1", "50%", "GHS 7,000"),
            (Kpi(offers, "Offers sent"), Kpi(offers, "Accepted"), Kpi(offers, "Declined"), Kpi(offers, "Acceptance rate"), Kpi(offers, "Average monthly salary")));
    }

    [Fact]
    public async Task A_head_of_department_sees_only_their_departments()
    {
        var r = await RunAs(_hod, Roles.HeadOfDepartment, new ReportFilter { Report = ReportKeys.Applications });
        Assert.True(r.LimitedToMyDepartments);
        Assert.Equal(3, r.Table.Rows.Count);
        Assert.DoesNotContain(r.Table.Rows, x => (string?)x.Cells[1] == "Yaw");
        Assert.Equal("VAC-APM-1 APM Officer", Assert.Single(r.VacancyOptions).Text);

        // Even when filtering on another department.
        Assert.Empty((await RunAs(_hod, Roles.HeadOfDepartment, new ReportFilter { Report = ReportKeys.Offers, DepartmentId = Finance })).Table.Rows);
    }

    [Fact]
    public async Task Onboarding_report_and_the_csv_export_protect_against_spreadsheet_formulas()
    {
        await Db(async db =>
        {
            var offer = db.Offers.Single(o => o.Status == OfferStatus.Accepted);
            var o = new OnboardingRecord
            {
                Number = "ONB-R-1", JobApplicationId = offer.JobApplicationId, OfferId = offer.Id, VacancyId = _apmVacancy, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, SiteId = 2, EmployeeUserId = _ro, EmployeeName = "Ama", JobTitle = "Officer", MonthlySalary = 8000,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), ReportLocation = "HR", StatusChangedAt = DateTime.UtcNow, CreatedByUserId = _ro
            };
            db.Onboardings.Add(o);
            await db.SaveChangesAsync();
            db.OnboardingTasks.Add(new OnboardingTask { OnboardingRecordId = o.Id, Team = OnboardingTeam.InformationTechnology, Title = "Email",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3) });
            db.OnboardingTasks.Add(new OnboardingTask { OnboardingRecordId = o.Id, Team = OnboardingTeam.HumanResources, Title = "File",
                Status = OnboardingTaskStatus.Done });
            await db.SaveChangesAsync();
            return 0;
        });
        var onboarding = await RunAs(_md, Roles.ManagingDirector, new ReportFilter { Report = ReportKeys.Onboarding });
        Assert.Equal(("1", "1", "1"), (Kpi(onboarding, "In progress"), Kpi(onboarding, "Letters not yet signed"), Kpi(onboarding, "Overdue checklist items")));
        Assert.Equal("1 / 2", Assert.Single(onboarding.Table.Rows).Cells[7]);

        Assert.Equal("'=HYPERLINK(\"\"x\"\")", ReportsController.Cell("=HYPERLINK(\"x\")")[1..^1]);
        Assert.Equal("'-5", ReportsController.Cell("-5"));
        Assert.Equal("-5", ReportsController.Cell(-5));                 // real numbers stay numbers
        Assert.Equal("\"Smith, Ama\"", ReportsController.Cell("Smith, Ama"));
        Assert.Equal("2026-09-21", ReportsController.Cell(new DateOnly(2026, 9, 21)));
        Assert.Equal("7000.5", ReportsController.Cell(7000.50m));
    }
}
