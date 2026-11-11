namespace Grsms.Application.Common.Interfaces;

/// <summary>
/// Sends an email. Business code only calls this interface; which implementation runs is decided
/// by configuration (see docs/06-configuration.md):
///   * no SMTP settings  → emails are saved as .eml files in a folder (good for development/testing);
///   * SMTP settings set → emails are sent through the mail server.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends one HTML email. With a mail server set, the email is handed to a background sender and this returns at once
    /// (11 November 2026: a mail server can take several seconds per email, which the person who clicked used to wait for);
    /// the sender retries and logs a failure. Without one, the .eml file is written before this returns.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="htmlBody">The message body as HTML. Any user-entered text in it must already be HTML-encoded.</param>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Sends one HTML email now and waits for the mail server's answer (throws if it fails). For the "Send a test email" button.</summary>
    Task SendNowAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
