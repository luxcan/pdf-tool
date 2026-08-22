using System.Globalization;
using PdfSharp.Pdf.IO;
using PdfTool.Core.Documents;

namespace PdfTool.Core.Splitting;

/// <summary>
/// Writes one file per <see cref="PageRange"/> out of a single document.
///
/// The source is opened once and read repeatedly, which matters because splitting a long scan into
/// single pages asks for hundreds of outputs and reopening the file for each would dominate the
/// time. Parts are named after the pages they hold, so a folder of them can be read without
/// opening any.
/// </summary>
public sealed class PdfSplitter
{
    public Task<SplitResult> SplitAsync(
        string inputPath,
        IReadOnlyList<PageRange> ranges,
        string outputDirectory,
        string? password = null,
        IProgress<SplitProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(ranges);

        if (ranges.Count == 0)
            throw new ArgumentException("At least one page range is required to split.", nameof(ranges));

        return Task.Run(
            () => Split(inputPath, ranges, outputDirectory, password, progress, cancellationToken),
            cancellationToken);
    }

    private static SplitResult Split(
        string inputPath,
        IReadOnlyList<PageRange> ranges,
        string outputDirectory,
        string? password,
        IProgress<SplitProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var source = PdfFileOpener.Open(inputPath, password, PdfDocumentOpenMode.Import);

        foreach (var range in ranges)
        {
            if (range.FirstPage < 1 || range.LastPage > source.PageCount || range.PageCount < 1)
            {
                throw new PdfToolException(
                    $"Pages {range.FirstPage}-{range.LastPage} are outside " +
                    $"'{Path.GetFileName(inputPath)}', which has {source.PageCount} page(s).");
            }
        }

        var stem = Path.GetFileNameWithoutExtension(inputPath);

        // Page numbers are padded to a common width so a folder of parts sorts into reading order
        // rather than putting page 10 in front of page 2.
        var digits = source.PageCount.ToString(CultureInfo.InvariantCulture).Length;

        var outputPaths = new List<string>(ranges.Count);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var range in ranges)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outputPath = Path.Combine(
                    outputDirectory, UniqueFileName(stem, range, digits, taken));

                using (var part = PdfOutput.Create())
                {
                    for (var page = range.FirstPage; page <= range.LastPage; page++)
                        part.AddPage(source.Pages[page - 1]);

                    // A page brings its whole resource listing with it, which on a document whose
                    // pages share one means every image in the original arrives in every part.
                    PdfResourcePruner.Prune(part, cancellationToken);

                    PdfOutput.Save(part, outputPath);
                }

                outputPaths.Add(outputPath);
                progress?.Report(new SplitProgress(outputPaths.Count, ranges.Count, Path.GetFileName(outputPath)));
            }
        }
        catch
        {
            // A split that cannot finish should leave nothing behind, the way a compression that
            // fails deletes its temporary file rather than handing back half a document.
            foreach (var written in outputPaths)
            {
                try
                {
                    File.Delete(written);
                }
                catch (Exception cleanupFailure) when (cleanupFailure is IOException or UnauthorizedAccessException)
                {
                    // The original failure is the one worth reporting.
                }
            }

            throw;
        }

        return new SplitResult(inputPath, outputDirectory, outputPaths);
    }

    /// <summary>
    /// A name no earlier part in this run has taken. Ranges may legitimately repeat or overlap, and
    /// two parts sharing a name would mean one silently overwriting the other while both are counted.
    /// </summary>
    private static string UniqueFileName(string stem, PageRange range, int digits, HashSet<string> taken)
    {
        var name = BuildFileName(stem, range, digits);

        if (taken.Add(name))
            return name;

        for (var copy = 2; ; copy++)
        {
            var candidate = $"{Path.GetFileNameWithoutExtension(name)} ({copy}).pdf";

            if (taken.Add(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// Names a part after the pages inside it: one page reads "-p07", several read "-p07-09".
    /// </summary>
    private static string BuildFileName(string stem, PageRange range, int digits)
    {
        var first = range.FirstPage.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');

        if (range.PageCount == 1)
            return $"{stem}-p{first}.pdf";

        var last = range.LastPage.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
        return $"{stem}-p{first}-{last}.pdf";
    }
}
