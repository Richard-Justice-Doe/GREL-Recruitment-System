using Grsms.Application.Onboarding;
using Grsms.Application.Reports;
using Grsms.Domain.Documents;
using Grsms.Domain.Hiring;
using Grsms.Domain.Onboarding;
using Grsms.Domain.Security;
using Grsms.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Grsms.Tests;

/// <summary>
/// docs/36 step AI-4: automatic document checks by rules (no AI): image size from the file header, "valid until" dates of ID-type
/// documents (expired, expiring, not given), the same file used twice or by another new employee (shown to staff only), and the
/// Document checks report.
/// </summary>
public class DocumentCheckTests : OnboardingTestBase
{
    /// <summary>A PNG header of the given size (enough for the size reader and the file-type check).</summary>
    private static MemoryStream Png(int w, int h)
    {
        var b = new byte[40];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(b, 0);
        BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(w)).CopyTo(b, 16);
        BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(h)).CopyTo(b, 20);
        return new MemoryStream(b);
    }

    [Fact]
    public void Image_size_is_read_from_png_and_jpeg_headers()
    {
        Assert.Equal((800, 600), ImageSize.TryRead(Png(800, 600).ToArray()));
        // JPEG: SOI, an APP0 segment (length 4), then SOF0 with height 0x0438 (1080) and width 0x0780 (1920).
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x04, 0x38, 0x07, 0x80, 0x03, 0x00, 0x00, 0x00];
        Assert.Equal((1920, 1080), ImageSize.TryRead(jpeg));
        Assert.Null(ImageSize.TryRead("%PDF-1.4"u8));
    }

    [Fact]
    public async Task Expiry_duplicates_and_low_resolution_are_flagged_and_reported()
    {
        // Two onboardings: Ama's and Kofi's.
        var amaApp = await WithOfferAsync(_candidate, "Ama Mensah", OfferStatus.Accepted);
        var ama = await StartAsync(amaApp);
        var kofiApp = await WithOfferAsync(_candidate2, "Kofi Boateng", OfferStatus.Accepted);
        var kofi = await StartAsync(kofiApp);
        var card = await DocAsync(ama, "GhanaCard");
        var ssnit = await DocAsync(ama, "SsnitCard");

        // Ama: a small photo of her Ghana Card with an expired date, and the same file for her SSNIT card.
        As(_candidate, Roles.Candidate);
        Assert.False((await RunDocs(s => s.UploadAsync(card.Id, "card.png", Png(640, 480), expiresOn: new DateOnly(1990, 1, 1)))).Succeeded); // not a plausible date
        Assert.True((await RunDocs(s => s.UploadAsync(card.Id, "card.png", Png(640, 480), expiresOn: Today.AddDays(-3)))).Succeeded);
        Assert.True((await RunDocs(s => s.UploadAsync(ssnit.Id, "same.png", Png(640, 480)))).Succeeded);
        // Kofi uploads the very same file as his Ghana Card.
        var kofiCardId = (await DocAsync(kofi, "GhanaCard")).Id;
        As(_candidate2, Roles.Candidate);
        Assert.True((await RunDocs(s => s.UploadAsync(kofiCardId, "mine.png", Png(640, 480)))).Succeeded);

        // What Ama sees: her own remarks, not that someone else used the same file.
        As(_candidate, Roles.Candidate);
        var mine = (await RunDocs(s => s.GetMineAsync(amaApp)))!.Documents.Single(d => d.Code == "GhanaCard");
        Assert.True(mine.HasExpiry);
        Assert.Contains(mine.Flags!, f => f.Code == "expired");
        Assert.Contains(mine.Flags!, f => f.Code == "low-resolution" && f.Message.Contains("640 × 480"));
        Assert.Contains(mine.Flags!, f => f.Code == "duplicate" && f.Message.Contains("SSNIT"));
        Assert.DoesNotContain(mine.Flags!, f => f.StaffOnly);

        // The RO sees everything and can correct the date.
        AsRecruiter();
        var forRo = (await RunDocs(s => s.GetForHrAsync(ama)))!.Documents.Single(d => d.Code == "GhanaCard");
        Assert.Contains(forRo.Flags!, f => f.Code == "duplicate-other" && f.Severity == "danger");
        Assert.True((await RunDocs(s => s.SetExpiryAsync(card.Id, Today.AddDays(30)))).Succeeded);
        forRo = (await RunDocs(s => s.GetForHrAsync(ama)))!.Documents.Single(d => d.Code == "GhanaCard");
        Assert.Contains(forRo.Flags!, f => f.Code == "expires-soon");
        Assert.False((await RunDocs(s => s.SetExpiryAsync(ssnit.Id, Today.AddDays(30)))).Succeeded); // the SSNIT card has no expiry

        // Kofi's card: no date given.
        var kofiCard = (await RunDocs(s => s.GetForHrAsync(kofi)))!.Documents.Single(d => d.Code == "GhanaCard");
        Assert.Contains(kofiCard.Flags!, f => f.Code == "no-expiry");

        // The report (personal data: HR with "Open personnel files").
        AsHrManager();
        using var scope = _db.NewScope();
        var report = await scope.ServiceProvider.GetRequiredService<ReportService>().RunAsync(new ReportFilter { Report = ReportKeys.DocumentChecks });
        Assert.Contains(report.Table.Rows, r => (string?)r.Cells[1] == "Kofi Boateng" && (string?)r.Cells[4] == "Act now");
        Assert.Equal("2", report.Kpis.Single(k => k.Label == "Onboardings concerned").Value);
    }
}
