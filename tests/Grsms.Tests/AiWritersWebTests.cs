using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Grsms.Application.Ai;
using Grsms.Application.Settings;
using Grsms.Domain.Advertisements;
using Grsms.Domain.Ai;
using Grsms.Domain.Recruitment;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Domain.Vacancies;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-1 through the pages: the "Draft with AI" panel appears only when the feature is switched on; the job description
/// draft and the four advert versions come back as form values (nothing saved); the advert's channel versions are saved with the
/// advert and used as the copy-ready text; the prompts carry the fair-hiring rules and send no personal data.
/// </summary>
public class AiWritersWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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

    private async Task SettingsAsync(Dictionary<string, string?> values)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(values);
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact]
    public async Task Recruiter_drafts_a_job_description_and_advert_versions_with_ai_then_saves_them_the_usual_way()
    {
        var hod = await UserAsync("ai.hod@grel.test", Roles.HeadOfDepartment);
        var ro = await UserAsync("ai.ro@grel.test", Roles.RecruitmentOfficer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int requestId, vacancyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-AI-2026-0001", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Safety Officer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.",
                BudgetCode = "AI", EstimatedAnnualCost = 1, RequestedByUserId = hod, AssignedRecruiterUserId = ro,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
            // The advert's vacancy comes from another request (a request with a vacancy can't be converted again).
            var advertised = new RecruitmentRequest
            {
                RequestNumber = "RR-AI-2026-0002", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Process Engineer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.",
                BudgetCode = "AI", EstimatedAnnualCost = 1, RequestedByUserId = hod, AssignedRecruiterUserId = ro,
                Status = RecruitmentRequestStatus.Approved, StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(advertised);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-AI-1", RecruitmentRequestId = advertised.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = 32, SiteId = 5,
                RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
                VacancyType = VacancyType.InternalAndExternal, JobDescription = "Runs the rubber processing line.", MinimumEducation = EducationLevel.Bachelors,
                MinYearsExperience = 2, ClosingDate = today.AddDays(10), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
            };
            vacancy.Requirements.Add(new VacancyRequirement { Type = RequirementType.Skill, Name = "Process control", IsEssential = true });
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            vacancyId = vacancy.Id;
        }
        var recruiter = await SignedInAsync("ai.ro@grel.test");
        var vacancyForm = $"/Vacancies/Create?requestId={requestId}";

        // Off: no panel, and the endpoint says so.
        Assert.DoesNotContain("Draft with AI", await recruiter.GetStringAsync(vacancyForm));
        var off = Json(await (await recruiter.PostFormAsync(vacancyForm, "/Ai/JobDescription", new() { ["jobTitle"] = "Safety Officer" })).Content.ReadAsStringAsync());
        Assert.False(off.GetProperty("ok").GetBoolean());
        Assert.Contains("switched off", off.GetProperty("problem").GetString());

        await SettingsAsync(new()
        {
            [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiProviderKey] = "Anthropic", [SettingCatalogue.AiApiKey] = "sk-test",
            [SettingCatalogue.AiFeatureKey(AiFeature.JobDescription)] = "true", [SettingCatalogue.AiFeatureKey(AiFeature.Advert)] = "true"
        });

        // ---- AI-1a: job description draft → form values
        Assert.Contains("Draft with AI", await recruiter.GetStringAsync(vacancyForm));
        factory.Ai.Reply = _ => new AiReply("""
            {"purpose":"Keeps people safe.","duties":["Inspect sites","Investigate incidents"],"minimum_education":"Diploma","field_of_study":"Occupational safety",
             "min_years_experience":3,"essential_skills":["Risk assessment"],"desirable_skills":["Report writing"],"essential_certifications":["NEBOSH IGC"],
             "desirable_certifications":[],"competencies":["Integrity"],"kpis":["Lost-time injury rate"],
             "skills_matrix":[{"skill":"Risk assessment","level":"Advanced","essential":true}]}
            """, 500, 400, "claude-sonnet-5-5");
        var jd = Json(await (await recruiter.PostFormAsync(vacancyForm, "/Ai/JobDescription", new()
        {
            ["jobTitle"] = "Safety Officer", ["recruitmentRequestId"] = requestId.ToString(), ["department"] = "Factory TBU", ["level"] = "Experienced",
            ["grade"] = "Grade 7", ["responsibilities"] = "Inspections\nIncident reports"
        })).Content.ReadAsStringAsync());
        Assert.True(jd.GetProperty("ok").GetBoolean());
        Assert.Equal("High", jd.GetProperty("confidence").GetString());
        var fields = jd.GetProperty("fields");
        Assert.StartsWith("Job purpose\nKeeps people safe.", fields.GetProperty("JobDescription").GetString());
        Assert.Contains("- Risk assessment: Advanced (essential)", fields.GetProperty("JobDescription").GetString());
        Assert.Equal(((int)EducationLevel.Diploma).ToString(), fields.GetProperty("MinimumEducation").GetString());
        Assert.Equal("NEBOSH IGC", fields.GetProperty("EssentialCertifications").GetString());
        // The prompt: fair-hiring rules, data in tags, no personal data (only what the recruiter typed).
        var call = factory.Ai.Calls[^1].Call;
        Assert.Contains("Never include requirements or wording about age, gender", call.Instructions);
        Assert.Contains("<details>", call.Input);
        Assert.Contains("Grade: Grade 7", call.Input);
        Assert.DoesNotContain("ai.ro", call.Input);

        // ---- AI-1b: advert versions → form values, then saved with the advert and used as copy-ready text
        var advertForm = $"/Adverts/Create?vacancyId={vacancyId}";
        Assert.Contains("Write the advert with AI", await recruiter.GetStringAsync(advertForm));
        factory.Ai.Reply = _ => new AiReply("""
            {"careers_portal":"Join GREL as a Process Engineer and keep our rubber line running.","external":"GREL seeks a Process Engineer.",
             "internal":"Internal vacancy: Process Engineer. Inform your HOD.","linked_in":"We're hiring a Process Engineer at Factory TBU!"}
            """, 800, 600, "claude-sonnet-5-5");
        var versions = Json(await (await recruiter.PostFormAsync(advertForm, "/Ai/AdvertVersions", new()
            { ["vacancyId"] = vacancyId.ToString(), ["benefits"] = "Medical cover" })).Content.ReadAsStringAsync());
        Assert.True(versions.GetProperty("ok").GetBoolean());
        Assert.Equal(4, versions.GetProperty("sections").GetArrayLength());
        Assert.Contains("Essential skills: Process control", factory.Ai.Calls[^1].Call.Input); // requirements kept as approved
        var v = versions.GetProperty("fields");

        var created = await recruiter.PostFormAsync(advertForm, "/Adverts/Create", new()
        {
            ["VacancyId"] = vacancyId.ToString(), ["Title"] = "Process Engineer – Factory TBU",
            ["Body"] = v.GetProperty("Body").GetString() + " Degree in engineering required, at the TBU factory.",
            ["HowToApply"] = "Apply online through the GREL careers page.",
            ["ExternalText"] = v.GetProperty("ExternalText").GetString()!, ["LinkedInText"] = v.GetProperty("LinkedInText").GetString()!,
            ["InternalNotice"] = v.GetProperty("InternalNotice").GetString()!,
            ["StartDate"] = today.ToString("yyyy-MM-dd"), ["EndDate"] = today.AddDays(10).ToString("yyyy-MM-dd"),
            ["Channels[0]"] = "CompanyWebsite", ["Channels[1]"] = "LinkedIn", ["Channels[2]"] = "Jobberman"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var details = await recruiter.GetStringAsync(created.Headers.Location!.ToString());
        Assert.Contains("Internal notice for staff (copy-ready)", details);
        Assert.Contains("We&#x27;re hiring a Process Engineer at Factory TBU!", details);   // LinkedIn copy text
        Assert.Contains("GREL seeks a Process Engineer.", details);                          // Jobberman copy text

        // Every AI call was logged against its record.
        using (var scope = factory.Services.CreateScope())
        {
            var logs = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AiRequestLogs.Where(l => l.Outcome == AiOutcome.Success).ToListAsync();
            Assert.Contains(logs, l => l.Feature == AiFeature.JobDescription && l.RecordType == "RecruitmentRequest" && l.RecordId == requestId);
            Assert.Contains(logs, l => l.Feature == AiFeature.Advert && l.RecordType == "Vacancy" && l.RecordId == vacancyId);
        }

        // ---- AI-2: the interview guide page: AI draft shown in the form (not saved), then saved by the recruiter.
        await SettingsAsync(new() { [SettingCatalogue.AiFeatureKey(AiFeature.InterviewQuestions)] = "true" });
        var guidePage = $"/Vacancies/Questions/{vacancyId}";
        Assert.Contains("Draft questions", await recruiter.GetStringAsync(guidePage));
        factory.Ai.Reply = _ => new AiReply("""{"questions":[{"category":"Technical","question":"How do you reduce line downtime?","guidance":"Data-led."}]}""", 200, 100, "m");
        var drafted = await (await recruiter.PostFormAsync(guidePage, $"/Vacancies/GenerateQuestions/{vacancyId}", new())).Content.ReadAsStringAsync();
        Assert.Contains("AI draft", drafted);
        Assert.Contains("How do you reduce line downtime?", drafted);
        Assert.Contains("No questions yet", await recruiter.GetStringAsync($"/Vacancies/Details/{vacancyId}"));
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(guidePage, $"/Vacancies/SaveQuestions/{vacancyId}", new()
        {
            ["Questions.Index"] = "a", ["Questions[a].Category"] = "Technical", ["Questions[a].Question"] = "How do you reduce line downtime?",
            ["Questions[a].Guidance"] = "Data-led."
        })).StatusCode);
        Assert.Contains("1 question for the panel", await recruiter.GetStringAsync($"/Vacancies/Details/{vacancyId}"));

        // Someone without the vacancy permission can't call the endpoints.
        await UserAsync("ai.emp@grel.test", Roles.Employee);
        var employee = await SignedInAsync("ai.emp@grel.test");
        var denied = await employee.PostFormAsync("/", "/Ai/JobDescription", new() { ["jobTitle"] = "X" });
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
    }
}
