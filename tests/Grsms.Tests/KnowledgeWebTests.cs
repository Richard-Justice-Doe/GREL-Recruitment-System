using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 AI-7 through the pages: an HR Manager adds a policy under Administration → Knowledge base; staff find it under
/// Policies &amp; procedures, ask (AI off: the matching sections), read the sections and preview the Word file; other staff can't
/// manage it and candidates can't open the staff pages.
/// </summary>
public class KnowledgeWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<HttpClient> UserAsync(string email, params string[] roles)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], CreatedAt = DateTime.UtcNow };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                await users.AddToRolesAsync(user, roles);
            }
        }
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Hr_adds_a_policy_and_staff_find_ask_and_read_it()
    {
        var hrm = await UserAsync("kb.hrm@grel.test", Roles.HrManager);
        Assert.Contains("Knowledge base", await hrm.GetStringAsync("/Administration"));
        Assert.Contains("Add a document", await hrm.GetStringAsync("/KnowledgeAdmin"));

        // Upload through the form (multipart, with the page's anti-forgery token).
        var page = await hrm.GetStringAsync("/KnowledgeAdmin/Create");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var form = new MultipartFormDataContent
        {
            { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" },
            { new StringContent("Leave Policy"), "Title" }, { new StringContent("HR Manager"), "Owner" }, { new StringContent("2.1"), "Version" },
            { new StringContent("2026-01-01"), "EffectiveDate" }, { new StringContent("Staff"), "Audience" }
        };
        var file = new ByteArrayContent(KnowledgeBaseTests.Docx(KnowledgeBaseTests.LeavePolicy).ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "file", "leave-policy.docx");
        var created = await hrm.PostAsync("/KnowledgeAdmin/Create", form);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        int id;
        using (var scope = factory.Services.CreateScope())
            id = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().KnowledgeDocuments.Where(d => d.Title == "Leave Policy").Select(d => d.Id).SingleAsync();
        var edit = await hrm.GetStringAsync(created.Headers.Location!.ToString());
        Assert.Contains("What GRSMS read from the file (4 sections)", edit);

        // Staff: the menu link, the library, a question (AI off: the matching sections), the sections and the Word preview.
        var staff = await UserAsync("kb.staff@grel.test", Roles.Employee);
        var home = await staff.GetStringAsync("/");
        Assert.Contains("Policies &amp; procedures", home);
        Assert.Contains("Leave Policy", await staff.GetStringAsync("/Knowledge"));
        var answer = await (await staff.PostFormAsync("/Knowledge", "/Knowledge/Ask", new() { ["question"] = "How many days of annual leave?" }))
            .Content.ReadAsStringAsync();
        Assert.Contains("Matching sections", answer);
        Assert.Contains($"/Knowledge/Document/{id}#s-", answer);
        Assert.Contains("24 working days", await staff.GetStringAsync($"/Knowledge/Document/{id}"));
        var preview = await staff.GetAsync($"/Knowledge/Open/{id}?view=html");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("24 working days", await preview.Content.ReadAsStringAsync());

        // Other staff can't manage it; candidates can't open the staff pages or the staff-only file.
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/KnowledgeAdmin")).Headers.Location!.ToString());
        var candidate = await UserAsync("kb.cand@mail.test", Roles.Candidate);
        Assert.Contains("/Account/AccessDenied", (await candidate.GetAsync("/Knowledge")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/Knowledge/Open/{id}")).StatusCode);

        // Archived: gone from the staff library.
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.PostFormAsync($"/KnowledgeAdmin/Edit/{id}", $"/KnowledgeAdmin/SetActive/{id}", new() { ["active"] = "false" })).StatusCode);
        Assert.DoesNotContain("Leave Policy", await staff.GetStringAsync("/Knowledge"));
    }
}
