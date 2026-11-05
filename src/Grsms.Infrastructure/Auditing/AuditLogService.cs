using Grsms.Application.Auditing;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Auditing;
using Grsms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Grsms.Infrastructure.Auditing;

/// <inheritdoc cref="IAuditLogService"/>
public class AuditLogService(ApplicationDbContext db, ICurrentUserService currentUser) : IAuditLogService
{
    public async Task LogAsync(string action, string? userId, string? userName, string? details = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            Action = action,
            NewValues = details,
            IpAddress = currentUser.IpAddress
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        // AsNoTracking: read-only screen, so skip EF's change tracking (faster, less memory).
        var q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.UserName))
            q = q.Where(a => a.UserName != null && a.UserName.Contains(query.UserName.Trim()));
        if (!string.IsNullOrWhiteSpace(query.EntityName))
            q = q.Where(a => a.EntityName == query.EntityName);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.EntityId))
            q = q.Where(a => a.EntityId == query.EntityId.Trim());
        if (query.From is { } from)
            q = q.Where(a => a.Timestamp >= from.Date);
        if (query.To is { } to)
            q = q.Where(a => a.Timestamp < to.Date.AddDays(1)); // include the whole "to" day

        // Clamp paging values so a hand-edited URL cannot request a huge page.
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(1, query.Page);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(a.Id, a.Timestamp, a.UserName, a.Action, a.EntityName, a.EntityId, a.OldValues, a.NewValues, a.IpAddress))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AuditLogDto>> SearchMineAsync(string userId, string? userName, AuditLogQuery query, CancellationToken ct = default)
    {
        // Composite keys are stored as "UserId,RoleId" (UserRoles) and "UserId,LoginProvider,Name" (UserTokens).
        var keyPrefix = userId + ",";
        var q = db.AuditLogs.AsNoTracking().Where(a =>
            a.UserId == userId ||
            (a.UserId == null && userName != null && a.UserName == userName && a.EntityName == null) || // events with no account id, e.g. a sign-in attempt
            (a.EntityName == "User" && a.EntityId == userId) ||
            ((a.EntityName == "UserRole" || a.EntityName == "UserToken") && a.EntityId != null && a.EntityId.StartsWith(keyPrefix)));

        if (!string.IsNullOrWhiteSpace(query.EntityName))
            q = q.Where(a => a.EntityName == query.EntityName);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);
        if (query.From is { } from)
            q = q.Where(a => a.Timestamp >= from.Date);
        if (query.To is { } to)
            q = q.Where(a => a.Timestamp < to.Date.AddDays(1));

        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var page = Math.Max(1, query.Page);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            // Someone else's IP address is theirs, not the viewer's: only the viewer's own rows keep it.
            .Select(a => new AuditLogDto(a.Id, a.Timestamp, a.UserName, a.Action, a.EntityName, a.EntityId, a.OldValues, a.NewValues,
                a.UserId == userId || (a.UserId == null && a.EntityName == null) ? a.IpAddress : null))
            .ToListAsync(ct);
        return new PagedResult<AuditLogDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<string>> GetEntityNamesAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName != null)
            .Select(a => a.EntityName!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
}
