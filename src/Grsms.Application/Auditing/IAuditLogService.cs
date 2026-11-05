using Grsms.Application.Common;

namespace Grsms.Application.Auditing;

/// <summary>One row on the Audit Trail screen.</summary>
public record AuditLogDto(
    long Id,
    DateTime Timestamp,
    string? UserName,
    string Action,
    string? EntityName,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress);

/// <summary>Filters typed into the Audit Trail screen. Every filter is optional.</summary>
public class AuditLogQuery
{
    /// <summary>Part of a user name/email.</summary>
    public string? UserName { get; set; }

    /// <summary>Exact record type, e.g. "Department".</summary>
    public string? EntityName { get; set; }

    /// <summary>Exact action, e.g. "Updated" or "SignInFailed".</summary>
    public string? Action { get; set; }

    /// <summary>Exact record id, to see the full history of one record.</summary>
    public string? EntityId { get; set; }

    /// <summary>First day to include (local date; the whole day is included).</summary>
    public DateTime? From { get; set; }

    /// <summary>Last day to include (the whole day is included).</summary>
    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Reads and writes the audit trail (Module 22).
///
/// Record changes (create/update/delete) are logged AUTOMATICALLY when data is saved, so business code
/// never has to call <see cref="LogAsync"/> for them. LogAsync is only for events that are not record
/// changes, such as sign-in, sign-out and failed sign-in attempts.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Writes one security/event row immediately.</summary>
    /// <param name="action">One of the AuditActions constants.</param>
    /// <param name="userId">The user involved, if known.</param>
    /// <param name="userName">The user name involved (for failed sign-ins this is what was typed).</param>
    /// <param name="details">Optional extra information, stored in the NewValues column.</param>
    Task LogAsync(string action, string? userId, string? userName, string? details = null, CancellationToken ct = default);

    /// <summary>Searches the trail, newest first.</summary>
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>
    /// "My audit trail" (every user, 5 November 2026): what the user did (every row recorded under their account, which
    /// includes sign-ins, failed sign-ins and lockouts on their account, and password changes) PLUS what others did to
    /// their account (their user record, role assignments and sign-in tokens). On rows made by someone else the IP
    /// address is removed. Filters: Action, EntityName, From, To and paging (UserName and EntityId are ignored).
    /// </summary>
    Task<PagedResult<AuditLogDto>> SearchMineAsync(string userId, string? userName, AuditLogQuery query, CancellationToken ct = default);

    /// <summary>Distinct record types that appear in the trail, for the filter drop-down.</summary>
    Task<IReadOnlyList<string>> GetEntityNamesAsync(CancellationToken ct = default);
}
