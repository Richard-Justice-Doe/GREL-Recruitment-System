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
/// <param name="Files">docs/36 AI-8: pictures (JPEG, PNG) or PDFs sent with the input, e.g. a scanned ID document. Never cached.</param>
public sealed record AiCall(string Instructions, string Input, JsonObject? Schema, int MaxOutputTokens = 4000, IReadOnlyList<AiFile>? Files = null);

/// <summary>A file sent with a request: its bytes, its type ("image/jpeg", "image/png" or "application/pdf") and its name.</summary>
public sealed record AiFile(byte[] Content, string MediaType, string FileName)
{
    public const string Jpeg = "image/jpeg", Png = "image/png", Pdf = "application/pdf";
    public bool IsPdf => MediaType == Pdf;
    public string Base64 => Convert.ToBase64String(Content);
}

public sealed record AiReply(string Text, int InputTokens, int OutputTokens, string Model);

/// <summary>
/// 23 November 2026: what kind of failure it was, so the page can say what to do about it.
/// </summary>
public enum AiFailure
{
    /// <summary>Anything else (the answer was cut off, filtered, empty or unreadable).</summary>
    Other,
    /// <summary>401/403, or Gemini's 400 "API key not valid": the Super Admin must fix the key.</summary>
    KeyRefused,
    /// <summary>The provider is overloaded or had a passing error (429 rate limit, 500/502/503/504, Anthropic 529). Retried first.</summary>
    Busy,
    /// <summary>The account's quota, credit or spending limit is used up: retrying does not help.</summary>
    Quota,
    /// <summary>No answer within the time allowed in AI settings.</summary>
    Timeout,
    /// <summary>The provider could not be reached at all (DNS, firewall, no internet on the server).</summary>
    Unreachable,
    /// <summary>The provider refused the request itself (e.g. an unknown model name): a settings problem.</summary>
    BadRequest
}

/// <summary>Why a call failed, in words safe to show (never the key). The provider's own reason is kept in the message.</summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(string message, AiFailure kind, Exception? inner = null) : base(message, inner) => Kind = kind;

    public AiProviderException(string message, bool keyRefused = false, Exception? inner = null)
        : this(message, keyRefused ? AiFailure.KeyRefused : AiFailure.Other, inner) { }

    public AiFailure Kind { get; }
    public bool KeyRefused => Kind == AiFailure.KeyRefused;

    /// <summary>Worth trying again after a short wait.</summary>
    public bool Transient => Kind is AiFailure.Busy or AiFailure.Unreachable;
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
