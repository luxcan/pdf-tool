using System.Globalization;
using System.Text;
using PDFtoImage;
using PdfSharp.Pdf;
using PdfTool.Core.Rendering;
using SkiaSharp;

namespace PdfTool.Core.Compression;

/// <summary>
/// Decodes an image the compressor cannot read itself by handing it back to PDFium, which already
/// ships with the renderer and understands every encoding a PDF may use.
///
/// The image is wrapped in a throwaway one-page document whose page is exactly its pixel size, so
/// what comes back is the image itself at full resolution rather than a view of some page it
/// happened to sit on. That keeps this a decoder like any other: the caller downsamples and
/// re-encodes the result through the same path it uses for JPEG and Flate images.
/// </summary>
internal static class PdfiumImageDecoder
{
    /// <summary>
    /// Ceiling on the pixels of a single decode, shared by every path that turns declared dimensions
    /// into a buffer. Four bytes each overflows an int well before it exhausts memory, and an image
    /// this large is already far past any sensible scan.
    /// </summary>
    internal const long MaximumPixels = 80_000_000;

    /// <summary>
    /// Colour spaces that mean the same thing wherever they appear. Any other name is shorthand for
    /// an entry in the page's own resources, which this document does not have and cannot carry.
    /// </summary>
    private static readonly HashSet<string> SelfContainedColorSpaces =
        ["/DeviceRGB", "/DeviceGray", "/DeviceCMYK"];

    /// <summary>
    /// The decoded image, or null when it cannot be recovered safely. Every failure is a reason to
    /// leave the original image alone, never to substitute something approximate.
    /// </summary>
    public static SKBitmap? TryDecode(
        PdfDictionary image,
        byte[] streamBytes,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        // Transparency lives in a separate stream that stays as it is. PDFium would compose it onto
        // the background here, and the mask would then be applied a second time when the page is
        // drawn, so these are left to the decoders that read only the samples they are given.
        if (image.Elements.ContainsKey("/SMask") || image.Elements.ContainsKey("/Mask"))
            return null;

        if ((long)width * height > MaximumPixels)
            return null;

        // A colour space named after an entry in the source page's resources cannot come along, and
        // PDFium answers an unresolvable one by drawing nothing rather than by failing.
        if (image.Elements["/ColorSpace"] is PdfName colorSpace
            && !SelfContainedColorSpaces.Contains(colorSpace.Value))
            return null;

        if (TryBuildSingleImageDocument(image, streamBytes, width, height) is not { } document)
            return null;

        var decoded = PdfiumGate.Run(() => Render(document, width), cancellationToken);

        // Last line of defence for everything else PDFium declines silently: a page it could not
        // draw comes back as the background it was given, and returning that would blank the image.
        if (decoded is not null && IsBlank(decoded))
        {
            decoded.Dispose();
            return null;
        }

        return decoded;
    }

    /// <summary>
    /// True when every pixel is the background the render started from, which is what an image
    /// PDFium refused to draw leaves behind. A real scan is never uniformly white to the last pixel.
    /// </summary>
    private static bool IsBlank(SKBitmap bitmap)
    {
        if (bitmap.ColorType is not (SKColorType.Rgba8888 or SKColorType.Bgra8888))
            return false;

        var pixels = bitmap.GetPixelSpan();
        var rowBytes = bitmap.RowBytes;

        for (var y = 0; y < bitmap.Height; y++)
        {
            var row = y * rowBytes;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = row + x * 4;

                if (pixels[offset] != 0xFF || pixels[offset + 1] != 0xFF || pixels[offset + 2] != 0xFF)
                    return false;
            }
        }

        return true;
    }

    private static SKBitmap? Render(byte[] document, int width)
    {
        try
        {
            using var stream = new MemoryStream(document);

            return Conversion.ToImage(stream, page: 0, leaveOpen: true, password: null, new RenderOptions
            {
                Width = width,
                WithAspectRatio = true,
                BackgroundColor = SKColors.White
            });
        }
        // A stream this code assembled is not a document the user chose, so a decoder that rejects
        // it is a fact about the image rather than a problem to report.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// A minimal document holding just this image, drawn across a page of its own pixel size.
    /// Returns null when the image dictionary points at other objects, which cannot come along.
    /// </summary>
    private static byte[]? TryBuildSingleImageDocument(
        PdfDictionary image, byte[] streamBytes, int width, int height)
    {
        if (TryWriteImageDictionary(image, streamBytes.Length) is not { } imageDictionary)
            return null;

        var content = Ascii($"q {width} 0 0 {height} 0 0 cm /Im0 Do Q\n");

        var objects = new List<byte[]>
        {
            Ascii("<</Type/Catalog/Pages 2 0 R>>"),
            Ascii("<</Type/Pages/Kids[3 0 R]/Count 1>>"),
            Ascii($"<</Type/Page/Parent 2 0 R/MediaBox[0 0 {width} {height}]" +
                  "/Resources<</XObject<</Im0 5 0 R>>>>/Contents 4 0 R>>"),
            Stream(Ascii($"<</Length {content.Length}>>"), content),
            Stream(Ascii(imageDictionary), streamBytes)
        };

        return Assemble(objects);
    }

    private static byte[] Assemble(List<byte[]> objects)
    {
        var output = new MemoryStream();
        output.Write(Ascii("%PDF-1.7\n"));

        var offsets = new List<long>(objects.Count);

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(Ascii($"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            output.Write(Ascii("\nendobj\n"));
        }

        var startOfTable = output.Position;

        output.Write(Ascii($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
            output.Write(Ascii($"{offset:D10} 00000 n \n"));

        output.Write(Ascii(
            $"trailer\n<</Size {objects.Count + 1}/Root 1 0 R>>\nstartxref\n{startOfTable}\n%%EOF\n"));

        return output.ToArray();
    }

    /// <summary>
    /// The image's own dictionary, copied entry for entry so the encoding is described exactly as
    /// the source document described it. The length is restated because it belongs to this copy.
    /// </summary>
    private static string? TryWriteImageDictionary(PdfDictionary image, int streamLength)
    {
        var builder = new StringBuilder("<<");

        foreach (var key in image.Elements.Keys)
        {
            if (key == "/Length")
                continue;

            WriteName(key, builder);

            if (!TryWriteValue(image.Elements[key], builder))
                return null;
        }

        return builder.Append($"/Length {streamLength}>>").ToString();
    }

    /// <summary>
    /// Writes one value in PDF syntax. Anything that refers to another object, or that this code
    /// does not recognise, answers false: a dictionary copied inaccurately would describe the
    /// samples wrongly, and a wrong description is worse than no decode at all.
    /// </summary>
    private static bool TryWriteValue(PdfItem? item, StringBuilder builder)
    {
        switch (item)
        {
            case PdfName name:
                WriteName(name.Value, builder);
                return true;

            case PdfInteger integer:
                builder.Append(integer.Value.ToString(CultureInfo.InvariantCulture)).Append(' ');
                return true;

            case PdfReal real:
                builder.Append(real.Value.ToString("R", CultureInfo.InvariantCulture)).Append(' ');
                return true;

            case PdfBoolean boolean:
                builder.Append(boolean.Value ? "true " : "false ");
                return true;

            case PdfNull:
                builder.Append("null ");
                return true;

            case PdfArray array:
                builder.Append('[');
                foreach (var element in array.Elements)
                {
                    if (!TryWriteValue(element, builder))
                        return false;
                }

                builder.Append(']');
                return true;

            // A dictionary carrying a stream is a separate object, which cannot be inlined here.
            case PdfDictionary dictionary when dictionary.Stream is null:
                builder.Append("<<");
                foreach (var key in dictionary.Elements.Keys)
                {
                    WriteName(key, builder);

                    if (!TryWriteValue(dictionary.Elements[key], builder))
                        return false;
                }

                builder.Append(">>");
                return true;

            default:
                return false;
        }
    }

    private static byte[] Stream(byte[] dictionary, byte[] contents)
    {
        var opening = Ascii("stream\n");
        var closing = Ascii("\nendstream");

        var buffer = new byte[dictionary.Length + opening.Length + contents.Length + closing.Length];
        var position = 0;

        foreach (var part in new[] { dictionary, opening, contents, closing })
        {
            part.CopyTo(buffer, position);
            position += part.Length;
        }

        return buffer;
    }

    /// <summary>
    /// Writes a name in PDF syntax. PDFsharp hands names back decoded, so anything a delimiter or a
    /// byte above the printable range has to be escaped again on the way out; leaving it raw would
    /// end the name early and desynchronise the dictionary that follows.
    /// </summary>
    private static void WriteName(string name, StringBuilder builder)
    {
        // The leading solidus is part of the value and introduces the name rather than belonging to it.
        builder.Append('/');

        foreach (var character in name.AsSpan(name.StartsWith('/') ? 1 : 0))
        {
            if (character is '#' or '/' or '%' or '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}'
                || character <= ' ' || character > '~')
            {
                builder.Append('#').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(character);
            }
        }

        builder.Append(' ');
    }

    /// <summary>
    /// Latin-1 rather than ASCII: every byte survives the trip. ASCII replaces anything above 0x7F
    /// with a question mark, which would corrupt the syntax silently rather than fail.
    /// </summary>
    private static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);
}
