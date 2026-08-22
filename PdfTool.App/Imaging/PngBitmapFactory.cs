using System.IO;
using System.Windows.Media.Imaging;

namespace PdfTool.App.Imaging;

/// <summary>Turns encoded PNG bytes into a frozen bitmap that is safe to hand to the UI thread.</summary>
internal static class PngBitmapFactory
{
    public static BitmapSource FromPng(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);

        using var stream = new MemoryStream(png);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        // OnLoad decodes immediately so the bitmap does not depend on the stream once disposed.
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        // Freezing is what allows a bitmap created off the UI thread to be bound to.
        bitmap.Freeze();
        return bitmap;
    }
}
