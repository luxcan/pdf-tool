using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.Core.Documents;

namespace PdfTool.Core.Merging;

/// <summary>
/// Writes a new PDF from an ordered list of <see cref="PageRef"/>s. This is the single code path
/// behind both merge modes: merging whole files just means passing every page of every file.
/// </summary>
public sealed class PdfMerger
{
    public Task<MergeResult> MergeAsync(
        IReadOnlyList<PageRef> pages,
        string outputPath,
        IReadOnlyDictionary<string, string>? passwords = null,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Validate(pages, outputPath);

        return Task.Run(
            () => Merge(pages, outputPath, passwords, progress, cancellationToken),
            cancellationToken);
    }

    private static void Validate(IReadOnlyList<PageRef> pages, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (pages.Count == 0)
            throw new ArgumentException("At least one page is required to merge.", nameof(pages));

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.SourcePath))
                throw new ArgumentException("A page was supplied without a source path.", nameof(pages));

            if (page.PageIndex < 0)
                throw new ArgumentException(
                    $"Page index {page.PageIndex} for '{page.SourcePath}' is negative.", nameof(pages));

            if (!PageRef.IsValidRotation(page.Rotation))
                throw new ArgumentException(
                    $"Rotation {page.Rotation} is not one of 0, 90, 180 or 270.", nameof(pages));
        }

        // Reading a source while overwriting it would corrupt both.
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (pages.Any(p => string.Equals(Path.GetFullPath(p.SourcePath), fullOutputPath, StringComparison.OrdinalIgnoreCase)))
            throw new PdfToolException("The output file cannot be one of the files being merged.");
    }

    private static MergeResult Merge(
        IReadOnlyList<PageRef> pages,
        string outputPath,
        IReadOnlyDictionary<string, string>? passwords,
        IProgress<MergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Pages may interleave across files, so each source is opened once and reused
        // rather than reopened per page.
        var sources = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var output = PdfOutput.Create();

            for (var i = 0; i < pages.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageRef = pages[i];
                var source = GetOrOpenSource(sources, pageRef.SourcePath, passwords);

                if (pageRef.PageIndex >= source.PageCount)
                {
                    throw new PdfToolException(
                        $"'{Path.GetFileName(pageRef.SourcePath)}' has {source.PageCount} page(s); " +
                        $"page {pageRef.PageIndex + 1} was requested.");
                }

                var added = output.AddPage(source.Pages[pageRef.PageIndex]);

                if (pageRef.Rotation != 0)
                    added.Rotate = (added.Rotate + pageRef.Rotation) % 360;

                progress?.Report(new MergeProgress(i + 1, pages.Count, Path.GetFileName(pageRef.SourcePath)));
            }

            // Taking some pages of a document takes the resource listing they share with the pages
            // left behind, so a few pages of a scan can arrive carrying all of its images.
            PdfResourcePruner.Prune(output, cancellationToken);

            var (pageCount, bytes) = PdfOutput.Save(output, outputPath);

            return new MergeResult(outputPath, pageCount, bytes);
        }
        finally
        {
            foreach (var source in sources.Values)
                source.Dispose();
        }
    }

    private static PdfDocument GetOrOpenSource(
        Dictionary<string, PdfDocument> sources,
        string sourcePath,
        IReadOnlyDictionary<string, string>? passwords)
    {
        if (sources.TryGetValue(sourcePath, out var existing))
            return existing;

        var password = passwords is not null && passwords.TryGetValue(sourcePath, out var found)
            ? found
            : null;

        var opened = PdfFileOpener.Open(sourcePath, password, PdfDocumentOpenMode.Import);
        sources[sourcePath] = opened;
        return opened;
    }
}
