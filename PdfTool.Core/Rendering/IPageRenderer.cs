namespace PdfTool.Core.Rendering;

/// <summary>
/// Rasterises single PDF pages for the page-picker preview. Kept behind an interface so the
/// presentation layer never takes a direct dependency on the native rendering engine.
/// </summary>
public interface IPageRenderer
{
    /// <summary>Renders one page to PNG bytes, scaled to the requested width, preserving aspect ratio.</summary>
    /// <param name="filePath">Full path of the source PDF.</param>
    /// <param name="pageIndex">Zero-based page index within that document.</param>
    /// <param name="widthPixels">Target width in pixels; height follows the page's aspect ratio.</param>
    /// <param name="password">Password, if the document is encrypted.</param>
    /// <param name="cancellationToken">Cancels rendering when the page scrolls out of view.</param>
    Task<byte[]> RenderPageAsync(
        string filePath,
        int pageIndex,
        int widthPixels,
        string? password = null,
        CancellationToken cancellationToken = default);
}
