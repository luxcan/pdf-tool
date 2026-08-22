using PdfTool.Core.Splitting;

namespace PdfTool.Core.Tests;

public sealed class PageRangeTests
{
    [Fact]
    public void EveryNPages_OfOne_GivesAPageEach()
    {
        var ranges = PageRange.EveryNPages(pageCount: 4, pagesPerFile: 1);

        Assert.Equal(
            [new PageRange(1, 1), new PageRange(2, 2), new PageRange(3, 3), new PageRange(4, 4)],
            ranges);
    }

    [Fact]
    public void EveryNPages_WhenPagesDoNotDivideEvenly_LeavesAShortLastRange()
    {
        var ranges = PageRange.EveryNPages(pageCount: 7, pagesPerFile: 3);

        Assert.Equal([new PageRange(1, 3), new PageRange(4, 6), new PageRange(7, 7)], ranges);
    }

    [Fact]
    public void EveryNPages_AskedForMoreThanTheDocumentHas_GivesOneRangeOverAllOfIt()
    {
        Assert.Equal([new PageRange(1, 5)], PageRange.EveryNPages(pageCount: 5, pagesPerFile: 50));
    }

    /// <summary>
    /// The step used to be added unchecked, so a huge chunk size wrapped past int.MaxValue and
    /// produced ranges with negative page numbers that later threw while being counted.
    /// </summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(1_000_000)]
    public void EveryNPages_WithAChunkLargerThanTheDocument_GivesOneWholeRange(int pagesPerFile)
    {
        var ranges = PageRange.EveryNPages(pageCount: 10, pagesPerFile);

        Assert.Equal([new PageRange(1, 10)], ranges);
        Assert.All(ranges, range => Assert.True(range.FirstPage >= 1, $"Got page {range.FirstPage}."));

        // Counting the pages must not overflow; Sum is checked arithmetic.
        Assert.Equal(10, ranges.Sum(range => range.PageCount));
    }

    [Fact]
    public void Parse_ReadsPagesAndRangesInOneList()
    {
        var ranges = PageRange.Parse("1-3, 5, 8-10", pageCount: 12);

        Assert.Equal([new PageRange(1, 3), new PageRange(5, 5), new PageRange(8, 10)], ranges);
    }

    [Theory]
    [InlineData("1-3,5")]
    [InlineData("  1 - 3 ,  5  ")]
    [InlineData("1-3; 5")]
    [InlineData("1-3,,5,")]
    public void Parse_ToleratesSpacingAndStraySeparators(string text)
    {
        Assert.Equal([new PageRange(1, 3), new PageRange(5, 5)], PageRange.Parse(text, pageCount: 12));
    }

    [Fact]
    public void Parse_KeepsTheOrderGiven_SoPagesCanBeTakenOutOfSequence()
    {
        var ranges = PageRange.Parse("9, 1-2", pageCount: 12);

        Assert.Equal([new PageRange(9, 9), new PageRange(1, 2)], ranges);
    }

    [Theory]
    [InlineData("", "at least one page")]
    [InlineData("   ", "at least one page")]
    [InlineData("abc", "not a page or a page range")]
    [InlineData("1-2-3", "too many dashes")]
    [InlineData("5-2", "runs backwards")]
    [InlineData("0", "numbered from 1")]
    [InlineData("-4", "not a page or a page range")]
    [InlineData("13", "only 12 page")]
    [InlineData("10-99", "only 12 page")]
    public void Parse_BadInput_SaysWhatIsWrongWithIt(string text, string expected)
    {
        var exception = Assert.Throws<PdfToolException>(() => PageRange.Parse(text, pageCount: 12));

        Assert.Contains(expected, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ARejectedPart_IsQuotedBackSoItCanBeFound()
    {
        var exception = Assert.Throws<PdfToolException>(() => PageRange.Parse("1-3, seven, 9", pageCount: 12));

        Assert.Contains("seven", exception.Message);
    }

    [Fact]
    public void PageCount_CountsBothEnds()
    {
        Assert.Equal(3, new PageRange(4, 6).PageCount);
        Assert.Equal(1, new PageRange(4, 4).PageCount);
    }
}
