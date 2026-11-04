using Grsms.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Persistence;

/// <summary>
/// One row per reference-number series, e.g. Key = "RR-HSE-2026", LastValue = 17.
/// Purely technical, so it is not audited and has no soft delete.
/// </summary>
public class NumberSequence
{
    public string Key { get; set; } = string.Empty;
    public int LastValue { get; set; }
}

/// <summary>
/// Hands out sequence numbers safely even when several people save at the same instant.
///
/// How: the increment is a single SQL statement, <c>UPDATE NumberSequences SET LastValue = LastValue + 1 WHERE Key = ...</c>,
/// run inside a transaction. MySQL locks that row until the transaction ends, so a second caller waits and then
/// gets the next number. The first time a series is used, its row is inserted; if two callers race to insert it,
/// the loser's insert fails on the primary key and it simply retries the update.
/// </summary>
public class NumberGenerator(ApplicationDbContext db) : INumberGenerator
{
    public async Task<int> NextAsync(string sequenceKey, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using (var tx = await db.Database.BeginTransactionAsync(ct))
            {
                var updated = await db.Set<NumberSequence>()
                    .Where(s => s.Key == sequenceKey)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastValue, x => x.LastValue + 1), ct);

                if (updated == 1)
                {
                    var value = await db.Set<NumberSequence>()
                        .Where(s => s.Key == sequenceKey)
                        .Select(s => s.LastValue)
                        .SingleAsync(ct);
                    await tx.CommitAsync(ct);
                    return value;
                }
            }

            // First number in this series: create its row.
            var row = new NumberSequence { Key = sequenceKey, LastValue = 1 };
            db.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return 1;
            }
            catch (DbUpdateException)
            {
                // Someone else created the row a moment ago; forget ours and go round again.
                db.Entry(row).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException($"Could not obtain the next number for '{sequenceKey}'.");
    }
}
