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
/// session, score validation by a Recruitment Officer not on the panel (9 Nov 2026), the applicant's view, results and lists.
/// </summary>
public class InterviewServiceTests : IAsyncLifetime
{
    private const int Apm = 31, Finance = 3;

    private readonly TestDatabase _db = new();
    private string _hod = "", _otherHod = "", _ro = "", _ro2 = "", _hrm = "", _engineer = "", _candidate = "", _candidate2 = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _otherHod = await _db.CreateUserAsync("finhod@grel.test", Finance, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _ro2 = await _db.CreateUserAsync("ro2@grel.test", 1, Roles.RecruitmentOfficer); // validates: not on the panels below
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
    private void AsValidator() => As(_ro2, Roles.RecruitmentOfficer);

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

        // Before validation, the HOD (on the panel) sees the others only after submitting their own, like everyone on it.
        As(_hod, Roles.HeadOfDepartment);
        var hodView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.True(hodView.ScorecardsHidden);
        Assert.Empty(hodView.Scorecards);

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

        // Scorecards can be updated until the scores are validated.
        Assert.True((await ScoreAsync(id, _engineer, 4)).Succeeded);
        Assert.True((await ScoreAsync(id, _hod, 4)).Succeeded);
        AsValidator();
        Assert.True((await Run(s => s.ValidateAsync(id, null))).Succeeded);
        Assert.False((await ScoreAsync(id, _engineer, 1)).Succeeded);
        Assert.Equal(InterviewStatus.Completed, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
    }

    [Fact]
    public async Task Closing_rules_cancel_needs_a_reason_and_validating_needs_the_scorecards()
    {
        var future = await ScheduledAsync(await ApplicationAsync(_candidate, "Ama"), DateTime.UtcNow.AddDays(3));
        AsValidator();
        Assert.False((await Run(s => s.ValidateAsync(future, null))).Succeeded); // not started
        AsRecruiter();
        Assert.False((await Run(s => s.NoShowAsync(future))).Succeeded);
        Assert.False((await Run(s => s.CancelAsync(future, ""))).Succeeded);
        Assert.True((await Run(s => s.CancelAsync(future, "Panel chair unavailable"))).Succeeded);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("cancelled"));
        Assert.False((await Run(s => s.CancelAsync(future, "Again"))).Succeeded); // already closed

        var now = await ScheduledAsync(await ApplicationAsync(_candidate2, "Kofi"));
        AsValidator();
        Assert.False((await Run(s => s.ValidateAsync(now, null))).Succeeded); // no scorecards yet
        AsRecruiter();
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

        // The engineer is on both panels and has two scorecards to do; the HOD has none left.
        As(_engineer, Roles.Employee);
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "mine" }))).TotalCount);
        Assert.Equal(2, await Run(s => s.CountMyPendingScorecardsAsync()));
        As(_hod, Roles.HeadOfDepartment);
        Assert.Equal(0, await Run(s => s.CountMyPendingScorecardsAsync()));

        // Not validated yet: the results show no scores.
        Assert.All((await Run(s => s.GetResultsAsync(_vacancyId)))!.Value.Results, r => Assert.Null(r.OverallAverage));

        // The engineer and the recruiter did not take part; the validator records that and validates both sessions.
        AsValidator();
        foreach (var interview in new[] { amaInterview, kofiInterview })
        {
            foreach (var member in new[] { _engineer, _ro })
                Assert.True((await Run(s => s.SetAttendanceAsync(interview, member, PanelAttendance.Excused, "On site visit"))).Succeeded);
            Assert.True((await Run(s => s.ValidateAsync(interview, null))).Succeeded);
        }

        As(_hod, Roles.HeadOfDepartment);
        var results = (await Run(s => s.GetResultsAsync(_vacancyId)))!.Value.Results;
        Assert.Equal(["Kofi", "Ama"], results.Select(r => r.ApplicantName).ToArray());
        Assert.Equal(100.0m, results[0].OverallAverage);

        As(_otherHod, Roles.HeadOfDepartment);
        Assert.Null(await Run(s => s.GetResultsAsync(_vacancyId)));
        Assert.Null(await Run(s => s.GetDetailsAsync(amaInterview)));

        // Reviewers see every session; filters work.
        AsRecruiter();
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "all", DivisionId = 2, DepartmentId = Apm }))).TotalCount);
        Assert.Equal(0, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "all", DivisionId = 1 }))).TotalCount);
        Assert.Equal(2, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "all", VacancyId = _vacancyId, Status = InterviewStatus.Completed }))).TotalCount);
        Assert.Equal(0, (await Run(s => s.SearchAsync(new InterviewFilter { Tab = "upcoming", VacancyId = _vacancyId }))).TotalCount); // validated, so no longer upcoming
    }

    // ------------------------------------------------------------------ score validation (9 Nov 2026)

    [Fact]
    public async Task All_scorecards_go_to_the_recruitment_officer_who_validates_or_returns_one_before_anyone_else_sees_them()
    {
        var app = await ApplicationAsync(_candidate, "Ama");
        var id = await ScheduledAsync(app); // panel: HOD, engineer, recruiter (_ro, the vacancy's owner)

        Assert.True((await ScoreAsync(id, _engineer, 3)).Succeeded);
        Assert.True((await ScoreAsync(id, _ro, 4)).Succeeded);
        Assert.Equal(InterviewStatus.Scheduled, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status); // HOD still to score
        AsValidator();
        var waiting = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.False(waiting.CanValidate);
        Assert.Contains("from hod.", waiting.ValidationBlockedReason);

        // The last scorecard moves it to "awaiting validation" and emails the vacancy's Recruitment Officer.
        Assert.True((await ScoreAsync(id, _hod, 5)).Succeeded);
        Assert.Equal(InterviewStatus.AwaitingValidation, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("Scores ready to validate"));

        // Nobody off the panel except Recruitment Officers sees the scores yet: not the HR Manager, not the results.
        As(_hrm, Roles.HrManager);
        var hrView = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.True(hrView.ScorecardsHidden);
        Assert.Null(hrView.AverageScore);
        Assert.Null((await Run(s => s.GetResultsAsync(_vacancyId)))!.Value.Results.Single().OverallAverage);

        // Only Recruitment Officers validate: not the HR Manager. One who sat on the panel (_ro) may validate too,
        // but never returns their own scorecard.
        Assert.False((await Run(s => s.ValidateAsync(id, null))).Succeeded);
        AsRecruiter();
        Assert.True((await Run(s => s.GetDetailsAsync(id)))!.CanValidate);
        Assert.False((await Run(s => s.ReturnScorecardAsync(id, _ro, "I would like to change my own ratings."))).Succeeded);

        // The validator returns the engineer's scorecard: it no longer counts, and the engineer is asked to correct it.
        AsValidator();
        Assert.True((await Run(s => s.GetDetailsAsync(id)))!.CanValidate);
        Assert.False((await Run(s => s.ReturnScorecardAsync(id, _engineer, "Fix"))).Succeeded); // reason too short
        Assert.True((await Run(s => s.ReturnScorecardAsync(id, _engineer, "No comments support the low communication rating."))).Succeeded);
        Assert.Equal(InterviewStatus.Scheduled, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: eng@grel.test") && m.Contains("Please correct your scorecard"));
        Assert.False((await Run(s => s.ValidateAsync(id, null))).Succeeded);
        As(_engineer, Roles.Employee);
        Assert.Equal(1, await Run(s => s.CountMyPendingScorecardsAsync()));
        Assert.True((await Run(s => s.GetDetailsAsync(id)))!.MyScorecardReturned);

        // Corrected and submitted again: back to the validator, who validates.
        Assert.True((await ScoreAsync(id, _engineer, 4)).Succeeded);
        Assert.Equal(InterviewStatus.AwaitingValidation, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
        AsValidator();
        Assert.True((await Run(s => s.ValidateAsync(id, "Checked against the panel notes."))).Succeeded);
        var stored = await Db(db => db.Interviews.SingleAsync(i => i.Id == id));
        Assert.Equal((InterviewStatus.Completed, _ro2, "Checked against the panel notes."), (stored.Status, stored.ValidatedByUserId, stored.ValidationNote));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("Scores validated"));

        // Now final and visible: Technical/Communication/Safety all 4s = 80, 4s = 80, 5s = 100 → 86.7.
        As(_hrm, Roles.HrManager);
        var final = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Equal(3, final.Scorecards.Count);
        Assert.Equal(86.7m, final.AverageScore);
        Assert.Equal(86.7m, (await Run(s => s.GetResultsAsync(_vacancyId)))!.Value.Results.Single().OverallAverage);
        Assert.False((await ScoreAsync(id, _engineer, 1)).Succeeded); // final
        AsValidator();
        Assert.False((await Run(s => s.ValidateAsync(id, null))).Succeeded); // already validated
    }

    [Fact]
    public async Task When_the_vacancy_owner_is_not_a_recruitment_officer_every_recruitment_officer_is_asked_and_one_on_the_panel_can_validate()
    {
        var id = await ScheduledAsync(await ApplicationAsync(_candidate, "Ama"));
        await Db(async db => { (await db.Vacancies.FindAsync(_vacancyId))!.RecruiterUserId = _hrm; return await db.SaveChangesAsync(); });
        foreach (var member in new[] { _ro, _engineer, _hod }) // the HOD last: nobody is emailed about their own action
            Assert.True((await ScoreAsync(id, member, 4)).Succeeded);

        // Both Recruitment Officers are emailed and get the to-do; the page names no single validator.
        var emails = _db.SentEmails();
        Assert.Contains(emails, m => m.Contains("To: ro@grel.test") && m.Contains("Scores ready to validate"));
        Assert.Contains(emails, m => m.Contains("To: ro2@grel.test") && m.Contains("Scores ready to validate"));
        Assert.DoesNotContain(emails, m => m.Contains("To: hrm@grel.test") && m.Contains("Scores ready to validate"));
        AsValidator();
        Assert.Equal(1, await Run(s => s.CountAwaitingMyValidationAsync()));
        Assert.Null((await Run(s => s.GetDetailsAsync(id)))!.ValidatorName);

        // _ro sat on the panel and still validates.
        AsRecruiter();
        Assert.Equal(1, await Run(s => s.CountAwaitingMyValidationAsync()));
        Assert.True((await Run(s => s.ValidateAsync(id, null))).Succeeded);
        Assert.Equal(InterviewStatus.Completed, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
    }

    [Fact]
    public async Task A_member_who_did_not_take_part_can_be_excused_but_the_hod_never()
    {
        var id = await ScheduledAsync(await ApplicationAsync(_candidate, "Ama"));
        Assert.True((await ScoreAsync(id, _hod, 4)).Succeeded);
        Assert.True((await ScoreAsync(id, _ro, 4)).Succeeded);

        AsValidator();
        Assert.False((await Run(s => s.SetAttendanceAsync(id, _engineer, PanelAttendance.Excused, ""))).Succeeded);   // reason needed
        Assert.False((await Run(s => s.SetAttendanceAsync(id, _ro, PanelAttendance.Absent, "Away"))).Succeeded);      // they scored
        Assert.True((await Run(s => s.SetAttendanceAsync(id, _engineer, PanelAttendance.Excused, "Called to the plant"))).Succeeded);
        Assert.Equal(InterviewStatus.AwaitingValidation, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);

        // Undo puts the engineer's scorecard back on the list of those waited for.
        Assert.True((await Run(s => s.SetAttendanceAsync(id, _engineer, PanelAttendance.NotRecorded, null))).Succeeded);
        Assert.Equal(InterviewStatus.Scheduled, (await Db(db => db.Interviews.SingleAsync(i => i.Id == id))).Status);
        Assert.True((await Run(s => s.SetAttendanceAsync(id, _engineer, PanelAttendance.Absent, "Did not come"))).Succeeded);
        As(_engineer, Roles.Employee);
        Assert.Equal(0, await Run(s => s.CountMyPendingScorecardsAsync()));

        // The HOD's scorecard is required: on another session, they cannot be excused.
        var other = await ScheduledAsync(await ApplicationAsync(_candidate2, "Kofi"));
        AsValidator();
        Assert.False((await Run(s => s.SetAttendanceAsync(other, _hod, PanelAttendance.Excused, "On leave"))).Succeeded);

        Assert.True((await Run(s => s.ValidateAsync(id, null))).Succeeded);
        As(_hrm, Roles.HrManager);
        Assert.Equal(80.0m, (await Run(s => s.GetDetailsAsync(id)))!.AverageScore);
    }

    // ------------------------------------------------------------------ clash check (12 November 2026, docs/34 quick win)

    [Fact]
    public async Task Scheduling_warns_when_a_panel_member_or_the_candidate_is_already_booked()
    {
        var first = await ApplicationAsync(_candidate, "Ama Mensah");
        var second = await ApplicationAsync(_candidate2, "Kofi Boateng");
        var tomorrow10 = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        await ScheduledAsync(first, tomorrow10);

        // Same panel, overlapping (10:30-11:30 against 10:00-11:00): refused with one line per busy person.
        AsRecruiter();
        var clash = await Run(s => s.ScheduleAsync(Form(second, tomorrow10.AddMinutes(30))));
        Assert.False(clash.Succeeded);
        Assert.All(clash.Errors, e => Assert.StartsWith(InterviewService.ClashPrefix, e));
        Assert.Equal(3, clash.Errors.Count); // the HOD, the engineer and the recruiter
        Assert.Contains(clash.Errors, e => e.Contains("10:00-11:00") && e.Contains("Ama Mensah"));

        // Right after the first one: no clash.
        Assert.True((await Run(s => s.ScheduleAsync(Form(second, tomorrow10.AddHours(1))))).Succeeded);

        // The candidate's own second round at the same time as the first.
        var round2 = Form(first, tomorrow10.AddMinutes(15), _otherHod, _hrm, _hod);
        var own = await Run(s => s.ScheduleAsync(round2));
        Assert.Contains(own.Errors, e => e.Contains("the candidate already has a session"));

        // "Schedule anyway" goes through.
        round2.IgnoreClashes = true;
        Assert.True((await Run(s => s.ScheduleAsync(round2))).Succeeded);
    }

    [Fact]
    public async Task Rescheduling_checks_clashes_too_but_not_against_itself()
    {
        var first = await ApplicationAsync(_candidate, "Ama Mensah");
        var second = await ApplicationAsync(_candidate2, "Kofi Boateng");
        var tomorrow10 = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var a = await ScheduledAsync(first, tomorrow10);
        var b = await ScheduledAsync(second, tomorrow10.AddHours(2));

        AsRecruiter();
        Assert.True((await Run(s => s.RescheduleAsync(a, tomorrow10.AddMinutes(15), null))).Succeeded); // moving within its own slot
        var onto = await Run(s => s.RescheduleAsync(b, tomorrow10, null));
        Assert.False(onto.Succeeded);
        Assert.StartsWith(InterviewService.ClashPrefix, onto.Errors[0]);
        Assert.True((await Run(s => s.RescheduleAsync(b, tomorrow10, null, default, ignoreClashes: true))).Succeeded);
    }

    // ------------------------------------------------------------------ panel replies and quorum (12 November 2026, docs/35)

    private async Task QuorumOnAsync()
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<Grsms.Application.Settings.ISettingsStore>()
            .SaveAsync(new Dictionary<string, string?> { [Grsms.Domain.Settings.SettingCatalogue.InterviewQuorum] = "true" });
    }

    [Fact]
    public async Task With_quorum_a_panel_needs_three_people_including_an_hr_manager()
    {
        await QuorumOnAsync();
        var app = await ApplicationAsync(_candidate, "Ama Mensah");
        var when = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
        AsRecruiter();
        var noHr = await Run(s => s.ScheduleAsync(Form(app, when, _hod, _engineer, _ro)));
        Assert.Contains(noHr.Errors, e => e.Contains("An HR Manager must be on the panel"));
        var tooSmall = await Run(s => s.ScheduleAsync(Form(app, when, _hod, _hrm)));
        Assert.Contains(tooSmall.Errors, e => e.Contains("at least 3 people"));
        var ok = await Run(s => s.ScheduleAsync(Form(app, when, _hod, _engineer, _hrm)));
        Assert.True(ok.Succeeded, string.Join(" ", ok.Errors));
        Assert.True(await Db(db => db.Interviews.Where(i => i.Id == ok.Value).Select(i => i.QuorumRequired).SingleAsync()));
    }

    [Fact]
    public async Task A_decline_alerts_the_recruiter_and_a_replacement_restores_quorum()
    {
        await QuorumOnAsync();
        var app = await ApplicationAsync(_candidate, "Ama Mensah");
        AsRecruiter();
        var id = (await Run(s => s.ScheduleAsync(Form(app, DateTime.UtcNow.Date.AddDays(2).AddHours(9), _hod, _engineer, _hrm)))).Value;

        As(_hrm, Roles.HrManager);
        Assert.True((await Run(s => s.RespondAsync(id, true, null))).Succeeded);
        As(_engineer, Roles.Employee);
        Assert.False((await Run(s => s.RespondAsync(id, false, null))).Succeeded); // a reason is needed
        Assert.True((await Run(s => s.RespondAsync(id, false, "On leave that week"))).Succeeded);
        Assert.Contains(_db.SentEmails().Select(m => m.Replace("=\r\n", "")), m => m.Contains("To: ro@grel.test") && m.Contains("Panel member declined") && m.Contains("Quorum at risk"));

        AsRecruiter();
        var page = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Contains(page.QuorumProblems, p => p.Contains("at least 3"));
        Assert.Equal(PanelResponse.Declined, page.Panel.Single(p => p.UserId == _engineer).Response);
        Assert.True(page.CanReplace);

        Assert.True((await Run(s => s.ReplacePanelMemberAsync(id, _engineer, _ro2))).Succeeded);
        page = (await Run(s => s.GetDetailsAsync(id)))!;
        Assert.Empty(page.QuorumProblems);
        Assert.Contains(page.Panel, p => p.UserId == _ro2 && p.Response == PanelResponse.Pending);
        Assert.Contains(_db.SentEmails().Select(m => m.Replace("=\r\n", "")), m => m.Contains("To: ro2@grel.test") && m.Contains("Please accept or decline"));
    }

    [Fact]
    public async Task The_head_of_department_is_replaced_only_by_a_delegate_from_the_department()
    {
        await QuorumOnAsync();
        var apmColleague = await _db.CreateUserAsync("apm2@grel.test", Apm, Roles.Employee);
        var app = await ApplicationAsync(_candidate, "Ama Mensah");
        AsRecruiter();
        var id = (await Run(s => s.ScheduleAsync(Form(app, DateTime.UtcNow.Date.AddDays(2).AddHours(9), _hod, _engineer, _hrm)))).Value;
        As(_hod, Roles.HeadOfDepartment);
        Assert.True((await Run(s => s.RespondAsync(id, false, "Travelling to Tsibu"))).Succeeded);

        AsRecruiter();
        Assert.Contains("same department", (await Run(s => s.ReplacePanelMemberAsync(id, _hod, _ro2))).Errors[0]);
        Assert.True((await Run(s => s.ReplacePanelMemberAsync(id, _hod, apmColleague))).Succeeded);
        var page = (await Run(s => s.GetDetailsAsync(id)))!;
        var delegateMember = page.Panel.Single(p => p.UserId == apmColleague);
        Assert.True(delegateMember.IsHodDelegate);
        Assert.True(delegateMember.IsChair); // the chair moved with the replacement
        Assert.Empty(page.QuorumProblems);
    }

    [Fact]
    public async Task Scores_cannot_be_validated_when_quorum_was_not_present()
    {
        await QuorumOnAsync();
        var app = await ApplicationAsync(_candidate, "Ama Mensah");
        AsRecruiter();
        var id = (await Run(s => s.ScheduleAsync(Form(app, DateTime.UtcNow.AddMinutes(-30), _hod, _engineer, _hrm)))).Value;
        Assert.True((await ScoreAsync(id, _hod, 4)).Succeeded);
        Assert.True((await ScoreAsync(id, _engineer, 4)).Succeeded);

        AsValidator(); // the HR Manager did not attend
        Assert.True((await Run(s => s.SetAttendanceAsync(id, _hrm, PanelAttendance.Absent, "Called away"))).Succeeded);
        var blocked = await Run(s => s.ValidateAsync(id, null));
        Assert.False(blocked.Succeeded);
        Assert.Contains("Quorum was not met", blocked.Errors[0]);
        Assert.Contains("an HR Manager", blocked.Errors[0]);
    }
}
