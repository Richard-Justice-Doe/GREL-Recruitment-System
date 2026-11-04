using System.Net;
using Grsms.Application.Surveys;
using Grsms.Domain.Applications;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Through the real pages: the survey invitation shows on the candidate's bell, opening it goes to the survey, the
/// candidate answers it, and HR opens the results page (which the candidate cannot).
/// </summary>
public class NotificationWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<string> UserAsync(string email, int? department, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = department, CreatedAt = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRolesAsync(user, roles);
        return user.Id;
    }

    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Candidate_follows_the_bell_to_the_survey_and_hr_sees_the_results()
    {
        var hod = await UserAsync("n.hod@grel.test", 27, Roles.HeadOfDepartment);
        var ro = await UserAsync("n.ro@grel.test", 1, Roles.RecruitmentOfficer);
        await UserAsync("n.hrm@grel.test", 1, Roles.HrManager);
        var candidate = await UserAsync("n.cand@mail.test", null, Roles.Candidate);

        int applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-GAR-2026-0096", DivisionId = 2, DepartmentId = 27, SiteId = 2, PositionTitle = "Welder", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Fleet.", BudgetCode = "GAR-26",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0096", RecruitmentRequestId = request.Id, JobTitle = "Welder", DivisionId = 2, DepartmentId = 27, SiteId = 2,
                RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
                VacancyType = VacancyType.External, JobDescription = "Welding.", MinimumEducation = EducationLevel.Certificate,
                ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90096", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod, FullName = "Abena Owusu", Email = "n.cand@mail.test", Phone = "0240000000",
                HighestEducation = EducationLevel.Certificate, Status = ApplicationStatus.NotSuccessful, SubmittedAt = DateTime.UtcNow.AddDays(-20),
                StatusChangedAt = DateTime.UtcNow.AddDays(-2)
            };
            // Real applications always start with a "submitted" step in their history (the tracker reads it).
            app.Actions.Add(new ApplicationActionEntry { Action = ApplicationAction.Submitted, ToStatus = ApplicationStatus.Submitted,
                ActorUserId = candidate, ActorName = "Abena Owusu", ActionAt = DateTime.UtcNow.AddDays(-20) });
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            applicationId = app.Id;
        }

        // The background job's survey step (run directly here; the worker is off in tests).
        using (var scope = factory.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<SurveyService>().InviteDueAsync(DateTime.UtcNow) >= 1);

        // ---- Candidate: the bell shows 1 unread; the notification leads to the survey
        var cand = await SignedInAsync("n.cand@mail.test");
        Assert.Contains("1 unread notification", await cand.GetStringAsync("/"));
        var list = await cand.GetStringAsync("/Notifications");
        Assert.Contains("How was your experience of applying to GREL?", list);
        long notificationId;
        int invitationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            notificationId = await db.Notifications.Where(n => n.UserId == candidate).Select(n => n.Id).SingleAsync();
            invitationId = await db.SurveyInvitations.Where(i => i.JobApplicationId == applicationId).Select(i => i.Id).SingleAsync();
        }
        var open = await cand.GetAsync($"/Notifications/Open/{notificationId}");
        Assert.Equal($"/Surveys/Take/{invitationId}", open.Headers.Location!.ToString());
        Assert.DoesNotContain("unread notification", await cand.GetStringAsync("/"));
        Assert.Contains("Answer the survey", await cand.GetStringAsync($"/MyApplications/Details/{applicationId}"));

        var surveyPage = $"/Surveys/Take/{invitationId}";
        Assert.Equal(HttpStatusCode.Redirect, (await cand.PostFormAsync(surveyPage, surveyPage, new()
        {
            ["rating_clarity"] = "4", ["rating_communication"] = "2", ["rating_interview"] = "", ["rating_time"] = "3", ["rating_overall"] = "3",
            ["text_comments"] = "Tell unsuccessful applicants sooner."
        })).StatusCode);
        Assert.Contains("already answered", await cand.GetStringAsync(surveyPage));
        Assert.Contains("/Account/AccessDenied", (await cand.GetAsync("/Surveys/Results")).Headers.Location!.ToString());

        // ---- HR Manager: the results page (too few answers yet to show them)
        var hrm = await SignedInAsync("n.hrm@grel.test");
        var results = await hrm.GetStringAsync("/Surveys/Results");
        Assert.Contains("Survey results", results);
        Assert.Contains("Results appear once at least 3", results);
        Assert.DoesNotContain("Tell unsuccessful applicants sooner", results);
    }
}
