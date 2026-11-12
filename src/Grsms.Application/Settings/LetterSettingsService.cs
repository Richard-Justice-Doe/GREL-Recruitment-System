using Grsms.Application.Common;
using Grsms.Application.Common.Interfaces;
using Grsms.Domain.Settings;

namespace Grsms.Application.Settings;

/// <summary>What the offer and appointment letters print in their letterhead and signature block.</summary>
public record LetterDetails(
    string CompanyName,
    string? Department,
    IReadOnlyList<string> AddressLines,
    string? Phone,
    string? Email,
    string? Website,
    string? SignatoryName,
    string? SignatoryTitle,
    bool HasSignature)
{
    /// <summary>Phone, email and website on one line, e.g. "Tel +233 … · hr@… · www.…".</summary>
    public string ContactLine => string.Join(" · ", new[] { Phone is null ? null : $"Tel {Phone}", Email, Website }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// 12 November 2026: Administration → Letterhead &amp; signature. The address, contacts and signatory printed on the offer and
/// appointment letters, plus an optional scanned signature (PNG or JPEG, at most 1 MB, virus-scanned, kept in the file store,
/// not in the web folder). Only the Super Admin ("settings.manage") changes them; the text is saved through
/// <see cref="SystemSettingsService"/>, so it gets the same checks and audit trail as every other setting.
/// </summary>
public class LetterSettingsService(ISettingsStore store, SystemSettingsService settings, IFileStorage files, IVirusScanner scanner)
{
    public const int MaxSignatureBytes = 1024 * 1024;
    private const string NotAllowed = "Only a Super Admin can change the letterhead and signature.";

    public bool CanManage => settings.CanManage;

    public LetterDetails Get()
    {
        string? Value(string key) => store.Get(key) is { Length: > 0 } v ? v.Trim() : null;
        return new LetterDetails(
            Value(SettingCatalogue.LetterCompany) ?? "Ghana Rubber Estates Limited",
            Value(SettingCatalogue.LetterDepartment),
            (Value(SettingCatalogue.LetterAddress) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Value(SettingCatalogue.LetterPhone), Value(SettingCatalogue.LetterEmail), Value(SettingCatalogue.LetterWebsite),
            Value(SettingCatalogue.LetterSignatoryName), Value(SettingCatalogue.LetterSignatoryTitle),
            Value(SettingCatalogue.LetterSignatureFile) is not null);
    }

    public IReadOnlyList<SettingRowDto> GetFields() => settings.GetGroup(SettingCatalogue.Letters);

    public Task<Result> SaveAsync(IReadOnlyDictionary<string, string?> values, CancellationToken ct = default) =>
        settings.SaveGroupAsync(SettingCatalogue.Letters, values, ct);

    public async Task<Result> SaveSignatureAsync(Stream content, string fileName, long length, CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        if (length <= 0) return Result.Failure("Choose the signature image to upload.");
        if (length > MaxSignatureBytes) return Result.Failure("The signature image must be 1 MB or smaller.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var extension = ImageExtension(bytes);
        if (extension is null) return Result.Failure("The signature must be a PNG or JPEG image.");

        buffer.Position = 0;
        var scan = await scanner.ScanAsync(buffer, fileName, ct);
        if (!scan.Clean) return Result.Failure($"The file was refused by the virus scanner{(scan.Threat is null ? "" : $": {scan.Threat}")}.");

        buffer.Position = 0;
        var stored = await files.SaveAsync(buffer, extension, ct);
        await store.SaveAsync(new Dictionary<string, string?> { [SettingCatalogue.LetterSignatureFile] = stored }, ct);
        return Result.Success();
    }

    public async Task<Result> RemoveSignatureAsync(CancellationToken ct = default)
    {
        if (!CanManage) return Result.Failure(NotAllowed);
        await store.SaveAsync(new Dictionary<string, string?> { [SettingCatalogue.LetterSignatureFile] = null }, ct);
        return Result.Success();
    }

    /// <summary>The signature image and its content type, or null when none is uploaded.</summary>
    public async Task<(Stream Content, string ContentType)?> OpenSignatureAsync(CancellationToken ct = default)
    {
        if (store.Get(SettingCatalogue.LetterSignatureFile) is not { Length: > 0 } name) return null;
        var stream = await files.OpenReadAsync(name, ct);
        if (stream is null) return null;
        return (stream, name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
    }

    /// <summary>".png" or ".jpg" from the file's first bytes (not its name), otherwise null.</summary>
    public static string? ImageExtension(byte[] b) =>
        b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 ? ".png"
        : b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF ? ".jpg"
        : null;
}
