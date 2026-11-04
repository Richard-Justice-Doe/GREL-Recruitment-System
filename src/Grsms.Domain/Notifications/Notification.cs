using Grsms.Domain.Common;

namespace Grsms.Domain.Notifications;

/// <summary>
/// One in-app notification (the bell). Created alongside every workflow email, so the two always match.
/// Plain bookkeeping: not soft-deleted, not audited, and read ones are removed after a retention period.
/// </summary>
public class Notification : INotAudited
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>Same as the email subject.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The email text without formatting, shortened.</summary>
    public string? Body { get; set; }

    /// <summary>Where clicking the notification goes: a path inside GRSMS only (e.g. "/Hiring/Case/12").</summary>
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>A reminder that has been sent, so each one goes out only once (e.g. "offer-expiring:12").</summary>
public class SentReminder : INotAudited
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
