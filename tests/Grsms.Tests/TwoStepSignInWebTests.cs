using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Grsms.Application.Settings;
using Grsms.Domain.Auditing;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Identity;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// 22 Nov 2026 (docs/41, H2): two-step sign-in through the real pages. A privileged member of staff (HR Manager) is sent to set
/// it up before anything else; set-up needs a correct code from the app and shows recovery codes once; from then on every
/// password sign-in asks for the code (wrong codes refused and audited) or a recovery code (single use). Everyday staff are not
/// forced. Codes are computed here exactly as an authenticator app does (RFC 6238, SHA-1, 30 s, 6 digits).
/// </summary>
public class TwoStepSignInWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
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

    /// <summary>The 6-digit code an authenticator app shows for this key now.</summary>
    private static string Totp(string formattedKey)
    {
        var key = Base32(formattedKey.Replace(" ", "").ToUpperInvariant());
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] Base32(string s)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in s.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return [.. bytes];
    }

    [Fact]
    public async Task Privileged_staff_set_up_two_step_sign_in_then_need_a_code_or_a_recovery_code_at_every_password_sign_in()
    {
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?> { [SettingCatalogue.RequireTwoStep] = "true" });
        await UserAsync("mfa.hrm@grel.test", Roles.HrManager);
        await UserAsync("mfa.staff@grel.test", Roles.Employee);

        // Everyday staff are not forced.
        var staff = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await staff.SignInAsync("mfa.staff@grel.test", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/")).StatusCode);

        // The HR Manager is sent to set it up, from any page.
        var hrm = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await hrm.SignInAsync("mfa.hrm@grel.test", Password)).StatusCode);
        foreach (var page in new[] { "/", "/Employees", "/Vacancies" })
            Assert.EndsWith("/Account/TwoStep", (await hrm.GetAsync(page)).Headers.Location!.ToString());
        var setup = await hrm.GetStringAsync("/Account/TwoStep");
        Assert.Contains("set up two-step sign-in before you continue", setup);
        Assert.Contains("data:image/png;base64,", setup); // the QR code
        var key = Regex.Match(setup, "<code class=\"user-select-all\">([a-z0-9 ]+)</code>").Groups[1].Value;
        Assert.NotEmpty(key);

        // A wrong code does not turn it on; the right one does, shows ten recovery codes once and lets them in.
        Assert.Contains("That code is not right", await (await hrm.PostFormAsync("/Account/TwoStep", "/Account/TwoStep", new() { ["code"] = "000000" })).Content.ReadAsStringAsync());
        var enabled = await (await hrm.PostFormAsync("/Account/TwoStep", "/Account/TwoStep", new() { ["code"] = Totp(key) })).Content.ReadAsStringAsync();
        Assert.Contains("Two-step sign-in is on", enabled);
        var recovery = Regex.Matches(enabled, "<span class=\"d-block border rounded px-2 py-1 text-center\">([^<]+)</span>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(10, recovery.Count);
        Assert.Equal(HttpStatusCode.OK, (await hrm.GetAsync("/")).StatusCode);
        Assert.DoesNotContain(recovery[0], await hrm.GetStringAsync("/Account/TwoStep")); // shown only once
        Assert.DoesNotContain("Turn off two-step sign-in", await hrm.GetStringAsync("/Account/TwoStep")); // required for the role

        // Next password sign-in: the code is asked for; a wrong one is refused and audited; the right one signs in.
        var next = factory.NewClient();
        var signIn = await next.SignInAsync("mfa.hrm@grel.test", Password);
        Assert.Contains("/Account/LoginWith2fa", signIn.Headers.Location!.ToString());
        Assert.Contains("/Account/Login", (await next.GetAsync("/")).Headers.Location!.ToString()); // not signed in yet
        var codePage = signIn.Headers.Location!.ToString();
        Assert.Contains("That code is not right", await (await next.PostFormAsync(codePage, "/Account/LoginWith2fa", new() { ["Code"] = "111111" })).Content.ReadAsStringAsync());
        var ok = await next.PostFormAsync(codePage, "/Account/LoginWith2fa", new() { ["Code"] = Totp(key) });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await next.GetAsync("/")).StatusCode);

        // Lost phone: a recovery code works once.
        var lost = factory.NewClient();
        await lost.SignInAsync("mfa.hrm@grel.test", Password);
        Assert.Equal(HttpStatusCode.Redirect, (await lost.PostFormAsync("/Account/LoginWith2fa?recovery=true", "/Account/LoginWith2fa",
            new() { ["Code"] = recovery[0], ["UseRecoveryCode"] = "true" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lost.GetAsync("/")).StatusCode);
        var again = factory.NewClient();
        await again.SignInAsync("mfa.hrm@grel.test", Password);
        Assert.Contains("already used", await (await again.PostFormAsync("/Account/LoginWith2fa?recovery=true", "/Account/LoginWith2fa",
            new() { ["Code"] = recovery[0], ["UseRecoveryCode"] = "true" })).Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var audits = await db.AuditLogs.Where(a => a.UserName == "mfa.hrm@grel.test").Select(a => new { a.Action, Details = a.NewValues }).ToListAsync();
            Assert.Contains(audits, a => a.Action == AuditActions.TwoStepEnabled);
            Assert.Contains(audits, a => a.Action == AuditActions.SignInFailed && a.Details == "Wrong two-step code");
            Assert.Contains(audits, a => a.Action == AuditActions.SignedIn && a.Details == "Method: password + authenticator code");
            Assert.Contains(audits, a => a.Action == AuditActions.SignedIn && a.Details == "Method: password + recovery code");
            await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(new Dictionary<string, string?> { [SettingCatalogue.RequireTwoStep] = "false" });
        }
    }
}
