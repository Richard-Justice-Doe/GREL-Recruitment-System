using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Grsms.Application.Common.Interfaces;
using Grsms.Application.Settings;
using Grsms.Domain.Ai;
using Grsms.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace Grsms.Application.Ai;

/// <summary>
/// The AI log and cache, written in their own database scope so that logging a call never saves (or is rolled back with)
/// the changes of the feature that made it. Implemented in Infrastructure/Ai/AiLogStore.
/// </summary>
public interface IAiLogStore
{
    Task<int> AddAsync(AiRequestLog log, CancellationToken ct = default);
    Task<decimal> CostSinceAsync(DateTime since, CancellationToken ct = default);
    Task<int> CallsSinceAsync(string userId, DateTime since, CancellationToken ct = default);
    Task<string?> CachedAsync(string key, DateTime now, CancellationToken ct = default);
    Task SaveCacheAsync(AiCacheEntry entry, CancellationToken ct = default);

    /// <summary>The usage page: totals per feature this month and the latest calls.</summary>
    Task<AiUsageDto> UsageAsync(DateTime monthStart, int latest, CancellationToken ct = default);

    /// <summary>Daily clean-up: prompts kept for troubleshooting after 30 days, expired cache entries.</summary>
    Task<int> PurgeAsync(DateTime promptsBefore, DateTime now, CancellationToken ct = default);
}

public sealed record AiUsageFeatureDto(AiFeature Feature, int Calls, int Failed, int InputTokens, int OutputTokens, decimal CostUsd);
public sealed record AiUsageCallDto(int Id, DateTime At, AiFeature Feature, string? UserId, string Model, AiOutcome Outcome, int InputTokens,
    int OutputTokens, decimal CostUsd, int DurationMs, string? Error, string? RecordType, int? RecordId);
public sealed record AiUsageDto(IReadOnlyList<AiUsageFeatureDto> Features, IReadOnlyList<AiUsageCallDto> Latest)
{
    public decimal TotalCostUsd => Features.Sum(f => f.CostUsd);
    public int TotalCalls => Features.Sum(f => f.Calls);
}

/// <summary>
/// 13 November 2026 (docs/36 §3): every AI feature calls the provider through here. In order, it:
///   1. checks the main switch, the feature's switch and that a provider and key are set (else: "AI is switched off");
///   2. checks the person's requests per minute and the month's budget;
///   3. answers from the cache when allowed (features that send no personal data only);
///   4. calls the provider with the feature's instructions, the data, and the JSON shape of <typeparamref name="T"/>;
///   5. logs counts, cost, time and outcome (never the key; the prompt only if "Keep prompts" is on).
/// Every failure becomes a short message for the page; nothing throws, so a feature's page always keeps working.
/// AI only drafts: callers show the result for a person to accept.
/// </summary>
public class AiGateway(IAiClient client, IAiLogStore store, ISettingsStore settings, ICurrentUserService me, ILogger<AiGateway> logger)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
        RespectNullableAnnotations = true,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() // needed by the schema export
    };

    /// <summary>The AI log's Error column (AiConfigurations: 300); the page shows the full reason.</summary>
    private const int MaxLogError = 300;

    public const string Unavailable ="AI is not available right now. You can write it yourself, or try again later.";

    /// <summary>The main switch is on and a key is set (does not look at feature switches).</summary>
    public bool IsConfigured => settings.GetBool(SettingCatalogue.AiEnabled) && !string.IsNullOrWhiteSpace(settings.Get(SettingCatalogue.AiApiKey));

    /// <summary>The feature may be used now: main switch, key, and the feature's own switch.</summary>
    public bool IsOn(AiFeature feature) =>
        IsConfigured && (feature == AiFeature.ConnectionTest || settings.GetBool(SettingCatalogue.AiFeatureKey(feature)));

    public async Task<AiResult<T>> RunAsync<T>(AiFeature feature, string promptVersion, string instructions, string input,
        (string Type, int Id)? record = null, bool useCache = false, int maxOutputTokens = 4000, CancellationToken ct = default,
        IReadOnlyList<AiFile>? files = null) where T : class
    {
        var now = DateTime.UtcNow;
        var log = new AiRequestLog
        {
            Feature = feature, UserId = me.UserId, PromptVersion = promptVersion, RecordType = record?.Type, RecordId = record?.Id, CreatedAt = now,
            CreatedBy = me.UserName
        };

        if (!IsOn(feature))
            return AiResult<T>.Fail(IsConfigured ? $"\"{AiFeatures.Label(feature)}\" is switched off in AI settings." : "AI is switched off in AI settings.");
        var connection = Connection();
        if (connection is null) return AiResult<T>.Fail("AI settings are incomplete (provider, key, model or endpoint).");
        (log.Provider, log.Model) = (connection.Provider, connection.Model);

        if (me.UserId is { } userId &&
            await store.CallsSinceAsync(userId, now.AddMinutes(-1), ct) >= Math.Max(1, settings.GetInt(SettingCatalogue.AiCallsPerMinute)))
            return AiResult<T>.Fail("Too many AI requests in a minute. Wait a moment and try again.", await LogAsync(log, AiOutcome.RateLimited, null, ct));

        var budget = settings.GetInt(SettingCatalogue.AiMonthlyBudgetUsd);
        if (budget > 0 && Prices() is not null &&
            await store.CostSinceAsync(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), ct) >= budget)
            return AiResult<T>.Fail("This month's AI budget has been reached. A Super Admin can raise it in AI settings.",
                await LogAsync(log, AiOutcome.BudgetReached, null, ct));

        // Only features that send no personal data are cached (generators, totals).
        var cacheable = useCache && !AiFeatures.SendsPersonalData(feature) && files is not { Count: > 0 };
        var key = Hash($"{feature}|{promptVersion}|{connection.Provider}|{connection.Model}|{instructions}|{input}");
        if (cacheable && await store.CachedAsync(key, now, ct) is { } cached && Parse<T>(cached) is { } fromCache)
        {
            var id = await LogAsync(log, AiOutcome.CacheHit, null, ct);
            return new AiResult<T>(fromCache, null, FromCache: true, LogId: id);
        }

        var isText = typeof(T) == typeof(string);
        var call = new AiCall(instructions, input, isText ? null : SchemaOf<T>(), maxOutputTokens, files);
        if (settings.GetBool(SettingCatalogue.AiKeepPrompts)) log.Prompt = $"{instructions}\n\n---\n\n{input}";

        var watch = Stopwatch.StartNew();
        AiReply reply;
        try
        {
            reply = await client.SendAsync(connection, call, ct);
        }
        catch (AiProviderException ex)
        {
            log.DurationMs = (int)watch.ElapsedMilliseconds;
            logger.LogWarning(ex, "AI {Feature} ({Provider} {Model}): {Message}", feature, connection.Provider, connection.Model, ex.Message);
            var id = await LogAsync(log, AiOutcome.Failed, ex.Message, ct);
            return AiResult<T>.Fail(Explain(ex), id);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            log.DurationMs = (int)watch.ElapsedMilliseconds;
            var id = await LogAsync(log, AiOutcome.Failed, "Timed out", ct);
            return AiResult<T>.Fail(Explain(new AiProviderException("Timed out.", AiFailure.Timeout)), id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing may break the page: an unexpected problem (e.g. an odd answer the SDK can't read) is logged and explained.
            log.DurationMs = (int)watch.ElapsedMilliseconds;
            logger.LogError(ex, "AI {Feature}: unexpected failure", feature);
            var id = await LogAsync(log, AiOutcome.Failed, $"Unexpected: {ex.GetType().Name}: {ex.Message}", ct);
            return AiResult<T>.Fail(Unavailable, id);
        }

        log.DurationMs = (int)watch.ElapsedMilliseconds;
        (log.InputTokens, log.OutputTokens, log.Model) = (reply.InputTokens, reply.OutputTokens, string.IsNullOrEmpty(reply.Model) ? log.Model : reply.Model);
        log.CostUsd = Cost(reply.InputTokens, reply.OutputTokens);

        var value = isText ? (T?)(object)reply.Text.Trim() : Parse<T>(reply.Text);
        if (value is null || (isText && string.IsNullOrWhiteSpace((string)(object)value)))
        {
            var id = await LogAsync(log, AiOutcome.BadAnswer, "The answer was not in the expected shape", ct);
            return AiResult<T>.Fail(Unavailable, id);
        }

        var logId = await LogAsync(log, AiOutcome.Success, null, ct);
        if (cacheable)
            await store.SaveCacheAsync(new AiCacheEntry { Key = key, Feature = feature, Output = reply.Text, ExpiresAt = now.AddDays(7), CreatedAt = now }, ct);
        return new AiResult<T>(value, null, LogId: logId);
    }

    /// <summary>
    /// 23 November 2026: what the person sees for each kind of failure: what happened (with the provider's own reason, which never
    /// contains the key) and what to do. Everyone can still write the text themselves.
    /// </summary>
    public static string Explain(AiProviderException ex) => ex.Kind switch
    {
        AiFailure.KeyRefused => $"The AI provider refused the API key. A Super Admin should check AI settings. (Provider said: {ex.Message})",
        AiFailure.Busy => $"The AI service is busy right now. Try again in a minute; if it keeps happening, a Super Admin can choose another model in AI settings. (Provider said: {ex.Message})",
        AiFailure.Quota => $"The AI provider account's usage limit or credit is used up. A Super Admin should check the provider account (billing and quotas). (Provider said: {ex.Message})",
        AiFailure.Timeout => "The AI service took too long to answer. Try again; if it keeps happening, a Super Admin can raise \"Wait at most\" or choose a faster model in AI settings.",
        AiFailure.Unreachable => "The server could not reach the AI provider (internet connection, firewall or proxy). Ask IT to check that the server can reach the provider's website.",
        AiFailure.BadRequest => $"The AI provider refused the request; a Super Admin should check the model name in AI settings. (Provider said: {ex.Message})",
        _ => Unavailable
    };

    /// <summary>The connection from the settings, or null when something needed is missing.</summary>
    public AiConnection? Connection()
    {
        if (!Enum.TryParse<AiProvider>(settings.Get(SettingCatalogue.AiProviderKey), ignoreCase: true, out var provider)) return null;
        var key = settings.Get(SettingCatalogue.AiApiKey);
        var model = settings.Get(SettingCatalogue.AiModel)?.Trim();
        if (string.IsNullOrWhiteSpace(model) && provider == AiProvider.Anthropic) model = "claude-sonnet-5-5";
        var endpoint = settings.Get(SettingCatalogue.AiAzureEndpoint)?.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model)) return null;
        if (provider == AiProvider.AzureOpenAI && (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out _))) return null;
        var apiVersion = settings.Get(SettingCatalogue.AiAzureApiVersion)?.Trim();
        return new AiConnection(provider, key.Trim(), model, endpoint, string.IsNullOrEmpty(apiVersion) ? "2024-10-21" : apiVersion,
            TimeSpan.FromSeconds(Math.Clamp(settings.GetInt(SettingCatalogue.AiTimeoutSeconds), 5, 300)));
    }

    /// <summary>Prices per million tokens (input, output), or null when not both set.</summary>
    public (decimal Input, decimal Output)? Prices() =>
        decimal.TryParse(settings.Get(SettingCatalogue.AiInputPrice), NumberStyles.Number, CultureInfo.InvariantCulture, out var i) &&
        decimal.TryParse(settings.Get(SettingCatalogue.AiOutputPrice), NumberStyles.Number, CultureInfo.InvariantCulture, out var o) && i >= 0 && o >= 0
            ? (i, o) : null;

    public decimal Cost(int inputTokens, int outputTokens) =>
        Prices() is { } p ? Math.Round((inputTokens * p.Input + outputTokens * p.Output) / 1_000_000m, 6) : 0m;

    private async Task<int?> LogAsync(AiRequestLog log, AiOutcome outcome, string? error, CancellationToken ct)
    {
        log.Outcome = outcome;
        log.Error = error is null ? null : error.Length > MaxLogError ? error[..(MaxLogError - 1)] + "…" : error;
        try
        {
            return await store.AddAsync(log, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AI log could not be written");
            return null;
        }
    }

    public static T? Parse<T>(string text) where T : class
    {
        if (typeof(T) == typeof(string)) return (T)(object)text;
        var json = text.Trim();
        // Some models wrap JSON in a code fence despite the schema.
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('\n');
            var end = json.LastIndexOf("```", StringComparison.Ordinal);
            if (start > 0 && end > start) json = json[(start + 1)..end];
        }
        if (TryDeserialize<T>(json) is { } value) return value;
        // ...or put a sentence before or after it: try the outermost {...}.
        var first = json.IndexOf('{');
        var last = json.LastIndexOf('}');
        var hasExtraText = first > 0 || last < json.Length - 1;
        return first >= 0 && last > first && hasExtraText ? TryDeserialize<T>(json[first..(last + 1)]) : null;
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// The JSON Schema of <typeparamref name="T"/> in the strict form providers accept for structured output: every object lists
    /// all its properties as required and allows no others. Answer types use only strings, numbers, booleans, enums, lists and
    /// nested records (no nullable members).
    /// </summary>
    public static JsonObject SchemaOf<T>()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(Json, typeof(T), new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        Strict(node);
        return node.AsObject();
    }

    private static void Strict(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("$schema");
                if (obj["properties"] is JsonObject props)
                {
                    obj["additionalProperties"] = false;
                    obj["required"] = new JsonArray(props.Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray());
                }
                foreach (var child in obj.Select(p => p.Value).ToList()) Strict(child);
                break;
            case JsonArray arr:
                foreach (var child in arr) Strict(child);
                break;
        }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
