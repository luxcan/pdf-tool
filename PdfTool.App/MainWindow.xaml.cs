using System.Windows;
using PdfTool.App.Interop;
using PdfTool.App.ViewModels;

namespace PdfTool.App;

/// <summary>
/// Code-behind is limited to drag-and-drop, the title bar and opening the About window, none of
/// which has a clean binding equivalent; every other interaction goes through
/// <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    /// <summary>
    /// Owned by this window, so it centres on the application and cannot be lost behind it.
    /// </summary>
    private void OnAboutRequested(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedFiles(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFilesDropped(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var files = GetDroppedFiles(e);
        if (files.Length == 0)
            return;

        e.Handled = true;
        await viewModel.AddFilesAsync(files);
    }

    private static string[] GetDroppedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files
            : [];
}
