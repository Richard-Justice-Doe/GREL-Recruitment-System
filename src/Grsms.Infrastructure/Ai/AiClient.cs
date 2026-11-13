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
/// Every problem becomes an <see cref="AiProviderException"/> with a message safe to log (never the key).
/// </summary>
public sealed class AiClient(HttpClient? http = null) : IAiClient
{
    private static readonly HttpClient Shared = new() { Timeout = Timeout.InfiniteTimeSpan }; // per-call time-out below
    private readonly HttpClient _http = http ?? Shared; // tests pass their own

    public async Task<AiReply> SendAsync(AiConnection connection, AiCall call, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(connection.Timeout);
        return connection.Provider == AiProvider.Anthropic
            ? await AnthropicAsync(connection, call, timeout.Token)
            : await OpenAiAsync(connection, call, timeout.Token);
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
                    HttpStatusCode.TooManyRequests => new AiProviderException("The provider's rate limit or quota was reached."),
                    >= HttpStatusCode.InternalServerError => new AiProviderException("The provider had a service error."),
                    _ => new AiProviderException($"The request was refused ({(int)response.StatusCode}){(message is null ? "" : $": {message}")}.")
                };
            }

            JsonNode? json;
            try { json = JsonNode.Parse(text); }
            catch (JsonException ex) { throw new AiProviderException("The provider's answer could not be read.", inner: ex); }

            var choice = json?["choices"]?[0];
            var finish = choice?["finish_reason"]?.GetValue<string>();
            if (choice?["message"]?["refusal"]?.GetValue<string>() is { Length: > 0 } refusal) throw new AiProviderException($"The model declined: {refusal}");
            if (finish == "length") throw new AiProviderException("The answer was too long and was cut off.");
            if (finish == "content_filter") throw new AiProviderException("The provider's content filter stopped the answer.");
            var content = choice?["message"]?["content"]?.GetValue<string>() ?? "";
            return new AiReply(content,
                json?["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
                json?["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0,
                json?["model"]?.GetValue<string>() ?? c.Model);
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
