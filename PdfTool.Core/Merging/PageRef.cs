namespace PdfTool.Core.Merging;

/// <summary>
/// One page taken from a source document, positioned by its place in the list handed to
/// <see cref="PdfMerger"/>. Both merge modes produce a list of these: "merge whole files"
/// builds every page in file order, "choose pages" builds whatever the user kept.
/// </summary>
/// <param name="SourcePath">Full path of the PDF the page is taken from.</param>
/// <param name="PageIndex">Zero-based index of the page within that source document.</param>
/// <param name="Rotation">
/// Extra clockwise rotation in degrees, applied on top of the page's existing rotation.
/// Must be 0, 90, 180 or 270.
/// </param>
public sealed record PageRef(string SourcePath, int PageIndex, int Rotation = 0)
{
    /// <summary>Rotations a page can carry; anything else is rejected before merging.</summary>
    public static bool IsValidRotation(int rotation) =>
        rotation is 0 or 90 or 180 or 270;
}
