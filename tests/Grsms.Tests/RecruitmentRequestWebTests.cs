using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// The recruitment request journey through the real web pages: a HOD raises a request, uploads the job
/// description and submits it; the Recruitment Officer, HR Manager and MD each approve from the details page.
/// </summary>
public class RecruitmentRequestWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    /// <summary>Creates a ready-to-use account (no forced password change) directly in the database.</summary>
    private async Task<string> UserAsync(string email, int? departmentId, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null) return existing.Id;

        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0],
            DepartmentId = departmentId, CreatedAt = DateTime.UtcNow
        };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRolesAsync(user, roles);
        return user.Id;
    }

    private async Task<HttpClient> SignedInAsync(string email)
    {
        var client = factory.NewClient();
        var login = await client.SignInAsync(email, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    [Fact]
    public async Task Hod_raises_uploads_and_submits_then_three_approvers_approve()
    {
        // HOD of Factory TBU (seeded department 32, Operations division).
        var hodId = await UserAsync("qa.hod@grel.test", 32, Roles.HeadOfDepartment, Roles.Employee);
        await UserAsync("qa.ro@grel.test", 1, Roles.RecruitmentOfficer);
        await UserAsync("qa.hrm@grel.test", 1, Roles.HrManager);
        await UserAsync("qa.md@grel.test", 1, Roles.ManagingDirector);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Departments.FindAsync(32))!.HeadOfDepartmentUserId = hodId;
            await db.SaveChangesAsync();
        }

        // ---- HOD raises the request
        var hod = await SignedInAsync("qa.hod@grel.test");
        var created = await hod.PostFormAsync("/RecruitmentRequests/Create", "/RecruitmentRequests/Create", new()
        {
            ["DepartmentId"] = "32",
            ["SiteId"] = "1",
            ["PositionTitle"] = "Quality Inspector",
            ["NumberOfPositions"] = "2",
            ["EmploymentType"] = "0",   // Permanent (enum value, as the drop-down posts it)
            ["VacancyReason"] = "0",    // New position
            ["Justification"] = "Production volume doubled; inspections now run on two shifts.",
            ["BudgetCode"] = "QA-OPS-26",
            ["EstimatedAnnualCost"] = "120000"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.ToString();
        var page = await hod.GetStringAsync(detailsUrl);
        Assert.Contains("Quality Inspector", page);
        Assert.Contains("Attach the job description before submitting", page);
        var id = int.Parse(Regex.Match(detailsUrl, @"/(\d+)$").Groups[1].Value);

        // ---- Upload the job description (multipart form, with the page's anti-forgery token)
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var upload = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" },
            { new StringContent("JobDescription"), "category" }
        };
        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%test\n%%EOF"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(pdf, "file", "Quality Inspector JD.pdf");
        var uploaded = await hod.PostAsync($"/RecruitmentRequests/Upload/{id}", upload);
        Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);

        page = await hod.GetStringAsync(detailsUrl);
        Assert.Contains("Quality Inspector JD.pdf", page);
        var attachmentId = Regex.Match(page, @"attachmentId=(\d+)").Groups[1].Value;

        // ---- Submit
        Assert.Equal(HttpStatusCode.Redirect, (await hod.PostFormAsync(detailsUrl, $"/RecruitmentRequests/Submit/{id}", new())).StatusCode);
        Assert.Contains("Recruitment review", await hod.GetStringAsync(detailsUrl));

        // ---- Three approvals
        await ApproveAsync("qa.ro@grel.test", id, "RecruitmentReview", extra: null);
        await ApproveAsync("qa.hrm@grel.test", id, "HrApproval", extra: ("BudgetValidated", "true"));
        await ApproveAsync("qa.md@grel.test", id, "MdApproval", extra: null);

        page = await hod.GetStringAsync(detailsUrl);
        Assert.Contains("Fully approved", page);

        // ---- The job description downloads intact for an approver
        var md = await SignedInAsync("qa.md@grel.test");
        var download = await md.GetAsync($"/RecruitmentRequests/Download/{id}?attachmentId={attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.StartsWith("%PDF", await download.Content.ReadAsStringAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Employees_cannot_open_recruitment_requests()
    {
        await UserAsync("plain.employee@grel.test", 1, Roles.Employee);
        var client = await SignedInAsync("plain.employee@grel.test");
        var response = await client.GetAsync("/RecruitmentRequests");
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    /// <summary>Signs in as an approver, checks the request is in their "Awaiting my action" list, and approves it.</summary>
    private async Task ApproveAsync(string email, int id, string stage, (string Key, string Value)? extra)
    {
        var client = await SignedInAsync(email);
        Assert.Contains($"/RecruitmentRequests/Details/{id}", await client.GetStringAsync("/RecruitmentRequests?tab=awaiting"));

        var fields = new Dictionary<string, string> { ["Decision"] = "Approve", ["ExpectedStatus"] = stage };
        if (extra is { } e) fields[e.Key] = e.Value;
        var result = await client.PostFormAsync($"/RecruitmentRequests/Details/{id}", $"/RecruitmentRequests/Decide/{id}", fields);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("tab=awaiting", result.Headers.Location!.ToString()); // success goes back to the waiting list
    }
}
