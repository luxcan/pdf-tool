using PdfTool.Core.Rendering;

namespace PdfTool.Core.Tests;

public sealed class PdfiumPageRendererTests
{
    private const int PngSignatureLength = 8;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task RenderPageAsync_ValidPage_ReturnsPngBytes()
    {
        using var temp = new TempDirectory();
        var path = TestPdfFactory.Create(temp.Path, "sample.pdf", pageCount: 2, widthSeed: 300);
        var renderer = new PdfiumPageRenderer();

        var png = await renderer.RenderPageAsync(path, pageIndex: 0, widthPixels: 120);

        Assert.NotEmpty(png);
        Assert.Equal(PngSignature, png.Take(PngSignatureLength));
    }

    [Fact]
    public async Task RenderPageAsync_PageBeyondEndOfDocument_Throws()
    {
        using var temp = new TempDirectory();
        var path = TestPdfFactory.Create(temp.Path, "sample.pdf", pageCount: 1, widthSeed: 300);
        var renderer = new PdfiumPageRenderer();

        await Assert.ThrowsAsync<PdfToolException>(
            () => renderer.RenderPageAsync(path, pageIndex: 4, widthPixels: 120));
    }

    [Fact]
    public async Task RenderPageAsync_NotAPdf_Throws()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("notes.txt");
        await File.WriteAllTextAsync(path, "not a pdf");
        var renderer = new PdfiumPageRenderer();

        await Assert.ThrowsAsync<PdfToolException>(
            () => renderer.RenderPageAsync(path, pageIndex: 0, widthPixels: 120));
    }

    [Fact]
    public async Task RenderPageAsync_ZeroWidth_Throws()
    {
        using var temp = new TempDirectory();
        var path = TestPdfFactory.Create(temp.Path, "sample.pdf", pageCount: 1, widthSeed: 300);
        var renderer = new PdfiumPageRenderer();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => renderer.RenderPageAsync(path, pageIndex: 0, widthPixels: 0));
    }
}
