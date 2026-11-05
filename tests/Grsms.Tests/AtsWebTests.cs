using System.Net;
using Grsms.Domain.Advertisements;
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
/// Phase 15 through the real pages: the apply page asks the knockout question; the recruiter sees the held rejection on
/// the ATS dashboard and overrides it; the applicant only ever sees "Under review"; only the Super Admin opens ATS settings.
/// </summary>
public class AtsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<string> UserAsync(string email, int? department, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = department, CreatedAt = DateTime.UtcNow, IsCandidate = roles.Contains(Roles.Candidate) };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRolesAsync(user, roles);
        return user.Id;
    }

    private async Task<HttpClient> SignedInAsync(string email, string password = Password)
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Knockout_question_on_apply_page_held_rejection_on_dashboard_override_and_settings_for_super_admin_only()
    {
        var hod = await UserAsync("a.hod@grel.test", 30, Roles.HeadOfDepartment);
        var ro = await UserAsync("a.ro@grel.test", 1, Roles.RecruitmentOfficer);
        var candidate = await UserAsync("a.cand@mail.test", null, Roles.Candidate);
        await UserAsync("a.hradmin@grel.test", 1, Roles.HrAdministrator);

        int vacancyId, applicationId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-DTC-2026-0097", DivisionId = 2, DepartmentId = 30, SiteId = 1, PositionTitle = "Driver",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "Transport.", BudgetCode = "DTC-26", EstimatedAnnualCost = 1, RequestedByUserId = hod,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0097", RecruitmentRequestId = request.Id, JobTitle = "Field Driver", DivisionId = 2, DepartmentId = 30,
                SiteId = 1, RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent,
                VacancyType = VacancyType.External, JobDescription = "Drives.", MinimumEducation = EducationLevel.Secondary,
                ClosingDate = today.AddDays(5), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow
            };
            vacancy.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Defensive driving", IsEssential = true });
            vacancy.KnockoutQuestions.Add(new VacancyKnockoutQuestion { Text = "Do you have a valid driver's licence?" });
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            var advert = new JobAdvert
            {
                AdvertNumber = "ADV-2026-0097", VacancyId = vacancy.Id, DivisionId = 2, DepartmentId = 30, RequestingHodUserId = hod, Title = "Field Driver",
                Body = "Text", HowToApply = "Online", StartDate = today, EndDate = today.AddDays(5), Status = AdvertStatus.Live,
                StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow, CreatedByUserId = ro
            };
            advert.Channels.Add(new AdvertChannelPost { Channel = AdvertChannel.CompanyWebsite, Status = ChannelPostStatus.Posted, PostedAt = DateTime.UtcNow });
            db.JobAdverts.Add(advert);

            // An application the ATS has rejected (as it does on submission) for answering "No".
            var app = new JobApplication
            {
                ApplicationNumber = "APP-2026-90097", VacancyId = vacancy.Id, ApplicantUserId = candidate, Source = ApplicationSource.CareersPage,
                DivisionId = 2, DepartmentId = 30, RequestingHodUserId = hod, FullName = "Kojo Antwi", Email = "a.cand@mail.test",
                Phone = "0240000000", HighestEducation = EducationLevel.Secondary, YearsExperience = 2, Skills = "Defensive driving",
                SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
            };
            app.KnockoutAnswers.Add(new KnockoutAnswer { Question = "Do you have a valid driver's licence?", Answer = false });
            app.RecordStep(ApplicationAction.Submitted, ApplicationStatus.Submitted, candidate, "Kojo Antwi", null, DateTime.UtcNow);
            var result = AtsEngine.Score(vacancy, app, null, null, app.KnockoutInputs(), new AtsSettings());
            app.ApplyScreening(result, DateTime.UtcNow);
            app.AtsDecision = AtsDecision.AutoRejected;
            app.AtsRejectionReason = string.Join("\n", result.RejectionReasons);
            app.RecordStep(ApplicationAction.AtsRejected, ApplicationStatus.AutoRejected, null, "ATS (automatic screening)", app.AtsRejectionReason, DateTime.UtcNow);
            db.JobApplications.Add(app);
            await db.SaveChangesAsync();
            (vacancyId, applicationId) = (vacancy.Id, app.Id);
        }

        // ---- The apply page asks the knockout question with Yes / No buttons (a second candidate who has not applied yet).
        await UserAsync("a.cand2@mail.test", null, Roles.Candidate);
        var newcomer = await SignedInAsync("a.cand2@mail.test");
        var apply = await newcomer.GetStringAsync("/MyApplications/Apply/ADV-2026-0097");
        Assert.Contains("Questions about the requirements", apply);
        Assert.Contains("Do you have a valid driver&#x27;s licence?", apply);
        Assert.Matches("name=\"Answers\\[\\d+\\]\" id=\"q-\\d+-no\" value=\"false\"", apply);

        // ---- The applicant whose application the ATS rejected still sees "Under review" and nothing about the ATS.
        var applicant = await SignedInAsync("a.cand@mail.test");
        var mine = await applicant.GetStringAsync("/MyApplications");
        Assert.Contains("Under review", mine);
        Assert.DoesNotContain("Automatically rejected", mine);
        var tracker = await applicant.GetStringAsync($"/MyApplications/Details/{applicationId}");
        Assert.DoesNotContain("ATS", tracker);

        // ---- The recruiter: the sidebar badge, the dashboard, and the application page all show the held rejection.
        var recruiter = await SignedInAsync("a.ro@grel.test");
        var home = await recruiter.GetStringAsync("/Ats");
        Assert.Contains("Field Driver", home);
        Assert.Contains("ATS rejections waiting for HR", home); // sidebar badge
        var dashboard = await recruiter.GetStringAsync($"/Ats/Vacancy/{vacancyId}");
        Assert.Contains("Rejected by the ATS: awaiting HR (1)", dashboard);
        Assert.Contains("driver&#x27;s licence", dashboard);
        Assert.Contains("Missing skill analysis", dashboard);
        Assert.Contains("Defensive driving", dashboard);
        var details = await recruiter.GetStringAsync($"/Applications/Details/{applicationId}");
        Assert.Contains("ATS decision: Awaiting HR", details);
        Assert.Contains("Knockout questions", details);

        // An override without a reason is refused; with one, the application goes to review.
        await recruiter.PostFormAsync($"/Ats/Vacancy/{vacancyId}", $"/Ats/Override/{applicationId}", new() { ["vacancyId"] = vacancyId.ToString(), ["reason"] = "" });
        var overridden = await recruiter.PostFormAsync($"/Ats/Vacancy/{vacancyId}", $"/Ats/Override/{applicationId}", new()
        {
            ["vacancyId"] = vacancyId.ToString(), ["reason"] = "Licence renewal in progress; confirmed by phone", ["returnTo"] = "application"
        });
        Assert.Equal($"/Applications/Details/{applicationId}", overridden.Headers.Location!.ToString());
        Assert.Contains("Overridden by HR", await recruiter.GetStringAsync($"/Applications/Details/{applicationId}"));
        using (var scope = factory.Services.CreateScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().JobApplications.SingleAsync(x => x.Id == applicationId);
            Assert.Equal((ApplicationStatus.UnderReview, AtsDecision.Overridden, ro), (a.Status, a.AtsDecision, a.AtsDecisionByUserId));
        }

        // ---- Settings: the Super Admin only.
        Assert.Contains("/Account/AccessDenied", (await recruiter.GetAsync("/Ats/Settings")).Headers.Location!.ToString());
        var hrAdmin = await SignedInAsync("a.hradmin@grel.test");
        Assert.Contains("/Account/AccessDenied", (await hrAdmin.GetAsync("/Ats/Settings")).Headers.Location!.ToString());

        var admin = await SignedInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        var settings = await admin.GetStringAsync("/Ats/Settings");
        Assert.Contains("Automatic rejection rules", settings);
        Assert.Contains("Not available: no API key", settings);
        var saved = await admin.PostFormAsync("/Ats/Settings", "/Ats/Settings", new()
        {
            ["EducationWeight"] = "40", ["ExperienceWeight"] = "20", ["SkillsWeight"] = "20", ["CertificationWeight"] = "20",
            ["HighlyRecommendedFrom"] = "90", ["RecommendedFrom"] = "80", ["ReviewRequiredFrom"] = "70", ["LowMatchFrom"] = "50",
            ["RejectOnKnockout"] = "true", ["RejectBelowLowMatch"] = "false", ["RejectBelowMinimumEducation"] = "true",
            ["RejectBelowMinimumExperience"] = "true", ["RejectMissingRequiredCertification"] = "true", ["RejectMissingRequiredSkill"] = "false",
            ["UseAiReader"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = await db.AtsSettings.SingleAsync();
            Assert.Equal((40, false), (s.EducationWeight, s.RejectBelowLowMatch));
            Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityName == "AtsSettings" && x.UserName == GrsmsWebFactory.AdminEmail));
        }
    }
}
