namespace PdfTool.Core.Merging;

/// <summary>Progress of a merge, reported after each page is added to the output.</summary>
/// <param name="PagesWritten">Pages added so far.</param>
/// <param name="TotalPages">Pages the merge will add in total.</param>
/// <param name="CurrentFileName">Name of the file the last page came from.</param>
public sealed record MergeProgress(int PagesWritten, int TotalPages, string CurrentFileName)
{
    public double Fraction => TotalPages == 0 ? 0 : (double)PagesWritten / TotalPages;
}
