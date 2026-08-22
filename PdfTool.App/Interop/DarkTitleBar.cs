using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PdfTool.App.Interop;

/// <summary>
/// The window chrome is drawn by Windows, not by the theme, so every window has to be told to go
/// dark as well or it wears a light title bar over a dark application.
/// </summary>
internal static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;

    /// <summary>
    /// Applies to a window that already has a handle, which is what OnSourceInitialized guarantees.
    /// The call is cosmetic and unsupported before Windows 10 2004, where the result is an error code
    /// and the title bar simply stays light.
    /// </summary>
    public static void Apply(Window window)
    {
        var isDark = 1;
        DwmSetWindowAttribute(
            new WindowInteropHelper(window).Handle, UseImmersiveDarkMode, ref isDark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
