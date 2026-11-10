using Grsms.Application.Common.Interfaces;

namespace Grsms.Application.Common;

// Codebase audit (10 November 2026, DU1-DU3): small helpers that were copied into many services.

/// <summary>Cleaning text typed into forms.</summary>
public static class Text
{
    /// <summary>Trims the text; empty or white space becomes null. Use with <c>using static Grsms.Application.Common.Text;</c>.</summary>
    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Typed-name electronic signatures (appointment letter, personal record, probation forms).</summary>
public static class Signatures
{
    /// <summary>True when two names match, ignoring case and extra spaces ("  ama   MENSAH " = "Ama Mensah").</summary>
    public static bool SameName(string a, string b) =>
        string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

    private static string Normal(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public static class UnitOfWorkExtensions
{
    /// <summary>
    /// Saves, turning an optimistic-concurrency conflict (someone else saved the same record at the same moment) into a failed
    /// <see cref="Result"/> with <paramref name="conflictMessage"/> instead of an exception.
    /// </summary>
    public static async Task<Result> TrySaveAsync(this IUnitOfWork unitOfWork, string conflictMessage, CancellationToken ct = default)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(conflictMessage);
        }
    }
}
