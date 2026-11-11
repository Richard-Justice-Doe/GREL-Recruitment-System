using Grsms.Domain.Settings;

namespace Grsms.Application.Settings;

/// <summary>
/// Phase 16: the settings in force. A value saved on the System settings screen wins; an empty one falls back to the
/// server configuration (same key, e.g. "Email:SmtpHost" = <c>Email__SmtpHost</c>), then to the built-in default.
/// Kept in memory and re-read at most once a minute (and at once after a save). Implemented in Infrastructure.
/// </summary>
public interface ISettingsStore
{
    /// <summary>The effective value (secrets come back decrypted), or null.</summary>
    string? Get(string key);

    int GetInt(string key);
    bool GetBool(string key);

    /// <summary>Where the effective value comes from: "screen", "server configuration", "default" or "not set".</summary>
    string SourceOf(string key);

    /// <summary>
    /// Saves values from the screen (key → value). An empty value removes the screen's value (back to the server
    /// configuration). A secret left null is not changed. Secrets are encrypted. Raises <see cref="Changed"/>.
    /// </summary>
    Task SaveAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default);

    /// <summary>Raised after a save, e.g. so the Microsoft 365 sign-in can pick up new values without a restart.</summary>
    event Action? Changed;
}

/// <summary>One setting as shown on the screen. Secrets never carry their value, only whether one is set.</summary>
public record SettingRowDto(SettingDefinition Definition, string? Value, string Source, bool HasValue);

/// <summary>One email template on the Email templates screen.</summary>
public record EmailTemplateRowDto(EmailTemplateDefinition Definition, bool IsCustomised, DateTime? UpdatedAt, string? UpdatedBy);

public class EmailTemplateFormDto
{
    public string Key { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    // Read-only, filled by the service
    public EmailTemplateDefinition? Definition { get; set; }
    public bool IsCustomised { get; set; }
    public string? PreviewSubject { get; set; }
    public string? PreviewHtml { get; set; }
}

/// <summary>Phase 16: one backup file.</summary>
public record BackupFileDto(string Name, string Kind, long SizeBytes, DateTime CreatedAt);

/// <summary>Phase 16: the backups folder as seen by GRSMS.</summary>
public record BackupOverviewDto(bool Configured, string? Folder, IReadOnlyList<BackupFileDto> Files, DateTime? RequestedAt, bool RequestLooksStuck);

/// <summary>
/// Phase 16: reads the backups made by the backup service (deploy/backup.sh) and asks it for one now. GRSMS itself never
/// runs mysqldump and never restores: restoring stays a server procedure (docs/21-production-deployment.md).
/// </summary>
public interface IBackupStore
{
    BackupOverviewDto Overview();

    /// <summary>Leaves a request file the backup service picks up within a minute. False when no backup folder is set.</summary>
    Task<bool> RequestBackupAsync(CancellationToken ct = default);

    /// <summary>Opens a backup for download, or null if the name is not one of the listed backups.</summary>
    Stream? Open(string name);
}
