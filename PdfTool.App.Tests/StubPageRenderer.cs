using PdfTool.Core.Rendering;
using SkiaSharp;

namespace PdfTool.App.Tests;

/// <summary>Returns a real but tiny PNG, so the UI has something to bind without touching PDFium.</summary>
internal sealed class StubPageRenderer : IPageRenderer
{
    private static readonly byte[] Png = CreatePng();

    public int RenderCount { get; private set; }

    /// <summary>Largest width any page has been asked for, so a zoom that fails to re-render shows up.</summary>
    public int WidestRenderRequested { get; private set; }

    public async Task<byte[]> RenderPageAsync(
        string filePath,
        int pageIndex,
        int widthPixels,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        RenderCount++;
        WidestRenderRequested = Math.Max(WidestRenderRequested, widthPixels);

        // The real renderer never completes synchronously. Matching that is what exposes a request
        // that gets turned away while an earlier render is still running.
        await Task.Yield();

        return Png;
    }

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(8, 11));
        bitmap.Erase(SKColors.LightGray);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
