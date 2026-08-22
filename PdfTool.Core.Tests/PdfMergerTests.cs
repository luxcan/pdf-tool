using PdfTool.Core.Merging;

namespace PdfTool.Core.Tests;

public sealed class PdfMergerTests
{
    private readonly PdfMerger _merger = new();

    [Fact]
    public async Task MergeAsync_WholeFilesInOrder_KeepsEveryPageInSequence()
    {
        using var temp = new TempDirectory();
        var first = TestPdfFactory.Create(temp.Path, "first.pdf", pageCount: 3, widthSeed: 100);
        var second = TestPdfFactory.Create(temp.Path, "second.pdf", pageCount: 2, widthSeed: 200);
        var output = temp.Combine("merged.pdf");

        var pages = AllPagesOf(first, 3).Concat(AllPagesOf(second, 2)).ToList();

        var result = await _merger.MergeAsync(pages, output);

        Assert.Equal(5, result.PageCount);
        Assert.True(File.Exists(output));
        Assert.Equal([100, 101, 102, 200, 201], TestPdfFactory.PageWidths(output));
    }

    [Fact]
    public async Task MergeAsync_SelectedPagesOnly_WritesOnlyThosePages()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 5, widthSeed: 300);
        var output = temp.Combine("selected.pdf");

        // The user deselected pages 2 and 4 (indexes 1 and 3).
        List<PageRef> pages =
        [
            new(source, 0),
            new(source, 2),
            new(source, 4)
        ];

        var result = await _merger.MergeAsync(pages, output);

        Assert.Equal(3, result.PageCount);
        Assert.Equal([300, 302, 304], TestPdfFactory.PageWidths(output));
    }

    [Fact]
    public async Task MergeAsync_InterleavedSources_FollowsTheRequestedOrder()
    {
        using var temp = new TempDirectory();
        var first = TestPdfFactory.Create(temp.Path, "a.pdf", pageCount: 2, widthSeed: 400);
        var second = TestPdfFactory.Create(temp.Path, "b.pdf", pageCount: 2, widthSeed: 500);
        var output = temp.Combine("interleaved.pdf");

        List<PageRef> pages =
        [
            new(second, 1),
            new(first, 0),
            new(second, 0),
            new(first, 1)
        ];

        await _merger.MergeAsync(pages, output);

        Assert.Equal([501, 400, 500, 401], TestPdfFactory.PageWidths(output));
    }

    [Fact]
    public async Task MergeAsync_SamePageTwice_DuplicatesIt()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 2, widthSeed: 600);
        var output = temp.Combine("duplicated.pdf");

        List<PageRef> pages = [new(source, 0), new(source, 0), new(source, 1)];

        await _merger.MergeAsync(pages, output);

        Assert.Equal([600, 600, 601], TestPdfFactory.PageWidths(output));
    }

    [Fact]
    public async Task MergeAsync_WithRotation_AppliesItToTheOutputPage()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 2, widthSeed: 700);
        var output = temp.Combine("rotated.pdf");

        List<PageRef> pages = [new(source, 0, Rotation: 90), new(source, 1, Rotation: 270)];

        await _merger.MergeAsync(pages, output);

        Assert.Equal([90, 270], TestPdfFactory.PageRotations(output));
    }

    [Fact]
    public async Task MergeAsync_ReportsProgressForEveryPage()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 4, widthSeed: 800);
        var output = temp.Combine("progress.pdf");

        // Progress<T> posts to the captured synchronisation context, so collect synchronously
        // instead to keep the assertions from racing the callbacks.
        var collected = new List<MergeProgress>();

        await _merger.MergeAsync(
            AllPagesOf(source, 4), output, progress: new SynchronousProgress<MergeProgress>(collected.Add));

        Assert.Equal(4, collected.Count);
        Assert.Equal([1, 2, 3, 4], collected.Select(p => p.PagesWritten));
        Assert.All(collected, p => Assert.Equal(4, p.TotalPages));
        Assert.All(collected, p => Assert.Equal("source.pdf", p.CurrentFileName));
        Assert.Equal(1d, collected[^1].Fraction);
    }

    [Fact]
    public async Task MergeAsync_CreatesMissingOutputDirectory()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 1, widthSeed: 900);
        var output = Path.Combine(temp.Path, "nested", "folder", "out.pdf");

        await _merger.MergeAsync(AllPagesOf(source, 1), output);

        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task MergeAsync_NoPages_Throws()
    {
        using var temp = new TempDirectory();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _merger.MergeAsync([], temp.Combine("out.pdf")));
    }

    [Fact]
    public async Task MergeAsync_NegativePageIndex_Throws()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 1, widthSeed: 100);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _merger.MergeAsync([new PageRef(source, -1)], temp.Combine("out.pdf")));
    }

    [Fact]
    public async Task MergeAsync_UnsupportedRotation_Throws()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 1, widthSeed: 100);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _merger.MergeAsync([new PageRef(source, 0, Rotation: 45)], temp.Combine("out.pdf")));
    }

    [Fact]
    public async Task MergeAsync_PageIndexBeyondEndOfDocument_ThrowsWithPageCount()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 2, widthSeed: 100);

        var exception = await Assert.ThrowsAsync<PdfToolException>(
            () => _merger.MergeAsync([new PageRef(source, 5)], temp.Combine("out.pdf")));

        Assert.Contains("2 page(s)", exception.Message);
        Assert.Contains("page 6", exception.Message);
    }

    [Fact]
    public async Task MergeAsync_OutputPathIsAlsoASource_Throws()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 1, widthSeed: 100);

        var exception = await Assert.ThrowsAsync<PdfToolException>(
            () => _merger.MergeAsync(AllPagesOf(source, 1), source));

        Assert.Contains("cannot be one of the files being merged", exception.Message);
    }

    [Fact]
    public async Task MergeAsync_MissingSourceFile_Throws()
    {
        using var temp = new TempDirectory();
        var missing = temp.Combine("does-not-exist.pdf");

        var exception = await Assert.ThrowsAsync<PdfToolException>(
            () => _merger.MergeAsync([new PageRef(missing, 0)], temp.Combine("out.pdf")));

        Assert.Contains("File not found", exception.Message);
    }

    [Fact]
    public async Task MergeAsync_SourceIsNotAPdf_Throws()
    {
        using var temp = new TempDirectory();
        var notPdf = temp.Combine("notes.txt");
        await File.WriteAllTextAsync(notPdf, "this is not a pdf");

        await Assert.ThrowsAsync<PdfToolException>(
            () => _merger.MergeAsync([new PageRef(notPdf, 0)], temp.Combine("out.pdf")));
    }

    [Fact]
    public async Task MergeAsync_CancelledBeforeStart_DoesNotWriteOutput()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "source.pdf", pageCount: 3, widthSeed: 100);
        var output = temp.Combine("cancelled.pdf");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _merger.MergeAsync(AllPagesOf(source, 3), output, cancellationToken: cts.Token));

        Assert.False(File.Exists(output));
    }

    private static List<PageRef> AllPagesOf(string path, int pageCount) =>
        [.. Enumerable.Range(0, pageCount).Select(i => new PageRef(path, i))];
}
