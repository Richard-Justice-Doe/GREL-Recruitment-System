using Grsms.Domain.Common;

namespace Grsms.Domain.Settings;

/// <summary>
/// Phase 16: one system setting changed on the System settings screen (table "SystemSettings"). Only settings someone
/// has saved have a row; every other setting uses the server configuration (appsettings / environment variables) or
/// its built-in default. Secrets (SMTP password, Microsoft 365 client secret) are stored ENCRYPTED with the server's
/// data-protection keys, and the audit trail records only that they changed, never their value.
/// </summary>
public class SystemSetting : BaseEntity
{
    /// <summary>The setting's key, e.g. "Email:SmtpHost" (see <see cref="SettingCatalogue"/>). Unique.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value as text (encrypted for secrets). Empty = use the server configuration / default.</summary>
    public string? SettingValue { get; set; }

    public bool IsSecret { get; set; }
}

/// <summary>Phase 16: an edited email template (table "EmailTemplates"). Templates nobody edited use the built-in wording.</summary>
public class EmailTemplate : BaseEntity
{
    /// <summary>The template's key, e.g. "application.received" (see <see cref="EmailTemplateCatalogue"/>). Unique.</summary>
    public string Key { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Plain text with {{Placeholders}}; a blank line starts a new paragraph. No HTML: it is shown as typed.</summary>
    public string Body { get; set; } = string.Empty;
}
