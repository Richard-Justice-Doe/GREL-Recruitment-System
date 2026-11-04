using System.Net;
using System.Text.RegularExpressions;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// The vacancy journey through the real web pages: a Recruitment Officer finds an approved request under
/// "Approved requests awaiting a vacancy", creates and submits the vacancy, and an HR Manager approves it,
/// after which it is listed as open and the request page links to it.
/// </summary>
public class VacancyWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<string> UserAsync(string email, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = 1, CreatedAt = DateTime.UtcNow };
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
    public async Task Recruiter_creates_vacancy_from_approved_request_and_hr_manager_publishes_it()
    {
        var hod = await UserAsync("v.hod@grel.test", Roles.HeadOfDepartment);
        var ro = await UserAsync("v.ro@grel.test", Roles.RecruitmentOfficer);
        await UserAsync("v.hrm@grel.test", Roles.HrManager);

        int requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-IT-2026-0099", DivisionId = 1, DepartmentId = 2, SiteId = 1, PositionTitle = "Network Engineer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Network growth across all estates.", BudgetCode = "IT-26", EstimatedAnnualCost = 100000,
                RequestedByUserId = hod, AssignedRecruiterUserId = ro, Status = RecruitmentRequestStatus.Approved,
                StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
        }

        // ---- Recruitment Officer: the request is waiting for a vacancy
        var recruiter = await SignedInAsync("v.ro@grel.test");
        Assert.Contains("RR-IT-2026-0099", await recruiter.GetStringAsync("/Vacancies?tab=awaiting"));

        var created = await recruiter.PostFormAsync($"/Vacancies/Create?requestId={requestId}", "/Vacancies/Create", new()
        {
            ["RecruitmentRequestId"] = requestId.ToString(),
            ["JobTitle"] = "Network Engineer",
            ["SiteId"] = "1",
            ["VacancyType"] = "2",          // Internal and external
            ["MinimumEducation"] = "6",     // Bachelor's
            ["MinYearsExperience"] = "2",
            ["JobDescription"] = "Designs, installs and maintains the wired and wireless networks connecting GREL estates and offices.",
            ["EssentialSkills"] = "Cisco routing\nFirewall configuration",
            ["ClosingDate"] = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd")
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.ToString();
        var id = int.Parse(Regex.Match(detailsUrl, @"/(\d+)$").Groups[1].Value);

        var page = await recruiter.GetStringAsync(detailsUrl);
        Assert.Contains("Cisco routing", page);
        Assert.Contains("Draft", page);

        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(detailsUrl, $"/Vacancies/Submit/{id}", new())).StatusCode);

        // ---- HR Manager approves (publishes)
        var hrm = await SignedInAsync("v.hrm@grel.test");
        Assert.Contains($"/Vacancies/Details/{id}", await hrm.GetStringAsync("/Vacancies?tab=pending"));
        var decided = await hrm.PostFormAsync($"/Vacancies/Details/{id}", $"/Vacancies/Decide/{id}", new()
        {
            ["Decision"] = "Approve",
            ["ExpectedStatus"] = "PendingApproval"
        });
        Assert.Equal(HttpStatusCode.Redirect, decided.StatusCode);

        // ---- Now open, and linked from the request
        Assert.Contains($"/Vacancies/Details/{id}", await recruiter.GetStringAsync("/Vacancies?tab=open"));
        Assert.Contains("Open for applications", await recruiter.GetStringAsync(detailsUrl));
        Assert.Contains($"/Vacancies/Details/{id}", await recruiter.GetStringAsync($"/RecruitmentRequests/Details/{requestId}"));
    }

    [Fact]
    public async Task Employees_cannot_open_vacancy_management()
    {
        await UserAsync("v.employee@grel.test", Roles.Employee);
        var client = await SignedInAsync("v.employee@grel.test");
        Assert.Contains("/Account/AccessDenied", (await client.GetAsync("/Vacancies")).Headers.Location!.ToString());
    }
}
