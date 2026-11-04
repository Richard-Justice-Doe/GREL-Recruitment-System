using System.Net;
using Grsms.Application.Reports;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>Reports through the real pages: every report renders for the MD, the CSV download works, HODs are told the
/// figures are limited to their departments, and employees and candidates are refused.</summary>
public class ReportWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<HttpClient> UserAsync(string email, int? department, params string[] roles)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(email) is null)
            {
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = department, CreatedAt = DateTime.UtcNow };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                await users.AddToRolesAsync(user, roles);
            }
        }
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Md_opens_every_report_and_downloads_csv_while_others_are_limited_or_refused()
    {
        var md = await UserAsync("r.md@grel.test", 1, Roles.ManagingDirector);
        Assert.Contains("Reports", await md.GetStringAsync("/")); // in the menu
        foreach (var (key, title, _) in ReportKeys.All)
        {
            var page = await md.GetStringAsync($"/Reports?Report={key}&DivisionId=2&From=2026-01-01&To=2026-12-31");
            Assert.Contains(System.Net.WebUtility.HtmlEncode(title), page);
        }
        Assert.Contains("Recruitment funnel", await md.GetStringAsync("/Reports"));

        var csv = await md.GetAsync("/Reports/Export?Report=requests");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("Request,Position,Division,Department", (await csv.Content.ReadAsStringAsync()).TrimStart('﻿'));
        Assert.Equal(HttpStatusCode.NotFound, (await md.GetAsync("/Reports/Export?Report=nonsense")).StatusCode);

        var hod = await UserAsync("r.hod@grel.test", 27, Roles.HeadOfDepartment);
        Assert.Contains("Showing the departments you head", await hod.GetStringAsync("/Reports?Report=vacancies"));

        var employee = await UserAsync("r.emp@grel.test", 3, Roles.Employee);
        Assert.Contains("/Account/AccessDenied", (await employee.GetAsync("/Reports")).Headers.Location!.ToString());
        Assert.DoesNotContain(">Reports<", (await employee.GetStringAsync("/")).Replace(" ", "").Replace("\n", "").Replace("\r", ""));
    }
}
