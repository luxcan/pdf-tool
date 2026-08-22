namespace PdfTool.Core.Splitting;

/// <summary>Progress of a split, reported after each output file is written.</summary>
/// <param name="FilesWritten">Files written so far.</param>
/// <param name="TotalFiles">Files the split will write in total.</param>
/// <param name="CurrentFileName">Name of the file just written.</param>
public sealed record SplitProgress(int FilesWritten, int TotalFiles, string CurrentFileName)
{
    public double Fraction => TotalFiles == 0 ? 0 : (double)FilesWritten / TotalFiles;
}
