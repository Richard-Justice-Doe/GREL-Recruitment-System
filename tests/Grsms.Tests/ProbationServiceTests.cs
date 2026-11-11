using Grsms.Application.Workforce;
using Grsms.Application.Onboarding;
using Grsms.Application.Probation;
using Grsms.Application.Reports;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Probation;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 21: probation management and confirmation: the probation starts at activation; the HOD's initial meeting signed by
/// both; the 3-month and final assessments (windows, ratings); the HR Manager's decision (confirm with the letter filed,
/// extend within 3 months in total, do not confirm, return); who may see and do what; the dashboard widgets and reports.
/// </summary>
public class ProbationServiceTests : OnboardingTestBase
{
    private async Task<T> RunProbation<T>(Func<ProbationService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ProbationService>());
    }

    private Task<ProbationRecord> ProbationAsync() => Db(db => db.ProbationRecords.SingleAsync());

    private void AsHod() => As(_hod, Roles.HeadOfDepartment);

    private static InitialMeetingForm Meeting(string signer) => new()
    {
        MeetingDate = Today,
        Objectives = [new() { Objective = "Run the line safely", SuccessIndicators = "No lost-time incidents", DueDate = Today.AddMonths(3) }],
        DevelopmentPlan = [new() { TrainingNeed = "Lock-out / tag-out", Timeline = "Month 1" }],
        SignatureName = signer
    };

    private static AssessmentForm Ratings(ProbationOutcome? outcome = null, int? months = null) => new()
    {
        ApplicationOfSkills = ProbationRating.Good, QualityOfWork = ProbationRating.VeryGood, Productivity = ProbationRating.Good,
        Cooperation = ProbationRating.Outstanding, Dependability = ProbationRating.Good,
        Comments = "Settled in well; reliable on shift.", ImprovementPlan = months is null ? null : "Weekly check-ins on reporting.",
        Outcome = outcome, ExtensionMonths = months, ExtensionReason = months is null ? null : "Reporting not yet to standard."
    };

    /// <summary>Activated, initial meeting signed by the HOD and the employee: In progress.</summary>
    private async Task<int> InProgressAsync()
    {
        await ActivatedAsync();
        var id = (await ProbationAsync()).Id;
        AsHod();
        Assert.True((await RunProbation(s => s.RecordInitialMeetingAsync(id, Meeting(_hod)))).Succeeded);
        As(_candidate, Roles.Employee);
        Assert.True((await RunProbation(s => s.SignAsEmployeeAsync(id, "Ama Mensah"))).Succeeded);
        return id;
    }

    /// <summary>Moves the probation's dates back so that its end date is today (assessments become due).</summary>
    private Task ReachEndAsync(int id) => Db(async db =>
    {
        var p = await db.ProbationRecords.SingleAsync(x => x.Id == id);
        var shift = p.EndDate.DayNumber - Today.DayNumber;
        p.StartDate = p.StartDate.AddDays(-shift);
        p.EndDate = Today;
        p.OriginalEndDate = p.OriginalEndDate.AddDays(-shift);
        return await db.SaveChangesAsync();
    });

    /// <summary>In progress, first assessment done, at the end date, final assessment with <paramref name="outcome"/>: awaiting HR.</summary>
    private async Task<int> AwaitingDecisionAsync(ProbationOutcome outcome, int? months = null)
    {
        var id = await InProgressAsync();
        await ReachEndAsync(id);
        AsHod();
        Assert.True((await RunProbation(s => s.AssessAsync(id, Ratings()))).Succeeded);
        var final = await RunProbation(s => s.AssessAsync(id, Ratings(outcome, months)));
        Assert.True(final.Succeeded, string.Join(" ", final.Errors));
        return id;
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Completing_the_onboarding_starts_a_six_month_probation()
    {
        await ActivatedAsync();
        var p = await ProbationAsync();
        Assert.Matches(@"^PRB-\d{4}-0001$", p.Number);
        Assert.Equal((Today, Today.AddMonths(6), Today.AddDays(7), _hod, ProbationStatus.InitialMeeting),
            (p.StartDate, p.EndDate, p.InitialMeetingDueDate, p.HodUserId, p.Status));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("initial meeting"));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("probation has started"));
    }

    [Fact]
    public async Task The_hod_records_and_signs_the_initial_meeting_and_the_employee_signs_it()
    {
        await ActivatedAsync();
        var id = (await ProbationAsync()).Id;

        AsRecruiter();
        Assert.False((await RunProbation(s => s.RecordInitialMeetingAsync(id, Meeting(_ro)))).Succeeded); // HOD only
        AsHod();
        var noObjective = Meeting(_hod);
        noObjective.Objectives.Clear();
        Assert.Contains((await RunProbation(s => s.RecordInitialMeetingAsync(id, noObjective))).Errors, e => e.Contains("objective"));
        Assert.Contains((await RunProbation(s => s.RecordInitialMeetingAsync(id, Meeting("Someone else")))).Errors, e => e.Contains("full name"));
        Assert.True((await RunProbation(s => s.RecordInitialMeetingAsync(id, Meeting(_hod)))).Succeeded);
        Assert.False((await RunProbation(s => s.AssessAsync(id, Ratings()))).Succeeded); // not signed by the employee yet

        As(_candidate2, Roles.Candidate);
        Assert.False((await RunProbation(s => s.SignAsEmployeeAsync(id, "Ama Mensah"))).Succeeded);
        As(_candidate, Roles.Employee);
        var mine = (await RunProbation(s => s.GetMineAsync())).Single();
        Assert.True(mine.CanSignAsEmployee);
        Assert.Equal("Run the line safely", mine.Objectives.Single().Objective);
        Assert.False((await RunProbation(s => s.SignAsEmployeeAsync(id, "A. Mensah"))).Succeeded);
        Assert.True((await RunProbation(s => s.SignAsEmployeeAsync(id, "Ama Mensah"))).Succeeded);

        var p = await ProbationAsync();
        Assert.Equal(ProbationStatus.InProgress, p.Status);
        Assert.NotNull(p.HodSignedAt);
        Assert.NotNull(p.EmployeeSignedAt);
        Assert.True(await Db(db => db.PersonnelFileDocuments.AnyAsync(d => d.CategoryId == PersonnelFileCategories.ProbationRecords && d.Title.Contains("initial meeting"))));
    }

    [Fact]
    public async Task Assessments_open_near_their_due_dates_and_need_every_rating()
    {
        var id = await InProgressAsync();
        AsHod();
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, Ratings()))).Errors, e => e.Contains("can be recorded from")); // 3 months away

        await Db(async db => // the 3-month point is today
        {
            var p = await db.ProbationRecords.SingleAsync();
            p.StartDate = Today.AddMonths(-3);
            return await db.SaveChangesAsync();
        });
        var missing = Ratings();
        missing.Productivity = null;
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, missing))).Errors, e => e.Contains("Rate every category"));
        Assert.True((await RunProbation(s => s.AssessAsync(id, Ratings()))).Succeeded); // ability to supervise may be empty

        var first = await Db(db => db.ProbationAssessments.SingleAsync());
        Assert.Equal((ProbationAssessmentKind.First, (ProbationRating?)null, (ProbationOutcome?)null), (first.Kind, first.AbilityToSupervise, first.Outcome));
        Assert.Equal(ProbationStatus.InProgress, (await ProbationAsync()).Status);
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.Confirm)))).Errors, e => e.Contains("final assessment can be recorded"));
    }

    [Fact]
    public async Task Confirmation_generates_the_letter_files_it_and_tells_the_employee()
    {
        var id = await AwaitingDecisionAsync(ProbationOutcome.Confirm);
        Assert.Equal(ProbationStatus.AwaitingHrDecision, (await ProbationAsync()).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hrm@grel.test") && m.Contains("needs your decision"));

        AsRecruiter();
        Assert.False((await RunProbation(s => s.HrDecideAsync(id, true, null))).Succeeded); // HR Manager only
        AsHrManager();
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, true, "Well done."))).Succeeded);

        var p = await ProbationAsync();
        Assert.Equal((ProbationStatus.Confirmed, _hrm), (p.Status, p.HrManagerUserId));
        var c = await Db(db => db.ConfirmationRecords.SingleAsync());
        Assert.Matches(@"^CNF-\d{4}-0001$", c.LetterNumber);
        Assert.Equal((_candidate, _hrm, Today), (c.EmployeeUserId, c.ApprovedByUserId, c.ConfirmationDate));
        var letterDoc = await Db(db => db.PersonnelFileDocuments.SingleAsync(d => d.Id == c.PersonnelFileDocumentId));
        Assert.Equal(PersonnelFileCategories.GeneratedLetters, letterDoc.CategoryId);
        Assert.Equal(2, await Db(db => db.PersonnelFileDocuments.CountAsync(d => d.CategoryId == PersonnelFileCategories.ProbationRecords && d.Title.Contains("assessment"))));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("is confirmed"));

        // The employee reads their own letter; another candidate cannot.
        As(_candidate, Roles.Employee);
        var letter = (await RunProbation(s => s.GetConfirmationLetterAsync(id)))!;
        Assert.Contains("we are pleased to confirm your appointment as <strong>Process Engineer</strong>", letter.Html);
        As(_candidate2, Roles.Candidate);
        Assert.Null(await RunProbation(s => s.GetConfirmationLetterAsync(id)));
        Assert.False(ProbationWorkflow.IsActive((await ProbationAsync()).Status));
    }

    [Fact]
    public async Task Extensions_move_the_end_date_and_never_exceed_three_months_in_total()
    {
        var id = await AwaitingDecisionAsync(ProbationOutcome.Extend, months: 2);
        AsHrManager();
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, true, null))).Succeeded);
        var p = await ProbationAsync();
        Assert.Equal((ProbationStatus.Extended, 2, Today.AddMonths(2)), (p.Status, p.ExtendedMonths, p.EndDate));
        Assert.Equal("Reporting not yet to standard.", p.ExtensionReason);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("has been extended"));

        // At the new end date: only one month is left.
        await ReachEndAsync(id);
        AsHod();
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.Extend, 2)))).Errors, e => e.Contains("1 to 1 month"));
        var noPlan = Ratings(ProbationOutcome.Extend, 1);
        noPlan.ImprovementPlan = null;
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, noPlan))).Errors, e => e.Contains("improvement plan"));
        Assert.True((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.Extend, 1)))).Succeeded);
        AsHrManager();
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, true, null))).Succeeded);
        Assert.Equal(3, (await ProbationAsync()).ExtendedMonths);

        await ReachEndAsync(id);
        AsHod();
        Assert.Contains((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.Extend, 1)))).Errors, e => e.Contains("maximum of 3 months"));
        Assert.True((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.Confirm)))).Succeeded);
    }

    [Fact]
    public async Task The_hr_manager_can_return_the_assessment_or_not_confirm()
    {
        var id = await AwaitingDecisionAsync(ProbationOutcome.DoNotConfirm);
        AsHrManager();
        Assert.False((await RunProbation(s => s.HrDecideAsync(id, false, null))).Succeeded); // comments required to return
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, false, "Please attach the warning letters first."))).Succeeded);
        Assert.Equal(ProbationStatus.InProgress, (await ProbationAsync()).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: hod@grel.test") && m.Contains("returned"));

        AsHod();
        Assert.True((await RunProbation(s => s.AssessAsync(id, Ratings(ProbationOutcome.DoNotConfirm)))).Succeeded);
        AsHrManager();
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, true, null))).Succeeded);
        Assert.Equal(ProbationStatus.NotConfirmed, (await ProbationAsync()).Status);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("outcome of your probation"));
        Assert.Empty(await Db(db => db.ConfirmationRecords.ToListAsync()));
    }

    [Fact]
    public async Task Only_hr_the_department_hod_and_the_employee_see_a_probation()
    {
        var id = await InProgressAsync();
        As(_itHod, Roles.HeadOfDepartment);
        Assert.Null(await RunProbation(s => s.GetAsync(id)));
        Assert.Empty(await RunProbation(s => s.SearchAsync(new ProbationFilter { Tab = "all" })));
        Assert.False((await RunProbation(s => s.AssessAsync(id, Ratings()))).Succeeded);
        As(_staff, Roles.Employee);
        Assert.Null(await RunProbation(s => s.GetAsync(id)));
        Assert.Empty(await RunProbation(s => s.GetMineAsync()));

        AsHod();
        Assert.Single(await RunProbation(s => s.SearchAsync(new ProbationFilter { Tab = "active" })));
        AsRecruiter();
        Assert.NotNull(await RunProbation(s => s.GetAsync(id)));
        As(_candidate, Roles.Employee);
        Assert.Single(await RunProbation(s => s.GetMineAsync()));
    }

    [Fact]
    public async Task Dashboard_widgets_and_reports_count_onboarding_and_probation()
    {
        var id = await AwaitingDecisionAsync(ProbationOutcome.Confirm);

        AsHrManager();
        using (var scope = _db.NewScope())
        {
            var panel = await scope.ServiceProvider.GetRequiredService<WorkforceDashboardService>().GetAsync();
            Assert.Equal(6, panel.Hr.Count);
            Assert.Equal(1, panel.Hr.Single(w => w.Title == "Confirmations pending").Count);
            Assert.Empty(panel.Hod);
        }
        AsHod();
        using (var scope = _db.NewScope())
        {
            var panel = await scope.ServiceProvider.GetRequiredService<WorkforceDashboardService>().GetAsync();
            Assert.Empty(panel.Hr);
            Assert.Equal(1, panel.Hod.Single(w => w.Title == "Probation cases").Count);
        }

        async Task<ReportResult> Report(string key)
        {
            using var scope = _db.NewScope();
            return await scope.ServiceProvider.GetRequiredService<ReportService>().RunAsync(new ReportFilter { Report = key });
        }
        AsHrManager();
        Assert.Single((await Report(ReportKeys.Probation)).Table.Rows);
        Assert.Equal(2, (await Report(ReportKeys.Beneficiaries)).Table.Rows.Count); // 60% + 40%
        Assert.Equal(10, (await Report(ReportKeys.DocumentVerification)).Table.Rows.Count(r => (string)r.Cells[4]! == "Verified")); // the 10 required documents
        Assert.Empty((await Report(ReportKeys.MissingDocuments)).Table.Rows); // everything was provided
        Assert.True((await RunProbation(s => s.HrDecideAsync(id, true, null))).Succeeded);
        Assert.Single((await Report(ReportKeys.Confirmations)).Table.Rows);
        Assert.Empty((await Report(ReportKeys.ProbationExtensions)).Table.Rows);

        // Personal-data reports are HR's only: a HOD gets an empty report saying why.
        AsHod();
        var hidden = await Report(ReportKeys.Beneficiaries);
        Assert.Empty(hidden.Table.Rows);
        Assert.Contains("Open personnel files", hidden.Kpis.Single().Hint);
        Assert.Single((await Report(ReportKeys.Probation)).Table.Rows); // the HOD's own department
    }
}
