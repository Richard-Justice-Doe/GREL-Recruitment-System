using Grsms.Application.Onboarding;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Appointment letters and onboarding (Module 9): starting only after an accepted offer, the default checklist and its
/// owners, the HR Manager's letter and the new employee's signature, who may tick which item, and completion.
/// </summary>
public class OnboardingServiceTests : IAsyncLifetime
{
    private const int Apm = 31, It = 2;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly TestDatabase _db = new();
    private string _hod = "", _itHod = "", _ro = "", _hrm = "", _staff = "", _candidate = "", _candidate2 = "";
    private int _vacancyId;

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _itHod = await _db.CreateUserAsync("ithod@grel.test", It, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _staff = await _db.CreateUserAsync("clerk@grel.test", 3, Roles.Employee);
        _candidate = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _candidate2 = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);

        _vacancyId = await Db(async db =>
        {
            (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
            (await db.Departments.FindAsync(It))!.HeadOfDepartmentUserId = _itHod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-O-1", DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Process Engineer",
                NumberOfPositions = 2, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = "VAC-O-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm,
                SiteId = 2, RequestingHodUserId = _hod, RecruiterUserId = _ro, ReportingManagerUserId = _hod, EmploymentType = EmploymentType.Permanent,
                NumberOfPositions = 2, VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
                ClosingDate = Today.AddDays(-20), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            return v.Id;
        });
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ helpers

    private void As(string userId, params string[] roles) => _db.CurrentUser.SignInAs(userId, userId, roles);
    private void AsRecruiter() => As(_ro, Roles.RecruitmentOfficer);
    private void AsHrManager() => As(_hrm, Roles.HrManager);

    private async Task<T> Run<T>(Func<OnboardingService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<OnboardingService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>An application with an approved hiring recommendation and an offer in the given state.</summary>
    private Task<int> WithOfferAsync(string applicant, string name, OfferStatus status, DateOnly? start = null) => Db(async db =>
    {
        var a = new JobApplication
        {
            ApplicationNumber = "APP-O-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name.Split(' ')[0].ToLowerInvariant()}@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors,
            Status = HiringWorkflow.ApplicationStatusFor(status), SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        var rec = new HiringRecommendation
        {
            Number = "HIR-O-" + a.Id, JobApplicationId = a.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
            ProposedSalary = 8500, ProposedStartDate = start ?? Today.AddDays(20), Justification = "Best candidate.", CreatedByUserId = _ro,
            Status = HiringStatus.Approved
        };
        db.HiringRecommendations.Add(rec);
        await db.SaveChangesAsync();
        db.Offers.Add(new Offer
        {
            Number = "OFF-O-" + a.Id, JobApplicationId = a.Id, HiringRecommendationId = rec.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = _hod, JobTitle = "Process Engineer", MonthlySalary = 8500, StartDate = start ?? Today.AddDays(20),
            ExpiresOn = Today.AddDays(5), Terms = "Probation: 6 months.", Status = status, SentAt = DateTime.UtcNow, SentByUserId = _ro,
            RespondedAt = status == OfferStatus.Accepted ? DateTime.UtcNow : null
        });
        await db.SaveChangesAsync();
        return a.Id;
    });

    private async Task<int> StartAsync(int app, DateOnly? start = null)
    {
        AsRecruiter();
        var form = (await Run(s => s.GetStartFormAsync(app))).Value!;
        if (start is not null) form.StartDate = start;
        var r = await Run(s => s.StartAsync(form));
        Assert.True(r.Succeeded, string.Join(" ", r.Errors));
        return r.Value;
    }

    private async Task IssueAndSignAsync(int id, int app)
    {
        AsHrManager();
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 8:00 to 17:00."))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.True((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded);
    }

    private Task<List<OnboardingTask>> TasksAsync(int id) => Db(db => db.OnboardingTasks.Where(t => t.OnboardingRecordId == id).OrderBy(t => t.SortOrder).ToListAsync());

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Onboarding_starts_after_an_accepted_offer_with_the_default_checklist_and_owners()
    {
        var pending = await WithOfferAsync(_candidate2, "Kofi Boateng", OfferStatus.Sent);
        AsRecruiter();
        Assert.False((await Run(s => s.GetStartFormAsync(pending))).Succeeded);

        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.GetStartFormAsync(app))).Succeeded); // HR only
        AsRecruiter();
        Assert.Single(await Run(s => s.ToStartAsync()));

        var form = (await Run(s => s.GetStartFormAsync(app))).Value!;
        Assert.Equal((Today.AddDays(20), _hod), (form.StartDate!.Value, form.ContactPersonUserId));
        form.ContactPersonUserId = _candidate; // the new employee cannot meet themselves
        Assert.False((await Run(s => s.StartAsync(form))).Succeeded);

        var id = await StartAsync(app);
        Assert.False((await Run(s => s.GetStartFormAsync(app))).Succeeded); // only once
        Assert.Empty(await Run(s => s.ToStartAsync()));

        var record = await Db(db => db.Onboardings.SingleAsync(o => o.Id == id));
        Assert.Matches(@"^ONB-\d{4}-0001$", record.Number);
        Assert.Equal((2, Apm, _hod, 2, 8500m), (record.DivisionId, record.DepartmentId, record.RequestingHodUserId, record.SiteId, record.MonthlySalary));

        var list = await TasksAsync(id);
        Assert.Equal(OnboardingWorkflow.DefaultChecklist.Count, list.Count);
        Assert.All(list.Where(t => t.Team == OnboardingTeam.HumanResources), t => Assert.Equal(_ro, t.AssignedToUserId));
        Assert.All(list.Where(t => t.Team == OnboardingTeam.Department), t => Assert.Equal(_hod, t.AssignedToUserId));
        Assert.All(list.Where(t => t.Team == OnboardingTeam.InformationTechnology), t => Assert.Equal(_itHod, t.AssignedToUserId));
        Assert.All(list.Where(t => t.Team == OnboardingTeam.NewEmployee), t => Assert.Null(t.AssignedToUserId));
        Assert.Equal(Today.AddDays(18), list.First(t => t.Team == OnboardingTeam.InformationTechnology).DueDate); // start - 2

        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ithod@grel.test") && m.Contains("2 task(s) for you"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("3 task(s) for you"));
        var history = await Db(db => db.ApplicationActions.Where(a => a.JobApplicationId == app).Select(a => a.Action).ToListAsync());
        Assert.Contains(ApplicationAction.OnboardingStarted, history);
    }

    [Fact]
    public async Task The_hr_manager_issues_the_letter_and_the_new_employee_signs_it_with_their_name()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);

        As(_candidate, Roles.Candidate);
        Assert.False((await Run(s => s.GetMyAppointmentAsync(app)))!.LetterReady);
        Assert.False((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded); // nothing to sign yet

        AsRecruiter();
        Assert.False((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 8:00 to 17:00."))).Succeeded); // HR Manager only
        AsHrManager();
        Assert.False((await Run(s => s.IssueLetterAsync(id, "Too short"))).Succeeded);
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 8:00 to 17:00."))).Succeeded);
        Assert.True((await Run(s => s.IssueLetterAsync(id, "Probation: 6 months. Hours: 7:30 to 16:30, revised."))).Succeeded);
        var record = await Db(db => db.Onboardings.SingleAsync(o => o.Id == id));
        Assert.Matches(@"^APT-\d{4}-0001$", record.LetterNumber); // a revision keeps the number
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("revised appointment letter"));

        As(_candidate2, Roles.Candidate);
        Assert.Null(await Run(s => s.GetMyAppointmentAsync(app)));
        Assert.False((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded);

        As(_candidate, Roles.Candidate);
        var mine = (await Run(s => s.GetMyAppointmentAsync(app)))!;
        Assert.True(mine.CanSign);
        Assert.Contains("7:30", mine.LetterTerms);
        Assert.Equal(5, mine.ToBring.Count);
        Assert.False((await Run(s => s.SignLetterAsync(app, "A. Mensah"))).Succeeded);
        Assert.True((await Run(s => s.SignLetterAsync(app, "  ama   MENSAH "))).Succeeded);
        Assert.False((await Run(s => s.SignLetterAsync(app, "Ama Mensah"))).Succeeded); // once

        record = await Db(db => db.Onboardings.SingleAsync(o => o.Id == id));
        Assert.NotNull(record.LetterAcknowledgedAt);
        Assert.Equal("10.0.0.1", record.LetterAcknowledgedIp);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hrm@grel.test") && m.Contains("signed"));

        // A signed letter is final: no revision, and the start date it states is fixed.
        AsHrManager();
        Assert.False((await Run(s => s.IssueLetterAsync(id, "Probation: 3 months, a later change."))).Succeeded);
        Assert.False((await Run(s => s.UpdateFirstDayAsync(id, Today.AddDays(25), null, "HR office, Abura", null, null, null))).Succeeded);
        Assert.True((await Run(s => s.UpdateFirstDayAsync(id, null, new TimeOnly(9, 0), "Main gate, Abura", _hod, null, "GREL-0457"))).Succeeded);
    }

    [Fact]
    public async Task Each_checklist_item_is_ticked_by_its_owner_the_hod_or_hr_and_completion_needs_everything()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted, start: Today);
        var id = await StartAsync(app, Today);
        var list = await TasksAsync(id);
        var itTask = list.First(t => t.Team == OnboardingTeam.InformationTechnology);
        var hrTask = list.First(t => t.Team == OnboardingTeam.HumanResources);
        var deptTask = list.First(t => t.Team == OnboardingTeam.Department);

        // Someone with no part in it sees nothing and can tick nothing.
        As(_staff, Roles.Employee);
        Assert.Null(await Run(s => s.GetAsync(id)));
        Assert.False((await Run(s => s.UpdateTaskAsync(itTask.Id, OnboardingTaskStatus.Done, null))).Succeeded);

        // The Head of IT sees the onboarding and their tasks, and can tick only those.
        As(_itHod, Roles.HeadOfDepartment);
        Assert.NotNull(await Run(s => s.GetAsync(id)));
        Assert.Equal(2, (await Run(s => s.MyTasksAsync())).Count);
        Assert.False((await Run(s => s.UpdateTaskAsync(hrTask.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.False((await Run(s => s.UpdateTaskAsync(itTask.Id, OnboardingTaskStatus.NotNeeded, null))).Succeeded); // reason needed
        Assert.True((await Run(s => s.UpdateTaskAsync(itTask.Id, OnboardingTaskStatus.Done, "Account ama.mensah@grel.example"))).Succeeded);
        Assert.Single(await Run(s => s.MyTasksAsync()));

        // The department's HOD ticks department items.
        As(_hod, Roles.HeadOfDepartment);
        Assert.True((await Run(s => s.UpdateTaskAsync(deptTask.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.False((await Run(s => s.CompleteAsync(id))).Succeeded); // HR completes

        // HR: completion needs the signed letter and no open item.
        AsRecruiter();
        Assert.Contains("Issue the appointment letter", (await Run(s => s.GetAsync(id)))!.CompleteBlockedReason);
        await IssueAndSignAsync(id, app);
        AsRecruiter();
        Assert.Contains("still to do", (await Run(s => s.GetAsync(id)))!.CompleteBlockedReason);
        foreach (var t in (await TasksAsync(id)).Where(t => t.Status == OnboardingTaskStatus.Open))
            Assert.True((await Run(s => s.UpdateTaskAsync(t.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.True((await Run(s => s.GetAsync(id)))!.CanComplete);
        Assert.True((await Run(s => s.CompleteAsync(id))).Succeeded);

        Assert.Equal(OnboardingStatus.Completed, (await Db(db => db.Onboardings.SingleAsync(o => o.Id == id))).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("completed"));
        Assert.False((await Run(s => s.UpdateTaskAsync(itTask.Id, OnboardingTaskStatus.Open, null))).Succeeded); // closed
        As(_itHod, Roles.HeadOfDepartment);
        Assert.Empty(await Run(s => s.MyTasksAsync()));
    }

    [Fact]
    public async Task Hr_reshapes_the_checklist_moving_the_start_date_moves_open_items_and_cancelling_needs_a_reason()
    {
        var app = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var id = await StartAsync(app);
        var itTask = (await TasksAsync(id)).First(t => t.Team == OnboardingTeam.InformationTechnology);

        AsRecruiter();
        Assert.False((await Run(s => s.AssignTaskAsync(itTask.Id, _candidate, null))).Succeeded); // not the new employee
        Assert.True((await Run(s => s.AssignTaskAsync(itTask.Id, _staff, Today.AddDays(15)))).Succeeded);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: clerk@grel.test") && m.Contains("a task for you"));
        As(_staff, Roles.Employee);
        Assert.NotNull(await Run(s => s.GetAsync(id))); // a task gives access

        AsRecruiter();
        Assert.True((await Run(s => s.AddTaskAsync(id, OnboardingTeam.Department, "Safety briefing at the APM factory", Today.AddDays(21), _hod))).Succeeded);
        var added = (await TasksAsync(id)).Last();
        Assert.Equal(("Safety briefing at the APM factory", _hod), (added.Title, added.AssignedToUserId));
        Assert.True((await Run(s => s.RemoveTaskAsync(added.Id))).Succeeded);
        Assert.Equal(OnboardingWorkflow.DefaultChecklist.Count, (await TasksAsync(id)).Count);

        // Starting a week later moves every open item a week later.
        Assert.True((await Run(s => s.UpdateFirstDayAsync(id, Today.AddDays(27), new TimeOnly(7, 30), "HR office, Abura", _hod, "Bring safety boots.", null))).Succeeded);
        Assert.Equal(Today.AddDays(22), (await TasksAsync(id)).First(t => t.Id == itTask.Id).DueDate);

        Assert.False((await Run(s => s.CancelAsync(id, null))).Succeeded);
        Assert.True((await Run(s => s.CancelAsync(id, "Did not report on the first day."))).Succeeded);
        As(_candidate, Roles.Candidate);
        Assert.Null(await Run(s => s.GetMyAppointmentAsync(app)));
    }
}
