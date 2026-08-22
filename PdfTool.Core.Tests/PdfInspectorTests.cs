using PdfTool.Core.Documents;

namespace PdfTool.Core.Tests;

public sealed class PdfInspectorTests
{
    private readonly PdfInspector _inspector = new();

    [Fact]
    public void Inspect_ValidDocument_ReturnsPageCountAndSize()
    {
        using var temp = new TempDirectory();
        var path = TestPdfFactory.Create(temp.Path, "sample.pdf", pageCount: 7, widthSeed: 100);

        var info = _inspector.Inspect(path);

        Assert.Equal(7, info.PageCount);
        Assert.Equal("sample.pdf", info.FileName);
        Assert.Equal(new FileInfo(path).Length, info.SizeBytes);
    }

    [Fact]
    public void Inspect_MissingFile_Throws()
    {
        using var temp = new TempDirectory();

        var exception = Assert.Throws<PdfToolException>(() => _inspector.Inspect(temp.Combine("nope.pdf")));

        Assert.Contains("File not found", exception.Message);
    }

    [Fact]
    public void Inspect_NotAPdf_Throws()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("notes.txt");
        File.WriteAllText(path, "definitely not a pdf");

        Assert.Throws<PdfToolException>(() => _inspector.Inspect(path));
    }

    [Fact]
    public void Inspect_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => _inspector.Inspect("  "));
    }
}
