using Grsms.Application.Common;
using Grsms.Application.Notifications;
using Grsms.Application.Surveys;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Interviews;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Surveys;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Module 10: every email becomes a bell notification (with a safe, local link), text messages, the automatic reminders
/// (each sent once), and the feedback surveys (invitations, confidential answers, results only from 3 responses).
/// </summary>
public class NotificationServiceTests : IAsyncLifetime
{
    private const int Apm = 31, It = 2;
    private static DateTime Now => DateTime.UtcNow;
    private static DateOnly Today => DateOnly.FromDateTime(Now);

    private readonly TestDatabase _db = new();
    private string _hod = "", _itHod = "", _ro = "", _hrm = "", _md = "", _ama = "", _kofi = "", _esi = "";
    private int _vacancyId;

    public async Task InitializeAsync()
    {
        _hod = await _db.CreateUserAsync("hod@grel.test", Apm, Roles.HeadOfDepartment);
        _itHod = await _db.CreateUserAsync("ithod@grel.test", It, Roles.HeadOfDepartment);
        _ro = await _db.CreateUserAsync("ro@grel.test", 1, Roles.RecruitmentOfficer);
        _hrm = await _db.CreateUserAsync("hrm@grel.test", 1, Roles.HrManager);
        _md = await _db.CreateUserAsync("md@grel.test", 1, Roles.ManagingDirector);
        _ama = await _db.CreateUserAsync("ama@mail.test", null, Roles.Candidate);
        _kofi = await _db.CreateUserAsync("kofi@mail.test", null, Roles.Candidate);
        _esi = await _db.CreateUserAsync("esi@mail.test", null, Roles.Candidate);

        _vacancyId = await Db(async db =>
        {
            (await db.Departments.FindAsync(Apm))!.HeadOfDepartmentUserId = _hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-N-1", DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Process Engineer",
                NumberOfPositions = 3, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Needed.", BudgetCode = "APM-26", EstimatedAnnualCost = 1, RequestedByUserId = _hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = Now
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var v = new Vacancy
            {
                VacancyNumber = "VAC-N-1", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = Apm,
                SiteId = 2, RequestingHodUserId = _hod, RecruiterUserId = _ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 3,
                VacancyType = VacancyType.External, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
                ClosingDate = Today.AddDays(-20), Status = VacancyStatus.Closed, StatusChangedAt = Now
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

    private async Task<T> Run<TService, T>(Func<TService, Task<T>> call) where TService : notnull
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private async Task<T> Db<T>(Func<ApplicationDbContext, Task<T>> call)
    {
        using var scope = _db.NewScope();
        return await call(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<int> ApplicationAsync(string applicant, string name, ApplicationStatus status, DateTime? statusChangedAt = null) => Db(async db =>
    {
        var a = new JobApplication
        {
            ApplicationNumber = "APP-N-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = applicant,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = name,
            Email = $"{name.ToLowerInvariant()}@mail.test", Phone = "024 123 4567", HighestEducation = EducationLevel.Bachelors,
            Status = status, SubmittedAt = Now.AddDays(-30), StatusChangedAt = statusChangedAt ?? Now
        };
        db.JobApplications.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    });

    /// <summary>A completed candidate survey from a new applicant (one application per person per vacancy).</summary>
    private async Task<int> CompletedSurveyAsync(int overall, string? comment)
    {
        var user = await _db.CreateUserAsync($"s{Guid.NewGuid():N}"[..10] + "@mail.test", null, Roles.Candidate);
        return await CompletedSurveyAsync(user, overall, comment);
    }

    private Task<int> CompletedSurveyAsync(string user, int overall, string? comment) => Db(async db =>
    {
        var app = new JobApplication
        {
            ApplicationNumber = "APP-S-" + Guid.NewGuid().ToString("N")[..6], VacancyId = _vacancyId, ApplicantUserId = user,
            Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod, FullName = "Someone",
            Email = "x@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors, Status = ApplicationStatus.NotSuccessful,
            SubmittedAt = Now.AddDays(-60), StatusChangedAt = Now.AddDays(-50)
        };
        db.JobApplications.Add(app);
        await db.SaveChangesAsync();
        var inv = new SurveyInvitation
        {
            Type = SurveyType.CandidateExperience, UserId = user, JobApplicationId = app.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
            RequestingHodUserId = _hod, Outcome = ApplicationStatus.NotSuccessful, SentAt = Now.AddDays(-5), ExpiresOn = Today.AddDays(25), CompletedAt = Now
        };
        db.SurveyInvitations.Add(inv);
        await db.SaveChangesAsync();
        db.SurveyAnswers.Add(new SurveyAnswer { SurveyInvitationId = inv.Id, QuestionKey = "overall", Rating = overall });
        if (comment is not null) db.SurveyAnswers.Add(new SurveyAnswer { SurveyInvitationId = inv.Id, QuestionKey = "comments", Text = comment });
        await db.SaveChangesAsync();
        return inv.Id;
    });

    // ------------------------------------------------------------------ notifications and text messages

    [Fact]
    public async Task Every_email_becomes_a_notification_with_a_local_link_and_texts_go_to_files_while_sms_is_off()
    {
        As(_ro, Roles.RecruitmentOfficer);
        await Run<WorkflowMailer, int>(async m =>
        {
            await m.ToUserAsync(_ama, "Offer of employment", "Congratulations <b>Ama</b> &amp; welcome .", "https://grsms.test/MyApplications/Details/7", "Open");
            await m.ToUserAsync(_ama, "Odd link", "Text", "//evil.example/steal", "Open");
            await m.TextAsync("024 123 4567", "GREL: test message");
            await m.TextAsync("12345", "not a usable number");
            return 0;
        });

        var list = await Run<INotificationCenter, IReadOnlyList<NotificationDto>>(n => n.ListAsync(_ama, 10));
        Assert.Equal(2, list.Count);
        var offer = list.Single(n => n.Title == "Offer of employment");
        Assert.Equal(("Congratulations Ama & welcome.", "/MyApplications/Details/7"), (offer.Body, offer.Link));
        Assert.Null(list.Single(n => n.Title == "Odd link").Link); // never a link to another site
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("Offer of employment"));

        Assert.Equal(2, await Run<INotificationCenter, int>(n => n.CountUnreadAsync(_ama)));
        Assert.Null(await Run<INotificationCenter, string?>(n => n.OpenAsync(_kofi, offer.Id))); // not Kofi's
        Assert.Equal("/MyApplications/Details/7", await Run<INotificationCenter, string?>(n => n.OpenAsync(_ama, offer.Id)));
        Assert.Equal(1, await Run<INotificationCenter, int>(n => n.CountUnreadAsync(_ama)));
        await Run<INotificationCenter, int>(async n => { await n.MarkAllReadAsync(_ama); return 0; });
        Assert.Equal(0, await Run<INotificationCenter, int>(n => n.CountUnreadAsync(_ama)));
        Assert.Equal(2, await Run<INotificationCenter, int>(n => n.PurgeAsync(Now.AddMinutes(1))));

        Assert.Equal(0, await Db(db => db.AuditLogs.CountAsync(a => a.EntityName == "Notification")));
        var text = Assert.Single(_db.SentTexts());
        Assert.Contains("To: 233241234567", text);

        Assert.Equal("233241234567", PhoneNumbers.ToInternational("+233 24 123 4567"));
        Assert.Equal("233241234567", PhoneNumbers.ToInternational("00233241234567"));
        Assert.Null(PhoneNumbers.ToInternational(""));
    }

    // ------------------------------------------------------------------ reminders

    [Fact]
    public async Task Reminders_go_out_once_for_offers_interviews_first_days_onboarding_items_and_waiting_approvals()
    {
        var tomorrow = Today.AddDays(1);
        var offered = await ApplicationAsync(_ama, "Ama", ApplicationStatus.Offer);
        var hired = await ApplicationAsync(_kofi, "Kofi", ApplicationStatus.Hired);
        var interviewed = await ApplicationAsync(_esi, "Esi", ApplicationStatus.Interview);
        await Db(async db =>
        {
            var rec = new HiringRecommendation
            {
                Number = "HIR-N-1", JobApplicationId = offered, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
                ProposedSalary = 8000, ProposedStartDate = Today.AddDays(20), Justification = "Good.", CreatedByUserId = _ro, Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            var offer = new Offer
            {
                Number = "OFF-N-1", JobApplicationId = offered, HiringRecommendationId = rec.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, JobTitle = "Process Engineer", MonthlySalary = 8000, StartDate = Today.AddDays(20), ExpiresOn = tomorrow,
                Status = OfferStatus.Sent, SentAt = Now.AddDays(-5), SentByUserId = _ro
            };
            db.Offers.Add(offer);
            var interview = new Interview
            {
                JobApplicationId = interviewed, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
                StartsAt = tomorrow.ToDateTime(new TimeOnly(10, 0)), Location = "Board Room", ScheduledByUserId = _ro
            };
            interview.Panel.Add(new InterviewPanelMember { UserId = _hod, IsChair = true });
            db.Interviews.Add(interview);
            db.RecruitmentRequests.Add(new RecruitmentRequest
            {
                RequestNumber = "RR-N-2", DivisionId = 2, DepartmentId = Apm, SiteId = 2, PositionTitle = "Chemist", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Lab.", BudgetCode = "APM-26",
                EstimatedAnnualCost = 1, RequestedByUserId = _hod, Status = RecruitmentRequestStatus.HrApproval, StatusChangedAt = Now.AddDays(-4)
            });
            await db.SaveChangesAsync();

            // Kofi's offer was accepted; he starts tomorrow, and the Head of IT has an overdue item.
            var accepted = new Offer
            {
                Number = "OFF-N-2", JobApplicationId = hired, HiringRecommendationId = rec.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, JobTitle = "Process Engineer", MonthlySalary = 8000, StartDate = tomorrow, ExpiresOn = Today.AddDays(-3),
                Status = OfferStatus.Accepted, SentAt = Now.AddDays(-10), SentByUserId = _ro, RespondedAt = Now.AddDays(-8)
            };
            db.Offers.Add(accepted);
            await db.SaveChangesAsync();
            var onboarding = new OnboardingRecord
            {
                Number = "ONB-N-1", JobApplicationId = hired, OfferId = accepted.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, SiteId = 2, EmployeeUserId = _kofi, EmployeeName = "Kofi", JobTitle = "Process Engineer", MonthlySalary = 8000,
                StartDate = tomorrow, ReportLocation = "HR office, Abura", ContactPersonUserId = _hod, StatusChangedAt = Now, CreatedByUserId = _ro
            };
            db.Onboardings.Add(onboarding);
            await db.SaveChangesAsync();
            db.OnboardingTasks.Add(new OnboardingTask
            {
                OnboardingRecordId = onboarding.Id, Team = OnboardingTeam.InformationTechnology, Title = "Create the email account",
                DueDate = Today.AddDays(-1), AssignedToUserId = _itHod, SortOrder = 1
            });
            await db.SaveChangesAsync();
            return 0;
        });

        // Offer, interview, first day, the Head of IT's summary and the request waiting 4 days at HR: 5 reminders.
        Assert.Equal(5, await Run<ReminderService, int>(r => r.RunAsync(Now)));
        var mail = _db.SentEmails();
        Assert.Contains(mail, m => m.Contains("To: ama@mail.test") && m.Contains("please reply to your offer"));
        Assert.Contains(mail, m => m.Contains("To: esi@mail.test") && m.Contains("is tomorrow"));
        Assert.Contains(mail, m => m.Contains("To: hod@grel.test") && m.Contains("with Esi tomorrow"));
        Assert.Contains(mail, m => m.Contains("To: kofi@mail.test") && m.Contains("Welcome to GREL tomorrow"));
        Assert.Contains(mail, m => m.Contains("To: hod@grel.test") && m.Contains("Kofi starts tomorrow"));
        Assert.Contains(mail, m => m.Contains("To: ithod@grel.test") && m.Contains("overdue"));
        Assert.Contains(mail, m => m.Contains("To: hrm@grel.test") && m.Contains("Waiting 4 days: RR-N-2"));
        Assert.DoesNotContain(mail, m => m.Contains("To: hod@grel.test") && m.Contains("RR-N-2")); // the requester is not reminded
        Assert.Equal(3, _db.SentTexts().Count); // offer, interview and first day go to the applicant's phone too
        Assert.True(await Run<INotificationCenter, int>(n => n.CountUnreadAsync(_hrm)) >= 1);

        // Running again the same day sends nothing new.
        Assert.Equal(0, await Run<ReminderService, int>(r => r.RunAsync(Now)));

        // A week later: the request is reminded again, and the Head of IT gets a new daily summary.
        Assert.Equal(2, await Run<ReminderService, int>(r => r.RunAsync(Now.AddDays(7))));
    }

    // ------------------------------------------------------------------ surveys

    [Fact]
    public async Task Applicants_are_invited_once_answers_are_confidential_and_results_need_three_responses()
    {
        var ended = await ApplicationAsync(_ama, "Ama", ApplicationStatus.NotSuccessful, Now.AddDays(-2));
        await ApplicationAsync(_kofi, "Kofi", ApplicationStatus.NotSuccessful, Now.AddHours(-2));    // too recent
        var yaw = await _db.CreateUserAsync("yaw@mail.test", null, Roles.Candidate);
        await ApplicationAsync(yaw, "Yaw", ApplicationStatus.OfferDeclined, Now.AddDays(-40));       // too old
        await ApplicationAsync(_esi, "Esi", ApplicationStatus.Withdrawn, Now.AddDays(-2));           // withdrawn: not asked

        Assert.Equal(1, await Run<SurveyService, int>(s => s.InviteDueAsync(Now)));
        Assert.Equal(0, await Run<SurveyService, int>(s => s.InviteDueAsync(Now)));
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: ama@mail.test") && m.Contains("experience of applying"));
        var inv = await Db(db => db.SurveyInvitations.SingleAsync());
        Assert.Equal((SurveyType.CandidateExperience, ended, 2, Apm, _hod, ApplicationStatus.NotSuccessful),
            (inv.Type, inv.JobApplicationId, inv.DivisionId, inv.DepartmentId, inv.RequestingHodUserId, inv.Outcome));

        // Only Ama can see and answer it; the overall rating is required and ratings are 1-5.
        As(_kofi, Roles.Candidate);
        Assert.Null(await Run<SurveyService, SurveyFormDto?>(s => s.GetFormAsync(inv.Id)));
        As(_ama, Roles.Candidate);
        Assert.Single(await Run<SurveyService, IReadOnlyList<MySurveyDto>>(s => s.OpenForApplicationAsync(ended)));
        var none = new Dictionary<string, string?>();
        Assert.False((await Run<SurveyService, Result>(s => s.SubmitAsync(inv.Id, new Dictionary<string, int?> { ["clarity"] = 4 }, none))).Succeeded);
        Assert.False((await Run<SurveyService, Result>(s => s.SubmitAsync(inv.Id, new Dictionary<string, int?> { ["overall"] = 6 }, none))).Succeeded);
        Assert.True((await Run<SurveyService, Result>(s => s.SubmitAsync(inv.Id,
            new Dictionary<string, int?> { ["overall"] = 4, ["clarity"] = 5, ["interview"] = null },
            new Dictionary<string, string?> { ["comments"] = "Faster feedback after the closing date, please." }))).Succeeded);
        Assert.False((await Run<SurveyService, Result>(s => s.SubmitAsync(inv.Id, new Dictionary<string, int?> { ["overall"] = 2 }, none))).Succeeded);
        Assert.Empty(await Run<SurveyService, IReadOnlyList<MySurveyDto>>(s => s.OpenForApplicationAsync(ended)));
        Assert.Equal(0, await Db(db => db.AuditLogs.CountAsync(a => a.EntityName == "SurveyAnswer")));

        // Results: only for HR / MD, and only from 3 responses.
        Assert.Null(await Run<SurveyService, SurveyResultsDto?>(s => s.ResultsAsync(new SurveyResultsFilter())));
        As(_hrm, Roles.HrManager);
        var early = (await Run<SurveyService, SurveyResultsDto?>(s => s.ResultsAsync(new SurveyResultsFilter())))!;
        Assert.Equal((1, 1, false), (early.Invited, early.Responded, early.EnoughToShow));
        Assert.Empty(early.Comments);

        await CompletedSurveyAsync(2, null);
        await CompletedSurveyAsync(3, "The interview panel was friendly.");
        var results = (await Run<SurveyService, SurveyResultsDto?>(s => s.ResultsAsync(new SurveyResultsFilter { DepartmentId = Apm })))!;
        Assert.True(results.EnoughToShow);
        Assert.Equal(3, results.Responded);
        var overall = results.Questions.Single(q => q.Key == "overall");
        Assert.Equal((3.0m, 3), (overall.Average, overall.Answers));
        Assert.Equal([0, 1, 1, 1, 0], overall.Distribution);
        Assert.Equal(0, results.Questions.Single(q => q.Key == "interview").Answers); // "not applicable" is not counted
        Assert.Equal(2, results.Comments.Count);
        Assert.False((await Run<SurveyService, SurveyResultsDto?>(s => s.ResultsAsync(new SurveyResultsFilter { DepartmentId = 3 })))!.EnoughToShow);
    }

    [Fact]
    public async Task New_employees_are_invited_the_day_after_their_onboarding_is_completed()
    {
        var hired = await ApplicationAsync(_kofi, "Kofi", ApplicationStatus.Hired, Now.AddDays(-20));
        await Db(async db =>
        {
            var rec = new HiringRecommendation
            {
                Number = "HIR-N-9", JobApplicationId = hired, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm, RequestingHodUserId = _hod,
                ProposedSalary = 8000, ProposedStartDate = Today, Justification = "Good.", CreatedByUserId = _ro, Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            var offer = new Offer
            {
                Number = "OFF-N-9", JobApplicationId = hired, HiringRecommendationId = rec.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, JobTitle = "Process Engineer", MonthlySalary = 8000, StartDate = Today.AddDays(-5), ExpiresOn = Today.AddDays(-10),
                Status = OfferStatus.Accepted, SentAt = Now.AddDays(-20), SentByUserId = _ro
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
            db.Onboardings.Add(new OnboardingRecord
            {
                Number = "ONB-N-9", JobApplicationId = hired, OfferId = offer.Id, VacancyId = _vacancyId, DivisionId = 2, DepartmentId = Apm,
                RequestingHodUserId = _hod, SiteId = 2, EmployeeUserId = _kofi, EmployeeName = "Kofi", JobTitle = "Process Engineer", MonthlySalary = 8000,
                StartDate = Today.AddDays(-5), ReportLocation = "HR office", Status = OnboardingStatus.Completed, StatusChangedAt = Now.AddDays(-2),
                CreatedByUserId = _ro
            });
            await db.SaveChangesAsync();
            return 0;
        });

        // The candidate survey (hired 20 days ago) and the new-employee survey (onboarding completed 2 days ago).
        Assert.Equal(2, await Run<SurveyService, int>(s => s.InviteDueAsync(Now)));
        var types = await Db(db => db.SurveyInvitations.Select(i => i.Type).ToListAsync());
        Assert.Contains(SurveyType.NewEmployee, types);
        Assert.Contains(_db.SentEmails(), m => m.Contains("To: kofi@mail.test") && m.Contains("How was your start at GREL?"));
    }
}
