namespace Grsms.Application.Notifications;

/// <summary>One notification as shown in the bell list.</summary>
public record NotificationDto(long Id, string Title, string? Body, string? Link, DateTime CreatedAt, bool IsRead);

/// <summary>
/// The in-app notifications (the bell) and the "already sent" record of reminders. Implemented in Infrastructure.
/// <see cref="AddAsync"/> saves in its own database context, so it never saves a caller's unfinished changes.
/// </summary>
public interface INotificationCenter
{
    /// <summary>Adds a notification for one user. <paramref name="link"/> may be a full address; only its path is kept.</summary>
    Task AddAsync(string userId, string title, string? body, string? link, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, int take, CancellationToken ct = default);
    Task<int> CountUnreadAsync(string userId, CancellationToken ct = default);

    /// <summary>Marks one of the user's notifications read and returns its link (null if not theirs or no link).</summary>
    Task<string?> OpenAsync(string userId, long id, CancellationToken ct = default);

    Task MarkAllReadAsync(string userId, CancellationToken ct = default);

    /// <summary>Records that a reminder was sent. Returns false if it had already been sent (so do not send it again).</summary>
    Task<bool> ClaimReminderAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes read notifications older than <paramref name="olderThan"/>. Returns how many.</summary>
    Task<int> PurgeAsync(DateTime olderThan, CancellationToken ct = default);
}

/// <summary>
/// Sends text messages. While SMS is switched off ("Sms:Enabled" = false) messages are only written to files,
/// like emails without an SMTP server. Implemented in Infrastructure.
/// </summary>
public interface ISmsSender
{
    /// <param name="phone">An international number without "+", e.g. 233241234567 (see <see cref="Common.PhoneNumbers"/>).</param>
    Task SendAsync(string phone, string text, CancellationToken ct = default);
}
