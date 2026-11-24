using System.Net;
using Grsms.Domain.Settings;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.DoesNotContain("Coming: AI-", page); // 15 Nov 2026: every feature of docs/36 is built
        Assert.Matches("<option value=\"Gemini\"( selected=\"selected\")?>Google Gemini</option>", page); // 14 Nov 2026
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

    /// <summary>
    /// 23 Nov 2026: a new key pasted on the page and "Save and test" pressed straight away is saved first, then tested; before,
    /// the old saved key was tested and the new one was lost.
    /// </summary>
    [Fact]
    public async Task A_key_pasted_and_tested_straight_away_is_saved_and_used()
    {
        var admin = factory.NewClient();
        await admin.SignInAsync(GrsmsWebFactory.AdminEmail, GrsmsWebFactory.AdminPassword);
        Assert.Contains("Save and test the connection", await admin.GetStringAsync("/AiSettings"));

        await admin.PostFormAsync("/AiSettings", "/AiSettings/Test", new()
        {
            [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiProviderKey] = "Gemini", [SettingCatalogue.AiApiKey] = "AIza-new-key-456",
            [SettingCatalogue.AiModel] = "gemini-2.5-flash", [SettingCatalogue.AiTimeoutSeconds] = "30", [SettingCatalogue.AiCallsPerMinute] = "20"
        });

        var page = await admin.GetStringAsync("/AiSettings");
        Assert.Contains("Hello from the test", page);           // tested...
        Assert.Contains("<option value=\"Gemini\" selected=\"selected\">", page); // ...with what was on the screen
        Assert.DoesNotContain("AIza-new-key-456", page);         // and the key is still never shown

        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<Grsms.Application.Settings.ISettingsStore>();
        Assert.Equal("AIza-new-key-456", store.Get(SettingCatalogue.AiApiKey));
    }
}
