using PdfSharp.Pdf;
using PdfTool.Core.Documents;
using PdfTool.Core.Merging;
using PdfTool.Core.Splitting;

namespace PdfTool.Core.Tests;

/// <summary>
/// A page carries the resource listing it shared with the rest of its document, so a part cut out of
/// one arrives holding every image in the original.
///
/// Half of these check that the waste goes. The other half check the far more important thing: that a
/// document whose drawing cannot be fully accounted for keeps every image it had. Being refused costs
/// a larger file; being wrong costs a blank page.
/// </summary>
public sealed class PdfResourcePrunerTests
{
    private const int SmallestEdge = 300;

    private readonly PdfSplitter _splitter = new();
    private readonly PdfMerger _merger = new();
    private readonly PdfInspector _inspector = new();

    // ============================== What it removes ==============================

    /// <summary>
    /// Every image is a different size, so this can say which one survived rather than only how many.
    /// Keeping the wrong image would leave the count right and the page showing someone else's scan.
    /// </summary>
    [Fact]
    public async Task Split_OfASharedListing_LeavesEachPartTheImageItsOwnPageDraws()
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 4);

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(4, 1), temp.Combine("parts"));

        for (var page = 0; page < result.OutputPaths.Count; page++)
        {
            var image = Assert.Single(TestPdfFactory.ImagesOnFirstPage(result.OutputPaths[page]));

            Assert.Equal(TestPdfFactory.ImageEdgeForPage(SmallestEdge, page), image.Width);
        }
    }

    /// <summary>The point of the exercise: four parts that each carried the whole document.</summary>
    [Fact]
    public async Task Split_OfASharedListing_WritesPartsSmallerThanTheDocument()
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 4);
        var sourceBytes = new FileInfo(source).Length;

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(4, 1), temp.Combine("parts"));

        Assert.All(result.OutputPaths, part => Assert.True(
            new FileInfo(part).Length < sourceBytes / 2,
            $"{Path.GetFileName(part)} is {new FileInfo(part).Length:N0} bytes of a {sourceBytes:N0} byte document."));
    }

    [Fact]
    public async Task Merge_OfSomePagesOfASharedListing_TakesOnlyTheirImages()
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 5);
        var output = temp.Combine("two-pages.pdf");

        await _merger.MergeAsync([new PageRef(source, 0), new PageRef(source, 3)], output);

        Assert.Equal(2, _inspector.Inspect(output).PageCount);

        // Each page arrives with its own copy of the listing, so each should now name its own image
        // and none of the three belonging to the pages left behind.
        Assert.Equal(
            TestPdfFactory.ImageEdgeForPage(SmallestEdge, 0),
            Assert.Single(TestPdfFactory.ImagesOn(output, 0)).Width);

        Assert.Equal(
            TestPdfFactory.ImageEdgeForPage(SmallestEdge, 3),
            Assert.Single(TestPdfFactory.ImagesOn(output, 1)).Width);
    }

    // ========================== What it must not remove ==========================

    /// <summary>
    /// Each of these can draw an image from a content stream the pruner never reads, so a document
    /// carrying one must come through with every image it had. The fixture is otherwise exactly the
    /// one the tests above prune to a quarter of its size, so a failure here is the guard giving way
    /// rather than the shape being unprunable anyway.
    /// </summary>
    public static TheoryData<string, Action<string>> UnaccountableShapes() => new()
    {
        { "a tiling pattern", path => TestPdfFactory.AddToFirstPageResources(path, "/Pattern", Pattern()) },
        { "a Type 3 font", path => TestPdfFactory.AddToFirstPageResources(path, "/Font", Type3Font()) },
        { "a soft mask", path => TestPdfFactory.AddToFirstPageResources(path, "/ExtGState", SoftMask()) },
        { "an annotation appearance", TestPdfFactory.AddAnnotationWithAppearance },
        { "a form in the listing", TestPdfFactory.AddFormToListing },
        { "an unreadable content stream", TestPdfFactory.CorruptFirstPageContent },
        { "a name drawn but not listed", path => TestPdfFactory.RenameEveryDrawnName(path, "/Elsewhere") }
    };

    /// <summary>
    /// The unaccountable shape is given to the first page only, and all four pages are kept together
    /// in one part so they go on sharing one listing. One page nobody can account for has to protect
    /// the images of the three that share its listing, because any of them might be the one it draws.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnaccountableShapes))]
    public async Task Split_OfADocumentItCannotAccountFor_KeepsEveryImage(string shape, Action<string> give)
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 4);
        give(source);

        var result = await _splitter.SplitAsync(source, [new PageRange(1, 4)], temp.Combine("parts"));

        var part = Assert.Single(result.OutputPaths);

        Assert.Equal(4, _inspector.Inspect(part).PageCount);

        Assert.True(
            TestPdfFactory.ImagesOnFirstPage(part).Count == 4,
            $"A document with {shape} kept {TestPdfFactory.ImagesOnFirstPage(part).Count} of its 4 images.");
    }

    [Theory]
    [MemberData(nameof(UnaccountableShapes))]
    public async Task Merge_OfADocumentItCannotAccountFor_KeepsEveryImage(string shape, Action<string> give)
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 4);
        give(source);

        var output = temp.Combine("merged.pdf");
        await _merger.MergeAsync([new PageRef(source, 0), new PageRef(source, 1)], output);

        Assert.True(
            TestPdfFactory.ImagesOnFirstPage(output).Count == 4,
            $"A document with {shape} kept {TestPdfFactory.ImagesOnFirstPage(output).Count} of its 4 images.");
    }

    // ============================ Documents with nothing to lose ============================

    /// <summary>A document that draws everything it lists must come through untouched.</summary>
    [Fact]
    public async Task Split_OfADocumentThatDrawsEverythingItLists_KeepsEveryImage()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.CreateWithImage(temp.Path, "ordinary.pdf", pageCount: 3, imageEdgePixels: 500);

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(3, 1), temp.Combine("parts"));

        Assert.All(result.OutputPaths, part => Assert.Single(TestPdfFactory.ImagesOnFirstPage(part)));
    }

    /// <summary>Merging whole files leaves nothing behind, so nothing may be dropped from any page.</summary>
    [Fact]
    public async Task Merge_OfWholeDocuments_KeepsEveryPagesOwnImage()
    {
        using var temp = new TempDirectory();
        var first = SharedListing(temp, "a.pdf", pageCount: 2);
        var second = SharedListing(temp, "b.pdf", pageCount: 2);

        var output = temp.Combine("merged.pdf");

        await _merger.MergeAsync(
            [new PageRef(first, 0), new PageRef(first, 1), new PageRef(second, 0), new PageRef(second, 1)],
            output);

        Assert.Equal(4, _inspector.Inspect(output).PageCount);

        // Nothing was left behind, so every page must still have the image it draws.
        for (var page = 0; page < 4; page++)
        {
            Assert.Equal(
                TestPdfFactory.ImageEdgeForPage(SmallestEdge, page % 2),
                Assert.Single(TestPdfFactory.ImagesOn(output, page)).Width);
        }
    }

    /// <summary>
    /// A text document has no listing to judge, so nothing should be read at all - the guard that
    /// keeps this from parsing every content stream of a document it cannot possibly help.
    /// </summary>
    [Fact]
    public async Task Split_OfADocumentWithNoImages_SucceedsAndChangesNothing()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "text.pdf", pageCount: 4, widthSeed: 200);

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(4, 2), temp.Combine("parts"));

        Assert.Equal(2, result.FileCount);
        Assert.Equal([200, 201], TestPdfFactory.PageWidths(result.OutputPaths[0]));
    }

    /// <summary>
    /// Pruning walks every content stream in the document, which on a long merge is long enough that
    /// a cancel arriving during it has to be heard.
    /// </summary>
    [Fact]
    public async Task Merge_CancelledBeforePruning_StopsRatherThanWritingTheFile()
    {
        using var temp = new TempDirectory();
        var source = SharedListing(temp, "scan.pdf", pageCount: 4);
        var output = temp.Combine("merged.pdf");

        using var cancellation = new CancellationTokenSource();

        var progress = new SynchronousProgress<MergeProgress>(report =>
        {
            if (report.PagesWritten == 4)
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _merger.MergeAsync(
            [.. Enumerable.Range(0, 4).Select(page => new PageRef(source, page))],
            output,
            progress: progress,
            cancellationToken: cancellation.Token));

        Assert.False(File.Exists(output), "A cancelled merge wrote its output anyway.");
    }

    // ==================================== Fixtures ====================================

    private static string SharedListing(TempDirectory temp, string fileName, int pageCount)
    {
        var path = TestPdfFactory.CreateWithDistinctImages(
            temp.Path, fileName, pageCount, smallestImageEdgePixels: SmallestEdge);

        TestPdfFactory.ShareOneResourceListing(path);

        // Every page lists all of them before the split; that is the shape being tested.
        Assert.Equal(pageCount, TestPdfFactory.ImagesOnFirstPage(path).Count);

        return path;
    }

    private static PdfDictionary Pattern()
    {
        var pattern = new PdfDictionary();
        pattern.Elements["/P0"] = new PdfDictionary();
        return pattern;
    }

    private static PdfDictionary Type3Font()
    {
        var font = new PdfDictionary();
        font.Elements["/Subtype"] = new PdfName("/Type3");

        var fonts = new PdfDictionary();
        fonts.Elements["/F0"] = font;
        return fonts;
    }

    private static PdfDictionary SoftMask()
    {
        var mask = new PdfDictionary();
        mask.Elements["/S"] = new PdfName("/Luminosity");

        var state = new PdfDictionary();
        state.Elements["/SMask"] = mask;

        var states = new PdfDictionary();
        states.Elements["/GS0"] = state;
        return states;
    }
}
