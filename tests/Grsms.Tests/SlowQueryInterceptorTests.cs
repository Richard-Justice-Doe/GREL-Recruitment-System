using Grsms.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grsms.Tests;

/// <summary>docs/32 (11 November 2026): slow database commands are logged with their time and SQL, never their parameter values.</summary>
public class SlowQueryInterceptorTests
{
    private sealed class ListLogger : ILogger<SlowQueryInterceptor>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)}");
    }

    private static async Task<List<string>> RunAsync(string threshold)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Database:SlowQueryMilliseconds"] = threshold }).Build();
        var logger = new ListLogger();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection)
            .AddInterceptors(new SlowQueryInterceptor(config, logger)).Options;
        await using var db = new ApplicationDbContext(options);
        // A deliberately slow query (counting to 3 million), with a parameter that must not reach the log.
        var secret = "GHA-123456789-0";
        await db.Database.SqlQueryRaw<long>(
            "WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x + 1 FROM n WHERE x < 3000000) SELECT COUNT(*) AS Value FROM n WHERE {0} <> ''", secret)
            .ToListAsync();
        return logger.Lines;
    }

    [Fact]
    public async Task A_slow_command_is_logged_without_its_values()
    {
        var line = Assert.Single(await RunAsync("1"));
        Assert.StartsWith("Warning: Slow database command:", line);
        Assert.Contains("WITH RECURSIVE", line);
        Assert.DoesNotContain("GHA-123456789-0", line);
    }

    [Fact]
    public async Task Nothing_is_logged_when_switched_off() => Assert.Empty(await RunAsync("0"));
}
