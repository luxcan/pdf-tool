using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Threading;
using PdfTool.App.Interop;
using PdfTool.App.ViewModels;

namespace PdfTool.App;

/// <summary>
/// Names the build, points at where it came from, and says where a newer one would be found. It is
/// deliberately the only place version information appears: a build reaches this application by hand
/// as often as by tag, so being able to ask it which one is installed is the point.
///
/// Nothing here goes near the network. The releases page is handed to the shell, because an
/// application whose claim is that files never leave the machine cannot then open a socket to ask
/// about itself.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>How long the copy button confirms itself before going back to offering the copy.</summary>
    private readonly DispatcherTimer _copyConfirmation =
        new() { Interval = TimeSpan.FromSeconds(2) };

    public AboutWindow()
    {
        InitializeComponent();

        DataContext = AboutInfo.Current;

        // One handler for every link in the window: RequestNavigate bubbles, and three identical
        // attributes on three hyperlinks would say nothing the address does not already.
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnLinkRequested));

        _copyConfirmation.Tick += OnCopyConfirmationElapsed;
    }

    private AboutInfo About => (AboutInfo)DataContext;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _copyConfirmation.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// A Hyperlink does not follow itself, so every link in the window arrives here.
    /// </summary>
    private void OnLinkRequested(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        Open(e.Uri.AbsoluteUri);
    }

    private void OnReleasesRequested(object sender, RoutedEventArgs e) => Open(About.ReleasesUrl);

    /// <summary>
    /// For the machine where this window is open but a browser is not - the address is worth more on
    /// the clipboard than read off the screen.
    /// </summary>
    private void OnCopyLinkRequested(object sender, RoutedEventArgs e)
    {
        // Another process can hold the clipboard open, in which case the copy is what fails rather
        // than the window: saying so is the whole of the handling this needs.
        try
        {
            Clipboard.SetText(About.ReleasesUrl);
            CopyLinkButton.Content = "Copied";
        }
        catch (ExternalException)
        {
            CopyLinkButton.Content = "Copy failed";
        }

        _copyConfirmation.Stop();
        _copyConfirmation.Start();
    }

    private void OnCopyConfirmationElapsed(object? sender, EventArgs e)
    {
        _copyConfirmation.Stop();
        CopyLinkButton.Content = "Copy link";
    }

    /// <summary>
    /// UseShellExecute is what hands the address to the browser rather than trying to run it.
    /// </summary>
    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
