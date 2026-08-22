using SkiaSharp;

namespace PdfTool.Core.Compression;

/// <summary>
/// Decides whether a decoded image still needs its colour channels.
///
/// Scanners routinely store a black-on-white page as RGB, and a colour JPEG of grey content costs
/// noticeably more than a grey one while looking identical. Getting this wrong in the other
/// direction drains the colour out of a logo or a stamp, so the test is deliberately hard to pass.
/// </summary>
internal static class ImageColorAnalyzer
{
    /// <summary>
    /// How far the channels of one pixel may drift apart before it counts as coloured. Chroma noise
    /// on a grey scan that has already been through a JPEG encoder reaches single digits, so a
    /// small allowance is what separates noise from intent.
    /// </summary>
    private const int ChannelSpreadTolerance = 24;

    /// <summary>
    /// Share of coloured pixels an image may contain and still count as grey. Anything a reader
    /// would notice as colour -- a logo, a stamp, a signature, a highlighted row -- covers far more
    /// of a page than this; scattered noise covers far less.
    /// </summary>
    private const double ColoredPixelBudget = 0.001;

    /// <summary>
    /// True when every pixel that matters is grey. Unrecognised pixel layouts answer false, because
    /// keeping colour that was not needed only costs bytes.
    /// </summary>
    public static bool IsEffectivelyGrayscale(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Gray8)
            return true;

        // Only the fully unpacked 8-bit layouts are read directly. Red and blue swap between these
        // two, which does not matter: the test is how far apart the three channels are, not which
        // is which.
        if (bitmap.ColorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888))
            return false;

        var pixelCount = bitmap.Width * bitmap.Height;
        var pixels = bitmap.GetPixelSpan();

        if (pixelCount <= 0 || pixels.Length < (long)pixelCount * 4)
            return false;

        var budget = (long)(pixelCount * ColoredPixelBudget);
        var colored = 0L;

        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            var offset = pixel * 4;
            int first = pixels[offset], second = pixels[offset + 1], third = pixels[offset + 2];

            var spread = Math.Max(first, Math.Max(second, third)) - Math.Min(first, Math.Min(second, third));

            if (spread > ChannelSpreadTolerance && ++colored > budget)
                return false;
        }

        return true;
    }
}
