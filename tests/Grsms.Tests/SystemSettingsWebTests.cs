using System.Net;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Identity;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// Phase 16 through the real pages (its own site instance, because it changes settings): the Super Admin saves settings
/// and a template; "Sign in with Microsoft 365" appears once its three values are set, without a restart; the Admin sees
/// everything read-only and cannot open Backups; the audit monitor opens.
/// </summary>
public class SystemSettingsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    private const string Password = "Test#Pass2026";

    private async Task<HttpClient> SignedInAsync(string email, string password)
    {
        var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.SignInAsync(email, password)).StatusCode);
        return client;
    }

    private static bool Denied(HttpResponseMessage r) =>
        r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied");

    [Fact]
    public async Task Closing_self_registration_blocks_the_register_page_and_hr_creates_candidate_accounts_instead()
    {
        var admin = await SignedInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        // Saving the workflow card with the switch unticked closes registration.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/SystemSettings", "/SystemSettings/Save",
            new() { ["group"] = SettingCatalogue.Workflow })).StatusCode);

        var visitor = factory.NewClient();
        Assert.Contains("/Account/Login", (await visitor.GetAsync("/Account/Register")).Headers.Location!.ToString());
        Assert.DoesNotContain("create a candidate account", await visitor.GetStringAsync("/Account/Login"));
        // A post (e.g. a saved form) is refused too, and no account is made.
        var post = await visitor.PostFormAsync("/Account/Login", "/Account/Register", new()
            { ["FullName"] = "Sneaky", ["Email"] = "sneaky@mail.test", ["Password"] = "Sneak#Pass2026", ["ConfirmPassword"] = "Sneak#Pass2026" });
        Assert.Contains("/Account/Login", post.Headers.Location!.ToString());
        using (var scope = factory.Services.CreateScope())
            Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("sneaky@mail.test"));

        // HR creates the candidate account; the person is emailed a link to choose a password, and can then sign in.
        var created = await admin.PostFormAsync("/Users/CreateCandidate", "/Users/CreateCandidate",
            new() { ["fullName"] = "Abena Trial", ["email"] = "abena.trial@mail.test" });
        Assert.Contains("candidates=True", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var mail = Directory.GetFiles(factory.MailFolder, "*.eml").Select(File.ReadAllText).Single(m => m.Contains("To: abena.trial@mail.test"));
        Assert.Contains("Your GREL careers account", mail);
        using (var scope = factory.Services.CreateScope())
        {
            var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("abena.trial@mail.test"))!;
            Assert.True(user.IsCandidate);
            Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(user, Roles.Candidate));
        }

        // Back on again for any other test.
        await admin.PostFormAsync("/SystemSettings", "/SystemSettings/Save",
            new() { ["group"] = SettingCatalogue.Workflow, [SettingCatalogue.AllowSelfRegistration] = "true" });
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/Account/Register")).StatusCode);
    }

    [Fact]
    public async Task Super_admin_changes_settings_and_templates_microsoft_sign_in_appears_and_admin_is_read_only()
    {
        var anonymous = factory.NewClient();
        Assert.DoesNotContain("Sign in with Microsoft", await anonymous.GetStringAsync("/Account/Login"));
        // Not configured: the sign-in method refuses to start.
        var early = await anonymous.PostFormAsync("/Account/Login", "/Account/ExternalLogin", new() { ["provider"] = "Microsoft" });
        Assert.DoesNotContain("microsoftonline", early.Headers.Location?.ToString() ?? "");

        var admin = await SignedInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        foreach (var page in new[] { "/SystemSettings", "/EmailTemplates", "/EmailTemplates/Edit/offer.sent", "/Backups", "/AuditMonitor" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(page)).StatusCode);
        Assert.Contains("No backup folder on this server", await admin.GetStringAsync("/Backups"));

        // Microsoft 365: the three values on the screen switch the button on straight away.
        var saved = await admin.PostFormAsync("/SystemSettings", "/SystemSettings/Save", new()
        {
            ["group"] = SettingCatalogue.Microsoft365,
            [SettingCatalogue.AzureTenant] = "11111111-2222-3333-4444-555555555555",
            [SettingCatalogue.AzureClient] = "66666666-7777-8888-9999-000000000000",
            [SettingCatalogue.AzureSecret] = "client-secret-value"
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Contains("Sign in with Microsoft", await factory.NewClient().GetStringAsync("/Account/Login"));
        var settingsPage = await admin.GetStringAsync("/SystemSettings");
        Assert.DoesNotContain("client-secret-value", settingsPage); // secrets are never shown back
        Assert.Contains("is shown", settingsPage);

        // A template saved through the page; the preview button does not save.
        var preview = await admin.PostFormAsync("/EmailTemplates/Edit/application.received", "/EmailTemplates/Edit/application.received", new()
            { ["Subject"] = "Thanks for applying: {{JobTitle}}", ["Body"] = "We have your application {{ApplicationNumber}}.", ["preview"] = "true" });
        Assert.Contains("We have your application APP-2026-00042.", await preview.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Customised", await admin.GetStringAsync("/EmailTemplates"));
        await admin.PostFormAsync("/EmailTemplates/Edit/application.received", "/EmailTemplates/Edit/application.received", new()
            { ["Subject"] = "Thanks for applying: {{JobTitle}}", ["Body"] = "We have your application {{ApplicationNumber}}." });
        Assert.Contains("Customised", await admin.GetStringAsync("/EmailTemplates"));

        // The Admin: everything read-only, nothing saved, no backups.
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "w.admin@grel.test", Email = "w.admin@grel.test", EmailConfirmed = true, FullName = "W Admin", CreatedAt = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            await users.AddToRolesAsync(user, [Roles.Admin, Roles.Employee]);
        }
        var readOnly = await SignedInAsync("w.admin@grel.test", Password);
        var view = await readOnly.GetStringAsync("/SystemSettings");
        Assert.Contains("Read-only.", view);
        Assert.Contains("<fieldset disabled=\"disabled\">", view);
        Assert.True(Denied(await readOnly.PostFormAsync("/SystemSettings", "/SystemSettings/Save",
            new() { ["group"] = SettingCatalogue.Workflow, [SettingCatalogue.VacancyClosingDays] = "99" })));
        Assert.True(Denied(await readOnly.PostFormAsync("/EmailTemplates", "/EmailTemplates/Reset/application.received", new())));
        Assert.True(Denied(await readOnly.GetAsync("/Backups")));
        Assert.Equal(HttpStatusCode.OK, (await readOnly.GetAsync("/AuditMonitor")).StatusCode);
        Assert.Contains("System settings", await readOnly.GetStringAsync("/")); // in the menu
    }
}
