using System.Security.Cryptography;
using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Documents;
using Microsoft.Extensions.Options;

namespace Grsms.Application.Documents;

/// <summary>Upload limits, from the "Uploads" settings section (see docs/06-configuration.md).</summary>
public class UploadOptions
{
    /// <summary>Largest file accepted, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; } = 10;
}

/// <summary>One file in an attachment list.</summary>
public record AttachmentDto(
    int Id,
    AttachmentCategory Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt,
    string? UploadedBy);

/// <summary>A file ready to send to the browser. The caller must dispose <see cref="Content"/>.</summary>
public record FileDownload(string FileName, string ContentType, Stream Content);

/// <summary>
/// Stores, lists and opens attachments for any module.
///
/// This service does NOT decide who may upload or download; the owning module's service checks that
/// first (e.g. only the requester may add files to a draft recruitment request) and then calls here.
///
/// Every upload is checked:
///   1. the extension is on the allowed list below;
///   2. the size is within Uploads:MaxFileSizeMb;
///   3. the first bytes of the file really match that type (a renamed .exe called "cv.pdf" is refused);
/// and is saved under a random name, with a SHA-256 fingerprint recorded.
/// </summary>
public class AttachmentService(IRepository<Attachment> attachments, IFileStorage storage, IUnitOfWork unitOfWork, IOptions<UploadOptions> options)
{
    /// <summary>
    /// Allowed file types: extension → (MIME type, the "magic bytes" every genuine file of that type starts with).
    /// Word/Excel 2007+ files are ZIP archives (PK..); older .doc/.xls files share the OLE header (D0 CF 11 E0).
    /// </summary>
    private static readonly Dictionary<string, (string Mime, byte[] Signature)> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B, 0x03, 0x04]),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", [0x50, 0x4B, 0x03, 0x04]),
        [".doc"] = ("application/msword", [0xD0, 0xCF, 0x11, 0xE0]),
        [".xls"] = ("application/vnd.ms-excel", [0xD0, 0xCF, 0x11, 0xE0]),
        [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
        [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
    };

    /// <summary>The allowed extensions, for the file picker's "accept" attribute and help text.</summary>
    public static string AcceptList => string.Join(",", AllowedTypes.Keys);

    public int MaxFileSizeMb => options.Value.MaxFileSizeMb;

    /// <summary>Lists the (non-deleted) files of one record, newest first.</summary>
    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(string ownerType, int ownerId, CancellationToken ct = default) =>
        (await attachments.ListAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId, ct))
            .OrderByDescending(a => a.CreatedAt)
            .Select(ToDto)
            .ToList();

    /// <summary>True if the record has at least one file in <paramref name="category"/>.</summary>
    public Task<bool> HasCategoryAsync(string ownerType, int ownerId, AttachmentCategory category, CancellationToken ct = default) =>
        attachments.AnyAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.Category == category, ct);

    /// <summary>Validates and stores a file, then saves its database record.</summary>
    public async Task<Result<int>> AddAsync(string ownerType, int ownerId, AttachmentCategory category,
        string fileName, Stream content, CancellationToken ct = default)
    {
        // Path.GetFileName strips any folder part a browser might send ("C:\fakepath\cv.pdf").
        var safeName = Path.GetFileName(fileName ?? "").Trim();
        var extension = Path.GetExtension(safeName);
        if (safeName.Length == 0 || !AllowedTypes.TryGetValue(extension, out var type))
            return Result.Failure<int>($"Only these file types are accepted: {AcceptList.Replace(",", ", ")}.");
        if (safeName.Length > 200)
            safeName = Path.GetFileNameWithoutExtension(safeName)[..(200 - extension.Length)] + extension;

        // Read into memory (files are small) so we can check the size, the signature and the fingerprint.
        var maxBytes = MaxFileSizeMb * 1024L * 1024L;
        using var buffer = new MemoryStream();
        await CopyWithLimitAsync(content, buffer, maxBytes, ct);
        if (buffer.Length > maxBytes)
            return Result.Failure<int>($"The file is larger than the {MaxFileSizeMb} MB limit.");
        if (buffer.Length == 0)
            return Result.Failure<int>("The file is empty.");
        if (!StartsWith(buffer.GetBuffer(), buffer.Length, type.Signature))
            return Result.Failure<int>($"'{safeName}' does not look like a genuine {extension} file. Please upload the original document.");

        buffer.Position = 0;
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(buffer, ct)).ToLowerInvariant();
        buffer.Position = 0;
        var storedName = await storage.SaveAsync(buffer, extension.ToLowerInvariant(), ct);

        var entity = new Attachment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            Category = category,
            OriginalFileName = safeName,
            StoredFileName = storedName,
            ContentType = type.Mime,
            SizeBytes = buffer.Length,
            Sha256 = sha
        };
        attachments.Add(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(entity.Id);
    }

    /// <summary>Removes a file from the record (soft delete: the file and its history are kept).</summary>
    public async Task<Result> RemoveAsync(string ownerType, int ownerId, int attachmentId, CancellationToken ct = default)
    {
        var entity = await attachments.GetByIdAsync(attachmentId, ct);
        if (entity is null || entity.OwnerType != ownerType || entity.OwnerId != ownerId)
            return Result.Failure("File not found.");

        attachments.Remove(entity);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Opens a file for download, checking it belongs to the given record.</summary>
    public async Task<FileDownload?> OpenAsync(string ownerType, int ownerId, int attachmentId, CancellationToken ct = default)
    {
        var entity = await attachments.GetByIdAsync(attachmentId, ct);
        if (entity is null || entity.OwnerType != ownerType || entity.OwnerId != ownerId) return null;

        var stream = await storage.OpenReadAsync(entity.StoredFileName, ct);
        return stream is null ? null : new FileDownload(entity.OriginalFileName, entity.ContentType, stream);
    }

    private static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.Category, a.OriginalFileName, a.ContentType, a.SizeBytes, a.CreatedAt, a.CreatedBy);

    /// <summary>Copies at most maxBytes + 1 bytes, so an oversized upload is detected without reading all of it.</summary>
    private static async Task CopyWithLimitAsync(Stream source, Stream target, long maxBytes, CancellationToken ct)
    {
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            await target.WriteAsync(chunk.AsMemory(0, read), ct);
            if (target.Length > maxBytes) return;
        }
    }

    private static bool StartsWith(byte[] data, long length, byte[] signature) =>
        length >= signature.Length && data.AsSpan(0, signature.Length).SequenceEqual(signature);
}
