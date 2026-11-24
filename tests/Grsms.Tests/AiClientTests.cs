using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Grsms.Application.Ai;
using Grsms.Domain.Ai;
using Grsms.Infrastructure.Ai;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-0: the OpenAI and Azure OpenAI requests (no network: a fake HTTP handler answers). Checks the URL, the key
/// header, the strict JSON-schema request, the usage counts, and how refusals and errors are reported.
/// </summary>
public class AiClientTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? Body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>Answers each request with the next of the given responses (the last one repeats); a null status = network failure.</summary>
    private sealed class Sequence(params (HttpStatusCode? Status, string Body)[] answers) : HttpMessageHandler
    {
        public readonly List<string> Bodies = [];
        public Func<HttpResponseMessage, HttpResponseMessage>? Shape;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answers[Math.Min(Bodies.Count - 1, answers.Length - 1)];
            if (status is null) throw new HttpRequestException("No such host is known.");
            var response = new HttpResponseMessage(status.Value) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            return Shape?.Invoke(response) ?? response;
        }
    }

    /// <summary>A client that records the waits between attempts instead of waiting.</summary>
    private static (AiClient Client, List<TimeSpan> Waits) Client(HttpMessageHandler handler)
    {
        var waits = new List<TimeSpan>();
        return (new AiClient(new HttpClient(handler), (d, _) => { waits.Add(d); return Task.CompletedTask; }), waits);
    }

    private static readonly JsonObject Schema = AiGateway.SchemaOf<AiAdminService.ConnectionCheck>();

    private static AiConnection Conn(AiProvider p) =>
        new(p, "sk-secret", "gpt-test", p == AiProvider.AzureOpenAI ? "https://grel.openai.azure.com/" : null, "2024-10-21", TimeSpan.FromSeconds(10));

    private const string Ok = """
        {"model":"gpt-test-2026","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"{\"ok\":true,\"reply\":\"Hi\"}"}}],
         "usage":{"prompt_tokens":42,"completion_tokens":7}}
        """;

    [Fact]
    public async Task OpenAi_request_uses_bearer_key_model_and_strict_schema()
    {
        var handler = new Handler(HttpStatusCode.OK, Ok);
        var reply = await new AiClient(new HttpClient(handler)).SendAsync(Conn(AiProvider.OpenAI), new AiCall("Be brief.", "Test.", Schema, 200));

        Assert.Equal(("{\"ok\":true,\"reply\":\"Hi\"}", 42, 7, "gpt-test-2026"), (reply.Text, reply.InputTokens, reply.OutputTokens, reply.Model));
        Assert.Equal("https://api.openai.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer sk-secret", handler.Request.Headers.Authorization!.ToString());
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("gpt-test", body["model"]!.GetValue<string>());
        // Reasoning models (GPT-5, o-series) think within this limit, so the allowance is added (23 Nov 2026).
        Assert.Equal(200 + AiClient.ReasoningAllowance, body["max_completion_tokens"]!.GetValue<int>());
        Assert.Equal("system", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.True(body["response_format"]!["json_schema"]!["strict"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Azure_request_uses_the_deployment_url_and_api_key_header()
    {
        var handler = new Handler(HttpStatusCode.OK, Ok);
        await new AiClient(new HttpClient(handler)).SendAsync(Conn(AiProvider.AzureOpenAI), new AiCall("x", "y", null));
        Assert.Equal("https://grel.openai.azure.com/openai/deployments/gpt-test/chat/completions?api-version=2024-10-21", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sk-secret", handler.Request.Headers.GetValues("api-key").Single());
        Assert.Null(handler.Request.Headers.Authorization);
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Null(body["model"]);           // the deployment decides the model
        Assert.Null(body["response_format"]); // plain text asked
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key\"}}", "API key was refused (401): Incorrect API key", AiFailure.KeyRefused)]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Rate limit reached for gpt-test on requests per min (RPM)\"}}", "rate limit", AiFailure.Busy)]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"You exceeded your current quota, please check your plan and billing details.\",\"code\":\"insufficient_quota\"}}", "quota or credit is used up", AiFailure.Quota)]
    [InlineData(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "service error (502)", AiFailure.Busy)]
    [InlineData(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"The model gpt-x does not exist\"}}", "model was not found", AiFailure.BadRequest)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Unknown model\"}}", "Unknown model", AiFailure.BadRequest)]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{\"}}]}", "too long", AiFailure.Other)]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"refusal\":\"I can't help with that\"}}]}", "declined", AiFailure.Other)]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[]}", "no answer", AiFailure.Other)]
    public async Task Problems_are_reported_with_their_kind_and_without_the_key(HttpStatusCode status, string body, string expected, AiFailure kind)
    {
        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            Client(new Handler(status, body)).Client.SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null)));
        Assert.Contains(expected, ex.Message);
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(kind == AiFailure.KeyRefused, ex.KeyRefused);
        Assert.DoesNotContain("sk-secret", ex.Message);
    }

    // ===================================================================== Retries (23 November 2026)

    private const string Overloaded = "{\"error\":{\"code\":503,\"message\":\"This model is currently experiencing high demand. Please try again later.\",\"status\":\"UNAVAILABLE\"}}";

    [Fact]
    public async Task A_busy_provider_is_tried_again_and_the_answer_is_used()
    {
        var handler = new Sequence((HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.OK, GeminiOk));
        var (client, waits) = Client(handler);
        var reply = await client.SendAsync(Gemini(), new AiCall("x", "y", Schema, 200));
        Assert.Equal("{\"ok\":true,\"reply\":\"Hi\"}", reply.Text);
        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal([TimeSpan.FromSeconds(1)], waits);
    }

    [Fact]
    public async Task A_provider_that_stays_busy_is_tried_three_times_then_reported_with_its_reason()
    {
        var handler = new Sequence((HttpStatusCode.ServiceUnavailable, Overloaded));
        var (client, waits) = Client(handler);
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => client.SendAsync(Gemini(), new AiCall("x", "y", null)));
        Assert.Equal(AiClient.MaxAttempts, handler.Bodies.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)], waits);
        Assert.Equal(AiFailure.Busy, ex.Kind);
        Assert.Equal("The provider had a service error (503): This model is currently experiencing high demand. Please try again later.", ex.Message);
    }

    [Fact]
    public async Task The_wait_the_provider_asks_for_is_used_up_to_the_maximum()
    {
        var handler = new Sequence((HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Slow down\"}}"), (HttpStatusCode.OK, Ok))
        {
            Shape = r => { r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return r; }
        };
        var (client, waits) = Client(handler);
        await client.SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null));
        Assert.Equal([AiClient.MaxRetryWait], waits);
    }

    [Fact]
    public async Task A_network_failure_is_tried_again_then_reported_as_unreachable()
    {
        var handler = new Sequence((null, ""));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).Client.SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null)));
        Assert.Equal((AiClient.MaxAttempts, AiFailure.Unreachable), (handler.Bodies.Count, ex.Kind));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"bad key\"}}")]
    [InlineData(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"no such model\"}}")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"You exceeded your current quota\",\"code\":\"insufficient_quota\"}}")]
    public async Task Problems_that_waiting_cannot_fix_are_not_retried(HttpStatusCode status, string body)
    {
        var handler = new Sequence((status, body));
        await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).Client.SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null)));
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task No_retry_is_made_when_the_time_allowed_would_run_out()
    {
        var handler = new Sequence((HttpStatusCode.ServiceUnavailable, Overloaded), (HttpStatusCode.OK, Ok));
        var conn = Conn(AiProvider.OpenAI) with { Timeout = TimeSpan.FromMilliseconds(500) }; // shorter than the 1 s wait
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).Client.SendAsync(conn, new AiCall("x", "y", null)));
        Assert.Equal((1, AiFailure.Busy), (handler.Bodies.Count, ex.Kind));
    }

    [Fact]
    public async Task A_provider_that_does_not_answer_in_time_is_reported_as_a_timeout()
    {
        var conn = Conn(AiProvider.OpenAI) with { Timeout = TimeSpan.FromMilliseconds(100) };
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Client(new Hang()).Client.SendAsync(conn, new AiCall("x", "y", null)));
        Assert.Equal(AiFailure.Timeout, ex.Kind);
    }

    private sealed class Hang : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException();
        }
    }

    [Fact]
    public async Task An_output_limit_above_the_models_maximum_is_sent_again_with_the_features_own_limit()
    {
        var handler = new Sequence(
            (HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"max_tokens is too large: 12192. This model supports at most 4096 completion tokens\"}}"),
            (HttpStatusCode.OK, Ok));
        await Client(handler).Client.SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null, 4000));
        Assert.Equal([4000 + AiClient.ReasoningAllowance, 4000],
            handler.Bodies.Select(b => JsonNode.Parse(b)!["max_completion_tokens"]!.GetValue<int>()));
    }

    // ===================================================================== Google Gemini (14 November 2026)

    private static AiConnection Gemini(string model = "gemini-test") =>
        new(AiProvider.Gemini, "AIza-secret", model, null, "2024-10-21", TimeSpan.FromSeconds(10));

    private const string GeminiOk = """
        {"candidates":[{"finishReason":"STOP","content":{"role":"model","parts":[{"text":"Planning…","thought":true},{"text":"{\"ok\":true,"},{"text":"\"reply\":\"Hi\"}"}]}}],
         "usageMetadata":{"promptTokenCount":40,"candidatesTokenCount":9,"thoughtsTokenCount":30},"modelVersion":"gemini-test-001"}
        """;

    [Fact]
    public async Task Gemini_request_uses_the_key_header_system_instruction_and_json_schema()
    {
        var handler = new Handler(HttpStatusCode.OK, GeminiOk);
        var reply = await new AiClient(new HttpClient(handler)).SendAsync(Gemini("models/gemini-test"), new AiCall("Be brief.", "Test.", Schema, 200));

        // The thought part is left out; thinking tokens count as output (they are billed).
        Assert.Equal(("{\"ok\":true,\"reply\":\"Hi\"}", 40, 39, "gemini-test-001"), (reply.Text, reply.InputTokens, reply.OutputTokens, reply.Model));
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent", handler.Request!.RequestUri!.ToString());
        Assert.Equal("AIza-secret", handler.Request.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("AIza", handler.Request.RequestUri.ToString()); // never in the URL (logs)
        var body = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("Be brief.", body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("Test.", body["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(200 + AiClient.GeminiThinkingAllowance, body["generationConfig"]!["maxOutputTokens"]!.GetValue<int>());
        Assert.Equal("application/json", body["generationConfig"]!["responseMimeType"]!.GetValue<string>());
        Assert.Equal("object", body["generationConfig"]!["responseJsonSchema"]!["type"]!.GetValue<string>());

        var plain = new Handler(HttpStatusCode.OK, GeminiOk);
        await new AiClient(new HttpClient(plain)).SendAsync(Gemini(), new AiCall("x", "y", null));
        Assert.Null(JsonNode.Parse(plain.Body!)!["generationConfig"]!["responseMimeType"]); // plain text asked
    }

    // Gemini says "You exceeded your current quota" for per-minute limits too; the quota id in the details tells them apart.
    private const string GeminiPerMinute = "{\"error\":{\"code\":429,\"message\":\"You exceeded your current quota, please check your plan and billing details.\",\"status\":\"RESOURCE_EXHAUSTED\"," +
        "\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.QuotaFailure\",\"violations\":[{\"quotaId\":\"GenerateRequestsPerMinutePerProjectPerModel-FreeTier\"}]}]}}";
    private const string GeminiPerDay = "{\"error\":{\"code\":429,\"message\":\"You exceeded your current quota, please check your plan and billing details.\",\"status\":\"RESOURCE_EXHAUSTED\"," +
        "\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.QuotaFailure\",\"violations\":[{\"quotaId\":\"GenerateRequestsPerDayPerProjectPerModel-FreeTier\"}]}]}}";

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":400,\"message\":\"API key not valid. Please pass a valid API key.\",\"status\":\"INVALID_ARGUMENT\"}}", "API key was refused", AiFailure.KeyRefused)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":400,\"message\":\"API key expired. Please renew the API key.\"}}", "API key expired", AiFailure.KeyRefused)]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":{\"message\":\"Permission denied\"}}", "API key was refused", AiFailure.KeyRefused)]
    [InlineData(HttpStatusCode.TooManyRequests, GeminiPerMinute, "rate limit", AiFailure.Busy)]
    [InlineData(HttpStatusCode.TooManyRequests, GeminiPerDay, "quota or credit is used up", AiFailure.Quota)]
    [InlineData(HttpStatusCode.ServiceUnavailable, Overloaded, "high demand", AiFailure.Busy)]
    [InlineData(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"models/gemini-x is not found\"}}", "not found", AiFailure.BadRequest)]
    [InlineData(HttpStatusCode.OK, "{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\",\"content\":{\"parts\":[{\"text\":\"{\"}]}}]}", "too long", AiFailure.Other)]
    [InlineData(HttpStatusCode.OK, "{\"candidates\":[{\"finishReason\":\"SAFETY\"}]}", "content filter", AiFailure.Other)]
    [InlineData(HttpStatusCode.OK, "{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", "safety filter", AiFailure.Other)]
    [InlineData(HttpStatusCode.OK, "{\"candidates\":[]}", "no answer", AiFailure.Other)]
    public async Task Gemini_problems_are_reported_with_their_kind_and_without_the_key(HttpStatusCode status, string body, string expected, AiFailure kind)
    {
        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            Client(new Handler(status, body)).Client.SendAsync(Gemini(), new AiCall("x", "y", null)));
        Assert.Contains(expected, ex.Message);
        Assert.Equal(kind, ex.Kind);
        Assert.DoesNotContain("AIza-secret", ex.Message);
    }

    // ===================================================================== Files with the input (docs/36 AI-8)

    private static readonly AiFile[] Files = [new("%PDF-1.4"u8.ToArray(), AiFile.Pdf, "card.pdf"), new([0xFF, 0xD8, 0xFF], AiFile.Jpeg, "card.jpg")];

    [Fact]
    public async Task OpenAi_sends_files_as_a_pdf_file_part_and_an_image_part_before_the_text()
    {
        var handler = new Handler(HttpStatusCode.OK, Ok);
        await new AiClient(new HttpClient(handler)).SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "Check these.", null, 200, Files));
        var content = JsonNode.Parse(handler.Body!)!["messages"]![1]!["content"]!.AsArray();
        Assert.Equal(["file", "image_url", "text"], content.Select(p => p!["type"]!.GetValue<string>()));
        Assert.Equal("data:application/pdf;base64," + Convert.ToBase64String("%PDF-1.4"u8.ToArray()), content[0]!["file"]!["file_data"]!.GetValue<string>());
        Assert.StartsWith("data:image/jpeg;base64,", content[1]!["image_url"]!["url"]!.GetValue<string>());
        Assert.Equal("Check these.", content[2]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Gemini_sends_files_as_inline_data_parts_before_the_text()
    {
        var handler = new Handler(HttpStatusCode.OK, GeminiOk);
        await new AiClient(new HttpClient(handler)).SendAsync(Gemini(), new AiCall("x", "Check these.", null, 200, Files));
        var parts = JsonNode.Parse(handler.Body!)!["contents"]![0]!["parts"]!.AsArray();
        Assert.Equal(3, parts.Count);
        Assert.Equal(("application/pdf", "image/jpeg"), (parts[0]!["inlineData"]!["mimeType"]!.GetValue<string>(), parts[1]!["inlineData"]!["mimeType"]!.GetValue<string>()));
        Assert.Equal(Convert.ToBase64String([0xFF, 0xD8, 0xFF]), parts[1]!["inlineData"]!["data"]!.GetValue<string>());
        Assert.Equal("Check these.", parts[2]!["text"]!.GetValue<string>());
    }
}
