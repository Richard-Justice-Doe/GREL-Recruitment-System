using System.Net;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>docs/36 step AI-0 through the pages: Administration → AI settings &amp; usage (save, key kept secret, test, usage).</summary>
public class AiSettingsWebTests(GrsmsWebFactory factory) : IClassFixture<GrsmsWebFactory>
{
    [Fact]
    public async Task Super_admin_sets_up_the_provider_tests_it_and_sees_the_usage()
    {
        var admin = factory.NewClient();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword)).StatusCode);
        Assert.Contains("/AiSettings", await admin.GetStringAsync("/Administration"));
        var page = await admin.GetStringAsync("/AiSettings");
        Assert.Contains("AI settings &amp; usage", page);
        Assert.Contains("Coming: AI-1", page); // features not built yet can't be switched on
        Assert.DoesNotContain("Monthly budget", await admin.GetStringAsync("/SystemSettings")); // own page, not System settings

        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/AiSettings", "/AiSettings/SaveService", new()
        {
            [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiProviderKey] = "Anthropic", [SettingCatalogue.AiApiKey] = "sk-ant-secret-123",
            [SettingCatalogue.AiModel] = "claude-sonnet-5-5", [SettingCatalogue.AiTimeoutSeconds] = "30", [SettingCatalogue.AiMonthlyBudgetUsd] = "25",
            [SettingCatalogue.AiInputPrice] = "3", [SettingCatalogue.AiOutputPrice] = "15", [SettingCatalogue.AiCallsPerMinute] = "20"
        })).StatusCode);
        page = await admin.GetStringAsync("/AiSettings");
        Assert.DoesNotContain("sk-ant-secret-123", page); // never shown again
        Assert.Contains("(set; type to replace)", page);

        await admin.PostFormAsync("/AiSettings", "/AiSettings/Test", new());
        page = await admin.GetStringAsync("/AiSettings");
        Assert.Contains("Hello from the test", page);  // the success message
        Assert.Contains("Connection test", page);      // in the usage table
        Assert.Contains("of USD 25", page);            // budget gauge
    }
}
