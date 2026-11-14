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
/// 13 November 2026 (docs/36): sends one request to the provider chosen in AI settings.
///   * Anthropic (Claude): the official Anthropic SDK already used by the CV reader; structured output via output_config.format.
///   * OpenAI and Azure OpenAI: the Chat Completions REST API (same body; Azure uses the deployment URL and an "api-key" header);
///     structured output via response_format json_schema (strict).
///   * Google Gemini (14 November 2026): the Gemini API generateContent REST call, key in the x-goog-api-key header (never in the
///     URL, so it can't end up in logs); structured output via generationConfig.responseJsonSchema.
/// Every problem becomes an <see cref="AiProviderException"/> with a message safe to log (never the key).
/// </summary>
public sealed class AiClient(HttpClient? http = null) : IAiClient
{
    private static readonly HttpClient Shared = new() { Timeout = Timeout.InfiniteTimeSpan }; // per-call time-out below
    private readonly HttpClient _http = http ?? Shared; // tests pass their own

    public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    /// <summary>
    /// Gemini's "thinking" models count their reasoning against the output limit, so a short answer could be cut off before it
    /// starts. This much is added on top of the feature's own limit (only tokens actually used are billed).
    /// </summary>
    public const int GeminiThinkingAllowance = 8192;

    public async Task<AiReply> SendAsync(AiConnection connection, AiCall call, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(connection.Timeout);
        return connection.Provider switch
        {
            AiProvider.Anthropic => await AnthropicAsync(connection, call, timeout.Token),
            AiProvider.Gemini => await GeminiAsync(connection, call, timeout.Token),
            _ => await OpenAiAsync(connection, call, timeout.Token)
        };
    }

    // ===================================================================== Anthropic

    private static async Task<AiReply> AnthropicAsync(AiConnection c, AiCall call, CancellationToken ct)
    {
        AnthropicClient client = new() { ApiKey = c.ApiKey, Timeout = c.Timeout };
        try
        {
            var request = new MessageCreateParams
            {
                Model = c.Model,
                MaxTokens = call.MaxOutputTokens,
                System = call.Instructions,
                Messages = [new UserMessageParam { Content = call.Input }],
            };
            if (call.Schema is not null)
                request = request with
                {
                    OutputConfig = new OutputConfig
                    {
                        Format = new JsonOutputFormat { Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(call.Schema.ToJsonString())! }
                    }
                };
            var response = await client.Messages.Create(request, ct);
            if (response.StopReason != "end_turn")
                throw new AiProviderException(response.StopReason == "max_tokens" ? "The answer was too long and was cut off." : $"Stopped early ({response.StopReason}).");
            var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
            return new AiReply(text, (int)response.Usage.InputTokens, (int)response.Usage.OutputTokens, c.Model);
        }
        catch (AnthropicUnauthorizedException ex) { throw new AiProviderException("The API key was refused.", keyRefused: true, ex); }
        catch (AnthropicRateLimitException ex) { throw new AiProviderException("The provider's rate limit was reached.", inner: ex); }
        catch (Anthropic5xxException ex) { throw new AiProviderException("The provider had a service error.", inner: ex); }
        catch (AnthropicIOException ex) { throw new AiProviderException("The provider could not be reached.", inner: ex); }
        catch (AnthropicApiException ex) { throw new AiProviderException($"The request was refused: {ex.Message}", inner: ex); }
    }

    // ===================================================================== OpenAI / Azure OpenAI

    private async Task<AiReply> OpenAiAsync(AiConnection c, AiCall call, CancellationToken ct)
    {
        var azure = c.Provider == AiProvider.AzureOpenAI;
        var url = azure
            ? $"{c.AzureEndpoint!.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(c.Model)}/chat/completions?api-version={Uri.EscapeDataString(c.AzureApiVersion)}"
            : "https://api.openai.com/v1/chat/completions";

        var body = new JsonObject
        {
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = call.Instructions },
                new JsonObject { ["role"] = "user", ["content"] = call.Input }),
            ["max_completion_tokens"] = call.MaxOutputTokens
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
        var finish = choice?["finish_reason"]?.GetValue<string>();
        if (choice?["message"]?["refusal"]?.GetValue<string>() is { Length: > 0 } refusal) throw new AiProviderException($"The model declined: {refusal}");
        if (finish == "length") throw new AiProviderException("The answer was too long and was cut off.");
        if (finish == "content_filter") throw new AiProviderException("The provider's content filter stopped the answer.");
        if (choice is null) throw new AiProviderException("The provider gave no answer.");
        var content = choice?["message"]?["content"]?.GetValue<string>() ?? "";
        return new AiReply(content,
            json?["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
            json?["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0,
            json?["model"]?.GetValue<string>() ?? c.Model);
    }

    // ===================================================================== Google Gemini

    private async Task<AiReply> GeminiAsync(AiConnection c, AiCall call, CancellationToken ct)
    {
        var model = c.Model.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? c.Model["models/".Length..] : c.Model;
        var generation = new JsonObject { ["maxOutputTokens"] = call.MaxOutputTokens + GeminiThinkingAllowance };
        if (call.Schema is not null)
        {
            generation["responseMimeType"] = "application/json";
            generation["responseJsonSchema"] = JsonNode.Parse(call.Schema.ToJsonString());
        }
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = call.Instructions }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = call.Input }) }),
            ["generationConfig"] = generation
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GeminiBaseUrl}{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", c.ApiKey);

        var json = await PostAsync(request, ct);
        if (json?["promptFeedback"]?["blockReason"]?.GetValue<string>() is { Length: > 0 } blocked)
            throw new AiProviderException($"The provider's safety filter stopped the request ({blocked}).");
        var candidate = (json?["candidates"] as JsonArray)?.FirstOrDefault(); // an empty list must not throw
        var finish = candidate?["finishReason"]?.GetValue<string>();
        if (finish == "MAX_TOKENS") throw new AiProviderException("The answer was too long and was cut off.");
        if (finish is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII" or "RECITATION")
            throw new AiProviderException($"The provider's content filter stopped the answer ({finish}).");
        if (candidate is null) throw new AiProviderException("The provider gave no answer.");
        // Thought summaries (if any) are marked "thought": true and are not part of the answer.
        var text = string.Concat((candidate["content"]?["parts"] as JsonArray ?? [])
            .Where(p => p?["thought"]?.GetValue<bool>() != true)
            .Select(p => p?["text"]?.GetValue<string>() ?? ""));
        var usage = json?["usageMetadata"];
        return new AiReply(text,
            usage?["promptTokenCount"]?.GetValue<int>() ?? 0,
            (usage?["candidatesTokenCount"]?.GetValue<int>() ?? 0) + (usage?["thoughtsTokenCount"]?.GetValue<int>() ?? 0), // thinking is billed as output
            json?["modelVersion"]?.GetValue<string>() ?? model);
    }

    // ===================================================================== Shared HTTP

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
            throw new AiProviderException("The provider could not be reached.", inner: ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var message = ErrorMessage(text);
                throw response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AiProviderException("The API key was refused.", keyRefused: true),
                    // Gemini answers a wrong key with 400 "API key not valid".
                    HttpStatusCode.BadRequest when message?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true
                        => new AiProviderException("The API key was refused.", keyRefused: true),
                    HttpStatusCode.TooManyRequests => new AiProviderException("The provider's rate limit or quota was reached."),
                    >= HttpStatusCode.InternalServerError => new AiProviderException("The provider had a service error."),
                    _ => new AiProviderException($"The request was refused ({(int)response.StatusCode}){(message is null ? "" : $": {message}")}.")
                };
            }

            try { return JsonNode.Parse(text); }
            catch (JsonException ex) { throw new AiProviderException("The provider's answer could not be read.", inner: ex); }
        }
    }

    private static string? ErrorMessage(string body)
    {
        try
        {
            var m = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>();
            return m is null ? null : m.Length > 200 ? m[..200] : m;
        }
        catch (JsonException) { return null; }
    }
}
