using PdfTool.Core.Documents;
using PdfTool.Core.Splitting;

namespace PdfTool.Core.Tests;

public sealed class PdfSplitterTests
{
    private readonly PdfSplitter _splitter = new();
    private readonly PdfInspector _inspector = new();

    [Fact]
    public async Task SplitAsync_EveryPage_WritesOneFilePerPage()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 4, widthSeed: 200);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(
            source, PageRange.EveryNPages(4, 1), output);

        Assert.Equal(4, result.FileCount);
        Assert.All(result.OutputPaths, path => Assert.True(File.Exists(path), $"{path} was not written."));
        Assert.All(result.OutputPaths, path => Assert.Equal(1, _inspector.Inspect(path).PageCount));
    }

    [Fact]
    public async Task SplitAsync_NamesEachPartAfterThePagesInside()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 5, widthSeed: 200);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(
            source, [new PageRange(1, 3), new PageRange(4, 4)], output);

        Assert.Equal(
            ["report-p1-3.pdf", "report-p4.pdf"],
            result.OutputPaths.Select(Path.GetFileName));
    }

    /// <summary>
    /// A folder of parts is read in a file browser, which sorts by name. Without padding, page 10
    /// would sit between pages 1 and 2.
    /// </summary>
    [Fact]
    public async Task SplitAsync_PadsPageNumbers_SoThePartsSortIntoReadingOrder()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 12, widthSeed: 200);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(12, 1), output);

        var names = result.OutputPaths.Select(Path.GetFileName).ToList();

        Assert.Equal("report-p01.pdf", names[0]);
        Assert.Equal("report-p12.pdf", names[^1]);
        Assert.Equal(names, names.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SplitAsync_ByRanges_KeepsTheRightPagesInTheRightPart()
    {
        using var temp = new TempDirectory();

        // Page widths encode which page is which, so the contents can be checked without rendering.
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 6, widthSeed: 300);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(
            source, [new PageRange(2, 3), new PageRange(6, 6)], output);

        Assert.Equal([301, 302], TestPdfFactory.PageWidths(result.OutputPaths[0]));
        Assert.Equal([305], TestPdfFactory.PageWidths(result.OutputPaths[1]));
    }

    [Fact]
    public async Task SplitAsync_LeavesTheSourceUntouched()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 4, widthSeed: 200);
        var before = await File.ReadAllBytesAsync(source);

        await _splitter.SplitAsync(source, PageRange.EveryNPages(4, 2), temp.Combine("parts"));

        Assert.Equal(before, await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task SplitAsync_CreatesTheOutputFolderIfItIsNotThere()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 2, widthSeed: 200);
        var output = temp.Combine("nested", "parts");

        Assert.False(Directory.Exists(output));

        var result = await _splitter.SplitAsync(source, PageRange.EveryNPages(2, 1), output);

        Assert.True(Directory.Exists(output));
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public async Task SplitAsync_ReportsProgressPerFile()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 3, widthSeed: 200);
        var collected = new List<SplitProgress>();

        await _splitter.SplitAsync(
            source,
            PageRange.EveryNPages(3, 1),
            temp.Combine("parts"),
            progress: new SynchronousProgress<SplitProgress>(collected.Add));

        Assert.Equal(3, collected.Count);
        Assert.Equal(3, collected[^1].FilesWritten);
        Assert.Equal(1d, collected[^1].Fraction);
    }

    [Fact]
    public async Task SplitAsync_PagesBeyondTheDocument_ThrowsBeforeWritingAnything()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 3, widthSeed: 200);
        var output = temp.Combine("parts");

        var exception = await Assert.ThrowsAsync<PdfToolException>(() => _splitter.SplitAsync(
            source, [new PageRange(1, 1), new PageRange(9, 9)], output));

        Assert.Contains("3 page(s)", exception.Message);

        // The first range was valid; a split that cannot finish should not leave half of one behind.
        Assert.False(Directory.Exists(output) && Directory.EnumerateFiles(output).Any());
    }

    /// <summary>
    /// Ranges may legitimately repeat. Naming parts after their pages alone made two of them the
    /// same file, so one silently overwrote the other while both were counted.
    /// </summary>
    [Fact]
    public async Task SplitAsync_RepeatedRanges_WritesAFileEachInsteadOfOverwriting()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "book.pdf", pageCount: 5, widthSeed: 200);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(
            source,
            [new PageRange(1, 3), new PageRange(1, 3), new PageRange(4, 4), new PageRange(4, 4)],
            output);

        Assert.Equal(4, result.FileCount);
        Assert.Equal(4, result.OutputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(result.FileCount, Directory.GetFiles(output).Length);
        Assert.All(result.OutputPaths, path => Assert.True(File.Exists(path), $"{path} is missing."));
    }

    /// <summary>
    /// A page written as "5" and as "5-5" is the same range said two ways, and used to collide
    /// because a single-page part is named without its second number.
    /// </summary>
    [Fact]
    public async Task SplitAsync_ASinglePageWrittenTwoWays_StillWritesTwoFiles()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "book.pdf", pageCount: 6, widthSeed: 200);
        var output = temp.Combine("parts");

        var result = await _splitter.SplitAsync(
            source, [new PageRange(5, 5), new PageRange(5, 5)], output);

        Assert.Equal(2, Directory.GetFiles(output).Length);
        Assert.Equal(2, result.OutputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Cancelling partway used to leave every part written so far in the user's folder while the
    /// status bar said only that the split was cancelled.
    /// </summary>
    [Fact]
    public async Task SplitAsync_CancelledPartWay_LeavesNoPartsBehind()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "book.pdf", pageCount: 40, widthSeed: 200);
        var output = temp.Combine("parts");

        using var cts = new CancellationTokenSource();

        var progress = new SynchronousProgress<SplitProgress>(report =>
        {
            if (report.FilesWritten >= 3)
                cts.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _splitter.SplitAsync(
            source, PageRange.EveryNPages(40, 1), output, progress: progress, cancellationToken: cts.Token));

        Assert.True(
            !Directory.Exists(output) || Directory.GetFiles(output).Length == 0,
            $"Left {Directory.GetFiles(output).Length} part(s) behind after cancelling.");
    }

    [Fact]
    public async Task SplitAsync_MissingFile_Throws()
    {
        using var temp = new TempDirectory();

        var exception = await Assert.ThrowsAsync<PdfToolException>(() => _splitter.SplitAsync(
            temp.Combine("nope.pdf"), [new PageRange(1, 1)], temp.Combine("parts")));

        Assert.Contains("File not found", exception.Message);
    }

    [Fact]
    public async Task SplitAsync_NoRanges_Throws()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 2, widthSeed: 200);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _splitter.SplitAsync(source, [], temp.Combine("parts")));
    }

    [Fact]
    public async Task SplitAsync_Cancelled_StopsWriting()
    {
        using var temp = new TempDirectory();
        var source = TestPdfFactory.Create(temp.Path, "report.pdf", pageCount: 6, widthSeed: 200);
        var output = temp.Combine("parts");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _splitter.SplitAsync(
            source, PageRange.EveryNPages(6, 1), output, cancellationToken: cts.Token));

        Assert.False(Directory.Exists(output) && Directory.EnumerateFiles(output).Any());
    }
}
