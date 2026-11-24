namespace Grsms.Domain.Documents;

/// <summary>
/// docs/36 AI-4: the width and height of a PNG or JPEG, read from the file's header only (no image library, nothing decoded).
/// Used to flag photos and scans of too low a resolution to read.
/// </summary>
public static class ImageSize
{
    public static (int Width, int Height)? TryRead(ReadOnlySpan<byte> b)
    {
        // PNG: 8-byte signature, then the IHDR chunk: length(4) "IHDR"(4) width(4) height(4), big-endian.
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[12] == (byte)'I' && b[13] == (byte)'H')
            return Valid(BigEndian(b[16..20]), BigEndian(b[20..24]));

        // JPEG: walk the markers to the first "start of frame" (SOF0–SOF15 except DHT C4, JPG C8, DAC CC).
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                if (marker == 0xFF) { i++; continue; }                         // padding
                if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; } // markers without a length
                if (marker == 0xD9 || marker == 0xDA) return null;             // end of image / start of scan before any frame
                var length = (b[i + 2] << 8) | b[i + 3];
                if (length < 2) return null;
                if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                    return Valid((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]); // height at +5, width at +7
                i += 2 + length;
            }
        }
        return null;
    }

    private static int BigEndian(ReadOnlySpan<byte> s) => (s[0] << 24) | (s[1] << 16) | (s[2] << 8) | s[3];

    private static (int, int)? Valid(int w, int h) => w > 0 && h > 0 && w < 100_000 && h < 100_000 ? (w, h) : null;
}
