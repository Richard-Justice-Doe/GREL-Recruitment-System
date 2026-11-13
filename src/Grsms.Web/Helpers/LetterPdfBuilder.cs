using Grsms.Application.Common.Interfaces;
using Grsms.Application.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Grsms.Web.Helpers;

/// <summary>What Views/Shared/_LetterPdf.cshtml renders: one letter partial on a self-contained A4 page.</summary>
public record LetterPdfModel(string Title, string PartialName, object Letter, bool Draft, string Css);

/// <summary>
/// 13 November 2026 (docs/26 Phase 24): the offer and appointment letters as PDF. The same Razor partial as on screen
/// (_OfferLetter, _AppointmentLetter) is rendered into one self-contained page (Bootstrap and the letter styles inline,
/// logo and signature as data: URIs, because the browser that prints it is not signed in), then printed by
/// <see cref="IPdfRenderer"/> (headless Chromium). A draft gets a "DRAFT" mark.
/// </summary>
public class LetterPdfBuilder(ICompositeViewEngine engine, ITempDataProvider tempData, IWebHostEnvironment env, LetterSettingsService letters, IPdfRenderer pdf)
{
    private static string? _css;

    public bool IsAvailable => pdf.IsAvailable;

    public async Task<byte[]?> BuildAsync(ControllerContext context, string title, string partialName, object letter, bool draft, CancellationToken ct)
    {
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = new LetterPdfModel(title, partialName, letter, draft, Css())
        };
        viewData["LogoSrc"] = DataUri(await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath, "images", "grel-logo.png"), ct), "image/png");
        viewData["SignatureSrc"] = "";
        if (await letters.OpenSignatureAsync(ct) is { } signature)
            await using (signature.Content)
            {
                using var buffer = new MemoryStream();
                await signature.Content.CopyToAsync(buffer, ct);
                viewData["SignatureSrc"] = DataUri(buffer.ToArray(), signature.ContentType);
            }

        var view = engine.GetView(null, "~/Views/Shared/_LetterPdf.cshtml", isMainPage: true).View
                   ?? throw new InvalidOperationException("The PDF letter page is missing.");
        await using var html = new StringWriter();
        var viewContext = new ViewContext(context, view, viewData, new TempDataDictionary(context.HttpContext, tempData), html, new HtmlHelperOptions());
        await view.RenderAsync(viewContext);
        return await pdf.RenderAsync(html.ToString(), ct);
    }

    private static string DataUri(byte[] bytes, string contentType) => $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";

    /// <summary>Bootstrap (the letters use its classes) plus the print styles of a letter, read once.</summary>
    private string Css() => _css ??= File.ReadAllText(Path.Combine(env.WebRootPath, "lib", "bootstrap", "dist", "css", "bootstrap.min.css")) + LetterCss;

    private const string LetterCss = """

        @page { size: A4; margin: 16mm 18mm; }
        html, body { background: #fff; color: #1d2125; }
        body { font-family: "Segoe UI", "Liberation Sans", "DejaVu Sans", Arial, sans-serif; font-size: 10.5pt; line-height: 1.45; }
        .card { border: 0 !important; box-shadow: none !important; background: #fff !important; }
        .card-body { padding: 0 !important; }
        .table { --bs-table-bg: transparent; }
        .table th { font-weight: 600; padding-right: 1.5rem; }
        .letterhead { display: flex; align-items: center; gap: 1rem; padding-bottom: 0.8rem; margin-bottom: 1.4rem; border-bottom: 3px solid #3cb54a; }
        .letterhead-logo { height: 70px; width: auto; flex: none; }
        .letterhead-name { font-size: 14pt; font-weight: 800; }
        .letterhead-sub { font-size: 9.5pt; color: #5c636a; }
        .letterhead-contact { margin: 0; margin-left: auto; font-size: 8.5pt; line-height: 1.35; text-align: right; color: #5c636a; font-style: normal; }
        .letter-signature { display: block; max-height: 70px; max-width: 220px; margin: 0.35rem 0; }
        .letter-signature-space { height: 3rem; }
        .letter-sign-box, .d-print-none, form, button { display: none !important; }
        .draft-mark { position: fixed; top: 38%; left: 0; right: 0; text-align: center; font-size: 110pt; font-weight: 800;
            color: rgba(220, 53, 69, 0.13); transform: rotate(-28deg); pointer-events: none; }
        * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        """;
}
