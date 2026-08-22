using PdfSharp.Pdf;

namespace PdfTool.Core.Documents;

/// <summary>
/// How this tool writes a PDF it has assembled page by page.
///
/// Merging and splitting both build a document out of pages taken from elsewhere, and an output
/// that compressed differently depending on which command produced it would be a surprise worth
/// avoiding. Keeping the options and the save in one place is what makes them the same.
/// </summary>
internal static class PdfOutput
{
    public static PdfDocument Create()
    {
        var document = new PdfDocument();
        ApplyOptions(document);
        return document;
    }

    /// <summary>
    /// The write settings, for a document that already exists rather than one being built - a file
    /// opened for rewriting has to be saved on the same terms as one assembled from scratch.
    /// </summary>
    public static void ApplyOptions(PdfDocument document)
    {
        document.Options.NoCompression = false;
        document.Options.CompressContentStreams = true;
        document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
    }

    /// <summary>
    /// Saves to the path, creating the folder if it is not there, and reports what landed on disk.
    /// Saving freezes the document, so the page count is read before rather than after.
    /// </summary>
    public static (int PageCount, long Bytes) Save(PdfDocument document, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var pageCount = document.PageCount;
        document.Save(path);

        return (pageCount, new FileInfo(path).Length);
    }
}
