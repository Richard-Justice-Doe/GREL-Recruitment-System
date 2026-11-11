using System.Net;
using System.Text.RegularExpressions;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 28 (docs/30): the employee pages through the real web site: the HR Manager adds a grade, registers an existing
/// employee and updates their job; a Recruitment Officer can look but not register; an employee cannot open the list.
/// </summary>
public class EmployeeWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task UserAsync(string email, params string[] roles)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = email.Split('@')[0], DepartmentId = 1, CreatedAt = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRolesAsync(user, roles);
    }

    private async Task<HttpClient> SignedInAsync(string email, params string[] roles)
    {
        await UserAsync(email, roles);
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, Password)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Hr_registers_an_existing_employee_and_updates_their_job()
    {
        var hr = await SignedInAsync("e.hrm@grel.test", Roles.HrManager);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // A grade, from the Administration page.
        Assert.Contains("/Grades", await hr.GetStringAsync("/Administration"));
        Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync("/Grades/Create", "/Grades/Create",
            new() { ["Code"] = "G7", ["Name"] = "Manager", ["SortOrder"] = "7", ["IsActive"] = "true" })).StatusCode);
        var gradeId = Regex.Match(await hr.GetStringAsync("/Grades"), @"/Grades/Edit/(\d+)").Groups[1].Value;

        // The menu and the list.
        var list = await hr.GetStringAsync("/Employees");
        Assert.Contains("Register existing employee", list);
        Assert.Contains("Import from CSV", list);

        var created = await hr.PostFormAsync("/Employees/Register", "/Employees/Register", new()
        {
            ["FullName"] = "Esi Quaye", ["Email"] = "e.quaye@grel.test", ["EmployeeNumber"] = "GREL-0777", ["PositionTitle"] = "Payroll Officer",
            ["DepartmentId"] = "1", ["SiteId"] = "1", ["EmploymentType"] = "Permanent", ["HireDate"] = "2017-02-01", ["Status"] = "Active"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.ToString();
        var id = Regex.Match(detailsUrl, @"/Employees/Details/(\d+)").Groups[1].Value;
        var page = await hr.GetStringAsync(detailsUrl);
        Assert.Contains("Esi Quaye", page);
        Assert.Contains("GREL-0777", page);
        Assert.Contains("Hired before GRSMS", page);

        // Update the job: the history gets a second line.
        var form = await hr.GetStringAsync($"/Employees/UpdateJob/{id}");
        var version = Regex.Match(form, @"name=""Version"" value=""([^""]+)""").Groups[1].Value;
        Assert.Equal(HttpStatusCode.Redirect, (await hr.PostFormAsync($"/Employees/UpdateJob/{id}", $"/Employees/UpdateJob/{id}", new()
        {
            ["Version"] = version, ["EffectiveFrom"] = today, ["PositionTitle"] = "Payroll Officer", ["DepartmentId"] = "1", ["SiteId"] = "1",
            ["GradeId"] = gradeId, ["EmploymentType"] = "Permanent", ["Note"] = "Grade given in the 2026 grading exercise."
        })).StatusCode);
        page = await hr.GetStringAsync(detailsUrl);
        Assert.Contains("G7: Manager", page);
        Assert.Contains("Grade given in the 2026 grading exercise.", page);

        // The CSV template.
        var template = await hr.GetAsync("/Employees/ImportTemplate");
        Assert.Equal("text/csv", template.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("StaffNumber,FullName,Email", (await template.Content.ReadAsStringAsync()).TrimStart('﻿'));

        // A Recruitment Officer can look, but not register or update.
        var ro = await SignedInAsync("e.ro@grel.test", Roles.RecruitmentOfficer);
        Assert.Contains("Esi Quaye", await ro.GetStringAsync("/Employees"));
        Assert.DoesNotContain("Register existing employee", await ro.GetStringAsync("/Employees"));
        Assert.Contains("/Account/AccessDenied", (await ro.GetAsync("/Employees/Register")).Headers.Location!.ToString());
        Assert.Contains("/Account/AccessDenied", (await ro.GetAsync($"/Employees/UpdateJob/{id}")).Headers.Location!.ToString());
        Assert.Contains("/Account/AccessDenied", (await ro.GetAsync("/Grades")).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Employees_cannot_open_the_employee_list()
    {
        var employee = await SignedInAsync("e.employee@grel.test", Roles.Employee);
        Assert.Contains("/Account/AccessDenied", (await employee.GetAsync("/Employees")).Headers.Location!.ToString());
        Assert.DoesNotContain("/Employees\"", await employee.GetStringAsync("/"));
    }
}
