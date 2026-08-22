using System.IO;
using System.Windows;
using System.Windows.Threading;
using PdfTool.App.Services;
using PdfTool.App.ViewModels;
using PdfTool.Core.Compression;
using PdfTool.Core.Documents;
using PdfTool.Core.Merging;
using PdfTool.Core.Rendering;
using PdfTool.Core.Splitting;

namespace PdfTool.App;

/// <summary>
/// Composition root. The object graph is small enough that wiring it by hand stays clearer than
/// pulling in a container.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var viewModel = new MainViewModel(
            new PdfInspector(),
            new PdfMerger(),
            new PdfSplitter(),
            new PdfCompressor(),
            new PdfiumPageRenderer(),
            new FileDialogService());

        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();

        // Supports dragging PDFs onto the executable, and "Open with" from Explorer.
        if (e.Args.Length > 0)
            _ = viewModel.AddFilesAsync(e.Args);
    }

    /// <summary>Last line of defence: report rather than vanish, so failures stay diagnosable.</summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);

        MessageBox.Show(
            $"Something went wrong:{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "PDF Tool - Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    /// <summary>
    /// The dialog shows only the message; the full stack goes to a known file. A failure to write
    /// the log must never replace the error the user is already being told about.
    /// </summary>
    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "pdftool-crash.txt"), exception.ToString());
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
            // Nothing useful left to do.
        }
    }
}
