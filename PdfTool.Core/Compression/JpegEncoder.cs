using SkiaSharp;

namespace PdfTool.Core.Compression;

/// <summary>
/// Encodes a bitmap as JPEG and reports the colour space the PDF must declare for it.
///
/// The declared colour space is read back out of the encoded bytes rather than assumed from what
/// was asked for. A mismatch between the two would not fail loudly; it would render the page in
/// the wrong colours, which is the kind of damage worth a few lines of parsing to rule out.
/// </summary>
internal static class JpegEncoder
{
    /// <summary>A JPEG and the PDF colour space that describes it, or null when either step failed.</summary>
    public sealed record Encoded(byte[] Bytes, string ColorSpace);

    public static Encoded? Encode(SKBitmap bitmap, int quality, bool asGrayscale)
    {
        SKBitmap? grayscale = null;

        try
        {
            if (asGrayscale)
            {
                // A failed conversion is a reason to fall back to colour, not to give up on the image.
                grayscale = bitmap.Copy(SKColorType.Gray8);
            }

            using var image = SKImage.FromBitmap(grayscale ?? bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

            if (data is null)
                return null;

            var bytes = data.ToArray();
            var colorSpace = ReadColorSpace(bytes);

            return colorSpace is null ? null : new Encoded(bytes, colorSpace);
        }
        finally
        {
            grayscale?.Dispose();
        }
    }

    /// <summary>
    /// The PDF colour space matching the component count in the JPEG's frame header, or null when
    /// the file cannot be read or uses a component count with no plain device equivalent.
    /// </summary>
    private static string? ReadColorSpace(byte[] jpeg) => ReadComponentCount(jpeg) switch
    {
        1 => "/DeviceGray",
        3 => "/DeviceRGB",
        _ => null
    };

    /// <summary>
    /// Walks the marker segments to the start-of-frame header and returns its component count, or
    /// zero if the structure does not hold up on the way there.
    /// </summary>
    private static int ReadComponentCount(byte[] jpeg)
    {
        const byte MarkerPrefix = 0xFF;

        if (jpeg.Length < 4 || jpeg[0] != MarkerPrefix || jpeg[1] != 0xD8)
            return 0;

        var position = 2;

        while (position + 3 < jpeg.Length)
        {
            if (jpeg[position] != MarkerPrefix)
                return 0;

            // Any number of fill bytes may pad the gap before a marker.
            while (position + 1 < jpeg.Length && jpeg[position + 1] == MarkerPrefix)
                position++;

            if (position + 3 >= jpeg.Length)
                return 0;

            var marker = jpeg[position + 1];

            // Markers that stand alone carry no length field to skip over.
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD8)
            {
                position += 2;
                continue;
            }

            // Image data begins, or the file ends, without a frame header having been found.
            if (marker is 0xD9 or 0xDA)
                return 0;

            var segmentLength = (jpeg[position + 2] << 8) | jpeg[position + 3];

            if (segmentLength < 2 || position + 2 + segmentLength > jpeg.Length)
                return 0;

            if (IsStartOfFrame(marker))
            {
                // Segment layout: length (2), sample precision (1), height (2), width (2), components (1).
                var componentCount = position + 9;
                return componentCount < jpeg.Length ? jpeg[componentCount] : 0;
            }

            position += 2 + segmentLength;
        }

        return 0;
    }

    /// <summary>
    /// True for the SOFn markers. The range they occupy is shared with three unrelated tables,
    /// which have to be stepped over rather than read as a frame.
    /// </summary>
    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
}
