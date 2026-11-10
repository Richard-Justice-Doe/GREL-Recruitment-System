using System.Net;
using System.Text.RegularExpressions;
using Grsms.Domain.Applications;
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
/// Interviews through the real pages: the Recruitment Officer schedules from the application page (panel with the HOD),
/// the applicant sees the invitation on their tracker, a panel member finds the scorecard in their tasks and submits it,
/// and the HOD reviews the results. Candidates cannot open the interview screens.
/// </summary>
public class InterviewWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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
    public async Task Recruiter_schedules_panel_member_scores_and_hod_reviews_results()
    {
        var hod = await UserAsync("i.hod@grel.test", 29, Roles.HeadOfDepartment);   // RuTAP
        var ro = await UserAsync("i.ro@grel.test", 1, Roles.RecruitmentOfficer);
        var agronomist = await UserAsync("i.agro@grel.test", 29, Roles.Employee);
        var candidate = await UserAsync("i.cand@mail.test", null, Roles.Candidate);

        int applicationId, vacancyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Departments.FindAsync(29))!.HeadOfDepartmentUserId = hod;
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-RUTAP-2026-0095", DivisionId = 2, DepartmentId = 29, SiteId = 4, PositionTitle = "Extension Officer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Outgrowers.", BudgetCode = "RT-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0095", RecruitmentRequestId = request.Id, JobTitle = "Extension Officer", DivisionId = 2, DepartmentId = 29,
                SiteId = 4, RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = VacancyType.External, JobDescription = "Advises outgrowers.", MinimumEducation = EducationLevel.Diploma,
                ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), Status = VacancyStatus.Closed, StatusChangedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90095", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 29, RequestingHodUserId = hod, FullName = "Esi Mensah", Email = "i.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Hnd, Status = ApplicationStatus.Shortlisted,
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            (applicationId, vacancyId) = (app.Id, vacancy.Id);
        }

        // ---- Recruitment Officer: schedule from the application page (session already under way, so it can be scored)
        var recruiter = await SignedInAsync("i.ro@grel.test");
        Assert.Contains($"/Interviews/Schedule?applicationId={applicationId}", await recruiter.GetStringAsync($"/Applications/Details/{applicationId}"));
        var scheduled = await recruiter.PostFormAsync($"/Interviews/Schedule?applicationId={applicationId}", "/Interviews/Schedule", new()
        {
            ["ApplicationId"] = applicationId.ToString(), ["Type"] = "0", ["Mode"] = "0",
            ["StartsAt"] = DateTime.UtcNow.AddMinutes(-20).ToString("yyyy-MM-ddTHH:mm"), ["DurationMinutes"] = "45",
            ["Location"] = "RuTAP office, Agona Nkwanta", ["PanelUserIds[0]"] = hod, ["PanelUserIds[1]"] = agronomist,
            ["ChairUserId"] = hod, ["Criteria"] = "Agronomy knowledge | 60\nCommunication with farmers | 40"
        });
        Assert.Equal(HttpStatusCode.Redirect, scheduled.StatusCode);
        var interviewUrl = scheduled.Headers.Location!.ToString();
        var interviewId = Regex.Match(interviewUrl, @"/(\d+)$").Groups[1].Value;

        // ---- The applicant sees the invitation on their tracker
        var applicant = await SignedInAsync("i.cand@mail.test");
        var tracker = await applicant.GetStringAsync($"/MyApplications/Details/{applicationId}");
        Assert.Contains("RuTAP office, Agona Nkwanta", tracker);
        Assert.Contains("/Account/AccessDenied", (await applicant.GetAsync("/Interviews")).Headers.Location!.ToString());

        // ---- The panel member: the scorecard is in their tasks; submit it
        var panel = await SignedInAsync("i.agro@grel.test");
        Assert.Contains("Submit your scorecard", await panel.GetStringAsync("/"));
        var page = await panel.GetStringAsync(interviewUrl);
        var criteria = Regex.Matches(page, @"name=""ratings\[(\d+)\]""").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, criteria.Count);
        Assert.Equal(HttpStatusCode.Redirect, (await panel.PostFormAsync(interviewUrl, $"/Interviews/Scorecard/{interviewId}", new()
        {
            [$"ratings[{criteria[0]}]"] = "4", [$"ratings[{criteria[1]}]"] = "5",
            ["recommendation"] = "StronglyRecommend", ["strengths"] = "Knows the farmers' realities"
        })).StatusCode);
        Assert.Contains("88.0/100", await panel.GetStringAsync(interviewUrl)); // 4/5×60 + 5/5×40

        // ---- The HOD scores too; the results show no score until the Recruitment Officer validates (9 Nov 2026)
        var hodClient = await SignedInAsync("i.hod@grel.test");
        Assert.DoesNotContain("88.0", await hodClient.GetStringAsync($"/Interviews/Results/{vacancyId}"));
        Assert.Equal(HttpStatusCode.Redirect, (await hodClient.PostFormAsync(interviewUrl, $"/Interviews/Scorecard/{interviewId}", new()
        {
            [$"ratings[{criteria[0]}]"] = "4", [$"ratings[{criteria[1]}]"] = "5",
            ["recommendation"] = "Recommend", ["strengths"] = "Good field experience"
        })).StatusCode);

        // ---- The vacancy's Recruitment Officer (not on the panel) is told, checks and validates the scores
        var validatorPage = await recruiter.GetStringAsync(interviewUrl);
        Assert.Contains("Check and validate the scores", validatorPage);
        var validated = await recruiter.PostFormAsync(interviewUrl, $"/Interviews/Validate/{interviewId}", new()
        {
            ["note"] = "Checked."
        });
        Assert.Equal(HttpStatusCode.Redirect, validated.StatusCode);

        // ---- Validation leads straight to hiring: the hiring case, with the scores and "write the hiring recommendation"
        // The Recruitment Officer lands on "Ready to recommend" for the vacancy and chooses who to proceed with.
        Assert.Equal($"/Hiring?tab=ready&vacancyId={vacancyId}", validated.Headers.Location!.ToString());
        var readyPage = await recruiter.GetStringAsync($"/Hiring?tab=ready&vacancyId={vacancyId}");
        Assert.Contains("Esi Mensah", readyPage);
        Assert.Contains("Proceed with the selected candidates", readyPage);
        var hiringCase = await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}");
        Assert.Contains("Proceed with this candidate", hiringCase);
        Assert.Contains("88.0", hiringCase);

        // Proceeding starts the hiring process: a draft recommendation to review and submit, and the background checks.
        var proceeded = await recruiter.PostFormAsync($"/Hiring?tab=ready&vacancyId={vacancyId}", "/Hiring/Proceed", new()
        {
            ["applicationIds"] = applicationId.ToString(), ["vacancyId"] = vacancyId.ToString()
        });
        Assert.Equal(HttpStatusCode.Redirect, proceeded.StatusCode);
        var draftCase = await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}");
        Assert.Contains("Referees named by the candidate", draftCase);
        Assert.Contains("Approve hire", draftCase);

        // ---- The Recruitment Officer approves the hire: no HOD, HR Manager or MD step (9 Nov 2026)
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync($"/Hiring/Case/{applicationId}", $"/Hiring/Recommend/{applicationId}", new()
        {
            ["salary"] = "6500", ["startDate"] = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
            ["justification"] = "Interview results 88.0/100; references from two outgrower schemes confirm field experience."
        })).StatusCode);
        Assert.Contains("Approved by", await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}"));

        // ---- Background checks cleared (as the recruiter would record them), the offer goes out and the candidate accepts
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var check in db.BackgroundChecks.Where(c => c.JobApplicationId == applicationId)) check.Status = Grsms.Domain.Hiring.CheckStatus.Cleared;
            await db.SaveChangesAsync();
        }
        Assert.Contains("Send the offer", await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}"));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync($"/Hiring/Case/{applicationId}", $"/Hiring/SendOffer/{applicationId}", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await applicant.PostFormAsync($"/MyApplications/Offer/{applicationId}", $"/MyApplications/RespondOffer/{applicationId}", new()
        {
            ["accept"] = "true", ["confirm"] = "true"
        })).StatusCode);

        // ---- The hiring case now offers the click to proceed to onboarding
        var acceptedCase = await recruiter.GetStringAsync($"/Hiring/Case/{applicationId}");
        Assert.Contains("Click to proceed to onboarding", acceptedCase);
        Assert.Contains($"/Onboarding/Start/{applicationId}", acceptedCase);

        // ---- The HOD reviews the results
        var results = await hodClient.GetStringAsync($"/Interviews/Results/{vacancyId}");
        Assert.Contains("Esi Mensah", results);
        Assert.Contains("88.0", results);
    }
}
