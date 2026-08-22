using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfTool.Core.Documents;
using SkiaSharp;

namespace PdfTool.Core.Compression;

/// <summary>
/// Downsamples and re-encodes the embedded images that dominate the size of most PDFs.
///
/// The approach is deliberately conservative: anything whose encoding cannot be interpreted with
/// confidence is left exactly as it was. Skipping an image costs some saving; corrupting one costs
/// the user their document.
/// </summary>
internal static class PdfImageOptimizer
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public static int Optimize(
        PdfDocument document,
        CompressionSettings settings,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var images = CollectImages(document);
        var masks = CollectMasks(images);
        var recompressed = 0;
        var processed = 0;

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Soft masks and stencil masks carry transparency in their own colour space; re-encoding
            // them as RGB JPEG would silently destroy it.
            if (!masks.Contains(image) && TryRecompress(image, settings, cancellationToken))
                recompressed++;

            processed++;
            progress?.Report(new CompressionProgress(processed, images.Count, recompressed));
        }

        return recompressed;
    }

    /// <summary>
    /// Every distinct image XObject reachable from any page, including inside form XObjects.
    /// PDFsharp resolves an indirect object to a single instance, so reference identity is what
    /// distinguishes a genuinely shared image from two copies of one.
    /// </summary>
    private static List<PdfDictionary> CollectImages(PdfDocument document)
    {
        var images = new List<PdfDictionary>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < document.PageCount; i++)
            CollectFromResources(document.Pages[i].Elements.GetDictionary("/Resources"), images, visited);

        return images;
    }

    private static void CollectFromResources(
        PdfDictionary? resources,
        List<PdfDictionary> images,
        HashSet<PdfDictionary> visited)
    {
        var xObjects = resources?.Elements.GetDictionary("/XObject");
        if (xObjects is null)
            return;

        foreach (var key in xObjects.Elements.Keys.ToList())
        {
            if (PdfElements.Resolve(xObjects.Elements[key]) is not { } xObject)
                continue;

            if (!visited.Add(xObject))
                continue;

            switch (PdfElements.ReadName(xObject, "/Subtype"))
            {
                case "/Image":
                    images.Add(xObject);
                    break;

                case "/Form":
                    // Form XObjects carry their own resource dictionary and can nest.
                    CollectFromResources(xObject.Elements.GetDictionary("/Resources"), images, visited);
                    break;
            }
        }
    }

    private static HashSet<PdfDictionary> CollectMasks(List<PdfDictionary> images)
    {
        var masks = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);

        foreach (var image in images)
        {
            foreach (var key in new[] { "/SMask", "/Mask" })
            {
                if (PdfElements.Resolve(image.Elements[key]) is { } mask)
                    masks.Add(mask);
            }
        }

        return masks;
    }

    private static bool TryRecompress(
        PdfDictionary image, CompressionSettings settings, CancellationToken cancellationToken)
    {
        if (image.Stream is null)
            return false;

        // A stencil mask is 1 bit per pixel and is not a picture; leave it alone.
        if (image.Elements.ContainsKey("/ImageMask"))
            return false;

        // A decode array remaps the samples, commonly to invert them. Re-encoding without applying
        // it would produce a picture that is not the one on the page.
        if (image.Elements.ContainsKey("/Decode"))
            return false;

        // A colour-key mask names sample values to treat as transparent, two per colour component.
        // Re-encoding moves samples off those exact values and can change the component count, so
        // the mask would no longer describe the image it belongs to.
        if (image.Elements.ContainsKey("/Mask"))
            return false;

        var width = image.Elements.GetInteger("/Width");
        var height = image.Elements.GetInteger("/Height");

        if (width <= 0 || height <= 0)
            return false;

        var originalBytes = image.Stream.Value;
        if (originalBytes.Length == 0)
            return false;

        using var source = Decode(image, width, height, originalBytes)
            ?? PdfiumImageDecoder.TryDecode(image, originalBytes, width, height, cancellationToken);

        if (source is null)
            return false;

        var scale = Math.Min(1d, (double)settings.MaxImageEdgePixels / Math.Max(width, height));
        var targetWidth = Math.Max(1, (int)Math.Round(width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(height * scale));

        SKBitmap? scaled = null;

        try
        {
            if (scale < 1d)
            {
                scaled = source.Resize(new SKImageInfo(targetWidth, targetHeight), Sampling);

                // A resize that fails is a reason to leave the image alone, not to re-encode it
                // at full size for no benefit.
                if (scaled is null)
                    return false;
            }

            var bitmap = scaled ?? source;

            var asGrayscale = settings.ConvertGrayscaleImages
                && ImageColorAnalyzer.IsEffectivelyGrayscale(bitmap);

            if (JpegEncoder.Encode(bitmap, settings.JpegQuality, asGrayscale) is not { } encoded)
                return false;

            // Re-encoding is only worth doing when it wins by enough to pay for the generation of
            // quality it costs; JPEG can easily exceed a well-compressed original outright.
            if (encoded.Bytes.Length >= originalBytes.Length * (1 - settings.MinimumImageSavingFraction))
                return false;

            image.Stream.Value = encoded.Bytes;
            image.Elements.SetInteger("/Width", bitmap.Width);
            image.Elements.SetInteger("/Height", bitmap.Height);
            image.Elements.SetInteger("/BitsPerComponent", 8);
            image.Elements.SetName("/Filter", "/DCTDecode");
            image.Elements.SetName("/ColorSpace", encoded.ColorSpace);

            // Whatever parameters described the old stream do not describe this one.
            image.Elements.Remove("/DecodeParms");

            return true;
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    private static SKBitmap? Decode(PdfDictionary image, int width, int height, byte[] streamBytes) =>
        ReadFilterName(image) switch
        {
            // Already a JPEG: the stream bytes are the JPEG file, which describes its own layout.
            // Decode parameters take no part in reading one, so scanners that attach a stray
            // /Quality entry are no reason to skip the image.
            "/DCTDecode" => SKBitmap.Decode(streamBytes),

            // Raw samples behind a Flate wrapper. A predictor changes how those bytes have to be
            // read and none is applied here, so any decode parameters at all rule the image out.
            "/FlateDecode" when !image.Elements.ContainsKey("/DecodeParms") =>
                DecodeRawSamples(image, width, height),

            // Anything else (JPX, CCITT, JBIG2, filter chains) is left untouched.
            _ => null
        };

    /// <summary>
    /// The single filter applied to the stream, or null when there is a chain of them and the raw
    /// bytes therefore mean nothing on their own. Some producers write a lone filter as a
    /// one-element array, which says exactly what the bare name says.
    /// </summary>
    private static string? ReadFilterName(PdfDictionary image) =>
        PdfElements.Dereference(image.Elements["/Filter"]) is PdfArray array
            ? array.Elements.Count == 1 ? PdfElements.NameOf(array.Elements[0]) : null
            : PdfElements.NameOf(image.Elements["/Filter"]);

    private static SKBitmap? DecodeRawSamples(PdfDictionary image, int width, int height)
    {
        if (image.Elements.GetInteger("/BitsPerComponent") != 8)
            return null;

        var componentsPerPixel = PdfElements.ReadName(image, "/ColorSpace") switch
        {
            "/DeviceRGB" => 3,
            "/DeviceGray" => 1,
            _ => 0
        };

        if (componentsPerPixel == 0)
            return null;

        var pixelCount = (long)width * height;

        // The same ceiling the renderer applies. A dictionary can declare dimensions far beyond what
        // its samples justify, and four bytes each overflows an int long before it exhausts memory.
        if (pixelCount > PdfiumImageDecoder.MaximumPixels)
            return null;

        var samples = image.Stream?.UnfilteredValue;
        if (samples is null || samples.Length < pixelCount * componentsPerPixel)
            return null;

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        // A refused native allocation reports itself as a null buffer rather than by throwing, and
        // writing to it would take the process down where no handler could intervene.
        if (bitmap.GetPixels() == IntPtr.Zero)
        {
            bitmap.Dispose();
            return null;
        }

        try
        {
            var pixels = new byte[pixelCount * 4];

            for (var pixel = 0L; pixel < pixelCount; pixel++)
            {
                var source = pixel * componentsPerPixel;
                var target = pixel * 4;

                if (componentsPerPixel == 3)
                {
                    pixels[target] = samples[source];
                    pixels[target + 1] = samples[source + 1];
                    pixels[target + 2] = samples[source + 2];
                }
                else
                {
                    pixels[target] = pixels[target + 1] = pixels[target + 2] = samples[source];
                }

                pixels[target + 3] = byte.MaxValue;
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
