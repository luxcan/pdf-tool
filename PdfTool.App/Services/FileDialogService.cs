using System.Windows;
using Microsoft.Win32;

namespace PdfTool.App.Services;

/// <inheritdoc />
internal sealed class FileDialogService : IFileDialogService
{
    private const string PdfFilter = "PDF documents (*.pdf)|*.pdf|All files (*.*)|*.*";

    public IReadOnlyList<string> PromptForPdfFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add PDF files",
            Filter = PdfFilter,
            Multiselect = true,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PromptForOutputFile(string suggestedFileName, string title)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = PdfFilter,
            FileName = suggestedFileName,
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PromptForFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(message, "PDF Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
}
