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
public class OnboardingServiceTests : OnboardingTestBase
{
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
        Assert.DoesNotContain(list, t => t.Team == OnboardingTeam.NewEmployee); // Phase 18: the new-hire documents replace these items
        Assert.Equal((OnboardingStatus.Draft, Today.AddDays(50)), (record.Status, record.ExpectedCompletionDate));
        var docs = await DocumentsAsync(id);
        Assert.Equal(16, docs.Count);
        Assert.All(docs, d => Assert.Equal(EmployeeDocumentStatus.Pending, d.Status));
        Assert.Equal(5, docs.Count(d => !d.IsRequired));
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
        Assert.Empty(mine.ToBring);
        Assert.Equal(OnboardingStatus.AwaitingEmployeeSubmission, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("new-hire documents"));
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
    public async Task Each_checklist_item_is_ticked_by_its_owner_the_hod_or_hr()
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
        Assert.False((await Run(s => s.CompleteInductionAsync(id))).Succeeded); // HR closes the induction
        Assert.Null((await Run(s => s.GetAsync(id)))!.Documents);              // a HOD never sees the personal documents
    }

    [Fact]
    public async Task The_full_workflow_runs_through_all_eight_stages_to_an_active_staff_account()
    {
        var (id, app) = await SubmittedAsync(Today);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("documents to verify"));

        // Stage moves are HR's (not the HOD's), and only in order.
        As(_hod, Roles.HeadOfDepartment);
        Assert.False((await Run(s => s.MarkDocumentsVerifiedAsync(id))).Succeeded);
        AsRecruiter();
        Assert.False((await Run(s => s.CompleteInductionAsync(id))).Succeeded);
        Assert.False((await Run(s => s.ActivateAsync(id, "GREL-0457"))).Succeeded);
        Assert.Contains("still need to be verified", (await Run(s => s.GetAsync(id)))!.VerifiedBlockedReason);

        // Verification → HR approval. The recruiter verified, so the HR Manager approves (not the other way round).
        await VerifyAllAsync(id);
        Assert.True((await Run(s => s.MarkDocumentsVerifiedAsync(id))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingHrApproval, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hrm@grel.test") && m.Contains("needs your approval"));
        Assert.False((await Run(s => s.HrDecideAsync(id, true, null))).Succeeded); // HR Manager only

        AsHrManager();
        Assert.False((await Run(s => s.HrDecideAsync(id, false, null))).Succeeded); // returning needs comments
        Assert.True((await Run(s => s.HrDecideAsync(id, true, "All in order."))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingDepartmentalInduction, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ithod@grel.test") && m.Contains("induction can begin"));

        // Induction: every task done → activation.
        AsRecruiter();
        Assert.Contains("still to do", (await Run(s => s.GetAsync(id)))!.InductionBlockedReason);
        foreach (var t in (await TasksAsync(id)).Where(t => t.Status == OnboardingTaskStatus.Open))
            Assert.True((await Run(s => s.UpdateTaskAsync(t.Id, OnboardingTaskStatus.Done, null))).Succeeded);
        Assert.True((await Run(s => s.CompleteInductionAsync(id))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingActivation, (await RecordAsync(id)).Status);

        // Activation needs the staff number and turns the account into a staff account.
        Assert.False((await Run(s => s.ActivateAsync(id, null))).Succeeded);
        Assert.True((await Run(s => s.ActivateAsync(id, "grel-0457"))).Succeeded);
        var done = await RecordAsync(id);
        Assert.Equal((OnboardingStatus.Completed, "GREL-0457"), (done.Status, done.EmployeeNumber));
        Assert.NotNull(done.CompletedAt);
        var account = await Db(db => db.Users.SingleAsync(u => u.Id == _candidate));
        Assert.Equal(("GREL-0457", (int?)Apm, false), (account.StaffNumber, account.DepartmentId, account.IsCandidate));
        var roles = await Db(db => (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == _candidate select r.Name).ToListAsync());
        Assert.Equal([Roles.Employee], roles);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("staff account is active"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("completed"));

        // The history has every step, and the finished onboarding is closed.
        Assert.Equal(
            [OnboardingAction.Started, OnboardingAction.LetterIssued, OnboardingAction.Submitted, OnboardingAction.DocumentsVerified,
             OnboardingAction.HrApproved, OnboardingAction.InductionCompleted, OnboardingAction.Activated],
            (await Run(s => s.GetAsync(id)))!.History.Select(h => h.Action));
        var itTask = (await TasksAsync(id)).First(t => t.Team == OnboardingTeam.InformationTechnology);
        Assert.False((await Run(s => s.UpdateTaskAsync(itTask.Id, OnboardingTaskStatus.Open, null))).Succeeded);
        As(_itHod, Roles.HeadOfDepartment);
        Assert.Empty(await Run(s => s.MyTasksAsync()));
    }

    [Fact]
    public async Task The_hr_manager_who_verified_the_documents_cannot_also_approve_and_returns_go_back_a_stage()
    {
        var (id, _) = await SubmittedAsync();
        AsHrManager(); // the HR Manager verifies this time
        foreach (var d in await DocumentsAsync(id))
            Assert.True((await RunDocs(s => s.VerifyAsync(d.Id))).Succeeded);
        Assert.True((await Run(s => s.MarkDocumentsVerifiedAsync(id))).Succeeded);
        Assert.Contains("another HR Manager", (await Run(s => s.GetAsync(id)))!.HrDecisionBlockedReason);
        Assert.False((await Run(s => s.HrDecideAsync(id, true, null))).Succeeded);

        var hrm2 = await _db.CreateUserAsync("hrm2@grel.test", 1, Roles.HrManager);
        As(hrm2, Roles.HrManager);
        Assert.True((await Run(s => s.HrDecideAsync(id, false, "Check the police clearance date again."))).Succeeded);
        Assert.Equal(OnboardingStatus.AwaitingDocumentVerification, (await RecordAsync(id)).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ro@grel.test") && m.Contains("returned for verification"));
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
