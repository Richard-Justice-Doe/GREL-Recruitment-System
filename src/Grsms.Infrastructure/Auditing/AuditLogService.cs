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

    /// <summary>Record types that are configuration: permissions, roles, role assignments and settings.</summary>
    private static readonly string[] SettingsTypes = ["RoleClaim", "Role", "UserRole", "AtsSettings", "SystemSetting", "EmailTemplate"];

    public async Task<AuditMonitorDto> MonitorAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var dayAgo = nowUtc.AddDays(-1);
        var weekAgo = nowUtc.AddDays(-7);
        // A week of rows is small (GREL's volumes: thousands, not millions), so it is read once and counted in memory.
        var week = await db.AuditLogs.AsNoTracking().Where(a => a.Timestamp >= weekAgo)
            .Select(a => new MonitorRow(a.Id, a.Timestamp, a.UserName, a.Action, a.EntityName, a.EntityId, a.OldValues, a.NewValues, a.IpAddress))
            .ToListAsync(ct);
        var day = week.Where(a => a.Timestamp >= dayAgo).ToList();

        AuditPeriodDto Period(IReadOnlyList<MonitorRow> list)
        {

            return new AuditPeriodDto(
                list.Count(a => a.Action == AuditActions.SignedIn),
                list.Count(a => a.Action == AuditActions.SignInFailed),
                list.Count(a => a.Action == AuditActions.LockedOut),
                list.Count(a => a.EntityName != null),
                list.Count(a => a.EntityName != null && SettingsTypes.Contains(a.EntityName)));
        }

        var failed = week.Where(a => a.Action == AuditActions.SignInFailed).ToList();
        var byAccount = failed.GroupBy(a => a.UserName ?? "(unknown)")
            .Select(g => new AuditCountDto(g.Key, g.Count(), g.Max(x => x.Timestamp))).OrderByDescending(x => x.Count).Take(10).ToList();
        var byAddress = failed.Where(a => a.IpAddress != null).GroupBy(a => a.IpAddress!)
            .Select(g => new AuditCountDto(g.Key, g.Count(), g.Max(x => x.Timestamp))).OrderByDescending(x => x.Count).Take(10).ToList();
        var activeUsers = week.Where(a => a.EntityName != null && a.UserName != null).GroupBy(a => a.UserName!)
            .Select(g => new AuditCountDto(g.Key, g.Count(), g.Max(x => x.Timestamp))).OrderByDescending(x => x.Count).Take(10).ToList();
        var byType = week.Where(a => a.EntityName != null).GroupBy(a => a.EntityName!)
            .Select(g => new AuditCountDto(g.Key, g.Count(), g.Max(x => x.Timestamp))).OrderByDescending(x => x.Count).Take(12).ToList();

        static AuditLogDto Dto(MonitorRow a) => new(a.Id, a.Timestamp, a.UserName, a.Action, a.EntityName, a.EntityId, a.OldValues, a.NewValues, a.IpAddress);
        var settingsChanges = week.Where(a => a.EntityName != null && SettingsTypes.Contains(a.EntityName)).OrderByDescending(a => a.Id).Take(20)
            .Select(a => Dto(a)).ToList();
        string[] securityActions = [AuditActions.SignInFailed, AuditActions.LockedOut, AuditActions.PasswordReset, AuditActions.PasswordChanged];
        var security = week.Where(a => securityActions.Contains(a.Action)).OrderByDescending(a => a.Id).Take(20).Select(a => Dto(a)).ToList();

        // Alerts: things worth a look today.
        var alerts = new List<string>();
        foreach (var g in day.Where(a => a.Action == AuditActions.SignInFailed).GroupBy(a => a.UserName ?? "(unknown)").Where(g => g.Count() >= 5))
            alerts.Add($"{g.Count()} failed sign-ins for {g.Key} in the last 24 hours (password guessing, or someone who forgot their password).");
        foreach (var g in day.Where(a => a.Action == AuditActions.SignInFailed && a.IpAddress != null).GroupBy(a => a.IpAddress!).Where(g => g.Count() >= 10))
            alerts.Add($"{g.Count()} failed sign-ins from address {g.Key} in the last 24 hours, across {g.Select(x => x.UserName).Distinct().Count()} account(s).");
        var lockouts = day.Count(a => a.Action == AuditActions.LockedOut);
        if (lockouts > 0) alerts.Add($"{lockouts} account lockout(s) in the last 24 hours.");
        var config = day.Count(a => a.EntityName != null && SettingsTypes.Contains(a.EntityName));
        if (config > 0) alerts.Add($"{config} change(s) to permissions, roles or settings in the last 24 hours: check they were expected.");

        return new AuditMonitorDto(Period(day), Period(week), alerts, byAccount, byAddress, activeUsers, byType, settingsChanges, security);
    }

    private sealed record MonitorRow(long Id, DateTime Timestamp, string? UserName, string Action, string? EntityName, string? EntityId,
        string? OldValues, string? NewValues, string? IpAddress);

    public async Task<IReadOnlyList<string>> GetEntityNamesAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName != null)
            .Select(a => a.EntityName!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
}
