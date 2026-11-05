using System.Net;
using System.Net.Mail;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grsms.Infrastructure.Email;

/// <summary>
/// Email settings, read from the "Email" section of appsettings.json / user-secrets.
/// See docs/06-configuration.md for examples.
/// </summary>
public class EmailOptions
{
    /// <summary>Sender address shown to recipients, e.g. recruitment@grel.example.</summary>
    public string FromAddress { get; set; } = "noreply@grsms.local";

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "GREL Recruitment";

    /// <summary>
    /// SMTP server name. LEAVE EMPTY to save emails as .eml files in <see cref="PickupDirectory"/>
    /// instead of sending them (the setting used until GREL's SMTP details are available).
    /// </summary>
    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;

    // Never put real credentials here: this file is in source control. Set them in user-secrets (development),
    // deploy/.env (production) or on the System settings screen (stored encrypted). See docs/06-configuration.md.
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>Folder for .eml files when no SMTP host is set. Program.cs turns a relative path into one inside the web app's folder.</summary>
    public string PickupDirectory { get; set; } = "App_Data/mail";
}

/// <summary>
/// Sends emails through SMTP, or, when no SMTP host is configured, writes each email as a .eml file
/// that can be opened in Outlook or any mail program. Business code only sees <see cref="IEmailService"/>.
/// </summary>
public class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger, Grsms.Application.Settings.ISettingsStore settings) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var _options = Effective();
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient();
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            // Development mode: drop the email into a folder instead of sending it.
            var folder = Path.GetFullPath(_options.PickupDirectory, AppContext.BaseDirectory);
            Directory.CreateDirectory(folder);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = folder;
            logger.LogInformation("Email '{Subject}' to {To} saved to {Folder} (no SMTP host configured)", subject, to, folder);
        }
        else
        {
            client.Host = _options.SmtpHost;
            client.Port = _options.SmtpPort;
            client.EnableSsl = _options.EnableSsl;
            if (!string.IsNullOrEmpty(_options.UserName))
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            logger.LogInformation("Sending email '{Subject}' to {To} via {Host}", subject, to, _options.SmtpHost);
        }

        await client.SendMailAsync(message, ct);
    }

    /// <summary>
    /// Phase 16: the server details in force: System settings first, then the "Email" configuration section, then the
    /// defaults (the store does that order). The pickup folder for saved emails stays a server setting.
    /// </summary>
    private EmailOptions Effective()
    {
        var o = options.Value;
        return new EmailOptions
        {
            FromAddress = settings.Get(SettingCatalogue.FromAddress) ?? o.FromAddress,
            FromName = settings.Get(SettingCatalogue.FromName) ?? o.FromName,
            SmtpHost = settings.Get(SettingCatalogue.SmtpHost),
            SmtpPort = settings.GetInt(SettingCatalogue.SmtpPort),
            EnableSsl = settings.GetBool(SettingCatalogue.SmtpSsl),
            UserName = settings.Get(SettingCatalogue.SmtpUser),
            Password = settings.Get(SettingCatalogue.SmtpPassword),
            PickupDirectory = o.PickupDirectory
        };
    }
}
