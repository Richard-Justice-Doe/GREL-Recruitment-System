using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// docs/32 (11 November 2026): writes a warning to the log for every database command slower than
/// "Database:SlowQueryMilliseconds" (default 500; 0 switches it off), with the time taken and the SQL. Only the SQL text is
/// logged, never the parameter values (which can hold personal data). Used to find what is slow on GREL's own MySQL server.
/// </summary>
public sealed class SlowQueryInterceptor(IConfiguration config, ILogger<SlowQueryInterceptor> logger) : DbCommandInterceptor
{
    private readonly int _thresholdMs = config.GetValue("Database:SlowQueryMilliseconds", 500);

    private void Check(DbCommand command, CommandExecutedEventData data)
    {
        if (_thresholdMs > 0 && data.Duration.TotalMilliseconds >= _thresholdMs)
            logger.LogWarning("Slow database command: {Milliseconds} ms. {Sql}", (int)data.Duration.TotalMilliseconds,
                command.CommandText.Length > 2000 ? command.CommandText[..2000] + " ..." : command.CommandText);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Check(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Check(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }
}
