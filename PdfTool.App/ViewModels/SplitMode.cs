namespace PdfTool.App.ViewModels;

/// <summary>How the pages of a document are divided between the files a split writes.</summary>
internal enum SplitMode
{
    /// <summary>Consecutive chunks of a fixed size. One page per file is this with a size of one.</summary>
    EveryNPages,

    /// <summary>A list the user writes out, such as "1-3, 5, 8-10", giving one file per range.</summary>
    Ranges
}
