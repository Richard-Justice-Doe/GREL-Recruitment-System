using System.Text.RegularExpressions;
using Grsms.Application.Settings;
using Microsoft.Extensions.Configuration;

namespace Grsms.Infrastructure.Settings;

/// <summary>
/// Phase 16: the Backups screen's view of the backup folder written by the backup service (deploy/backup.sh).
///
///   * The folder is "Backups:Folder" (the production set-up mounts the backups folder there). Empty = not configured.
///   * Only files named like the backup service's own files are listed or opened, so a crafted name can never reach
///     another file on the server.
///   * "Back up now" leaves a request file (requests/backup-request); the backup service checks for it every 30 seconds,
///     makes the backup and removes the file. GRSMS itself never runs mysqldump and never restores.
///   * Database dumps can be downloaded. The files archive cannot: it holds the data-protection KEYS as well as every
///     uploaded document, so it is copied from the server by IT, never through the web.
/// </summary>
public sealed partial class BackupStore(IConfiguration config) : IBackupStore
{
    /// <summary>Relative to the backup folder. deploy/backup.sh creates "requests" owned by the app's user (the app runs without root).</summary>
    public const string RequestFileName = "requests/backup-request";

    private string? Folder => config["Backups:Folder"] is { Length: > 0 } f ? Path.GetFullPath(f, AppContext.BaseDirectory) : null;

    public BackupOverviewDto Overview()
    {
        var folder = Folder;
        if (folder is null || !Directory.Exists(folder)) return new BackupOverviewDto(false, folder, [], null, false);

        var files = new DirectoryInfo(folder).GetFiles("grsms-*")
            .Where(f => BackupName().IsMatch(f.Name))
            .Select(f => new BackupFileDto(f.Name, f.Name.StartsWith("grsms-db-") ? "Database" : "Files and keys", f.Length, f.LastWriteTimeUtc))
            .OrderByDescending(f => f.CreatedAt).ToList();
        var request = new FileInfo(Path.Combine(folder, RequestFileName));
        DateTime? requested = request.Exists ? request.LastWriteTimeUtc : null;
        return new BackupOverviewDto(true, folder, files, requested, requested is { } r && DateTime.UtcNow - r > TimeSpan.FromMinutes(10));
    }

    public async Task<bool> RequestBackupAsync(CancellationToken ct = default)
    {
        if (Folder is not { } folder || !Directory.Exists(folder)) return false;
        var path = Path.Combine(folder, RequestFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DateTime.UtcNow.ToString("O"), ct);
        return true;
    }

    public Stream? Open(string name)
    {
        if (Folder is not { } folder || !BackupName().IsMatch(name ?? "") || !name!.StartsWith("grsms-db-")) return null;
        var path = Path.Combine(folder, name);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    [GeneratedRegex(@"^grsms-(db|files)-\d{8}-\d{4}\.(sql|tar)\.gz$")]
    private static partial Regex BackupName();
}
