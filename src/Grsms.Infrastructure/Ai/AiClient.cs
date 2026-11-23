using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Grsms.Application.Ai;
using Grsms.Domain.Ai;

namespace Grsms.Infrastructure.Ai;

/// <summary>
/// 13 November 2026 (docs/36), rewritten 23 November 2026: sends one request to the provider chosen in AI settings.
///   * Anthropic (Claude): the official Anthropic SDK; structured output via output_config.format. The SDK retries busy errors itself.
///   * OpenAI and Azure OpenAI: the Chat Completions REST API (same body; Azure uses the deployment URL and an "api-key" header);
///     structured output via response_format json_schema (strict).
///   * Google Gemini (14 November 2026): the Gemini API generateContent REST call, key in the x-goog-api-key header (never in the
///     URL, so it can't end up in logs); structured output via generationConfig.responseJsonSchema.
/// <para>
/// Reliability (23 November 2026):
///   * A busy provider (429 rate limit, 500/502/503/504, "model overloaded") or a dropped connection is tried again up to
///     <see cref="MaxAttempts"/> times in all, waiting a little longer each time (or as long as the provider asks, up to
///     <see cref="MaxRetryWait"/>), but never beyond the time allowed in AI settings.
///   * Reasoning models (OpenAI GPT-5 and o-series, Gemini 2.5 and later) spend part of the output limit "thinking", so
///     <see cref="ReasoningAllowance"/> is added on top of each feature's limit; only tokens actually used are billed. If the model
///     says that limit is above its maximum, the request is sent once more with the feature's own limit.
///   * Quota or credit used up, an unknown model, or a refused key are not retried: retrying cannot help.
/// </para>
/// Every problem becomes an <see cref="AiProviderException"/> with its <see cref="AiFailure"/> kind and the provider's own reason,
/// safe to log and show (never the key).
/// </summary>
public sealed class AiClient : IAiClient
{
    /// <summary>One shared connection pool for the whole app; connections are renewed every few minutes so DNS changes are seen.</summary>
    private static readonly HttpClient Shared = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = Timeout.InfiniteTimeSpan // the time allowed comes from AI settings, per call
    };

    private readonly HttpClient _http;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="http">Tests pass their own.</param>
    /// <param name="delay">How to wait between attempts (tests pass one that does not wait).</param>
    public AiClient(HttpClient? http = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http ?? Shared;
        _delay = delay ?? Task.Delay;
    }

    public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    public const string OpenAiUrl = "https://api.openai.com/v1/chat/completions";

    /// <summary>Tokens added to each feature's output limit for the model's own reasoning (OpenAI and Gemini).</summary>
    public const int ReasoningAllowance = 8192;

    /// <summary>Kept for the existing callers and tests: the same allowance.</summary>
    public const int GeminiThinkingAllowance = ReasoningAllowance;

    /// <summary>Attempts in all for a busy provider (the first try and two more).</summary>
    public const int MaxAttempts = 3;

    /// <summary>The longest wait between attempts, even when the provider asks for longer.</summary>
    public static readonly TimeSpan MaxRetryWait = TimeSpan.FromSeconds(8);

    public async Task<AiReply> SendAsync(AiConnection connection, AiCall call, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(connection.Timeout);
        var deadline = DateTime.UtcNow + connection.Timeout;
        try
        {
            return connection.Provider == AiProvider.Anthropic
                ? await AnthropicAsync(connection, call, timeout.Token) // the SDK retries busy errors itself
                : await WithRetriesAsync(connection, call, deadline, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException($"No answer within {connection.Timeout.TotalSeconds:0} seconds.", AiFailure.Timeout);
        }
    }

    private async Task<AiReply> WithRetriesAsync(AiConnection c, AiCall call, DateTime deadline, CancellationToken ct)
    {
        var allowance = ReasoningAllowance;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return c.Provider == AiProvider.Gemini
                    ? await GeminiAsync(c, call, call.MaxOutputTokens + allowance, ct)
                    : await OpenAiAsync(c, call, call.MaxOutputTokens + allowance, ct);
            }
            catch (OutputLimitTooHighException ex)
            {
                if (allowance == 0) throw ex.Inner;
                allowance = 0; // an older model with a small output maximum: ask again with the feature's own limit
                attempt--;
            }
            catch (RetryableException ex) when (attempt < MaxAttempts)
            {
                var wait = ex.RetryAfter is { } asked && asked > TimeSpan.Zero
                    ? (asked > MaxRetryWait ? MaxRetryWait : asked)
                    : TimeSpan.FromSeconds(attempt == 1 ? 1 : 3);
                if (DateTime.UtcNow + wait >= deadline) throw ex.Inner; // no time left for another try
                await _delay(wait, ct);
            }
            catch (RetryableException ex)
            {
                throw ex.Inner;
            }
        }
    }

    // ===================================================================== Anthropic

    private static readonly Lock AnthropicLock = new();
    private static (string Key, TimeSpan Timeout, AnthropicClient Client)? _anthropic;

    /// <summary>One SDK client per key (it keeps its own connection pool), replaced when the key or time-out changes.</summary>
    private static AnthropicClient AnthropicFor(AiConnection c)
    {
        lock (AnthropicLock)
        {
            if (_anthropic is { } cached && cached.Key == c.ApiKey && cached.Timeout == c.Timeout) return cached.Client;
            var client = new AnthropicClient { ApiKey = c.ApiKey, Timeout = c.Timeout, MaxRetries = MaxAttempts - 1 };
            _anthropic = (c.ApiKey, c.Timeout, client);
            return client;
        }
    }

    private static async Task<AiReply> AnthropicAsync(AiConnection c, AiCall call, CancellationToken ct)
    {
        var client = AnthropicFor(c);
        Message response;
        try
        {
            var request = new MessageCreateParams
            {
                Model = c.Model,
                // Current Claude models always think first, within max_tokens: the same allowance as OpenAI and Gemini, so a short
                // answer (the connection test asks for 200 tokens) is not cut off. Only tokens used are billed.
                MaxTokens = call.MaxOutputTokens + ReasoningAllowance,
                System = call.Instructions,
                Messages = [new UserMessageParam { Content = AnthropicContent(call) }],
            };
            if (call.Schema is not null)
                request = request with
                {
                    OutputConfig = new OutputConfig
                    {
                        Format = new JsonOutputFormat { Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(call.Schema.ToJsonString())! }
                    }
                };
            response = await client.Messages.Create(request, ct);
        }
        // Most specific first.
        catch (AnthropicUnauthorizedException ex) { throw Fail("The API key was refused", 401, Reason(ex), AiFailure.KeyRefused, ex); }
        catch (AnthropicForbiddenException ex) { throw Fail("The API key was refused", 403, Reason(ex), AiFailure.KeyRefused, ex); }
        catch (AnthropicRateLimitException ex) { throw Fail("The provider's rate limit was reached", 429, Reason(ex), AiFailure.Busy, ex); }
        catch (AnthropicOverloadedException ex) { throw Fail("The provider is overloaded", 529, Reason(ex), AiFailure.Busy, ex); }
        catch (Anthropic5xxException ex) { throw Fail("The provider had a service error", null, Reason(ex), AiFailure.Busy, ex); }
        catch (AnthropicNotFoundException ex) { throw Fail("The model was not found; check the model name in AI settings", 404, Reason(ex), AiFailure.BadRequest, ex); }
        catch (AnthropicBadRequestException ex)
        {
            var reason = Reason(ex);
            throw IsQuota(reason)
                ? Fail("The provider account has no credit or quota left", 400, reason, AiFailure.Quota, ex)
                : Fail("The request was refused", 400, reason, AiFailure.BadRequest, ex);
        }
        catch (AnthropicIOException ex) { throw new AiProviderException("The provider could not be reached.", AiFailure.Unreachable, ex); }
        catch (AnthropicApiException ex) { throw Fail("The request was refused", null, Reason(ex), AiFailure.BadRequest, ex); }

        switch (response.StopReason)
        {
            case "end_turn" or "stop_sequence": break;
            case "max_tokens": throw new AiProviderException("The answer was too long and was cut off.", AiFailure.Other);
            case "refusal": throw new AiProviderException("The model declined to answer.", AiFailure.Other);
            default: throw new AiProviderException($"The answer stopped early ({response.StopReason}).", AiFailure.Other);
        }
        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return new AiReply(text, (int)response.Usage.InputTokens, (int)response.Usage.OutputTokens, c.Model);
    }

    private static string? Reason(AnthropicApiException ex) => Clean(ex is AnthropicServiceException s ? s.ApiMessage : null);

    // ===================================================================== OpenAI / Azure OpenAI

    private async Task<AiReply> OpenAiAsync(AiConnection c, AiCall call, int outputLimit, CancellationToken ct)
    {
        var azure = c.Provider == AiProvider.AzureOpenAI;
        var url = azure
            ? $"{c.AzureEndpoint!.Trim().TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(c.Model)}/chat/completions?api-version={Uri.EscapeDataString(c.AzureApiVersion)}"
            : OpenAiUrl;

        var body = new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = call.Instructions },
                new JsonObject { ["role"] = "user", ["content"] = OpenAiContent(call) }),
            ["max_completion_tokens"] = outputLimit
        };
        if (!azure) body["model"] = c.Model;
        if (call.Schema is not null)
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "answer", ["strict"] = true, ["schema"] = JsonNode.Parse(call.Schema.ToJsonString()) }
            };

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        if (azure) request.Headers.Add("api-key", c.ApiKey);
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", c.ApiKey);

        var json = await PostAsync(request, ct);
        var choice = (json?["choices"] as JsonArray)?.FirstOrDefault();
        if (choice is null) throw new AiProviderException("The provider gave no answer.", AiFailure.Other);
        if (Str(choice["message"]?["refusal"]) is { Length: > 0 } refusal) throw new AiProviderException($"The model declined: {refusal}", AiFailure.Other);
        switch (Str(choice["finish_reason"]))
        {
            case "length": throw new AiProviderException("The answer was too long and was cut off.", AiFailure.Other);
            case "content_filter": throw new AiProviderException("The provider's content filter stopped the answer.", AiFailure.Other);
        }
        return new AiReply(Str(choice["message"]?["content"]) ?? "",
            Int(json?["usage"]?["prompt_tokens"]), Int(json?["usage"]?["completion_tokens"]), Str(json?["model"]) ?? c.Model);
    }

    // ===================================================================== Google Gemini

    private async Task<AiReply> GeminiAsync(AiConnection c, AiCall call, int outputLimit, CancellationToken ct)
    {
        var model = c.Model.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? c.Model["models/".Length..] : c.Model;
        var generation = new JsonObject { ["maxOutputTokens"] = outputLimit };
        if (call.Schema is not null)
        {
            generation["responseMimeType"] = "application/json";
            generation["responseJsonSchema"] = JsonNode.Parse(call.Schema.ToJsonString());
        }
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = call.Instructions }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = GeminiParts(call) }),
            ["generationConfig"] = generation
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GeminiBaseUrl}{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", c.ApiKey);

        var json = await PostAsync(request, ct);
        if (Str(json?["promptFeedback"]?["blockReason"]) is { Length: > 0 } blocked)
            throw new AiProviderException($"The provider's safety filter stopped the request ({blocked}).", AiFailure.Other);
        var candidate = (json?["candidates"] as JsonArray)?.FirstOrDefault(); // an empty list must not throw
        if (candidate is null) throw new AiProviderException("The provider gave no answer.", AiFailure.Other);
        var finish = Str(candidate["finishReason"]);
        if (finish == "MAX_TOKENS") throw new AiProviderException("The answer was too long and was cut off.", AiFailure.Other);
        if (finish is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII" or "RECITATION" or "IMAGE_SAFETY")
            throw new AiProviderException($"The provider's content filter stopped the answer ({finish}).", AiFailure.Other);
        // Thought summaries (if any) are marked "thought": true and are not part of the answer.
        var text = string.Concat((candidate["content"]?["parts"] as JsonArray ?? [])
            .Where(p => p?["thought"] is not JsonValue t || !t.TryGetValue<bool>(out var isThought) || !isThought)
            .Select(p => Str(p?["text"]) ?? ""));
        var usage = json?["usageMetadata"];
        return new AiReply(text,
            Int(usage?["promptTokenCount"]),
            Int(usage?["candidatesTokenCount"]) + Int(usage?["thoughtsTokenCount"]), // thinking is billed as output
            Str(json?["modelVersion"]) ?? model);
    }

    // ===================================================================== Files with the input (docs/36 AI-8)
    // The files come first and the text after, as each provider recommends for questions about a picture or document.

    private static List<ContentBlockParam> AnthropicContent(AiCall call)
    {
        if (call.Files is not { Count: > 0 } files) return [new TextBlockParam { Text = call.Input }]; // the same as plain text
        var blocks = new List<ContentBlockParam>();
        foreach (var f in files)
            blocks.Add(f.IsPdf
                ? new DocumentBlockParam { Source = new Base64PdfSource { Data = f.Base64 } }
                : new ImageBlockParam { Source = new Base64ImageSource { Data = f.Base64, MediaType = f.MediaType == AiFile.Png ? MediaType.ImagePng : MediaType.ImageJpeg } });
        blocks.Add(new TextBlockParam { Text = call.Input });
        return blocks;
    }

    private static JsonNode OpenAiContent(AiCall call)
    {
        if (call.Files is not { Count: > 0 } files) return JsonValue.Create(call.Input)!;
        var parts = new JsonArray();
        foreach (var f in files)
            parts.Add(f.IsPdf
                ? new JsonObject { ["type"] = "file", ["file"] = new JsonObject { ["filename"] = f.FileName, ["file_data"] = $"data:{f.MediaType};base64,{f.Base64}" } }
                : new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = $"data:{f.MediaType};base64,{f.Base64}", ["detail"] = "high" } });
        parts.Add(new JsonObject { ["type"] = "text", ["text"] = call.Input });
        return parts;
    }

    private static JsonArray GeminiParts(AiCall call)
    {
        var parts = new JsonArray();
        foreach (var f in call.Files ?? [])
            parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = f.MediaType, ["data"] = f.Base64 } });
        parts.Add(new JsonObject { ["text"] = call.Input });
        return parts;
    }

    // ===================================================================== Shared HTTP

    /// <summary>A failure worth another try: the exception to throw if there are no tries left, and how long the provider asked to wait.</summary>
    private sealed class RetryableException(AiProviderException inner, TimeSpan? retryAfter) : Exception(inner.Message, inner)
    {
        public AiProviderException Inner { get; } = inner;
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }

    /// <summary>The model's output maximum is below the limit asked (older models): ask again without the reasoning allowance.</summary>
    private sealed class OutputLimitTooHighException(AiProviderException inner) : Exception(inner.Message, inner)
    {
        public AiProviderException Inner { get; } = inner;
    }

    /// <summary>Sends the request and returns the JSON answer, or throws a safe <see cref="AiProviderException"/>.</summary>
    private async Task<JsonNode?> PostAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new RetryableException(new AiProviderException("The provider could not be reached.", AiFailure.Unreachable, ex), null);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw Classify(response, ErrorMessage(text), text);
            try { return JsonNode.Parse(text); }
            catch (JsonException ex) { throw new AiProviderException("The provider's answer could not be read.", AiFailure.Other, ex); }
        }
    }

    /// <summary>Turns an error status and the provider's reason into the right exception (retried or not).</summary>
    private static Exception Classify(HttpResponseMessage response, string? reason, string body)
    {
        var quota = IsQuota(reason, body);
        var code = (int)response.StatusCode;
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                return Fail("The API key was refused", code, reason, AiFailure.KeyRefused);
            // Gemini answers a wrong or expired key with 400 "API key not valid" / "API key expired".
            case HttpStatusCode.BadRequest when reason?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true:
                return Fail("The API key was refused", code, reason, AiFailure.KeyRefused);
            case HttpStatusCode.BadRequest when IsOutputLimit(reason):
                return new OutputLimitTooHighException(Fail("The output limit is above this model's maximum", code, reason, AiFailure.BadRequest));
            case HttpStatusCode.NotFound:
                return Fail("The model was not found; check the model name (or Azure deployment) in AI settings", code, reason, AiFailure.BadRequest);
            case HttpStatusCode.TooManyRequests when quota:
                return Fail("The provider account's quota or credit is used up", code, reason, AiFailure.Quota);
            case HttpStatusCode.TooManyRequests:
                return new RetryableException(Fail("The provider's rate limit was reached", code, reason, AiFailure.Busy), RetryAfter(response));
            case >= HttpStatusCode.InternalServerError:
                return new RetryableException(Fail("The provider had a service error", code, reason, AiFailure.Busy), RetryAfter(response));
            default:
                return quota
                    ? Fail("The provider account's quota or credit is used up", code, reason, AiFailure.Quota)
                    : Fail("The request was refused", code, reason, AiFailure.BadRequest);
        }
    }

    private static AiProviderException Fail(string what, int? status, string? reason, AiFailure kind, Exception? inner = null) =>
        new($"{what}{(status is null ? "" : $" ({status})")}{(reason is null ? "" : $": {reason}")}.", kind, inner);

    /// <summary>
    /// The account limit is used up, not a passing rate limit: OpenAI "exceeded your current quota" (insufficient_quota), Gemini's
    /// daily quota (a "...PerDay..." quota id in the error details), Anthropic "credit balance is too low". Gemini says "You exceeded
    /// your current quota" for per-minute limits too, so its details decide: "...PerMinute..." is only busy.
    /// </summary>
    private static bool IsQuota(string? reason, string? body = null)
    {
        bool In(string? text, string s) => text?.Contains(s, StringComparison.OrdinalIgnoreCase) == true;
        if (In(body, "PerDay") || In(body, "insufficient_quota")) return true;
        if (In(body, "PerMinute") || In(reason, "per minute") || In(reason, "per min") || In(reason, "requests per") || In(reason, "tokens per")) return false;
        return In(reason, "quota") || In(reason, "credit balance") || In(reason, "billing") || In(reason, "spend limit") || In(reason, "spending limit");
    }

    private static bool IsOutputLimit(string? reason) =>
        reason is not null &&
        (reason.Contains("max_tokens", StringComparison.OrdinalIgnoreCase) || reason.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase)
         || reason.Contains("max_output_tokens", StringComparison.OrdinalIgnoreCase) || reason.Contains("maxOutputTokens", StringComparison.OrdinalIgnoreCase));

    private static TimeSpan? RetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter is { } r
            ? r.Delta ?? (r.Date is { } at ? at - DateTimeOffset.UtcNow : null)
            : null;

    /// <summary>The provider's own reason from the error body (OpenAI, Azure, Gemini and Anthropic all use error.message).</summary>
    private static string? ErrorMessage(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            if (node is JsonArray { Count: > 0 } list) node = list[0]; // Gemini sometimes wraps the error in a list
            return Clean(Str(node?["error"]?["message"]) ?? Str(node?["message"]));
        }
        catch (JsonException) { return null; } // an HTML error page from a proxy
    }

    /// <summary>One line, at most 200 characters, without the closing full stop (the caller adds its own).</summary>
    private static string? Clean(string? m)
    {
        if (string.IsNullOrWhiteSpace(m)) return null;
        m = m.ReplaceLineEndings(" ").Trim().TrimEnd('.');
        return m.Length > 200 ? m[..200] : m;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    private static int Int(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;
}
