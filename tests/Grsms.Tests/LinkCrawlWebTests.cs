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
using Xunit.Abstractions;

namespace Grsms.Tests;

/// <summary>
/// Presentation readiness (docs/33, 11 November 2026): signs in as each role, starts at the home page and follows every link
/// on every page it reaches (same site, GET only). A page must never fail (500) or be missing (404), and nobody should be
/// shown a link to a page they may not open. This is what a presenter clicking around would hit.
/// </summary>
public class LinkCrawlWebTests(GrsmsWebFactory factory, ITestOutputHelper output) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";
    private static readonly Regex Href = new("href=\"(/[^\"#]*)\"", RegexOptions.Compiled);

    // Links that are not pages: sign-out, files, an endless month-by-month calendar, the public site's own pages.
    private static readonly string[] Skip = ["/Account/Logout", "/Download", "/Export", "ImportTemplate", "month=", "/lib/", "/css/", "/js/", "/img/", "/favicon",
        "/health", "/Account/ExternalLogin", "/Backups/Get"];

    private async Task<string> UserAsync(string email, int? department, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is { } existing) return existing.Id;
        var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = department,
            IsCandidate = roles.Contains(Roles.Candidate), CreatedAt = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(u, Password)).Succeeded);
        await users.AddToRolesAsync(u, roles);
        return u.Id;
    }

    private async Task SeedAsync()
    {
        var hod = await UserAsync("lc.hod@grel.test", 31, Roles.HeadOfDepartment);
        var ro = await UserAsync("lc.ro@grel.test", 1, Roles.RecruitmentOfficer);
        var cand = await UserAsync("lc.cand@mail.test", null, Roles.Candidate);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.RecruitmentRequests.Any(r => r.RequestNumber == "RR-LC-1")) return;
        var awaiting = new RecruitmentRequest
        {
            RequestNumber = "RR-LC-1", DivisionId = 2, DepartmentId = 31, SiteId = 2, PositionTitle = "Field Officer", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
            EstimatedAnnualCost = 1, RequestedByUserId = hod, Status = RecruitmentRequestStatus.HrApproval, StatusChangedAt = DateTime.UtcNow,
            DateRequested = DateTime.UtcNow
        };
        var approved = new RecruitmentRequest
        {
            RequestNumber = "RR-LC-2", DivisionId = 2, DepartmentId = 31, SiteId = 2, PositionTitle = "Process Engineer", NumberOfPositions = 1,
            EmploymentType = EmploymentType.Permanent, VacancyReason = VacancyReason.NewPosition, Justification = "Needed.", BudgetCode = "X",
            EstimatedAnnualCost = 1, RequestedByUserId = hod, AssignedRecruiterUserId = ro, Status = RecruitmentRequestStatus.Approved,
            StatusChangedAt = DateTime.UtcNow, ApprovedAt = DateTime.UtcNow, DateRequested = DateTime.UtcNow
        };
        db.RecruitmentRequests.AddRange(awaiting, approved);
        await db.SaveChangesAsync();
        var v = new Vacancy
        {
            VacancyNumber = "VAC-LC-1", RecruitmentRequestId = approved.Id, JobTitle = "Process Engineer", DivisionId = 2, DepartmentId = 31, SiteId = 2,
            RequestingHodUserId = hod, RecruiterUserId = ro, EmploymentType = EmploymentType.Permanent, NumberOfPositions = 1,
            VacancyType = VacancyType.InternalAndExternal, JobDescription = "Runs the line.", MinimumEducation = EducationLevel.Bachelors,
            ClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), Status = VacancyStatus.Published, StatusChangedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };
        db.Vacancies.Add(v);
        await db.SaveChangesAsync();
        var app = new JobApplication
        {
            ApplicationNumber = "APP-LC-1", VacancyId = v.Id, ApplicantUserId = cand, Source = ApplicationSource.CareersPage, DivisionId = 2, DepartmentId = 31,
            RequestingHodUserId = hod, FullName = "Linda Crawl", Email = "lc.cand@mail.test", Phone = "0240000000", HighestEducation = EducationLevel.Bachelors,
            Status = ApplicationStatus.Submitted, SubmittedAt = DateTime.UtcNow, StatusChangedAt = DateTime.UtcNow
        };
        app.Actions.Add(new ApplicationActionEntry { Action = ApplicationAction.Submitted, ToStatus = ApplicationStatus.Submitted, ActorUserId = cand,
            ActorName = "Linda Crawl", ActionAt = DateTime.UtcNow });
        db.JobApplications.Add(app);
        await db.SaveChangesAsync();
    }

    /// <summary>Visits every page reachable from the home page; returns the problems found ("page ← linked from").</summary>
    private async Task<List<string>> CrawlAsync(string email, string password, int maxPages = 400)
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, password)).StatusCode);
        var queue = new Queue<(string Url, string From)>([("/", "(start)")]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/" };
        var problems = new List<string>();
        var visited = 0;
        while (queue.Count > 0 && visited < maxPages)
        {
            var (url, from) = queue.Dequeue();
            visited++;
            var response = await client.GetAsync(url);
            var status = (int)response.StatusCode;
            if (status is 301 or 302)
            {
                var to = response.Headers.Location?.ToString() ?? "";
                if (to.Contains("/Account/AccessDenied")) problems.Add($"link to a page this user may not open: {url}  ← {from}");
                else if (to.Contains("/Account/Login")) problems.Add($"signed out by: {url}  ← {from}");
                else if (to.StartsWith('/') && seen.Add(to)) queue.Enqueue((to, url + " (redirect)"));
                continue;
            }
            if (status >= 400) { problems.Add($"{status}: {url}  ← {from}"); continue; }
            if (response.Content.Headers.ContentType?.MediaType != "text/html") continue;
            var html = await response.Content.ReadAsStringAsync();
            foreach (Match m in Href.Matches(html))
            {
                var link = WebUtility.HtmlDecode(m.Groups[1].Value);
                if (Skip.Any(s => link.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                if (seen.Add(link)) queue.Enqueue((link, url));
            }
        }
        output.WriteLine($"{email}: {visited} pages visited, {problems.Count} problem(s)");
        foreach (var p in problems) output.WriteLine("   " + p);
        return problems;
    }

    [Fact]
    public async Task Every_link_works_for_every_role_in_the_demo()
    {
        await SeedAsync();
        var people = new (string Email, string? Role, int? Department)[]
        {
            (GrsmsWebFactory.AdminEmail, null, null), // Super Admin
            ("lc.hrm@grel.test", Roles.HrManager, 1),
            ("lc.ro@grel.test", Roles.RecruitmentOfficer, 1),
            ("lc.md@grel.test", Roles.ManagingDirector, 1),
            ("lc.hod@grel.test", Roles.HeadOfDepartment, 31),
            ("lc.hradmin@grel.test", Roles.HrAdministrator, 1),
            ("lc.itm@grel.test", Roles.ItManager, 2),
            ("lc.emp@grel.test", Roles.Employee, 3),
            ("lc.cand@mail.test", Roles.Candidate, null),
        };
        var all = new List<string>();
        foreach (var (email, role, dept) in people)
        {
            if (role is not null) await UserAsync(email, dept, role);
            all.AddRange((await CrawlAsync(email, role is null ? GrsmsWebFactory.AdminPassword : Password)).Select(p => $"[{role ?? "Super Admin"}] {p}"));
        }
        Assert.True(all.Count == 0, "Problems found:\n" + string.Join("\n", all));
    }
}
