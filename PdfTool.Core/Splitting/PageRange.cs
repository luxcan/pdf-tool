using System.Globalization;

namespace PdfTool.Core.Splitting;

/// <summary>
/// A run of pages destined for one output file, numbered from 1 as the reader sees them rather than
/// from 0 as the library stores them. Both ends are included.
/// </summary>
public readonly record struct PageRange(int FirstPage, int LastPage)
{
    public int PageCount => LastPage - FirstPage + 1;

    /// <summary>
    /// Consecutive runs of at most <paramref name="pagesPerFile"/> pages covering the whole
    /// document. The last run is short whenever the pages do not divide evenly.
    /// </summary>
    public static IReadOnlyList<PageRange> EveryNPages(int pageCount, int pagesPerFile)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pagesPerFile, 1);

        // Asking for more pages per file than the document holds means one file, and clamping says
        // so without letting the step overflow into ranges with negative page numbers.
        var step = Math.Min(pagesPerFile, pageCount);

        var ranges = new List<PageRange>();

        for (var first = 1; first <= pageCount; first += step)
            ranges.Add(new PageRange(first, Math.Min(first + step - 1, pageCount)));

        return ranges;
    }

    /// <summary>
    /// Reads a list like "1-3, 5, 8-10" into ranges, one output file each.
    ///
    /// Every rejection is a typo the user can fix, so each says which part was wrong and what a
    /// good one looks like rather than reporting that parsing failed.
    /// </summary>
    public static IReadOnlyList<PageRange> Parse(string text, int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);

        var ranges = new List<PageRange>();

        foreach (var part in (text ?? string.Empty).Split([',', ';'], StringSplitOptions.TrimEntries))
        {
            // A stray separator is a slip rather than an instruction; only an entirely empty list
            // is worth complaining about.
            if (part.Length == 0)
                continue;

            ranges.Add(ParsePart(part, pageCount));
        }

        if (ranges.Count == 0)
            throw new PdfToolException("Enter at least one page or range, such as 1-3, 5, 8-10.");

        return ranges;
    }

    private static PageRange ParsePart(string part, int pageCount)
    {
        var ends = part.Split('-', StringSplitOptions.TrimEntries);

        if (ends.Length > 2)
        {
            throw new PdfToolException(
                $"'{part}' has too many dashes. Use a page number like 5, or a range like 1-3.");
        }

        var first = ParsePageNumber(ends[0], part, pageCount);
        var last = ends.Length == 1 ? first : ParsePageNumber(ends[1], part, pageCount);

        if (last < first)
            throw new PdfToolException($"'{part}' runs backwards. Put the lower page number first.");

        return new PageRange(first, last);
    }

    private static int ParsePageNumber(string value, string part, int pageCount)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page))
        {
            throw new PdfToolException(
                $"'{part}' is not a page or a page range. Use a page number like 5, or a range like 1-3.");
        }

        if (page < 1)
            throw new PdfToolException($"'{part}' refers to page {page}; pages are numbered from 1.");

        if (page > pageCount)
        {
            throw new PdfToolException(
                $"'{part}' refers to page {page}, but the document has only {pageCount} page(s).");
        }

        return page;
    }
}
