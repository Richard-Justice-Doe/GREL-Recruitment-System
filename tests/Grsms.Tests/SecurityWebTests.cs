using System.Net;
using System.Text.RegularExpressions;
using Grsms.Domain.Security;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 12 hardening, through the real site: the Content-Security-Policy and other headers, no inline script on any
/// main page (so the policy never breaks a page), sign-in that does not reveal whether an account exists, and the rate
/// limit on the sign-in form.
/// </summary>
public partial class SecurityWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>|\son(click|change|submit|load|input|error)\s*=|javascript:", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Every_response_carries_the_content_security_policy_and_pages_have_no_inline_script()
    {
        var anonymous = factory.NewClient();
        var login = await anonymous.GetAsync("/Account/Login");
        var csp = string.Join(";", login.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self';", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Equal("DENY", login.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", login.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.False(login.Headers.Contains("Server"));
        Assert.Contains("Content-Security-Policy", (await anonymous.GetAsync("/css/site.css")).Headers.Select(h => h.Key)); // static files too

        // The policy blocks inline scripts and onclick="..." handlers, so none may exist on any page.
        var admin = await AdminAsync();
        foreach (var page in new[] { "/Account/Login", "/Careers" })
            Assert.DoesNotMatch(InlineScript(), await anonymous.GetStringAsync(page));
        foreach (var page in new[] { "/", "/Units", "/Departments", "/Users", "/AuditLogs", "/Reports", "/Notifications", "/Vacancies", "/Account/ChangePassword" })
            Assert.DoesNotMatch(InlineScript(), await admin.GetStringAsync(page));
    }

    [Fact]
    public async Task A_wrong_password_never_reveals_whether_an_account_exists_or_is_deactivated()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var inactive = new ApplicationUser { UserName = "s.gone@grel.test", Email = "s.gone@grel.test", EmailConfirmed = true, FullName = "Gone", IsActive = false, CreatedAt = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(inactive, Password)).Succeeded);
            await users.AddToRoleAsync(inactive, Roles.Employee);
            var unconfirmed = new ApplicationUser { UserName = "s.new@mail.test", Email = "s.new@mail.test", EmailConfirmed = false, FullName = "New", CreatedAt = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(unconfirmed, Password)).Succeeded);
            await users.AddToRoleAsync(unconfirmed, Roles.Candidate);
        }

        async Task<string> TryAsync(string email, string password)
        {
            var client = factory.NewClient();
            var response = await client.PostFormAsync("/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
            return await response.Content.ReadAsStringAsync();
        }

        // Wrong password: the same answer as for an email with no account at all.
        foreach (var email in new[] { "s.gone@grel.test", "s.new@mail.test", "nobody@grel.test" })
        {
            var page = await TryAsync(email, "Wrong#Pass2026");
            Assert.Contains("Incorrect email or password", page);
            Assert.DoesNotContain("deactivated", page);
            Assert.DoesNotContain("confirm your email", page);
        }

        // Right password: the real reason is explained.
        Assert.Contains("deactivated", await TryAsync("s.gone@grel.test", Password));
        Assert.Contains("confirm your email", await TryAsync("s.new@mail.test", Password));

        // Wrong passwords on a blocked account still count towards the lockout.
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await users.GetAccessFailedCountAsync((await users.FindByEmailAsync("s.gone@grel.test"))!) >= 1);
        }
    }

    [Fact]
    public async Task SweetAlert_is_served_by_grsms_itself_on_every_layout_so_the_policy_allows_it()
    {
        var anonymous = factory.NewClient();
        var admin = await AdminAsync();
        foreach (var page in new[] { await anonymous.GetStringAsync("/Account/Login"), await anonymous.GetStringAsync("/Careers"), await admin.GetStringAsync("/") })
        {
            Assert.Contains("/lib/sweetalert2/dist/sweetalert2.min.js", page);
            Assert.Contains("/lib/sweetalert2/dist/sweetalert2.min.css", page);
        }
        var script = await anonymous.GetAsync("/lib/sweetalert2/dist/sweetalert2.min.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Contains("sweetalert2 v11.14.5", await script.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_health_check_answers_without_sign_in_and_shows_no_details()
    {
        var response = await factory.NewClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_sign_in_form_refuses_more_than_the_allowed_attempts_per_minute()
    {
        using var strict = factory.WithWebHostBuilder(b => b.UseSetting("Security:SignInAttemptsPerMinute", "3"));
        var client = strict.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostFormAsync("/Account/Login", "/Account/Login",
                new() { ["Email"] = "guess@grel.test", ["Password"] = $"Guess#{i}Pass2026" })).StatusCode);

        var refused = await client.PostFormAsync("/Account/Login", "/Account/Login", new() { ["Email"] = "guess@grel.test", ["Password"] = "Guess#9Pass2026" });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.Contains("Retry-After"));
        Assert.Contains("Too many attempts", await refused.Content.ReadAsStringAsync());

        // Reading the page is not limited, and the other forms share the same limit.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostFormAsync("/Account/ForgotPassword", "/Account/ForgotPassword",
            new() { ["Email"] = "guess@grel.test" })).StatusCode);
    }
}
