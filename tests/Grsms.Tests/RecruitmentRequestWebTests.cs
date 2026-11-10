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
/// The recruitment request (QF-81-11) journey through the real web pages: a department staff member fills in the form,
/// uploads the job description and submits it; the Head of Department, HR Manager and MD each approve from the details page.
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
    public async Task Staff_member_raises_the_form_then_hod_hr_manager_and_md_approve()
    {
        // Factory TBU (seeded department 32, Operations division): a staff member and their HOD.
        await UserAsync("qa.staff@grel.test", 32, Roles.Employee);
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

        // ---- The staff member opens the form: every QF-81-11 section and the rich-text editor are there
        var staff = await SignedInAsync("qa.staff@grel.test");
        var form = await staff.GetStringAsync("/RecruitmentRequests/Create");
        Assert.Contains("data-steps", form);                       // usability U4: one section per step
        Assert.Contains("data-step=\"Employment details\"", form);
        foreach (var text in new[] { "Engagement type", "Category", "Purpose", "Internal restructuring", "Request date", "Specific job title",
                                     "Number of staff required", "Proposed starting date", "Description of the post", "data-rich-text",
                                     "Educational level", "Level of training", "Certificate(s) required", "Professional experience",
                                     "Driving licence", "Not needed", "Desirable", "Mandatory", "Observations / remarks", "rich-text-editor.js" })
            Assert.Contains(text, form);

        // ---- ... fills it in and saves the draft
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await staff.PostFormAsync("/RecruitmentRequests/Create", "/RecruitmentRequests/Create", new()
        {
            ["DepartmentId"] = "32",
            ["SiteId"] = "1",
            ["EmploymentType"] = "Permanent",          // radio buttons post the enum name
            ["StaffCategory"] = "Junior",
            ["VacancyReason"] = "Expansion",
            ["RequestDate"] = today.ToString("yyyy-MM-dd"),
            ["PositionTitle"] = "Quality Inspector",
            ["NumberOfPositions"] = "2",
            ["ProposedStartDate"] = today.AddDays(45).ToString("yyyy-MM-dd"),
            ["PostDescription"] = "<p>Inspects <strong>every batch</strong> of crude palm oil before dispatch.</p><ul><li>Samples tanks</li><li>Keeps records</li></ul>",
            ["Justification"] = "Production volume doubled; inspections now run on two shifts.",
            ["EducationLevel"] = "4",                   // Diploma (the drop-down posts the enum value)
            ["QualificationCategory"] = "Food science",
            ["MinYearsExperience"] = "2",
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.ToString();
        var page = await staff.GetStringAsync(detailsUrl);
        Assert.Contains("Quality Inspector", page);
        Assert.Contains("<strong>every batch</strong>", page);   // the formatting is shown back
        Assert.Contains("Junior", page);
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
        var uploaded = await staff.PostAsync($"/RecruitmentRequests/Upload/{id}", upload);
        Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);

        page = await staff.GetStringAsync(detailsUrl);
        Assert.Contains("Quality Inspector JD.pdf", page);
        var attachmentId = Regex.Match(page, @"attachmentId=(\d+)").Groups[1].Value;

        // ---- Submit: it waits for the Head of Department
        Assert.Equal(HttpStatusCode.Redirect, (await staff.PostFormAsync(detailsUrl, $"/RecruitmentRequests/Submit/{id}", new())).StatusCode);
        Assert.Contains("Department Head review", await staff.GetStringAsync(detailsUrl));

        // ---- Three approvals
        await ApproveAsync("qa.hod@grel.test", id, "DepartmentHeadReview", extra: null);
        await ApproveAsync("qa.hrm@grel.test", id, "HrApproval", extra: ("BudgetValidated", "true"));
        await ApproveAsync("qa.md@grel.test", id, "MdApproval", extra: null);

        page = await staff.GetStringAsync(detailsUrl);
        Assert.Contains("Fully approved", page);

        // ---- A Recruitment Officer is offered the conversion into a vacancy
        var ro = await SignedInAsync("qa.ro@grel.test");
        Assert.Contains("Convert into a job vacancy", await ro.GetStringAsync(detailsUrl));

        // ---- The job description downloads intact for an approver
        var md = await SignedInAsync("qa.md@grel.test");
        var download = await md.GetAsync($"/RecruitmentRequests/Download/{id}?attachmentId={attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.StartsWith("%PDF", await download.Content.ReadAsStringAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Staff_can_open_recruitment_requests_but_candidates_cannot()
    {
        await UserAsync("plain.employee@grel.test", 1, Roles.Employee);
        var employee = await SignedInAsync("plain.employee@grel.test");
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/RecruitmentRequests")).StatusCode);

        await UserAsync("plain.candidate@grel.test", null, Roles.Candidate);
        var candidate = await SignedInAsync("plain.candidate@grel.test");
        var response = await candidate.GetAsync("/RecruitmentRequests");
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
