using System.Net;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>17 November 2026: the celebrations pages, the Home widget, the Action Center line and HR's settings, through the site.</summary>
public class CelebrationWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<(HttpClient Client, string Id)> UserAsync(string email, string name, params string[] roles)
    {
        string id;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, DepartmentId = 2, CreatedAt = DateTime.UtcNow };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                await users.AddToRolesAsync(user, roles);
            }
            id = user.Id;
        }
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return (client, id);
    }

    [Fact]
    public async Task Staff_see_todays_anniversary_leave_a_message_and_hr_changes_the_settings()
    {
        var (kofiClient, kofiId) = await UserAsync("cw.kofi@grel.test", "Kofi Boateng", Roles.Employee);
        int kofi;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var e = new Domain.Employees.Employee { EmployeeNumber = "GREL-CW-1", UserId = kofiId, PositionTitle = "Network Engineer", DepartmentId = 2, SiteId = 1,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10), Status = Domain.Employees.EmployeeStatus.Active, StatusChangedAt = DateTime.UtcNow };
            db.Employees.Add(e);
            await db.SaveChangesAsync();
            kofi = e.Id;
        }

        var (staff, _) = await UserAsync("cw.staff@grel.test", "Akua Owusu", Roles.Employee);
        Assert.Contains("View celebrations", await staff.GetStringAsync("/"));                 // the Home widget
        Assert.Contains("Work anniversaries today", await staff.GetStringAsync("/ActionCenter")); // the Action Center line
        var page = await staff.GetStringAsync("/Celebrations");
        Assert.Contains("Kofi Boateng", page);
        Assert.Contains("10 Years Anniversary", page);
        Assert.Contains(">KB</span>", page); // initials while there is no photo
        Assert.Contains("Kofi Boateng", await staff.GetStringAsync("/Celebrations/Calendar"));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/Celebrations/Photo/{kofi}")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await staff.PostFormAsync("/Celebrations", "/Celebrations/Post",
            new() { ["employeeId"] = kofi.ToString(), ["kind"] = "Anniversary", ["text"] = "Congratulations! 🎉" })).StatusCode);
        Assert.Contains("Akua Owusu:</span> Congratulations!", await staff.GetStringAsync("/Celebrations")); // the emoji is HTML-encoded

        // HR's settings: switch anniversaries off; staff can't open the settings.
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/Celebrations/Settings")).Headers.Location!.ToString());
        var (hr, _) = await UserAsync("cw.hrm@grel.test", "Ama Hr", Roles.HrManager);
        Assert.Contains("Celebrate work anniversaries", await hr.GetStringAsync("/Celebrations/Settings"));
        Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync("/Celebrations/Settings", "/Celebrations/Settings", new()
        {
            ["Celebrations:Birthdays"] = "true", ["Celebrations:Notifications"] = "true", ["Celebrations:Messages"] = "true", ["Celebrations:FromHourGmt"] = "7"
        })).StatusCode);
        Assert.DoesNotContain("Kofi Boateng", await staff.GetStringAsync("/Celebrations"));

        // 22 Nov 2026: Kofi (registered by hand, no personal record) adds his birthday; every colleague sees it. HR can enter it too.
        Assert.Contains("Add your birthday", await kofiClient.GetStringAsync("/Celebrations"));
        Assert.DoesNotContain("Add your birthday", await staff.GetStringAsync("/Celebrations")); // no employee record
        var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-40);
        Assert.Equal(HttpStatusCode.Redirect, (await kofiClient.PostFormAsync("/Celebrations", "/Celebrations/MyBirthday",
            new() { ["dateOfBirth"] = dob.ToString("yyyy-MM-dd") })).StatusCode);
        var withBirthday = await kofiClient.GetStringAsync("/Celebrations");
        Assert.DoesNotContain("Add your birthday", withBirthday);
        Assert.Contains($"Your birthday: <b>{dob:d MMMM}</b>", withBirthday);
        Assert.Contains("Kofi Boateng", await staff.GetStringAsync("/Celebrations"));
        Assert.Contains("Save date of birth", await hr.GetStringAsync($"/Employees/Details/{kofi}"));

        // 22 Nov 2026: Kofi uploads his own photo from "My photo" (in his account menu); colleagues see it on his celebration card.
        Assert.Contains("/MyPhoto", await kofiClient.GetStringAsync("/"));
        var myPhoto = await kofiClient.GetStringAsync("/MyPhoto");
        Assert.Contains("Add your photo", myPhoto);
        Assert.Equal(HttpStatusCode.NotFound, (await kofiClient.GetAsync("/MyPhoto/Photo")).StatusCode);
        var token = System.Text.RegularExpressions.Regex.Match(myPhoto, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var png = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));
        png.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        var upload = new MultipartFormDataContent { { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" }, { png, "photo", "kofi.png" } };
        Assert.Equal(HttpStatusCode.Redirect, (await kofiClient.PostAsync("/MyPhoto/Upload", upload)).StatusCode);
        Assert.Contains("Change your photo", await kofiClient.GetStringAsync("/MyPhoto"));
        Assert.Equal("image/png", (await kofiClient.GetAsync("/MyPhoto/Photo")).Content.Headers.ContentType!.MediaType);
        Assert.Equal("image/png", (await staff.GetAsync($"/Celebrations/Photo/{kofi}")).Content.Headers.ContentType!.MediaType);
        Assert.Contains("You have no employee record yet", await staff.GetStringAsync("/MyPhoto")); // Akua has no employee record

        // Candidates can't open it.
        var (candidate, _) = await UserAsync("cw.cand@mail.test", "Kwame Cand", Roles.Candidate);
        Assert.Contains("/Account/AccessDenied", (await candidate.GetAsync("/Celebrations")).Headers.Location!.ToString());
    }
}
