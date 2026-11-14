using System.Net;
using System.Text.RegularExpressions;
using Grsms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grsms.Tests.Infrastructure;

/// <summary>
/// Starts the complete GRSMS website in memory (real Program.cs, controllers, views, Identity, filters),
/// with SQLite instead of MySQL and emails written to a temporary folder.
/// A first HR Administrator is created through the normal SeedAdmin start-up path.
/// </summary>
public sealed class GrsmsWebFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@grel.test";
    public const string AdminPassword = "Admin#Pass2026";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    public string MailFolder { get; } = Path.Combine(Path.GetTempPath(), "grsms-test-mail-" + Guid.NewGuid().ToString("N"));

    public GrsmsWebFactory()
    {
        _connection.Open();
        TestDatabase.CreateSchema(_connection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "unused-replaced-below");
        builder.UseSetting("SeedAdmin:Email", AdminEmail);
        builder.UseSetting("SeedAdmin:Password", AdminPassword);
        builder.UseSetting("SeedAdmin:FullName", "Test Admin");
        builder.UseSetting("Email:PickupDirectory", MailFolder);
        builder.UseSetting("Sms:PickupDirectory", Path.Combine(MailFolder, "sms"));
        // Every test client signs in from the same (empty) address; the limit itself is tested in SecurityWebTests.
        builder.UseSetting("Security:SignInAttemptsPerMinute", "10000");
        // Keep test logs and key files out of the source folder.
        builder.UseSetting("Logs:Folder", Path.Combine(Path.GetTempPath(), "grsms-test-logs"));
        builder.UseSetting("DataProtection:KeysFolder", Path.Combine(MailFolder, "keys"));
        builder.UseSetting("Storage:RootFolder", Path.Combine(MailFolder, "uploads"));
        // The test database is one shared in-memory connection, which must not be used by a
        // background job at the same time as the test. Automatic closing is tested directly instead.
        builder.UseSetting("Vacancies:AutoClose", "false");
        builder.UseSetting("Interviews:EnforceQuorum", "false"); // docs/35: tests that check quorum switch it on themselves

        builder.ConfigureTestServices(services =>
        {
            TestDatabase.UseSqlite(services, _connection);
            // 13 Nov 2026: no browser in the tests; the real renderer is tested on its own (ChromiumPdfRendererTests).
            services.RemoveAll<Grsms.Application.Common.Interfaces.IPdfRenderer>();
            services.AddSingleton<Grsms.Application.Common.Interfaces.IPdfRenderer>(Pdf);
            services.RemoveAll<Grsms.Application.Ai.IAiClient>(); // docs/36: never a real AI provider in tests
            services.AddSingleton<Grsms.Application.Ai.IAiClient>(Ai);
        });
    }

    /// <summary>The AI provider the site uses in tests.</summary>
    public FakeAiClient Ai { get; } = new();

    /// <summary>The PDF renderer the site uses in tests (records the last letter page).</summary>
    public FakePdfRenderer Pdf { get; } = new();

    /// <summary>
    /// An HTTPS client that keeps cookies but does NOT follow redirects, so tests can check where they point. It also checks
    /// every record page it opens for "one primary button per page" (docs/37 C6, <see cref="PrimaryActionCheck"/>).
    /// </summary>
    public HttpClient NewClient() =>
        CreateDefaultClient(new Uri("https://localhost"), new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(), new PrimaryActionCheck());

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        if (Directory.Exists(MailFolder)) Directory.Delete(MailFolder, recursive: true);
    }
}

/// <summary>Small helpers for driving forms the way a browser does.</summary>
public static partial class HttpTestExtensions
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    /// <summary>Opens <paramref name="formUrl"/>, reads its anti-forgery token, then posts the fields to <paramref name="postUrl"/>.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string formUrl, string postUrl, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(formUrl);
        var token = TokenRegex().Match(page);
        Assert.True(token.Success, $"No anti-forgery token found on {formUrl}");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string email, string password) =>
        client.PostFormAsync("/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
}
