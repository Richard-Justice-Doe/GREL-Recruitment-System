using System.Text.RegularExpressions;
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
/// docs/32 (11 November 2026): the menu numbers are kept per user for 30 seconds (other people's changes show within that
/// time), any form the user posts clears them at once, and the bell is always live.
/// </summary>
public class NavCountsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private static int OpenVacancies(string html) =>
        int.Parse(Regex.Match(html, @"title=""Open vacancies"">(\d+)<").Groups[1].Value is { Length: > 0 } n ? n : "0");

    [Fact]
    public async Task Menu_numbers_are_kept_briefly_and_refreshed_by_the_users_own_post()
    {
        string hod;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var hrm = new ApplicationUser { UserName = "nc.hrm@grel.test", Email = "nc.hrm@grel.test", EmailConfirmed = true, FullName = "HR", DepartmentId = 1, CreatedAt = DateTime.UtcNow };
            await users.CreateAsync(hrm, "Test#Pass2026");
            await users.AddToRoleAsync(hrm, Roles.HrManager);
            var h = new ApplicationUser { UserName = "nc.hod@grel.test", Email = "nc.hod@grel.test", EmailConfirmed = true, FullName = "HOD", DepartmentId = 31, CreatedAt = DateTime.UtcNow };
            await users.CreateAsync(h, "Test#Pass2026");
            await users.AddToRoleAsync(h, Roles.HeadOfDepartment);
            hod = h.Id;
        }
        var client = factory.NewClient();
        await client.SignInAsync("nc.hrm@grel.test", "Test#Pass2026");
        var before = OpenVacancies(await client.GetStringAsync("/Vacancies"));

        // Someone else publishes a vacancy.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r = new RecruitmentRequest
            {
                RequestNumber = "RR-NC-1", DivisionId = 2, DepartmentId = 31, SiteId = 2, PositionTitle = "Fitter", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(r);
            await db.SaveChangesAsync();
            db.Vacancies.Add(new Vacancy
            {
                VacancyNumber = "VAC-NC-1", RecruitmentRequestId = r.Id, JobTitle = "Fitter", DivisionId = 2, DepartmentId = 31, SiteId = 2,
                RequestingHodUserId = hod, RecruiterUserId = hod, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1, VacancyType = VacancyType.External,
                JobDescription = "Fits.", MinimumEducation = EducationLevel.Certificate, ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(9),
                Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // The page itself is live; the menu number is the kept one.
        var page = await client.GetStringAsync("/Vacancies");
        Assert.Contains("VAC-NC-1", page);
        Assert.Equal(before, OpenVacancies(page));

        // Any form the user posts clears their numbers.
        await client.PostFormAsync("/Notifications", "/Notifications/MarkAllRead", new());
        Assert.Equal(before + 1, OpenVacancies(await client.GetStringAsync("/Vacancies")));
    }
}
