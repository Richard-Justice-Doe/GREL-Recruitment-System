using Grsms.Application.Notifications;
using Grsms.Domain.Notifications;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Infrastructure.Notifications;

/// <summary>
/// Stores in-app notifications and sent-reminder keys. Writes go through a fresh database context (own scope), so
/// adding a notification never saves changes the calling service has not finished yet.
/// </summary>
public class NotificationCenter(ApplicationDbContext db, IServiceScopeFactory scopes) : INotificationCenter
{
    public async Task AddAsync(string userId, string title, string? body, string? link, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var own = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        own.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = Cut(title, 200)!,
            Body = Cut(body, 500),
            Link = LocalPath(link),
            CreatedAt = DateTime.UtcNow
        });
        await own.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, int take, CancellationToken ct = default) =>
        await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.Id).Take(take)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(ct);

    public Task<int> CountUnreadAsync(string userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

    public async Task<string?> OpenAsync(string userId, long id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return null;
        if (n.ReadAt is null)
        {
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return n.Link;
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task<bool> ClaimReminderAsync(string key, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var own = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await own.SentReminders.AnyAsync(r => r.Key == key, ct)) return false;
        own.SentReminders.Add(new SentReminder { Key = Cut(key, 200)!, SentAt = DateTime.UtcNow });
        try
        {
            await own.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false; // claimed at the same moment by another run (unique key)
        }
    }

    public Task<int> PurgeAsync(DateTime olderThan, CancellationToken ct = default) =>
        db.Notifications.Where(n => n.ReadAt != null && n.CreatedAt < olderThan).ExecuteDeleteAsync(ct);

    /// <summary>Keeps only the path and query of a link, so a notification can never send someone to another site.</summary>
    internal static string? LocalPath(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        if (Uri.TryCreate(link, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https") return Cut(abs.PathAndQuery, 500);
        return link.StartsWith('/') && !link.StartsWith("//") && !link.StartsWith("/\\") ? Cut(link, 500) : null;
    }

    private static string? Cut(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
