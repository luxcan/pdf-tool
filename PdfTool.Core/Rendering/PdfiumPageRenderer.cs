using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace PdfTool.Core.Rendering;

/// <summary>
/// Renders pages with PDFium (via PDFtoImage). PDFium is synchronous and not safe to call
/// concurrently, so renders are serialised onto a background thread.
/// </summary>
public sealed class PdfiumPageRenderer : IPageRenderer
{
    public Task<byte[]> RenderPageAsync(
        string filePath,
        int pageIndex,
        int widthPixels,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(widthPixels, 1);

        return PdfiumGate.RunAsync(
            () => Render(filePath, pageIndex, widthPixels, password), cancellationToken);
    }

    private static byte[] Render(string filePath, int pageIndex, int widthPixels, string? password)
    {
        var options = new RenderOptions
        {
            Width = widthPixels,
            WithAspectRatio = true,
            WithAnnotations = true,
            BackgroundColor = SKColors.White
        };

        if (!File.Exists(filePath))
            throw new PdfToolException($"File not found: {filePath}");

        try
        {
            // The string overload of ToImage takes base-64 content, not a path, so read the file directly.
            using var stream = File.OpenRead(filePath);
            using var bitmap = Conversion.ToImage(stream, pageIndex, leaveOpen: true, password, options);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }
        catch (PdfPasswordProtectedException ex)
        {
            throw new PdfPasswordRequiredException(filePath, ex);
        }
        // PDFium reports an out-of-range page as an argument error; to callers it is a document
        // problem like any other, so it is reported the same way.
        catch (Exception ex) when (ex is PdfPageNotFoundException or ArgumentOutOfRangeException)
        {
            throw new PdfToolException(
                $"Page {pageIndex + 1} does not exist in '{Path.GetFileName(filePath)}'.", ex);
        }
        catch (PdfException ex)
        {
            throw new PdfToolException(
                $"'{Path.GetFileName(filePath)}' could not be rendered. It may be corrupt.", ex);
        }
    }
}
