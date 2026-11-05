using System.Net;
using System.Text.RegularExpressions;
using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>
/// End-to-end checks through the real web pages: sign-in rules, role protection,
/// master-data screens, forced password change and candidate registration.
/// One site instance is shared by all tests in this class (xUnit "class fixture").
/// </summary>
public class WebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    [Fact]
    public async Task Anonymous_visitors_are_sent_to_the_login_page()
    {
        var response = await factory.NewClient().GetAsync("/Departments");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Login_page_has_security_headers_and_no_microsoft_button_until_configured()
    {
        var response = await factory.NewClient().GetAsync("/Account/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.DoesNotContain("Sign in with Microsoft", html);
    }

    [Fact]
    public async Task Stylesheets_scripts_and_fonts_load_before_signing_in()
    {
        // Otherwise the login page would appear without any styling.
        var client = factory.NewClient();
        foreach (var path in new[] { "/css/site.css", "/js/site.js", "/lib/bootstrap/dist/css/bootstrap.min.css", "/lib/fontawesome/webfonts/fa-solid-900.woff2" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_sign_in_and_manage_departments()
    {
        var client = factory.NewClient();
        var login = await client.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var list = await client.GetStringAsync("/Departments");
        Assert.Contains("Health, Safety &amp; Environment (HSE)", list);

        var create = await client.PostFormAsync("/Departments/Create", "/Departments/Create", new()
        {
            ["Name"] = "Plantation Operations",
            ["Code"] = "PLANT",
            ["DivisionId"] = "2",
            ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var details = await client.GetStringAsync(create.Headers.Location!.ToString());
        Assert.Contains("Plantation Operations", details);
        Assert.Contains("was added", details); // success banner

        var audit = await client.GetStringAsync("/AuditLogs?EntityName=Department");
        Assert.Contains("Plantation Operations", audit);
        Assert.Contains("admin@grel.test", audit);
    }

    [Fact]
    public async Task Posting_without_the_anti_forgery_token_is_rejected()
    {
        var client = factory.NewClient();
        await client.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);

        var response = await client.PostAsync("/Sites/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Sneaky", ["Code"] = "SNK"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        // Create a throwaway staff account so the admin is not locked for other tests.
        var admin = factory.NewClient();
        await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        await admin.PostFormAsync("/Users/Create", "/Users/Create", new()
        {
            ["FullName"] = "Lock Test", ["Email"] = "lock@grel.test",
            ["SelectedRoles"] = "Employee", ["IsActive"] = "true", ["TemporaryPassword"] = "Temp#Pass2026"
        });

        var client = factory.NewClient();
        string html = "";
        for (var i = 0; i < 5; i++)
            html = await (await client.SignInAsync("lock@grel.test", "Wrong#Pass2026")).Content.ReadAsStringAsync();

        Assert.Contains("locked", html);
        var users = await admin.GetStringAsync("/Users");
        Assert.Contains("Locked", users);
    }

    [Fact]
    public async Task Staff_with_a_temporary_password_must_change_it_first()
    {
        var admin = factory.NewClient();
        await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        var created = await admin.PostFormAsync("/Users/Create", "/Users/Create", new()
        {
            ["FullName"] = "Kofi Recruiter", ["Email"] = "kofi@grel.test", ["DepartmentId"] = "1",
            ["SelectedRoles"] = "Recruitment Officer", ["IsActive"] = "true", ["TemporaryPassword"] = "Temp#Pass2026"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var client = factory.NewClient();
        await client.SignInAsync("kofi@grel.test", "Temp#Pass2026");
        var home = await client.GetAsync("/");
        Assert.Contains("/Account/ChangePassword", home.Headers.Location!.ToString());

        var changed = await client.PostFormAsync("/Account/ChangePassword", "/Account/ChangePassword", new()
        {
            ["CurrentPassword"] = "Temp#Pass2026", ["NewPassword"] = "Kofi#Own2026!", ["ConfirmPassword"] = "Kofi#Own2026!"
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        // A Recruitment Officer is staff but not an HR Administrator: master data is off limits.
        var denied = await client.GetAsync("/Departments");
        Assert.Contains("/Account/AccessDenied", denied.Headers.Location!.ToString());
    }

    [Fact]
    public async Task A_new_staff_account_is_emailed_a_link_to_choose_their_password_and_never_the_password_itself()
    {
        var admin = factory.NewClient();
        await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        var created = await admin.PostFormAsync("/Users/Create", "/Users/Create", new()
        {
            ["FullName"] = "Efua Welcome", ["Email"] = "efua.welcome@grel.test", ["DepartmentId"] = "1",
            ["SelectedRoles"] = "Employee", ["TemporaryPassword"] = "Temp#Pass2026", ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var mail = Directory.GetFiles(factory.MailFolder, "*.eml").Select(File.ReadAllText).Single(m => m.Contains("To: efua.welcome@grel.test"));
        var body = Decode(mail);
        Assert.Contains("Subject: Your account for the GREL recruitment system", mail);
        Assert.Contains("Sign in with: efua.welcome@grel.test", body);
        Assert.Contains("Your roles: Employee", body);
        Assert.DoesNotContain("Temp#Pass2026", body); // never the password

        // The link lets them choose their own password, which then works for signing in (no forced change).
        var link = System.Net.WebUtility.HtmlDecode(Regex.Match(body, "https://localhost(/Account/ResetPassword[^\"]+)").Groups[1].Value);
        var person = factory.NewClient();
        var reset = await person.PostFormAsync(link, "/Account/ResetPassword", new()
        {
            ["Email"] = "efua.welcome@grel.test",
            ["Code"] = System.Web.HttpUtility.ParseQueryString(new Uri("https://localhost" + link).Query)["code"]!,
            ["Password"] = "Efua#Own2026!", ["ConfirmPassword"] = "Efua#Own2026!"
        });
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await person.SignInAsync("efua.welcome@grel.test", "Efua#Own2026!")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await person.GetAsync("/")).StatusCode); // not sent to "change password"

        static string Decode(string m)
        {
            if (!m.Contains("Content-Transfer-Encoding: base64")) return DecodeQuotedPrintable(m);
            var split = m.IndexOf("\r\n\r\n", StringComparison.Ordinal) is var i and >= 0 ? i : m.IndexOf("\n\n", StringComparison.Ordinal);
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(string.Concat(m[split..].Where(c => !char.IsWhiteSpace(c)))));
        }
    }

    [Fact]
    public async Task Candidate_registers_confirms_email_and_cannot_open_staff_pages()
    {
        var client = factory.NewClient();
        var register = await client.PostFormAsync("/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "Esi Candidate", ["Email"] = "esi@example.test",
            ["Password"] = "Esi#Pass2026", ["ConfirmPassword"] = "Esi#Pass2026"
        });
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);

        // Unconfirmed: sign-in is refused with a helpful message.
        var early = await (await client.SignInAsync("esi@example.test", "Esi#Pass2026")).Content.ReadAsStringAsync();
        Assert.Contains("confirm your email", early);

        // Open the confirmation link from the saved .eml file.
        var mail = Directory.GetFiles(factory.MailFolder, "*.eml")
            .Select(File.ReadAllText)
            .Single(m => m.Contains("esi@example.test"));
        var link = Regex.Match(DecodeQuotedPrintable(mail), "https://localhost(/Account/ConfirmEmail[^\"]+)").Groups[1].Value;
        var confirm = await client.GetStringAsync(System.Net.WebUtility.HtmlDecode(link));
        Assert.Contains("Email confirmed", confirm);

        var login = await client.SignInAsync("esi@example.test", "Esi#Pass2026");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("Welcome, Esi Candidate", await client.GetStringAsync("/"));
        Assert.Contains("/Account/AccessDenied", (await client.GetAsync("/Users")).Headers.Location!.ToString());
    }

    /// <summary>.eml files wrap long lines with "=" and encode "=" as "=3D"; undo that to read the link.</summary>
    private static string DecodeQuotedPrintable(string text) =>
        Regex.Replace(text.Replace("=\r\n", "").Replace("=\n", ""), "=([0-9A-F]{2})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
}
