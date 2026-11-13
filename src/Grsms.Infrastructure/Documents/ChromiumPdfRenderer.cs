using System.Diagnostics;
using Grsms.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Grsms.Infrastructure.Documents;

/// <summary>
/// 13 November 2026 (docs/26 Phase 24, decision Q-O3): PDF letters through headless Chromium. The browser is run directly
/// (<c>--headless --print-to-pdf</c>), so no extra library is needed:
///   * "Pdf:ChromiumPath" in the configuration names the browser; otherwise the usual places are tried: /usr/bin/chromium
///     (installed in the Docker image), Google Chrome, Microsoft Edge.
///   * The page must be self-contained (styles inline, images as data: URIs): it is opened from a temporary file with no
///     network access to GRSMS and no sign-in.
///   * One letter at a time, at most 60 seconds each; every run uses its own temporary profile, deleted afterwards.
/// </summary>
public sealed class ChromiumPdfRenderer(IConfiguration config, ILogger<ChromiumPdfRenderer> logger) : IPdfRenderer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private readonly Lazy<string?> _path = new(() => Find(config["Pdf:ChromiumPath"]));

    public bool IsAvailable => _path.Value is not null;

    public static string? Find(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? configured : null;
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] candidates =
        [
            "/usr/bin/chromium", "/usr/bin/chromium-browser", "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable",
            Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe")
        ];
        return candidates.FirstOrDefault(p => p.Length > 0 && File.Exists(p));
    }

    public async Task<byte[]?> RenderAsync(string html, CancellationToken ct = default)
    {
        if (_path.Value is not { } browser) return null;
        var work = Path.Combine(Path.GetTempPath(), "grsms-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var input = Path.Combine(work, "letter.html");
        var output = Path.Combine(work, "letter.pdf");
        await File.WriteAllTextAsync(input, html, ct);

        await _oneAtATime.WaitAsync(ct);
        try
        {
            var start = new ProcessStartInfo(browser)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
            };
            foreach (var arg in new[]
                     {
                         "--headless", "--disable-gpu", "--no-sandbox", "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                         "--disable-dev-shm-usage", "--hide-scrollbars", $"--user-data-dir={Path.Combine(work, "profile")}",
                         "--no-pdf-header-footer", "--print-to-pdf-no-header", "--run-all-compositor-stages-before-draw",
                         "--virtual-time-budget=3000", $"--print-to-pdf={output}", new Uri(input).AbsoluteUri
                     })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Chromium did not start.");
            var stderr = process.StandardError.ReadToEndAsync(ct);
            _ = process.StandardOutput.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                if (ct.IsCancellationRequested) throw;
                logger.LogWarning("PDF: Chromium took longer than {Seconds} s and was stopped", Timeout.TotalSeconds);
                return null;
            }

            if (File.Exists(output) && new FileInfo(output).Length > 0) return await File.ReadAllBytesAsync(output, ct);
            logger.LogWarning("PDF: Chromium exited with code {Code} without a PDF. {Error}", process.ExitCode, (await stderr).Trim());
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            logger.LogWarning(ex, "PDF: Chromium could not be run from {Path}", browser);
            return null;
        }
        finally
        {
            _oneAtATime.Release();
            try { Directory.Delete(work, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
