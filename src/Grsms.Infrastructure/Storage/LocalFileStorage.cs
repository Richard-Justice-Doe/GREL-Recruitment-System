using Grsms.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Grsms.Infrastructure.Storage;

/// <summary>Settings from the "Storage" section (see docs/06-configuration.md).</summary>
public class StorageOptions
{
    /// <summary>
    /// Root folder for uploaded files: a local folder (e.g. D:\GRSMS\uploads) or a file-server share
    /// (e.g. \\fileserver\grsms). Relative paths are placed inside the web application's folder by Program.cs.
    /// It must NOT be inside wwwroot, so files can never be downloaded without a permission check.
    /// </summary>
    public string RootFolder { get; set; } = "App_Data/uploads";
}

/// <summary>
/// Stores files in a folder tree by year and month, e.g. uploads/2026/11/3f2a9c...e9.pdf.
/// File names are random, so they cannot be guessed and two uploads called "CV.pdf" never collide.
/// </summary>
public class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private string Root => Path.GetFullPath(options.Value.RootFolder);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Forward slashes in the stored name so it is the same on Windows and Linux.
        var storedName = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var fullPath = ToFullPath(storedName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // FileMode.CreateNew: never overwrite an existing file.
        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, ct);
        return storedName;
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default)
    {
        var fullPath = ToFullPath(storedName);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    /// <summary>Turns a stored name into a full path, refusing anything that would escape the root folder (e.g. "../").</summary>
    private string ToFullPath(string storedName)
    {
        var root = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, storedName.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid stored file name.");
        return full;
    }
}
