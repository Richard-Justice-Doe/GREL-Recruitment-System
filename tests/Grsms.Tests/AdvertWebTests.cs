using System.Net;
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
/// The advert journey through the real web pages: a Recruitment Officer opens a published vacancy, creates an advert,
/// sends it for review; the HR Manager reviews it and the IT Manager approves and publishes it and records the LinkedIn
/// posting (11 Nov 2026, docs/29 change 2); the advert then appears on the public
/// careers page for anyone, without signing in.
/// </summary>
public class AdvertWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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
    public async Task Recruiter_advertises_a_published_vacancy_and_the_public_sees_it_on_the_careers_page()
    {
        var hod = await UserAsync("a.hod@grel.test", Roles.HeadOfDepartment);
        var ro = await UserAsync("a.ro@grel.test", Roles.RecruitmentOfficer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // A published vacancy (its own workflow is covered by the vacancy tests).
        int vacancyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new RecruitmentRequest
            {
                RequestNumber = "RR-TBU-2026-0098", DivisionId = 2, DepartmentId = 32, SiteId = 5, PositionTitle = "Process Engineer",
                NumberOfPositions = 1, EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition,
                Justification = "New processing line.", BudgetCode = "TBU-26", EstimatedAnnualCost = 120000,
                RequestedByUserId = hod, AssignedRecruiterUserId = ro, Status = RecruitmentRequestStatus.Approved,
                StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow
            };
            db.RecruitmentRequests.Add(request);
            await db.SaveChangesAsync();
            var vacancy = new Vacancy
            {
                VacancyNumber = "VAC-2026-0098", RecruitmentRequestId = request.Id, JobTitle = "Process Engineer",
                DivisionId = 2, DepartmentId = 32, SiteId = 5, RequestingHodUserId = hod, RecruiterUserId = ro,
                EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1, VacancyType = VacancyType.InternalAndExternal,
                JobDescription = "Runs and improves the rubber processing line at the TBU factory, Tsibu.",
                MinimumEducation = EducationLevel.Bachelors, MinYearsExperience = 2, ClosingDate = today.AddDays(10),
                Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
            };
            db.Vacancies.Add(vacancy);
            await db.SaveChangesAsync();
            vacancyId = vacancy.Id;
        }

        // ---- Recruitment Officer: "Create advert" on the vacancy page, pre-filled form
        var recruiter = await SignedInAsync("a.ro@grel.test");
        Assert.Contains($"/Adverts/Create?vacancyId={vacancyId}", await recruiter.GetStringAsync($"/Vacancies/Details/{vacancyId}"));
        var form = await recruiter.GetStringAsync($"/Adverts/Create?vacancyId={vacancyId}");
        Assert.Contains("rubber processing line", form);

        var created = await recruiter.PostFormAsync($"/Adverts/Create?vacancyId={vacancyId}", "/Adverts/Create", new()
        {
            ["VacancyId"] = vacancyId.ToString(),
            ["Title"] = "Process Engineer – Factory TBU",
            ["Body"] = "Runs and improves the rubber processing line at the TBU factory, Tsibu. Degree in engineering required.",
            ["HowToApply"] = "Apply online through the GREL careers page.",
            ["StartDate"] = today.ToString("yyyy-MM-dd"),
            ["EndDate"] = today.AddDays(10).ToString("yyyy-MM-dd"),
            ["Channels[0]"] = "CompanyWebsite",
            ["Channels[1]"] = "LinkedIn"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.ToString();
        var id = int.Parse(Regex.Match(detailsUrl, @"/(\d+)$").Groups[1].Value);

        // ---- 11 Nov 2026 (docs/29 change 2): the recruiter cannot publish; they send it for review
        Assert.Contains("Send for review", await recruiter.GetStringAsync(detailsUrl));
        Assert.Contains("/Account/AccessDenied", (await recruiter.PostFormAsync(detailsUrl, $"/Adverts/Publish/{id}", new())).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await recruiter.PostFormAsync(detailsUrl, $"/Adverts/Submit/{id}", new())).StatusCode);
        Assert.Contains("Awaiting HR review", await recruiter.GetStringAsync(detailsUrl));

        // ---- HR Manager: review and send to IT
        await UserAsync("a.hrm@grel.test", Roles.HrManager);
        var hr = await SignedInAsync("a.hrm@grel.test");
        Assert.Contains("Approve and send to IT", await hr.GetStringAsync(detailsUrl));
        Assert.Contains("/Account/AccessDenied", (await hr.PostFormAsync(detailsUrl, $"/Adverts/Publish/{id}", new())).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync(detailsUrl, $"/Adverts/HrApprove/{id}", new())).StatusCode);

        // ---- IT Manager: the to-do and the cards on the home page, then approve and publish (start date today → live)
        await UserAsync("a.itm@grel.test", Roles.ItManager);
        var it = await SignedInAsync("a.itm@grel.test");
        var home = await it.GetStringAsync("/");
        Assert.Contains("Awaiting your approval", home);
        Assert.Contains($"/Adverts/Details/{id}", home);
        Assert.Contains("Approve and publish now", await it.GetStringAsync(detailsUrl));
        Assert.Equal(HttpStatusCode.Redirect, (await it.PostFormAsync(detailsUrl, $"/Adverts/Publish/{id}", new())).StatusCode);
        var page = await it.GetStringAsync(detailsUrl);
        Assert.Contains("Published since", page);
        Assert.Contains("Copy-ready text for LinkedIn", page);
        var number = Regex.Match(page, @"ADV-\d{4}-\d{4}").Value;
        Assert.Contains($"/Adverts/Details/{id}", await recruiter.GetStringAsync("/Adverts?tab=live"));

        // ---- The IT Manager records the LinkedIn posting (the recruiter may not)
        var channelId = Regex.Match(page, @"name=""channelId"" value=""(\d+)""").Groups[1].Value;
        var posting = new Dictionary<string, string> { ["channelId"] = channelId, ["reference"] = "https://www.linkedin.com/jobs/view/123" };
        Assert.Contains("/Account/AccessDenied", (await recruiter.PostFormAsync(detailsUrl, $"/Adverts/MarkPosted/{id}", posting)).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await it.PostFormAsync(detailsUrl, $"/Adverts/MarkPosted/{id}", posting)).StatusCode);
        Assert.Contains("https://www.linkedin.com/jobs/view/123", await recruiter.GetStringAsync(detailsUrl));

        // ---- Anyone, without signing in, sees it on the careers page
        var visitor = factory.NewClient();
        var careers = await visitor.GetAsync("/Careers");
        Assert.Equal(HttpStatusCode.OK, careers.StatusCode);
        var careersHtml = await careers.Content.ReadAsStringAsync();
        Assert.Contains("Process Engineer", careersHtml);
        var job = await visitor.GetStringAsync($"/Careers/Details/{number}");
        Assert.Contains("rubber processing line", job);
        Assert.Contains("Tsibu", job); // the site
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/Careers/Details/ADV-0000-0000")).StatusCode);
    }

    [Fact]
    public async Task Employees_cannot_open_advert_management_but_can_see_the_careers_page()
    {
        await UserAsync("a.employee@grel.test", Roles.Employee);
        var client = await SignedInAsync("a.employee@grel.test");
        Assert.Contains("/Account/AccessDenied", (await client.GetAsync("/Adverts")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Careers")).StatusCode);
    }
}
