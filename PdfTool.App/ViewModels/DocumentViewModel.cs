using System.IO;
using PdfTool.App.Formatting;
using PdfTool.Core.Documents;

namespace PdfTool.App.ViewModels;

/// <summary>One source PDF, one row of the file table.</summary>
internal sealed class DocumentViewModel(PdfDocumentInfo info)
{
    public string FilePath { get; } = info.FilePath;

    public string FileName { get; } = info.FileName;

    public int PageCount { get; } = info.PageCount;

    public string SizeText { get; } = FileSizeFormatter.Format(info.SizeBytes);

    /// <summary>Folder column: the file name already carries the leaf, so only the folder is shown.</summary>
    public string FolderPath { get; } = Path.GetDirectoryName(info.FilePath) ?? string.Empty;

    /// <summary>What a screen reader announces for the row, in place of the type name.</summary>
    public override string ToString() => FileName;
}
