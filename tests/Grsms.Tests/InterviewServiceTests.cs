using Grsms.Application.Interviews;
using Grsms.Domain.Applications;
using Grsms.Domain.Interviews;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Interviews, assessments and scorecards (Module 7): scheduling rules (shortlisted only, panel with the HOD, criteria
/// adding up to 100), invitations, scorecards (timing, completeness, weighted score, independent scoring), closing a
/// session, the applicant's view, results and lists.
/// </summary>
public class InterviewServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro = "", _hrm = "", _engineer = "", _candidate = "", _candidate2 = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _engineer = await _db.CreateUserAsync("eng@grel.test", Apm, Roles.Employee);
        _candidate = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _candidate2 = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);

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

    private async Task<T> Run<T>(Func<InterviewService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<InterviewService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private int _vacancyId;

    /// <summary>An application for a Factory APM vacancy, already at <paramref name="status"/>.</summary>
    private Task<int> ApplicationAsync(string applicant, string name, ApplicationStatus status = ApplicationStatus.Shortlisted) => Db(async db =>
    {
        if (_vacancyId == 0)
        {
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-I-1", DivisionId = 2, DepartmentId = Apm, SiteId = 1, PositionTitle = "Process Engineer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = "VAC-I-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm,
                SiteId = 1, RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
                ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            _vacancyId = v.Id;
        }
        var a = new JobApplication
        {
            ApplicationNumber = "APP-I-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name}@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors, Status = status,
            SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    });

    private ScheduleFormDto Form(int applicationId, DateTime? startsAt = null, params string[] panel) => new()
    {
        ApplicationId = applicationId,
        Type = InterviewType.Interview,
        Mode = InterviewMode.InPerson,
        StartsAt = startsAt ?? DateTime.UtcNow.AddMinutes(-30), // already under way, so scorecards are open
        DurationMinutes = 60,
        Location = "Board Room, Head Office",
        CandidateInstructions = "Bring your certificates.",
        PanelUserIds = panel.Length > 0 ? [.. panel] : [_hod, _engineer, _ro],
        ChairUserId = _hod,
        Criteria = "Technical knowledge | 50\nCommunication | 30\nSafety awareness | 20"
    };

    private async Task<int> ScheduledAsync(int applicationId, DateTime? startsAt = null)
    {
        AsRecruiter();
        var result = await Run(s => s.ScheduleAsync(Form(applicationId, startsAt)));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Value;
    }

    /// <summary>Submits a scorecard as <paramref name="userId"/> rating every criterion <paramref name="rating"/>.</summary>
    private async Task<Grsms.Application.Common.Result> ScoreAsync(int interviewId, string userId, int rating,
        PanelRecommendation recommendation = PanelRecommendation.Recommend, string? concerns = null)
    {
        As(userId, Roles.Employee);
        var criteria = await Db(db => db.InterviewCriteria.Where(c => c.InterviewId == interviewId).Select(c => c.Id).ToListAsync());
        return await Run(s => s.SubmitScorecardAsync(interviewId, criteria.ToDictionary(id => id, _ => rating), recommendation, "Clear thinker", concerns));
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Scheduling_needs_a_shortlisted_applicant_a_panel_with_the_hod_and_criteria_adding_up_to_100()
    {
        var app = await ApplicationAsync(_candidate, "Ama");
        var notShortlisted = await ApplicationAsync(_candidate2, "Kofi", ApplicationStatus.UnderReview);

        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.ScheduleAsync(Form(app)))).Succeeded);          // HODs don't schedule

        AsRecruiter();
        Assert.False((await Run(s => s.ScheduleAsync(Form(notShortlisted)))).Succeeded);
        Assert.False((await Run(s => s.ScheduleAsync(Form(app, null, _engineer, _ro)))).Succeeded);   // no HOD
        Assert.False((await Run(s => s.ScheduleAsync(Form(app, null, _hod)))).Succeeded);            // panel of 1
        Assert.False((await Run(s => s.ScheduleAsync(Form(app, null, _hod, _candidate)))).Succeeded); // the applicant
        var badWeights = Form(app);
        badWeights.Criteria = "Technical | 60\nCommunication | 30";
        Assert.False((await Run(s => s.ScheduleAsync(badWeights))).Succeeded);

        // The form starts with the HOD on the panel and the default criteria.
        var form = (await Run(s => s.GetScheduleFormAsync(app))).Value!;
        Assert.Equal([_hod], form.PanelUserIds);
        Assert.Contains("Technical knowledge and skills | 30", form.Criteria);

        var id = await ScheduledAsync(app);
        var stored = await Db(db => db.Interviews.Include(i => i.Panel).Include(i => i.Criteria).SingleAsync(i => i.Id == id));
        Assert.Equal((1, 3, 3, 2, Apm, _hod), (stored.Round, stored.Panel.Count, stored.Criteria.Count, stored.DivisionId, stored.DepartmentId, stored.RequestingHodUserId));
        Assert.True(stored.Panel.Single(p => p.UserId == _hod).IsChair);
        Assert.Equal(ApplicationStatus.Interview, (await Db(db => db.JobApplications.SingleAsync(a => a.Id == app))).Status);

        var emails = _db.SentEmails();
        Assert.Contains(emails, m => m.Contains("To: ama@mail.test") && m.Contains("Invitation"));
        Assert.Contains(emails, m => m.Contains("To: eng@grel.test") && m.Contains("Panel:"));

        // A second round can follow.
        Assert.Equal(2, (await Run(s => s.GetScheduleFormAsync(app))).Value!.Round);
    }

    [Fact]
    public async Task Scorecards_open_at_the_start_need_every_rating_and_stay_hidden_until_you_submit_your_own()
    {
        var app = await ApplicationAsync(_candidate, "Ama");
        var later = await ScheduledAsync(await ApplicationAsync(_candidate2, "Kofi"), DateTime.UtcNow.AddDays(2));
        Assert.False((await ScoreAsync(later, _engineer, 4)).Succeeded); // not started yet

        var id = await ScheduledAsync(app);
        Assert.False((await ScoreAsync(id, _otherHod, 4)).Succeeded);   // not on the panel
        Assert.False((await ScoreAsync(id, _engineer, 6)).Succeeded);   // out of range
        Assert.False((await ScoreAsync(id, _engineer, 2, PanelRecommendation.DoNotRecommend)).Succeeded); // concerns required

        // Technical 5/5 × 50 + Communication 5/5 × 30 + Safety 5/5 × 20 = 100; all 3s = 60.
        Assert.True((await ScoreAsync(id, _engineer, 3)).Succeeded);
        var engineerView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Equal(60.0m, engineerView.MyScorecard!.WeightedScore);

        // The department's HOD reviews every scorecard, even before submitting their own.
        As(_hod, Roles.HeadOfDepartment);
        var hodView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Single(hodView.Scorecards);
        Assert.False(hodView.ScorecardsHidden);

        // Another panel member without a scorecard cannot see the others.
        As(_ro, Roles.Employee); // signed in only as staff, not as a reviewer
        var roPanelView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.True(roPanelView.ScorecardsHidden);
        Assert.Empty(roPanelView.Scorecards);
        Assert.True((await ScoreAsync(id, _ro, 5, PanelRecommendation.StronglyRecommend)).Succeeded);
        As(_ro, Roles.Employee);
        var after = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Equal(2, after.Scorecards.Count);
        Assert.Equal(80.0m, after.AverageScore);

        // Scorecards can be updated until the session is completed.
        Assert.True((await ScoreAsync(id, _engineer, 4)).Succeeded);
        AsRecruiter();
        Assert.True((await Run(s => s.CompleteAsync(id))).Succeeded);
        Assert.False((await ScoreAsync(id, _engineer, 1)).Succeeded);
        Assert.Equal(InterviewStatus.Completed, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
    }

    [Fact]
    public async Task Closing_rules_cancel_needs_a_reason_and_completing_needs_a_scorecard()
    {
        var future = await ScheduledAsync(await ApplicationAsync(_candidate, "Ama"), DateTime.UtcNow.AddDays(3));
        AsRecruiter();
        Assert.False((await Run(s => s.CompleteAsync(future))).Succeeded); // not started
        Assert.False((await Run(s => s.NoShowAsync(future))).Succeeded);
        Assert.False((await Run(s => s.CancelAsync(future, ""))).Succeeded);
        Assert.True((await Run(s => s.CancelAsync(future, "Panel chair unavailable"))).Succeeded);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("cancelled"));
        Assert.False((await Run(s => s.CancelAsync(future, "Again"))).Succeeded); // already closed

        var now = await ScheduledAsync(await ApplicationAsync(_candidate2, "Kofi"));
        AsRecruiter();
        Assert.False((await Run(s => s.CompleteAsync(now))).Succeeded); // no scorecards yet
        Assert.True((await Run(s => s.NoShowAsync(now))).Succeeded);
        var history = await Db(db => db.ApplicationActions.Where(a => a.Action == ApplicationAction.InterviewNoShow).CountAsync());
        Assert.Equal(1, history);
    }

    [Fact]
    public async Task Applicants_see_when_and_where_but_never_scores()
    {
        var app = await ApplicationAsync(_candidate, "Ama");
        var id = await ScheduledAsync(app, DateTime.UtcNow.AddDays(1));

        As(_candidate, Roles.Candidate);
        var mine = Assert.Single(await Run(s => s.ForMyApplicationAsync(app)));
        Assert.Equal(("Board Room, Head Office", "Bring your certificates.", InterviewStatus.Scheduled), (mine.Location, mine.Instructions, mine.Status));
        Assert.Null(await Run(s => s.GetDetailsAsync(id)));

        As(_candidate2, Roles.Candidate);
        Assert.Empty(await Run(s => s.ForMyApplicationAsync(app)));
    }

    [Fact]
    public async Task Results_rank_applicants_by_average_and_lists_show_each_persons_sessions()
    {
        var ama = await ApplicationAsync(_candidate, "Ama");
        var kofi = await ApplicationAsync(_candidate2, "Kofi");
        var amaInterview = await ScheduledAsync(ama);
        var kofiInterview = await ScheduledAsync(kofi);
        Assert.True((await ScoreAsync(amaInterview, _hod, 3)).Succeeded);
        Assert.True((await ScoreAsync(kofiInterview, _hod, 5)).Succeeded);

        As(_hod, Roles.HeadOfDepartment);
        var results = (await Run(s => s.GetResultsAsync(_vacancyId)))!.Value.Results;
        Assert.Equal(["Kofi", "Ama"], results.Select(r => r.ApplicantName).ToArray());
        Assert.Equal(100.0m, results[0].OverallAverage);

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Run(s => s.GetResultsAsync(_vacancyId)));
        Assert.Null(await Run(s => s.GetDetailsAsync(amaInterview)));

        // The engineer is on both panels and has two scorecards to do; the HOD has none left.
        As(_engineer, Roles.Employee);
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "mine" }))).TotalCount);
        Assert.Equal(2, await Run(s => s.CountMyPendingScorecardsAsync()));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Equal(0, await Run(s => s.CountMyPendingScorecardsAsync()));

        // Reviewers see every session; filters work.
        AsRecruiter();
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "all", DivisionId = 2, DepartmentId = Apm }))).TotalCount);
        Assert.Equal(0, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "all", DivisionId = 1 }))).TotalCount);
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "upcoming", VacancyId = _vacancyId }))).TotalCount);
    }
}
