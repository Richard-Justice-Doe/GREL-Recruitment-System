using Grsms.Application.Ai;

namespace Grsms.Tests.Infrastructure;

/// <summary>docs/36: stands in for the AI provider in every test. Answers with <see cref="Reply"/> and records the calls.</summary>
public sealed class FakeAiClient : IAiClient
{
    public List<(AiConnection Connection, AiCall Call)> Calls { get; } = [];

    /// <summary>What to answer (default: a connection-test answer). Throw from it to simulate a provider failure.</summary>
    public Func<AiCall, AiReply> Reply { get; set; } = _ => new AiReply("{\"ok\":true,\"reply\":\"Hello from the test\"}", 120, 30, "test-model");

    public Task<AiReply> SendAsync(AiConnection connection, AiCall call, CancellationToken ct = default)
    {
        Calls.Add((connection, call));
        return Task.FromResult(Reply(call));
    }
}
