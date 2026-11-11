using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/32 (11 November 2026): the home page's action cards, each with its count and the list that shows it. They replace
/// the KPI row with 7-day charts.
/// </summary>
public class DashboardCardsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private async Task<string> UserAsync(string email, int department, string role)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = department, CreatedAt = DateTime.UtcNow };
        await users.CreateAsync(u, "Test#Pass2026");
        await users.AddToRoleAsync(u, role);
        return u.Id;
    }

    [Fact]
    public async Task The_hr_manager_sees_action_cards_that_open_the_right_lists()
    {
        await UserAsync("dc.hrm@grel.test", 1, Roles.HrManager);
        var hod = await UserAsync("dc.hod@grel.test", 31, Roles.HeadOfDepartment);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r = new RecruitmentRequest
            {
                RequestNumber = "RR-DC-1", DivisionId = 2, DepartmentId = 31, SiteId = 2, PositionTitle = "Tapper", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(r);
            await db.SaveChangesAsync();
            db.Vacancies.Add(new Vacancy
            {
                VacancyNumber = "VAC-DC-1", RecruitmentRequestId = r.Id, JobTitle = "Tapper", DivisionId = 2, DepartmentId = 31, SiteId = 2,
                RequestingHodUserId = hod, RecruiterUserId = hod, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
                VacancyType = VacancyType.External, JobDescription = "Taps.", MinimumEducation = EducationLevel.Certificate,
                ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(9), Status = VacancyStatus.PendingApproval, StatusChangedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.NewClient();
        await client.SignInAsync("dc.hrm@grel.test", "Test#Pass2026");
        var home = await client.GetStringAsync("/");

        foreach (var title in new[] { "Requests awaiting your approval", "Vacancies awaiting publication", "Applications received", "Interviews this week", "Offers awaiting the candidate" })
            Assert.Contains(title, home);
        Assert.Contains("href=\"/Vacancies?tab=pending\"", home);
        Assert.Contains("href=\"/RecruitmentRequests?tab=awaiting\"", home);
        Assert.Contains("href=\"/Hiring?tab=offers\"", home);
        Assert.Contains("/Interviews?tab=upcoming&amp;From=", home);
        Assert.Contains("Action needed", home);              // the vacancy waiting for approval
        Assert.DoesNotContain("kpi-bars", home);             // the old 7-day charts are gone
        Assert.DoesNotContain("Registered candidates", home);
    }

    [Fact]
    public async Task An_employee_without_recruitment_access_gets_no_empty_cards()
    {
        await UserAsync("dc.emp@grel.test", 3, Roles.Employee);
        var client = factory.NewClient();
        await client.SignInAsync("dc.emp@grel.test", "Test#Pass2026");
        var home = await client.GetStringAsync("/");
        Assert.DoesNotContain("Vacancies awaiting publication", home);
        Assert.DoesNotContain("Offers awaiting the candidate", home);
        Assert.DoesNotContain("Requests awaiting your approval", home);
    }
}
