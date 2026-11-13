using System.Text.Json.Nodes;
using Grsms.Domain.Ai;

namespace Grsms.Application.Ai;

/// <summary>Where and how to reach the AI provider, read from the settings for each call (so a change applies at once).</summary>
public sealed record AiConnection(AiProvider Provider, string ApiKey, string Model, string? AzureEndpoint, string AzureApiVersion, TimeSpan Timeout);

/// <summary>
/// One request. <see cref="Instructions"/> is the system prompt (fixed per feature and version); <see cref="Input"/> is the data,
/// which the instructions tell the model to treat as data only. <see cref="Schema"/> (a JSON Schema object) asks for a JSON
/// answer of exactly that shape; null = plain text.
/// </summary>
public sealed record AiCall(string Instructions, string Input, JsonObject? Schema, int MaxOutputTokens = 4000);

public sealed record AiReply(string Text, int InputTokens, int OutputTokens, string Model);

/// <summary>Why a call failed, in words safe to show (never the key).</summary>
public sealed class AiProviderException(string message, bool keyRefused = false, Exception? inner = null) : Exception(message, inner)
{
    public bool KeyRefused { get; } = keyRefused;
}

/// <summary>
/// 13 November 2026 (docs/36): one call to the chosen provider (Infrastructure/Ai/AiClient: Anthropic SDK, or the OpenAI /
/// Azure OpenAI REST API). Features never use this directly: they go through <see cref="AiGateway"/>, which adds the switches,
/// budget, cache, rate limit and log.
/// </summary>
public interface IAiClient
{
    /// <exception cref="AiProviderException">The provider could not be reached, refused the key or the request, or stopped early.</exception>
    Task<AiReply> SendAsync(AiConnection connection, AiCall call, CancellationToken ct = default);
}

/// <summary>What a feature gets back from the gateway: the answer, or why there is none (shown to the user as is).</summary>
public sealed record AiResult<T>(T? Value, string? Problem, bool FromCache = false, int? LogId = null)
{
    public bool Succeeded => Value is not null && Problem is null;
    public static AiResult<T> Fail(string problem, int? logId = null) => new(default, problem, LogId: logId);
}
