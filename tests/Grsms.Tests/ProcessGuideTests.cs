using Grsms.Application.Process;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/37 CR5 (step C2): the process guide: the stages of the requisition and candidate journeys worked out from the records,
/// the next action, owner and next approver, and the colour against the workflow targets (green, amber, red).
/// </summary>
public class ProcessGuideTests : IAsyncLifetime
{
    private const int Apm = 31;
    private readonly TestDatabase _db = new();
    private string _hod = "", _ro = "", _hrm = "", _cand = "";

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("p.hod@grel.test", Apm, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("p.ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("p.hrm@grel.test", 1, Roles.HrManager);
        _cand = await _db.CreateUserAsync("p.cand@mail.test", null, Roles.Candidate);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        using var scope = _db.NewScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<T> Run<T>(Func<ProcessGuideService, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ProcessGuideService>());
    }

    private Task<RecruitmentRequest> RequestAsync(RecruitmentRequestStatus status, DateTime changedAt) => Db(async db =>
    {
        var r = new RecruitmentRequest
        {
            RequestNumber = "RR-P-" + Guid.NewGuid().ToString("N")[..4], DivisionId = 2, DepartmentId = Apm, SiteId = 3, PositionTitle = "Tapper", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "P", EstimatedAnnualCost = 1,
            RequestedByUserId = _hod, AssignedRecruiterUserId = _ro, Status = status, StatusChangedAt = changedAt, DateRequested = changedAt.AddDays(-1)
        };
        db.RecruitmentRequests.Add(r);
        await db.SaveChangesAsync();
        db.RecruitmentRequestActions.Add(new RecruitmentRequestAction
        {
            RecruitmentRequestId = r.Id, Action = WorkflowAction.Approved, FromStatus = RecruitmentRequestStatus.DepartmentHeadReview, ToStatus = status,
            ActorUserId = _hod, ActorName = "Kwesi Hod", ActionAt = changedAt
        });
        await db.SaveChangesAsync();
        return r;
    });

    [Fact]
    public void Action_center_priority_follows_targets_and_age()
    {
        using var scope = _db.NewScope();
        var center = scope.ServiceProvider.GetRequiredService<ActionCenterService>();
        var now = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc); // Friday
        Grsms.Application.Dashboard.DashboardTaskDto Task(Grsms.Application.Dashboard.DashboardTaskTarget target, Enum status, DateTime since) =>
            new(target, 1, "Tapper", "RR-1", "Approve", "Kofi", status, since);

        // HR approval: target 2 working days (default).
        var fresh = center.Rate(Task(Grsms.Application.Dashboard.DashboardTaskTarget.Request, RecruitmentRequestStatus.HrApproval, now.AddHours(-2)), now);
        Assert.Equal((ActionPriority.Medium, 2), (fresh.Priority, fresh.TargetWorkingDays!.Value)); // due Tuesday: within 2 working days
        var late = center.Rate(Task(Grsms.Application.Dashboard.DashboardTaskTarget.Request, RecruitmentRequestStatus.HrApproval, now.AddDays(-7)), now);
        Assert.Equal((ActionPriority.High, ProcessHealth.Overdue), (late.Priority, late.Health));
        Assert.Contains("Overdue", late.Why);

        // No target: by age (Medium after 3 working days, High after 7).
        var shortlist = Grsms.Application.Dashboard.DashboardTaskTarget.Shortlist;
        Assert.Equal(ActionPriority.Low, center.Rate(Task(shortlist, VacancyStatus.Published, now.AddDays(-1)), now).Priority);
        Assert.Equal(ActionPriority.Medium, center.Rate(Task(shortlist, VacancyStatus.Published, now.AddDays(-4)), now).Priority);
        Assert.Equal(ActionPriority.High, center.Rate(Task(shortlist, VacancyStatus.Published, now.AddDays(-14)), now).Priority);
    }

    [Fact]
    public void Health_follows_the_working_day_target()
    {
        var monday = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ProcessHealth.None, ProcessGuideService.HealthOf(monday, null, monday.AddDays(10)));
        Assert.Equal(ProcessHealth.OnTrack, ProcessGuideService.HealthOf(monday, 4, monday.AddDays(1)));      // 1 of 4
        Assert.Equal(ProcessHealth.Approaching, ProcessGuideService.HealthOf(monday, 4, monday.AddDays(3)));  // 3 of 4 (75 %)
        Assert.Equal(ProcessHealth.Overdue, ProcessGuideService.HealthOf(monday, 2, monday.AddDays(7)));      // next Monday: 5 working days
        Assert.Equal(ProcessHealth.OnTrack, ProcessGuideService.HealthOf(new DateTime(2026, 11, 13, 9, 0, 0), 2, new DateTime(2026, 11, 15, 9, 0, 0))); // Fri → Sun: 0
    }

    [Fact]
    public async Task A_request_at_hr_review_names_the_hr_managers_its_target_and_the_last_action()
    {
        var r = await RequestAsync(RecruitmentRequestStatus.HrApproval, DateTime.UtcNow.AddDays(-14));
        var g = (await Run(s => s.ForRequestAsync(r.Id)))!;
        Assert.Equal([StageState.Done, StageState.Done, StageState.Current, StageState.Upcoming], g.Stages.Take(4).Select(s => s.State));
        Assert.Equal("Waiting for the HR Manager's approval.", g.NextAction);
        Assert.Equal("HR Manager", g.Owner);
        Assert.Contains("p.hrm", g.OwnerNames.Single());
        Assert.StartsWith("HR Manager", g.NextApprover);
        Assert.Equal(2, g.TargetWorkingDays); // System settings default
        Assert.Equal(ProcessHealth.Overdue, g.Health);
        Assert.NotNull(g.DueDate);
        Assert.Equal("Kwesi Hod", g.LastAction!.Who);
        Assert.Contains("14 days", g.PendingFor(DateTime.UtcNow));
    }

    [Fact]
    public async Task An_approved_request_moves_on_to_the_vacancy_and_a_rejected_one_stops()
    {
        var r = await RequestAsync(RecruitmentRequestStatus.Approved, DateTime.UtcNow.AddHours(-3));
        var g = (await Run(s => s.ForRequestAsync(r.Id)))!;
        Assert.Equal(StageState.Current, g.Stages[4].State);
        Assert.Equal("Convert the approved request into a vacancy.", g.NextAction);
        Assert.Equal("Recruitment Officer", g.Owner);

        var rejected = await RequestAsync(RecruitmentRequestStatus.Rejected, DateTime.UtcNow);
        var stopped = (await Run(s => s.ForRequestAsync(rejected.Id)))!;
        Assert.True(stopped.Stopped);
        Assert.Contains(stopped.Stages, s => s.State == StageState.Stopped);
    }

    [Fact]
    public async Task A_sent_offer_waits_for_the_candidate_until_the_reply_by_date()
    {
        var r = await RequestAsync(RecruitmentRequestStatus.Approved, DateTime.UtcNow.AddDays(-30));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var appId = await Db(async db =>
        {
            var v = new Vacancy
            {
                VacancyNumber = "VAC-P-1", RecruitmentRequestId = r.Id, JobTitle = "Tapper", DivisionId = 2, DepartmentId = Apm, SiteId = 3, RequestingHodUserId = _hod,
                RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1, VacancyType = VacancyType.External, JobDescription = "Taps.",
                ClosingDate = today.AddDays(-5), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(v);
            await db.SaveChangesAsync();
            var a = new JobApplication
            {
                ApplicationNumber = "APP-P-1", VacancyId = v.Id, ApplicantUserId = _cand, Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, FullName = "Yaa Asante", Email = "y@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Basic,
                Status = ApplicationStatus.Offer, SubmittedAt = DateTime.UtcNow.AddDays(-20), StatusChangedAt = DateTime.UtcNow.AddDays(-1)
            };
            db.JobApplications.Add(a);
            await db.SaveChangesAsync();
            var rec = new HiringRecommendation
            {
                Number = "HIR-P-1", JobApplicationId = a.Id, VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, ProposedSalary = 0,
                ProposedStartDate = today.AddDays(20), Justification = "Good.", CreatedByUserId = _ro, Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            db.Offers.Add(new Offer
            {
                Number = "OFF-P-1", JobApplicationId = a.Id, HiringRecommendationId = rec.Id, VacancyId = v.Id, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, JobTitle = "Tapper", StartDate = today.AddDays(20), ExpiresOn = today.AddDays(6), Status = OfferStatus.Sent,
                SentAt = DateTime.UtcNow.AddDays(-1), SentByUserId = _ro
            });
            await db.SaveChangesAsync();
            return a.Id;
        });

        var g = (await Run(s => s.ForApplicationAsync(appId)))!;
        Assert.Equal(["Applied", "Screening", "Interview", "Hire approval", "Offer", "Onboarding", "Employee"], g.Stages.Select(s => s.Label));
        Assert.Equal(StageState.Current, g.Stages[4].State);
        Assert.Equal("Candidate", g.Owner);
        Assert.Equal(["Yaa Asante"], g.OwnerNames);
        Assert.Equal(today.AddDays(6), g.DueDate);
        Assert.Equal(5, g.TargetWorkingDays);
    }
}
