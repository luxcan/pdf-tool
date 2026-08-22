namespace PdfTool.App.ViewModels;

/// <summary>
/// One step of the page-picker's tile size.
///
/// The caption under each preview is a fixed height whatever the tile, so the taller steps hand
/// proportionally more of themselves to the page image rather than scaling everything evenly.
/// </summary>
/// <param name="Name">Short label, sized to sit in a segment on the toolbar.</param>
/// <param name="Description">What the step is good for, shown on hover.</param>
/// <param name="TileWidth">Width of the whole tile, caption included.</param>
/// <param name="TileHeight">Height of the whole tile.</param>
/// <param name="ThumbnailPixels">
/// Width the page is rasterised at. Kept comfortably above the width it is drawn at, so the preview
/// stays sharp on a display running above 100% scaling.
/// </param>
internal sealed record PageZoomOption(
    string Name,
    string Description,
    int TileWidth,
    int TileHeight,
    int ThumbnailPixels)
{
    public static IReadOnlyList<PageZoomOption> All { get; } =
    [
        new("S", "Smallest tiles, with the most pages on screen at once.", 164, 252, 150),
        new("M", "Roomier tiles, still several rows at a time.", 240, 360, 250),
        new("L", "Large enough to read a heading and tell pages apart.", 330, 490, 350),
        new("XL", "Largest tiles, for separating pages that look alike.", 440, 650, 480)
    ];

    /// <summary>What a screen reader announces for the segment, in place of the record's own dump.</summary>
    public override string ToString() => Name;
}
