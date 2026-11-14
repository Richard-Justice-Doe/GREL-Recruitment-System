using System.Net;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>docs/37 CR5 (step C3): the Action Center through the pages, for an approver, and for a candidate; and the process guide on a request.</summary>
public class ActionCenterWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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
    public async Task An_hr_manager_sees_an_overdue_approval_as_high_priority_and_the_request_page_shows_the_guide()
    {
        var hod = await UserAsync("ac.hod@grel.test", 32, Roles.HeadOfDepartment);
        await UserAsync("ac.hrm@grel.test", 1, Roles.HrManager);
        await UserAsync("ac.cand@mail.test", null, Roles.Candidate);
        int requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r = new RecruitmentRequest
            {
                RequestNumber = "RR-AC-2026-0001", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Field Supervisor", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "AC",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.HrApproval,
                StatusChangedAt = DateTime.UtcNow.AddDays(-10), DateRequested = DateTime.UtcNow.AddDays(-12)
            };
            db.RecruitmentRequests.Add(r);
            await db.SaveChangesAsync();
            requestId = r.Id;
        }

        var hrm = await SignedInAsync("ac.hrm@grel.test");
        Assert.Contains("/ActionCenter", await hrm.GetStringAsync("/"));
        var page = await hrm.GetStringAsync("/ActionCenter");
        Assert.Contains("Field Supervisor", page);
        Assert.Contains("data-priority=\"High\"", page);
        Assert.Contains("Overdue: target 2 working days", page);
        Assert.Contains("Field Supervisor", await hrm.GetStringAsync("/ActionCenter?priority=High"));
        Assert.DoesNotContain("Field Supervisor", await hrm.GetStringAsync("/ActionCenter?priority=Low"));

        var request = await hrm.GetStringAsync($"/RecruitmentRequests/Details/{requestId}");
        Assert.Contains("process-timeline", request);
        Assert.Contains("Waiting for the HR Manager&#x27;s approval.", request);
        Assert.Contains("Overdue", request);

        var candidate = await SignedInAsync("ac.cand@mail.test");
        Assert.Contains("Action Center", await candidate.GetStringAsync("/ActionCenter"));
    }
}
