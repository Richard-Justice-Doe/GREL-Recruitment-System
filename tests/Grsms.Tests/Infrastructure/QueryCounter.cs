using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grsms.Tests.Infrastructure;

/// <summary>
/// Counts the SQL commands EF Core runs inside <c>using (var c = QueryCounter.Start())</c> on the current async flow (tests in
/// other classes run in parallel and are not counted). Used to keep pages free of N+1 queries (codebase audit P1, 10 Nov 2026).
/// </summary>
public sealed class QueryCounter : IDisposable
{
    private static readonly AsyncLocal<QueryCounter?> Current = new();
    private static readonly object Gate = new();
    private static bool _subscribed;
    private int _count;

    public int Count => _count;

    public static QueryCounter Start()
    {
        lock (Gate)
        {
            if (!_subscribed)
            {
                DiagnosticListener.AllListeners.Subscribe(new ListenerObserver());
                _subscribed = true;
            }
        }
        var counter = new QueryCounter();
        Current.Value = counter;
        return counter;
    }

    public void Dispose() => Current.Value = null;

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name) listener.Subscribe(new EventObserver());
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    private sealed class EventObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> e)
        {
            if (e.Key == RelationalEventId.CommandExecuted.Name && Current.Value is { } c) Interlocked.Increment(ref c._count);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
