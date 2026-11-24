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
    public async Task A_recruitment_officer_sees_an_overdue_review_and_an_approved_request_to_turn_into_a_vacancy_as_high_priority()
    {
        var hod = await UserAsync("ac.hod@grel.test", 32, Roles.HeadOfDepartment);
        await UserAsync("ac.ro@grel.test", 1, Roles.RecruitmentOfficer);
        await UserAsync("ac.cand@mail.test", null, Roles.Candidate);
        int requestId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r = new RecruitmentRequest
            {
                RequestNumber = "RR-AC-2026-0001", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Field Supervisor", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "AC",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.RecruitmentReview,
                StatusChangedAt = DateTime.UtcNow.AddDays(-10), DateRequested = DateTime.UtcNow.AddDays(-12)
            };
            db.RecruitmentRequests.Add(r);
            // 17 Nov 2026: approved by the MD, so the Recruitment Officer creates the vacancy: always high priority.
            db.RecruitmentRequests.Add(new RecruitmentRequest
            {
                RequestNumber = "RR-AC-2026-0002", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Weighbridge Clerk", NumberOfPositions = 1,
                EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "AC",
                EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.Approved,
                StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow, DateRequested = DateTime.UtcNow.AddDays(-3)
            });
            await db.SaveChangesAsync();
            requestId = r.Id;
        }

        var hrm = await SignedInAsync("ac.ro@grel.test");
        Assert.Contains("/ActionCenter", await hrm.GetStringAsync("/"));
        var page = await hrm.GetStringAsync("/ActionCenter");
        Assert.Contains("Field Supervisor", page);
        Assert.Contains("data-priority=\"High\"", page);
        Assert.Contains("Overdue: target 2 working days", page);
        Assert.Contains("data-timing=\"Overdue\"", page); // G2: in the red section
        Assert.Contains("Completed in the last 14 days", page);
        Assert.Contains("Field Supervisor", await hrm.GetStringAsync("/ActionCenter?priority=High"));
        Assert.DoesNotContain("Field Supervisor", await hrm.GetStringAsync("/ActionCenter?priority=Low"));
        var high = await hrm.GetStringAsync("/ActionCenter?priority=High");
        Assert.Contains("Weighbridge Clerk", high);
        Assert.Contains("Approved by the Managing Director: create the vacancy", high);

        var request = await hrm.GetStringAsync($"/RecruitmentRequests/Details/{requestId}");
        Assert.Contains("process-timeline", request);
        Assert.Contains("Waiting for the Recruitment Officer to verify the request", request);
        Assert.Contains("RequestVerified", request); // the three checks on the decision form
        Assert.Contains("Overdue", request);

        var candidate = await SignedInAsync("ac.cand@mail.test");
        Assert.Contains("Action Center", await candidate.GetStringAsync("/ActionCenter"));
    }
}
