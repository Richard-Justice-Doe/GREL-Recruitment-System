using System.Net;
using Grsms.Domain.Applications;
using Grsms.Domain.Hiring;
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
/// Hiring through the real pages: the Managing Director gives the final approval on the hiring case page (and sees it in
/// their tasks), the recruiter sends the offer, and the candidate reads the offer letter and accepts it.
/// </summary>
public class HiringWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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
    public async Task Md_approves_recruiter_offers_and_candidate_accepts_on_the_offer_letter()
    {
        var hod = await UserAsync("h.hod@grel.test", 27, Roles.HeadOfDepartment);   // Garage
        var ro = await UserAsync("h.ro@grel.test", 1, Roles.RecruitmentOfficer);
        var hrm = await UserAsync("h.hrm@grel.test", 1, Roles.HrManager);
        await UserAsync("h.md@grel.test", 1, Roles.ManagingDirector);
        var candidate = await UserAsync("h.cand@mail.test", null, Roles.Candidate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        int applicationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-GAR-2026-0094", DivisionId = 2, DepartmentId = 27, SiteId = 2, PositionTitle = "Auto Electrician",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Fleet.", BudgetCode = "GAR-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0094", RecruitmentRequestId = request.Id, JobTitle = "Auto Electrician", DivisionId = 2, DepartmentId = 27,
                SiteId = 2, RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
                VacancyType = VacancyType.External, JobDescription = "Vehicle electrics.", MinimumEducation = EducationLevel.Certificate,
                ClosingDate = today.AddDays(-3), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90094", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod, FullName = "Kojo Antwi", Email = "h.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Certificate, Status = ApplicationStatus.Interview,
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            db.BackgroundChecks.Add(new BackgroundCheck { JobApplicationId = app.Id, Type = CheckType.References, Subject = "Former foreman",
                Status = CheckStatus.Cleared, CompletedAt = DateTime.UtcNow, RecordedByUserId = ro });
            db.HiringRecommendations.Add(new HiringRecommendation
            {
                Number = "HIR-2026-0094", JobApplicationId = app.Id, VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 27, RequestingHodUserId = hod,
                ProposedSalary = 4200, ProposedStartDate = today.AddDays(21), Justification = "Best practical assessment.", CreatedByUserId = ro,
                Status = HiringStatus.AwaitingMd, HodDecision = HiringDecision.Approve, HodUserId = hod, HrDecision = HiringDecision.Approve, HrUserId = hrm
            });
            await db.SaveChangesAsync();
            applicationId = app.Id;
        }
        var casePage = $"/Hiring/Case/{applicationId}";

        // ---- MD: the decision is in their tasks; approve on the case page
        var md = await SignedInAsync("h.md@grel.test");
        Assert.Contains("Hire Kojo Antwi", await md.GetStringAsync("/"));
        Assert.Contains("Your final decision as Managing Director", await md.GetStringAsync(casePage));
        Assert.Equal(HttpStatusCode.Redirect, (await md.PostFormAsync(casePage, $"/Hiring/Decide/{applicationId}", new() { ["decision"] = "Approve" })).StatusCode);

        // ---- Recruiter: send the offer
        var recruiter = await SignedInAsync("h.ro@grel.test");
        Assert.Contains("Send offer", await recruiter.GetStringAsync(casePage));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(casePage, $"/Hiring/SendOffer/{applicationId}", new()
        {
            ["salary"] = "4200", ["startDate"] = today.AddDays(21).ToString("yyyy-MM-dd"), ["expiresOn"] = today.AddDays(7).ToString("yyyy-MM-dd"),
            ["terms"] = "Probation: 6 months."
        })).StatusCode);

        // ---- Candidate: read the letter, then accept (the confirmation box is required)
        var cand = await SignedInAsync("h.cand@mail.test");
        Assert.Contains("Read and reply to my offer", await cand.GetStringAsync($"/MyApplications/Details/{applicationId}"));
        var letter = await cand.GetStringAsync($"/MyApplications/Offer/{applicationId}");
        Assert.Contains("Auto Electrician", letter);
        Assert.Contains("4,200.00", letter);
        Assert.Contains("Abura", letter);
        Assert.Contains("/images/grel-logo.png", letter); // GREL letterhead (12 Nov 2026)
        Assert.Equal(HttpStatusCode.OK, (await cand.GetAsync("/images/grel-logo.png")).StatusCode);
        await cand.PostFormAsync($"/MyApplications/Offer/{applicationId}", $"/MyApplications/RespondOffer/{applicationId}", new() { ["accept"] = "true" });
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(OfferStatus.Sent, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Offers.SingleAsync(o => o.JobApplicationId == applicationId)).Status);
        Assert.Equal(HttpStatusCode.Redirect, (await cand.PostFormAsync($"/MyApplications/Offer/{applicationId}", $"/MyApplications/RespondOffer/{applicationId}",
            new() { ["accept"] = "true", ["confirm"] = "true" })).StatusCode);
        Assert.Contains("you have been hired", await cand.GetStringAsync($"/MyApplications/Details/{applicationId}"));

        // Candidates cannot open the hiring screens.
        Assert.Contains("/Account/AccessDenied", (await cand.GetAsync(casePage)).Headers.Location!.ToString());
    }
}
