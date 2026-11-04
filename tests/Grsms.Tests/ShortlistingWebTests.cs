using System.Net;
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
/// Shortlisting through the real pages: the Recruitment Officer recommends on the board and asks the HOD, the HOD sees
/// the task on their home page and agrees, and the Recruitment Officer shortlists. An Employee cannot open the board.
/// </summary>
public class ShortlistingWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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
    public async Task Recruiter_recommends_hod_agrees_and_recruiter_shortlists_on_the_board()
    {
        var hod = await UserAsync("s.hod@grel.test", 30, Roles.HeadOfDepartment);   // DTC
        var ro = await UserAsync("s.ro@grel.test", 1, Roles.RecruitmentOfficer);
        var candidate = await UserAsync("s.cand@mail.test", null, Roles.Candidate);
        await UserAsync("s.emp@grel.test", 1, Roles.Employee);

        int vacancyId, applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Departments.FindAsync(30))!.HeadOfDepartmentUserId = hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-DTC-2026-0096", DivisionId = 2, DepartmentId = 30, SiteId = 1, PositionTitle = "Trainer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Training.", BudgetCode = "DTC-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0096", RecruitmentRequestId = request.Id, JobTitle = "Field Trainer", DivisionId = 2, DepartmentId = 30,
                SiteId = 1, RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = VacancyType.External, JobDescription = "Trains outgrowers.", MinimumEducation = EducationLevel.Diploma,
                ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow
            };
            vacancy.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Adult training", IsEssential = true });
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90096", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 30, RequestingHodUserId = hod, FullName = "Yaa Asantewaa", Email = "s.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Bachelors, YearsExperience = 4, Skills = "Adult training",
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            app.ApplyScreening(CvScreening.Score(vacancy, app), DateTime.UtcNow);
            app.RecordStep(ApplicationAction.Submitted, ApplicationStatus.Submitted, candidate, "Yaa Asantewaa", null, DateTime.UtcNow);
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            (vacancyId, applicationId) = (vacancy.Id, app.Id);
        }
        var board = $"/Shortlisting/Vacancy/{vacancyId}";

        // ---- Recruitment Officer: the ranked board shows the score; recommend and ask the HOD
        var recruiter = await SignedInAsync("s.ro@grel.test");
        var page = await recruiter.GetStringAsync(board);
        Assert.Contains("Yaa Asantewaa", page);
        Assert.Contains("<strong>100</strong>", page);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(board, $"/Shortlisting/Recommend/{applicationId}", new()
        {
            ["vacancyId"] = vacancyId.ToString(), ["recommendation"] = "Shortlist", ["note"] = "Ideal profile"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(board, "/Shortlisting/SendToHod", new() { ["vacancyId"] = vacancyId.ToString() })).StatusCode);

        // ---- HOD: the task is on their home page; they agree
        var hodClient = await SignedInAsync("s.hod@grel.test");
        Assert.Contains("Give your view on 1 shortlist recommendation", await hodClient.GetStringAsync("/"));
        Assert.Contains("Your view as Head of Department", await hodClient.GetStringAsync(board));
        Assert.Equal(HttpStatusCode.Redirect, (await hodClient.PostFormAsync(board, $"/Shortlisting/HodReview/{applicationId}", new()
        {
            ["vacancyId"] = vacancyId.ToString(), ["view"] = "Shortlist"
        })).StatusCode);

        // ---- Recruitment Officer shortlists from the application page
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync($"/Applications/Details/{applicationId}", $"/Shortlisting/Shortlist/{applicationId}", new()
        {
            ["vacancyId"] = vacancyId.ToString(), ["returnTo"] = "application"
        })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var status = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().JobApplications.SingleAsync(a => a.Id == applicationId)).Status;
            Assert.Equal(ApplicationStatus.Shortlisted, status);
        }

        // ---- An Employee cannot open the board
        var employee = await SignedInAsync("s.emp@grel.test");
        Assert.Contains("/Account/AccessDenied", (await employee.GetAsync(board)).Headers.Location!.ToString());
    }
}
