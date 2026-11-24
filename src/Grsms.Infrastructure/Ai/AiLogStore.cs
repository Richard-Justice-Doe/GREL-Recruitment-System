using Grsms.Application.Ai;
using Grsms.Domain.Ai;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Infrastructure.Ai;

/// <summary>
/// 13 November 2026 (docs/36): the AI log and cache in their own database scope, so a call is logged even when the feature's own
/// changes are not saved, and logging never saves them. These rows are not in the audit trail (they are a log themselves).
/// </summary>
public sealed class AiLogStore(IServiceScopeFactory scopes) : IAiLogStore
{
    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        using var scope = scopes.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public Task<int> AddAsync(AiRequestLog log, CancellationToken ct = default) => WithDbAsync(async db =>
    {
        if (log.CreatedAt == default) log.CreatedAt = DateTime.UtcNow;
        db.Set<AiRequestLog>().Add(log);
        await db.SaveChangesAsync(ct);
        return log.Id;
    });

    public Task<decimal> CostSinceAsync(DateTime since, CancellationToken ct = default) => WithDbAsync(async db =>
        // Summed here: SQLite (tests) can't sum decimals in SQL. A month of calls is small.
        (await db.Set<AiRequestLog>().Where(l => l.CreatedAt >= since && l.CostUsd > 0).Select(l => l.CostUsd).ToListAsync(ct)).Sum());

    public Task<int> CallsSinceAsync(string userId, DateTime since, CancellationToken ct = default) => WithDbAsync(db =>
        db.Set<AiRequestLog>().CountAsync(l => l.UserId == userId && l.CreatedAt >= since &&
                                               (l.Outcome == AiOutcome.Success || l.Outcome == AiOutcome.Failed || l.Outcome == AiOutcome.BadAnswer), ct));

    public Task<string?> CachedAsync(string key, DateTime now, CancellationToken ct = default) => WithDbAsync(db =>
        db.Set<AiCacheEntry>().Where(c => c.Key == key && c.ExpiresAt > now).Select(c => (string?)c.Output).FirstOrDefaultAsync(ct));

    public Task SaveCacheAsync(AiCacheEntry entry, CancellationToken ct = default) => WithDbAsync(async db =>
    {
        var existing = await db.Set<AiCacheEntry>().FirstOrDefaultAsync(c => c.Key == entry.Key, ct);
        if (existing is null) db.Set<AiCacheEntry>().Add(entry);
        else (existing.Output, existing.ExpiresAt, existing.Feature) = (entry.Output, entry.ExpiresAt, entry.Feature);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two people asked the same at the same moment: the other answer is kept.
        }
        return 0;
    });

    public Task<AiUsageDto> UsageAsync(DateTime monthStart, int latest, CancellationToken ct = default) => WithDbAsync(async db =>
    {
        var month = await db.Set<AiRequestLog>().AsNoTracking().Where(l => l.CreatedAt >= monthStart)
            .Select(l => new { l.Feature, l.Outcome, l.InputTokens, l.OutputTokens, l.CostUsd }).ToListAsync(ct);
        var features = month.GroupBy(l => l.Feature).Select(g => new AiUsageFeatureDto(g.Key,
                g.Count(l => l.Outcome is AiOutcome.Success or AiOutcome.CacheHit),
                g.Count(l => l.Outcome is AiOutcome.Failed or AiOutcome.BadAnswer),
                g.Sum(l => l.InputTokens), g.Sum(l => l.OutputTokens), g.Sum(l => l.CostUsd)))
            .OrderByDescending(f => f.CostUsd).ThenByDescending(f => f.Calls).ToList();
        var calls = await db.Set<AiRequestLog>().AsNoTracking().OrderByDescending(l => l.Id).Take(latest)
            .Select(l => new AiUsageCallDto(l.Id, l.CreatedAt, l.Feature, l.UserId, l.Model, l.Outcome, l.InputTokens, l.OutputTokens, l.CostUsd,
                l.DurationMs, l.Error, l.RecordType, l.RecordId)).ToListAsync(ct);
        return new AiUsageDto(features, calls);
    });

    public Task<int> PurgeAsync(DateTime promptsBefore, DateTime now, CancellationToken ct = default) => WithDbAsync(async db =>
    {
        var n = await db.Set<AiRequestLog>().Where(l => l.Prompt != null && l.CreatedAt < promptsBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Prompt, (string?)null), ct);
        n += await db.Set<AiCacheEntry>().IgnoreQueryFilters().Where(c => c.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        return n;
    });
}
