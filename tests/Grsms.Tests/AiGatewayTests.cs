using Grsms.Application.Ai;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Security;
using Grsms.Domain.Settings;
using Grsms.Infrastructure.Persistence;
using Grsms.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-0: the AI gateway. Off until switched on; each feature has its own switch; requests per minute and the monthly
/// budget are enforced; answers of features without personal data are cached, others never; every call is logged (counts and
/// cost, not the prompt unless asked); provider failures become a message, never an exception; answers are checked against the
/// expected JSON shape.
/// </summary>
public class AiGatewayTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public AiGatewayTests() => _db.CurrentUser.SignInAs("hrm-1", "hrm@grel.test", Roles.HrManager);

    public void Dispose() => _db.Dispose();

    public sealed record Draft(string Title, List<string> Duties, int Years);

    private async Task SetAsync(Dictionary<string, string?> values)
    {
        using var scope = _db.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsStore>().SaveAsync(values);
    }

    private Task OnAsync(params AiFeature[] features) => SetAsync(new Dictionary<string, string?>
    {
        [SettingCatalogue.AiEnabled] = "true", [SettingCatalogue.AiProviderKey] = "Anthropic", [SettingCatalogue.AiApiKey] = "sk-test",
        [SettingCatalogue.AiInputPrice] = "3", [SettingCatalogue.AiOutputPrice] = "15"
    }.Concat(features.Select(f => KeyValuePair.Create(SettingCatalogue.AiFeatureKey(f), (string?)"true"))).ToDictionary());

    private async Task<AiResult<T>> RunAsync<T>(AiFeature feature, string input = "Process Engineer", bool cache = false) where T : class
    {
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<AiGateway>().RunAsync<T>(feature, "v1", "Draft a job.", input, ("Vacancy", 7), cache);
    }

    private async Task<List<AiRequestLog>> LogsAsync()
    {
        using var scope = _db.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AiRequestLogs.OrderBy(l => l.Id).ToListAsync();
    }

    private void Answer(string json, int input = 1000, int output = 500) => _db.Ai.Reply = _ => new AiReply(json, input, output, "claude-sonnet-5-5");

    [Fact]
    public async Task Nothing_is_sent_until_ai_and_the_feature_are_switched_on()
    {
        Assert.Contains("switched off", (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);
        await OnAsync();
        Assert.Contains("\"Job description drafts\" is switched off", (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);
        Assert.Empty(_db.Ai.Calls);

        // OpenAI needs a model name; Azure needs its endpoint too.
        await SetAsync(new() { [SettingCatalogue.AiFeatureKey(AiFeature.JobDescription)] = "true", [SettingCatalogue.AiProviderKey] = "AzureOpenAI", [SettingCatalogue.AiModel] = "gpt-dep" });
        Assert.Contains("incomplete", (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);
        Assert.Empty(_db.Ai.Calls);
    }

    [Fact]
    public async Task A_typed_answer_is_parsed_logged_with_cost_and_cached_only_for_features_without_personal_data()
    {
        await OnAsync(AiFeature.JobDescription, AiFeature.CvSummary);
        Answer("{\"title\":\"Process Engineer\",\"duties\":[\"Run the line\",\"Report\"],\"years\":5}");

        var first = await RunAsync<Draft>(AiFeature.JobDescription, cache: true);
        Assert.True(first.Succeeded);
        Assert.Equal(["Run the line", "Report"], first.Value!.Duties);
        // The JSON shape sent is strict: every property required, nothing else allowed.
        var schema = _db.Ai.Calls[0].Call.Schema!;
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(3, schema["required"]!.AsArray().Count);
        Assert.Equal("claude-sonnet-5-5", _db.Ai.Calls[0].Connection.Model); // default model for Anthropic

        var second = await RunAsync<Draft>(AiFeature.JobDescription, cache: true);
        Assert.True(second.FromCache);
        Assert.Single(_db.Ai.Calls);

        // Personal data is never cached, even when asked.
        await RunAsync<Draft>(AiFeature.CvSummary, cache: true);
        await RunAsync<Draft>(AiFeature.CvSummary, cache: true);
        Assert.Equal(3, _db.Ai.Calls.Count);

        var logs = await LogsAsync();
        Assert.Equal([AiOutcome.Success, AiOutcome.CacheHit, AiOutcome.Success, AiOutcome.Success], logs.Select(l => l.Outcome));
        Assert.Equal(0.0105m, logs[0].CostUsd); // 1,000 × 3 + 500 × 15 per million
        Assert.Equal(("Vacancy", 7, "hrm-1"), (logs[0].RecordType!, logs[0].RecordId!.Value, logs[0].UserId!));
        Assert.All(logs, l => Assert.Null(l.Prompt)); // prompts are not kept by default
    }

    [Fact]
    public async Task Failures_become_messages_and_are_logged()
    {
        await OnAsync(AiFeature.JobDescription);
        _db.Ai.Reply = _ => throw new AiProviderException("The API key was refused.", keyRefused: true);
        Assert.Contains("refused the API key", (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);

        _db.Ai.Reply = _ => throw new AiProviderException("The provider could not be reached.");
        Assert.Equal(AiGateway.Unavailable, (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);

        Answer("not json at all");
        Assert.Equal(AiGateway.Unavailable, (await RunAsync<Draft>(AiFeature.JobDescription)).Problem);

        // A code fence around the JSON is tolerated.
        Answer("```json\n{\"title\":\"T\",\"duties\":[],\"years\":1}\n```");
        Assert.True((await RunAsync<Draft>(AiFeature.JobDescription)).Succeeded);

        Assert.Equal([AiOutcome.Failed, AiOutcome.Failed, AiOutcome.BadAnswer, AiOutcome.Success], (await LogsAsync()).Select(l => l.Outcome));
    }

    [Fact]
    public async Task Requests_per_minute_and_the_monthly_budget_are_enforced_and_prompts_kept_only_when_asked()
    {
        await OnAsync(AiFeature.JobDescription);
        await SetAsync(new() { [SettingCatalogue.AiCallsPerMinute] = "2", [SettingCatalogue.AiKeepPrompts] = "true" });
        Answer("{\"title\":\"T\",\"duties\":[],\"years\":1}");
        Assert.True((await RunAsync<Draft>(AiFeature.JobDescription, "a")).Succeeded);
        Assert.True((await RunAsync<Draft>(AiFeature.JobDescription, "b")).Succeeded);
        Assert.Contains("Too many AI requests", (await RunAsync<Draft>(AiFeature.JobDescription, "c")).Problem);
        Assert.Contains("Draft a job.", (await LogsAsync())[0].Prompt);

        // Budget: USD 1 a month; one answer of a million output tokens costs USD 15.
        await SetAsync(new() { [SettingCatalogue.AiCallsPerMinute] = "100", [SettingCatalogue.AiMonthlyBudgetUsd] = "1" });
        Answer("{\"title\":\"T\",\"duties\":[],\"years\":1}", 0, 1_000_000);
        Assert.True((await RunAsync<Draft>(AiFeature.JobDescription, "d")).Succeeded);
        Assert.Contains("budget has been reached", (await RunAsync<Draft>(AiFeature.JobDescription, "e")).Problem);
        Assert.Equal(AiOutcome.BudgetReached, (await LogsAsync())[^1].Outcome);

        // Purging clears kept prompts older than 30 days.
        using var scope = _db.NewScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IAiLogStore>().PurgeAsync(DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow) > 0);
        Assert.All(await LogsAsync(), l => Assert.Null(l.Prompt));
    }

    [Fact]
    public async Task The_connection_test_needs_the_super_admin_and_reports_the_reply()
    {
        await OnAsync();
        using (var scope = _db.NewScope())
            Assert.False((await scope.ServiceProvider.GetRequiredService<AiAdminService>().TestAsync()).Succeeded); // HR Manager

        _db.CurrentUser.SignInAs("sa-1", "sa@grel.test", Roles.SuperAdmin);
        using (var scope = _db.NewScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<AiAdminService>();
            var result = await admin.TestAsync();
            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            Assert.Contains("Hello from the test", result.Value);

            // Saving checks the provider's needs.
            Assert.False((await admin.SaveServiceAsync(new Dictionary<string, string?> { [SettingCatalogue.AiProviderKey] = "OpenAI" })).Succeeded); // no model
            Assert.False((await admin.SaveServiceAsync(new Dictionary<string, string?>
                { [SettingCatalogue.AiProviderKey] = "AzureOpenAI", [SettingCatalogue.AiModel] = "d", [SettingCatalogue.AiAzureEndpoint] = "http://x" })).Succeeded);
            Assert.False((await admin.SaveServiceAsync(new Dictionary<string, string?>
                { [SettingCatalogue.AiProviderKey] = "Anthropic", [SettingCatalogue.AiInputPrice] = "three" })).Succeeded);
            Assert.True((await admin.SaveServiceAsync(new Dictionary<string, string?>
                { [SettingCatalogue.AiProviderKey] = "Anthropic", [SettingCatalogue.AiEnabled] = "true" })).Succeeded);
        }
        // The key was not posted, so it was kept.
        using (var scope = _db.NewScope())
            Assert.Equal("sk-test", scope.ServiceProvider.GetRequiredService<ISettingsStore>().Get(SettingCatalogue.AiApiKey));
    }
}
