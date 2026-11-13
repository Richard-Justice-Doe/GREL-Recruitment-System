using Grsms.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grsms.Tests;

/// <summary>
/// 13 Nov 2026: the real headless-Chromium renderer. Runs only where a Chromium browser (Chrome, Edge, chromium) is installed,
/// e.g. a developer PC or the Docker image; elsewhere it checks that the renderer reports itself unavailable.
/// </summary>
public class ChromiumPdfRendererTests
{
    [Fact]
    public async Task Renders_a_self_contained_page_to_a_pdf_when_a_browser_is_installed()
    {
        var renderer = new ChromiumPdfRenderer(new ConfigurationBuilder().Build(), NullLogger<ChromiumPdfRenderer>.Instance);
        if (!renderer.IsAvailable)
        {
            Assert.Null(await renderer.RenderAsync("<p>x</p>"));
            return;
        }
        var pdf = await renderer.RenderAsync("<!DOCTYPE html><html><body><h1>Offer of employment</h1><p>GHS 4,200.00</p></body></html>");
        Assert.NotNull(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 500);
    }

    [Fact]
    public void A_configured_path_that_does_not_exist_means_no_pdf()
    {
        Assert.Null(ChromiumPdfRenderer.Find(Path.Combine(Path.GetTempPath(), "no-such-browser.exe")));
    }
}
