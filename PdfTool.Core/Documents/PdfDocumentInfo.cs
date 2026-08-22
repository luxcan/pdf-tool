namespace PdfTool.Core.Documents;

/// <summary>Metadata about a source PDF, read once when the user adds it to the list.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="PageCount">Number of pages the document contains.</param>
/// <param name="SizeBytes">Size of the file on disk.</param>
public sealed record PdfDocumentInfo(string FilePath, int PageCount, long SizeBytes)
{
    public string FileName => Path.GetFileName(FilePath);
}
