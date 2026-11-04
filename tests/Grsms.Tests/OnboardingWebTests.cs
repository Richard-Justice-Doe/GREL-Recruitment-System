using System.Net;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
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
/// Onboarding through the real pages: the recruiter starts it from the hiring case, the HR Manager issues the appointment
/// letter, the Head of IT ticks their task from their home page, and the new employee reads and signs the letter and sees
/// their first-day information.
/// </summary>
public class OnboardingWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<string> UserAsync(string email, string fullName, int? department, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, DepartmentId = department, CreatedAt = DateTime.UtcNow };
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
    public async Task Recruiter_starts_hr_manager_issues_it_ticks_and_the_new_employee_signs()
    {
        var hod = await UserAsync("o.hod@grel.test", "Garage Head", 27, Roles.HeadOfDepartment);          // Garage
        var itHod = await UserAsync("o.ithod@grel.test", "Head of IT", 2, Roles.HeadOfDepartment);        // IT
        var ro = await UserAsync("o.ro@grel.test", "Recruiter", 1, Roles.RecruitmentOfficer);
        await UserAsync("o.hrm@grel.test", "HR Manager", 1, Roles.HrManager);
        var candidate = await UserAsync("o.cand@mail.test", "Efua Asante", null, Roles.Candidate);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

        int applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Departments.FindAsync(2))!.HeadOfDepartmentUserId = itHod;
            (await db.Departments.FindAsync(27))!.HeadOfDepartmentUserId = hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-GAR-2026-0095", DivisionId = 2, DepartmentId = 27, SiteId = 2, PositionTitle = "Diesel Mechanic",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Fleet.", BudgetCode = "GAR-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0095", RecruitmentRequestId = request.Id, JobTitle = "Diesel Mechanic", DivisionId = 2, DepartmentId = 27,
                SiteId = 2, RequestingHodUserId = hod, RecruiterUserId = ro, ReportingManagerUserId = hod, EmploymentType = EmploymentType.Permanent,
                NumberOfPositions = 1, VacancyType = VacancyType.External, JobDescription = "Engines.", MinimumEducation = EducationLevel.Certificate,
                ClosingDate = start.AddDays(-40), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90095", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod, FullName = "Efua Asante", Email = "o.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Certificate, Status = ApplicationStatus.Hired,
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            var rec = new HiringRecommendation
            {
                Number = "HIR-2026-0095", JobApplicationId = app.Id, VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod,
                ProposedSalary = 3900, ProposedStartDate = start, Justification = "Best practical test.", CreatedByUserId = ro, Status = HiringStatus.Approved
            };
            db.HiringRecommendations.Add(rec);
            await db.SaveChangesAsync();
            db.Offers.Add(new Offer
            {
                Number = "OFF-2026-0095", JobApplicationId = app.Id, HiringRecommendationId = rec.Id, VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27,
                RequestingHodUserId = hod, JobTitle = "Diesel Mechanic", MonthlySalary = 3900, StartDate = start, ExpiresOn = start.AddDays(-7),
                Status = OfferStatus.Accepted, SentAt = DateTime.UtcNow, SentByUserId = ro, RespondedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            applicationId = app.Id;
        }

        // ---- Recruiter: the accepted offer is in their tasks and on the hiring case; start onboarding
        var recruiter = await SignedInAsync("o.ro@grel.test");
        Assert.Contains("Onboard Efua Asante", await recruiter.GetStringAsync("/"));
        Assert.Contains("Start onboarding", await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}"));
        var startPage = $"/Onboarding/Start/{applicationId}";
        var response = await recruiter.PostFormAsync(startPage, $"/Onboarding/Start/{applicationId}", new()
        {
            ["ApplicationId"] = applicationId.ToString(), ["StartDate"] = start.ToString("yyyy-MM-dd"), ["ReportTime"] = "07:30",
            ["ReportLocation"] = "Garage workshop, Abura", ["ContactPersonUserId"] = hod, ["FirstDayNotes"] = "Wear safety boots."
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var details = response.Headers.Location!.ToString();
        Assert.Contains("Open the onboarding", await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}"));

        // ---- HR Manager: issue the appointment letter
        var hrm = await SignedInAsync("o.hrm@grel.test");
        var id = details.Split('/').Last();
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync(details, $"/Onboarding/IssueLetter/{id}",
            new() { ["terms"] = "Probation: 6 months. Hours: 7:30 to 16:30, Monday to Friday." })).StatusCode);

        // ---- Head of IT: the task is on their home page; tick it on the onboarding page
        var it = await SignedInAsync("o.ithod@grel.test");
        Assert.Contains("Create the email account and system access: Efua Asante", await it.GetStringAsync("/"));
        int itTaskId;
        using (var scope = factory.Services.CreateScope())
            itTaskId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OnboardingTasks
                .Where(t => t.OnboardingRecordId == int.Parse(id) && t.Team == OnboardingTeam.InformationTechnology).Select(t => t.Id).FirstAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await it.PostFormAsync(details, $"/Onboarding/UpdateTask/{id}",
            new() { ["taskId"] = itTaskId.ToString(), ["status"] = "Done", ["notes"] = "efua.asante@grel.example" })).StatusCode);

        // ---- New employee: first-day information, then read and sign the letter
        var employee = await SignedInAsync("o.cand@mail.test");
        var tracker = await employee.GetStringAsync($"/MyApplications/Details/{applicationId}");
        Assert.Contains("Your first day", tracker);
        Assert.Contains("Garage workshop, Abura", tracker);
        Assert.Contains("Ghana Card", tracker);
        Assert.Contains("Read and sign my appointment letter", tracker);
        var letterPage = $"/MyApplications/Appointment/{applicationId}";
        var letter = await employee.GetStringAsync(letterPage);
        Assert.Contains("Letter of appointment: Diesel Mechanic", letter);
        Assert.Contains("3,900.00", letter);
        await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{applicationId}", new() { ["fullName"] = "Efua Asante" }); // box not ticked
        Assert.Equal(HttpStatusCode.Redirect, (await employee.PostFormAsync(letterPage, $"/MyApplications/SignLetter/{applicationId}",
            new() { ["fullName"] = "Efua Asante", ["confirm"] = "true" })).StatusCode);
        Assert.Contains("Accepted and signed electronically", await employee.GetStringAsync(letterPage));

        using (var scope = factory.Services.CreateScope())
        {
            var o = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Onboardings.SingleAsync(x => x.JobApplicationId == applicationId);
            Assert.Equal(("Efua Asante", new TimeOnly(7, 30)), (o.LetterAcknowledgedName, o.ReportTime));
        }

        // Candidates cannot open the staff onboarding screens.
        Assert.Contains("/Account/AccessDenied", (await employee.GetAsync(details)).Headers.Location!.ToString());
    }
}
