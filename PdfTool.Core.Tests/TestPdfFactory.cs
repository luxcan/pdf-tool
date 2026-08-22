using System.Text;
using System.Runtime.InteropServices;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace PdfTool.Core.Tests;

/// <summary>
/// Builds fixture PDFs whose page widths encode which file and page each came from, so a merged
/// document can be checked for exact page order without rendering or reading text.
/// </summary>
internal static class TestPdfFactory
{
    private const int PageHeightPoints = 500;

    /// <summary>Fixed, so every fixture is byte-for-byte the same from one run to the next.</summary>
    private const int BaseSeed = 20260809;

    public static string Create(string directory, string fileName, int pageCount, int widthSeed)
    {
        var path = Path.Combine(directory, fileName);

        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            page.Width = XUnitOf(widthSeed + i);
            page.Height = XUnitOf(PageHeightPoints);
        }

        document.Save(path);
        return path;
    }

    /// <summary>Page widths of a document, rounded to whole points, in document order.</summary>
    public static IReadOnlyList<int> PageWidths(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return [.. Enumerable.Range(0, document.PageCount)
            .Select(i => (int)Math.Round(document.Pages[i].Width.Point))];
    }

    /// <summary>Page rotations of a document, in document order.</summary>
    public static IReadOnlyList<int> PageRotations(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return [.. Enumerable.Range(0, document.PageCount).Select(i => document.Pages[i].Rotate)];
    }

    /// <summary>
    /// Builds a document carrying one large photographic-style image per page, which is the shape
    /// the compressor is meant to shrink. The image is deliberately noisy so it does not compress
    /// away to nothing and leave the assertions measuring rounding error.
    /// </summary>
    public static string CreateWithImage(string directory, string fileName, int pageCount, int imageEdgePixels)
    {
        var imagePath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-source.jpg");
        File.WriteAllBytes(imagePath, CreateNoisyJpeg(imageEdgePixels, imageEdgePixels));

        return DrawImageOnPages(directory, fileName, pageCount, imagePath);
    }

    /// <summary>
    /// Builds a document whose image is stored as raw samples behind a Flate wrapper rather than as
    /// a JPEG, which is the other encoding the compressor reads directly.
    /// </summary>
    public static string CreateWithPngImage(
        string directory, string fileName, int pageCount, int imageEdgePixels)
    {
        var imagePath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-source.png");
        File.WriteAllBytes(imagePath, CreateNoisyPng(imageEdgePixels, imageEdgePixels));

        return DrawImageOnPages(directory, fileName, pageCount, imagePath);
    }

    private static string DrawImageOnPages(string directory, string fileName, int pageCount, string imagePath)
    {
        var path = Path.Combine(directory, fileName);

        using var document = new PdfDocument();
        using var image = XImage.FromFile(imagePath);

        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            page.Width = XUnitOf(595);
            page.Height = XUnitOf(842);

            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(image, 0, 0, 595, 842);
        }

        document.Save(path);
        return path;
    }

    /// <summary>
    /// Builds a document whose image is grey in every pixel but stored in colour, which is what a
    /// scanner produces from a black-on-white page and the case worth storing as greyscale.
    /// </summary>
    public static string CreateWithGrayscaleImage(
        string directory, string fileName, int pageCount, int imageEdgePixels)
    {
        var imagePath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-gray.jpg");
        File.WriteAllBytes(imagePath, CreateNoisyJpeg(imageEdgePixels, imageEdgePixels, grayscale: true));

        return DrawImageOnPages(directory, fileName, pageCount, imagePath);
    }

    /// <summary>
    /// A document carrying a different image on every page, each a different size so that a test can
    /// tell which one it is looking at. The other builders draw one image everywhere, which PDFsharp
    /// stores once and every page then shares - so nothing is unused and no image is distinguishable
    /// from another.
    /// </summary>
    public static string CreateWithDistinctImages(
        string directory, string fileName, int pageCount, int smallestImageEdgePixels)
    {
        var path = Path.Combine(directory, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);

        using var document = new PdfDocument();

        for (var i = 0; i < pageCount; i++)
        {
            var edge = ImageEdgeForPage(smallestImageEdgePixels, i);
            var imagePath = Path.Combine(directory, $"{stem}-source{i}.jpg");
            File.WriteAllBytes(imagePath, CreateNoisyJpeg(edge, edge, seed: BaseSeed + i));

            var page = document.AddPage();
            page.Width = XUnitOf(595);
            page.Height = XUnitOf(842);

            using var image = XImage.FromFile(imagePath);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(image, 0, 0, 595, 842);
        }

        document.Save(path);
        return path;
    }

    /// <summary>
    /// The pixel edge <see cref="CreateWithDistinctImages"/> gives page <paramref name="pageIndex"/>,
    /// which is how a test says which page's image it expects to find.
    /// </summary>
    public static int ImageEdgeForPage(int smallestImageEdgePixels, int pageIndex) =>
        smallestImageEdgePixels + (pageIndex * 40);

    /// <summary>
    /// Rewrites a document so every page shares one resource listing naming every image in it, while
    /// each page still draws only its own. This is the shape real scanners and layout software
    /// produce, and the reason a page taken out of one arrives carrying the whole document's images.
    /// </summary>
    public static void ShareOneResourceListing(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        var images = Enumerable
            .Range(0, document.PageCount)
            .Select(page => ImagesOnPage(document, page).Single())
            .ToList();

        var listing = new PdfDictionary(document);
        for (var i = 0; i < images.Count; i++)
            listing.Elements[NameOfImage(i)] = images[i].Reference ?? (PdfItem)images[i];

        for (var i = 0; i < document.PageCount; i++)
        {
            var page = document.Pages[i];
            var xObjects = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!;

            // Every page draws the one name its writer gave it. Renaming inside the stream, rather
            // than authoring a replacement, keeps the rest of the page's drawing intact.
            Rename(page, from: xObjects.Elements.Keys.Single(), to: NameOfImage(i));

            page.Elements.GetDictionary("/Resources")!.Elements["/XObject"] = listing;
        }

        document.Save(path);
    }

    /// <summary>Puts a form XObject into the listing, which is a thing that draws without being an image.</summary>
    public static void AddFormToListing(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        var form = new PdfDictionary(document);
        form.Elements["/Type"] = new PdfName("/XObject");
        form.Elements["/Subtype"] = new PdfName("/Form");

        document.Pages[0].Elements
            .GetDictionary("/Resources")!.Elements
            .GetDictionary("/XObject")!.Elements["/Fm0"] = form;

        document.Save(path);
    }

    /// <summary>Adds an entry to the first page's /Resources, for the shapes pruning must refuse.</summary>
    public static void AddToFirstPageResources(string path, string key, PdfItem value)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        document.Pages[0].Elements.GetDictionary("/Resources")!.Elements[key] = value;
        document.Save(path);
    }

    /// <summary>Gives the first page an annotation that draws itself from an appearance stream.</summary>
    public static void AddAnnotationWithAppearance(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        var appearance = new PdfDictionary(document);
        appearance.Elements["/Type"] = new PdfName("/XObject");
        appearance.Elements["/Subtype"] = new PdfName("/Form");

        var appearances = new PdfDictionary(document);
        appearances.Elements["/N"] = appearance;

        var annotation = new PdfDictionary(document);
        annotation.Elements["/Type"] = new PdfName("/Annot");
        annotation.Elements["/Subtype"] = new PdfName("/Stamp");
        annotation.Elements["/AP"] = appearances;

        document.Pages[0].Elements["/Annots"] = new PdfArray(document, annotation);
        document.Save(path);
    }

    /// <summary>
    /// Leaves the first page claiming a filter its bytes do not honour, which is what a stream damaged
    /// in transit looks like: PDFsharp answers it with no bytes rather than with an error.
    /// </summary>
    public static void CorruptFirstPageContent(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        var content = ContentStreamsOf(document.Pages[0]).First();

        content.Stream!.Value = "not deflate data"u8.ToArray();
        content.Elements["/Filter"] = new PdfName("/FlateDecode");

        document.Save(path);
    }

    /// <summary>Replaces the name every page draws, leaving the listing it draws from alone.</summary>
    public static void RenameEveryDrawnName(string path, string to)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        for (var i = 0; i < document.PageCount; i++)
            Rename(document.Pages[i], from: NameOfImage(i), to: to);

        document.Save(path);
    }

    private static void Rename(PdfPage page, string from, string to)
    {
        foreach (var content in ContentStreamsOf(page))
        {
            var text = Encoding.ASCII.GetString(content.Stream!.UnfilteredValue);

            // Latin1 round-trips every byte, where ASCII would replace anything above 0x7F with '?'.
            content.Stream.Value = Encoding.Latin1.GetBytes(
                text.Replace($"{from} Do", $"{to} Do", StringComparison.Ordinal));

            // The replacement is plain text, so whatever filter described the bytes it replaces no
            // longer describes them.
            content.Elements.Remove("/Filter");
        }
    }

    private static IEnumerable<PdfDictionary> ContentStreamsOf(PdfPage page)
    {
        var contents = page.Elements["/Contents"];
        var dereferenced = contents is PdfReference reference ? reference.Value : contents;

        var items = dereferenced is PdfArray array ? array.Elements.ToList() : [contents];

        foreach (var item in items)
        {
            var stream = item is PdfReference indirect ? indirect.Value as PdfDictionary : item as PdfDictionary;

            if (stream?.Stream is not null)
                yield return stream;
        }
    }

    private static string NameOfImage(int index) => $"/Im{index}";

    /// <summary>
    /// Adds an entry to every image XObject on every page, so a document can be given the shape of
    /// one produced by a real writer without having to hand-build the file.
    /// </summary>
    public static void AddToEveryImageDictionary(string path, string key, PdfItem value)
    {
        using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
        {
            for (var page = 0; page < document.PageCount; page++)
            {
                foreach (var image in ImagesOnPage(document, page))
                    image.Elements[key] = value;
            }

            document.Save(path);
        }
    }

    /// <summary>Pixel dimensions of every image XObject on the first page, so downsampling is observable.</summary>
    public static IReadOnlyList<(int Width, int Height)> ImageSizesOnFirstPage(string path) =>
        [.. ImagesOnFirstPage(path).Select(image => (image.Width, image.Height))];

    /// <summary>Every image XObject on the first page, described by the entries a test cares about.</summary>
    public static IReadOnlyList<ImageEntry> ImagesOnFirstPage(string path) => ImagesOn(path, 0);

    /// <summary>The same, for a document where each page has to be checked in its own right.</summary>
    public static IReadOnlyList<ImageEntry> ImagesOn(string path, int pageIndex)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);

        return [.. ImagesOnPage(document, pageIndex).Select(image => new ImageEntry(
            image.Elements.GetInteger("/Width"),
            image.Elements.GetInteger("/Height"),
            NameOrEmpty(image, "/Filter"),
            NameOrEmpty(image, "/ColorSpace"),
            image.Elements.ContainsKey("/DecodeParms")))];
    }

    /// <summary>
    /// Reads an entry that is usually a name but need not be: an array colour space is ordinary, and
    /// GetName throws on one rather than reporting it. Empty is what GetName itself answers for a key
    /// that is absent, so a test asserting on a real name reads the same either way.
    /// </summary>
    private static string NameOrEmpty(PdfDictionary image, string key) =>
        (image.Elements[key] is PdfReference reference ? reference.Value : image.Elements[key]) switch
        {
            PdfName name => name.Value,
            PdfNameObject name => name.Value,
            _ => string.Empty
        };

    /// <summary>The parts of an image XObject the compression tests assert on.</summary>
    public sealed record ImageEntry(
        int Width, int Height, string Filter, string ColorSpace, bool HasDecodeParms);

    private static List<PdfDictionary> ImagesOnPage(PdfDocument document, int pageIndex)
    {
        var xObjects = document.Pages[pageIndex].Elements
            .GetDictionary("/Resources")?.Elements.GetDictionary("/XObject");

        if (xObjects is null)
            return [];

        var images = new List<PdfDictionary>();

        foreach (var key in xObjects.Elements.Keys)
        {
            var xObject = xObjects.Elements[key] switch
            {
                PdfReference reference => reference.Value as PdfDictionary,
                PdfDictionary dictionary => dictionary,
                _ => null
            };

            if (xObject?.Elements.GetName("/Subtype") == "/Image")
                images.Add(xObject);
        }

        return images;
    }

    private static byte[] CreateNoisyJpeg(int width, int height, bool grayscale = false, int seed = BaseSeed)
    {
        using var bitmap = CreateNoisyBitmap(width, height, grayscale, seed);
        using var image = SKImage.FromBitmap(bitmap);

        // High quality keeps chroma unsubsampled, so a grey source survives the round trip grey
        // rather than picking up colour the compressor would then have to keep.
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        return data.ToArray();
    }

    private static byte[] CreateNoisyPng(int width, int height)
    {
        using var bitmap = CreateNoisyBitmap(width, height, grayscale: false, BaseSeed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKBitmap CreateNoisyBitmap(int width, int height, bool grayscale, int seed)
    {
        var pixels = new byte[width * height * 4];
        var random = new Random(seed);
        random.NextBytes(pixels);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Equal channels make the pixel grey without making it flat, so the image still has
            // detail to compress and the colour test has something real to measure.
            if (grayscale)
                pixels[i + 1] = pixels[i + 2] = pixels[i];

            pixels[i + 3] = byte.MaxValue;
        }

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap;
    }

    private static PdfSharp.Drawing.XUnit XUnitOf(double points) =>
        PdfSharp.Drawing.XUnit.FromPoint(points);
}
