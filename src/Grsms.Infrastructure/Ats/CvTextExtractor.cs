using System.IO.Compression;
using System.Text;
using System.Xml;
using Grsms.Application.Applications;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Grsms.Infrastructure.Ats;

/// <summary>
/// Phase 15: reads the text of a CV on GREL's own server. Nothing is sent anywhere.
///   * PDF:  with PdfPig (open-source, Apache 2.0), page by page in reading order. A scanned CV (a picture of paper)
///           has no text, so the result is empty and staff are told to read the file itself.
///   * DOCX: a Word file is a zip of XML files; the paragraphs of word/document.xml are read directly (no Word needed).
///           The XML reader refuses DTDs, so a crafted file cannot make it fetch or expand anything.
/// Files have already been checked by AttachmentService (type, size, first bytes) before they get here.
/// Limits: 60 pages of PDF and 20 MB of Word XML, so a hostile file cannot keep the server busy.
/// </summary>
public sealed class CvTextExtractor(ILogger<CvTextExtractor> logger) : ICvTextExtractor
{
    private const int MaxPdfPages = 60;
    private const long MaxDocxXmlBytes = 20 * 1024 * 1024;

    public async Task<string?> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx")) return null;

        // Both libraries need to jump around in the file, so read it into memory first (uploads are at most a few MB).
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        try
        {
            var text = ext == ".pdf" ? Pdf(buffer) : Docx(buffer);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A damaged or unusual file: screening carries on with the profile only.
            logger.LogWarning(ex, "Could not read the text of CV file {FileName}", fileName);
            return null;
        }
    }

    private static string Pdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var text = new StringBuilder();
        foreach (var page in pdf.GetPages().Take(MaxPdfPages))
            text.AppendLine(ContentOrderTextExtractor.GetText(page));
        return text.ToString();
    }

    private static string Docx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml");
        if (entry is null || entry.Length > MaxDocxXmlBytes) return "";

        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        using var reader = XmlReader.Create(entry.Open(), settings);
        var text = new StringBuilder();
        var more = reader.Read();
        while (more)
        {
            // ReadElementContentAsString already moves to the next node, so it must not be followed by another Read().
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && !reader.IsEmptyElement)
            {
                text.Append(reader.ReadElementContentAsString());
                more = !reader.EOF;
                continue;
            }
            if (reader.NodeType is XmlNodeType.Element or XmlNodeType.EndElement)
            switch (reader.LocalName)
            {
                case "tab" when reader.NodeType == XmlNodeType.Element:
                    text.Append(' ');
                    break;
                case "br" or "cr" when reader.NodeType == XmlNodeType.Element:
                    text.Append('\n');
                    break;
                case "p" when reader.NodeType == XmlNodeType.EndElement:
                    text.Append('\n');
                    break;
                case "tc" when reader.NodeType == XmlNodeType.EndElement: // table cells: keep them apart
                    text.Append(" | ");
                    break;
            }
            more = reader.Read();
        }
        return text.ToString();
    }
}
