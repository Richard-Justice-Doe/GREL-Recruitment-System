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
        Assert.Equal(200, body["max_completion_tokens"]!.GetValue<int>());
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
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key\"}}", "API key was refused", true)]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", "rate limit", false)]
    [InlineData(HttpStatusCode.BadGateway, "{}", "service error", false)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Unknown model\"}}", "Unknown model", false)]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"{\"}}]}", "too long", false)]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"refusal\":\"I can't help with that\"}}]}", "declined", false)]
    public async Task Problems_are_reported_without_the_key(HttpStatusCode status, string body, string expected, bool keyRefused)
    {
        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            new AiClient(new HttpClient(new Handler(status, body))).SendAsync(Conn(AiProvider.OpenAI), new AiCall("x", "y", null)));
        Assert.Contains(expected, ex.Message);
        Assert.Equal(keyRefused, ex.KeyRefused);
        Assert.DoesNotContain("sk-secret", ex.Message);
    }
}
