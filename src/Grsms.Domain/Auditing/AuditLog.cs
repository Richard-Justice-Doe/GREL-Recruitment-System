namespace Grsms.Domain.Auditing;

/// <summary>
/// One line in the audit trail (Module 22). A row is written automatically whenever any record is
/// created, changed or deleted, and also for security events such as sign-in and sign-out.
///
/// Audit rows are never edited or deleted, so this class deliberately does NOT inherit
/// <see cref="Common.BaseEntity"/> (it has no soft-delete or "updated by" fields).
/// </summary>
public class AuditLog
{
    /// <summary>Primary key. A long (BIGINT) because this table grows quickly.</summary>
    public long Id { get; set; }

    /// <summary>When it happened (UTC).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Identity user id of the person responsible, or null for system/anonymous actions.</summary>
    public string? UserId { get; set; }

    /// <summary>User name (email) at the time of the action, kept even if the user is later renamed.</summary>
    public string? UserName { get; set; }

    /// <summary>What happened. See <see cref="AuditActions"/> for the standard values.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Which kind of record was affected, e.g. "Department". Empty for sign-in events.</summary>
    public string? EntityName { get; set; }

    /// <summary>Primary key of the affected record, as text.</summary>
    public string? EntityId { get; set; }

    /// <summary>The changed fields BEFORE the change, as JSON. Null when a record is created.</summary>
    public string? OldValues { get; set; }

    /// <summary>The changed fields AFTER the change, as JSON. Null when a record is deleted.</summary>
    public string? NewValues { get; set; }

    /// <summary>IP address of the browser that made the request.</summary>
    public string? IpAddress { get; set; }
}

/// <summary>The standard words stored in <see cref="AuditLog.Action"/>. Using constants avoids typos.</summary>
public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string SignedIn = "SignedIn";
    public const string SignInFailed = "SignInFailed";
    public const string LockedOut = "LockedOut";
    public const string SignedOut = "SignedOut";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
}
