using Grsms.Application.Common.Interfaces;

namespace Grsms.Tests.Infrastructure;

/// <summary>13 Nov 2026: stands in for headless Chromium in the web tests; keeps the last HTML it was given.</summary>
public sealed class FakePdfRenderer : IPdfRenderer
{
    public bool IsAvailable => true;
    public string? LastHtml { get; private set; }

    public Task<byte[]?> RenderAsync(string html, CancellationToken ct = default)
    {
        LastHtml = html;
        return Task.FromResult<byte[]?>("%PDF-1.7\n%fake\n%%EOF"u8.ToArray());
    }
}
