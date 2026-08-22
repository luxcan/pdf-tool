using PdfSharp.Pdf.IO;

namespace PdfTool.Core.Documents;

/// <summary>
/// Reads the metadata needed to show a file in the list. Opening in <see cref="PdfDocumentOpenMode.Import"/>
/// is deliberate: a file that inspects cleanly is a file that will merge cleanly.
/// </summary>
public sealed class PdfInspector
{
    public PdfDocumentInfo Inspect(string filePath, string? password = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var document = PdfFileOpener.Open(filePath, password, PdfDocumentOpenMode.Import);

        if (document.PageCount == 0)
            throw new PdfToolException($"'{Path.GetFileName(filePath)}' contains no pages.");

        return new PdfDocumentInfo(filePath, document.PageCount, new FileInfo(filePath).Length);
    }
}
