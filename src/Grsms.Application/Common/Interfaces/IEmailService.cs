namespace Grsms.Application.Common.Interfaces;

/// <summary>
/// Sends an email. Business code only calls this interface; which implementation runs is decided
/// by configuration (see docs/06-configuration.md):
///   * no SMTP settings  → emails are saved as .eml files in a folder (good for development/testing);
///   * SMTP settings set → emails are sent through the mail server (added when SMTP details are provided).
/// </summary>
public interface IEmailService
{
    /// <summary>Sends one HTML email.</summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="htmlBody">The message body as HTML. Any user-entered text in it must already be HTML-encoded.</param>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
