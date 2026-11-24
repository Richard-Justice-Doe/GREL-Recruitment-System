using Grsms.Application.Common.Interfaces;

namespace Grsms.Infrastructure.Storage;

/// <summary>
/// Phase 27: the placeholder virus scanner, used only when no ClamAV is configured (Antivirus:Host empty: development, tests; since
/// 22 Nov 2026 production uses <see cref="ClamAvScanner"/>). Every file passes; the extension, size and real-content ("magic bytes") checks in
/// AttachmentService still apply. Replace this registration in DependencyInjection.cs to connect a real scanner.
/// </summary>
public sealed class NoVirusScanner : IVirusScanner
{
    public Task<VirusScanResult> ScanAsync(Stream content, string fileName, CancellationToken ct = default) =>
        Task.FromResult(VirusScanResult.Passed);
}
