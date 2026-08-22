namespace PdfTool.App.Services;

/// <summary>
/// The file pickers, behind an interface so the view model can be exercised without a desktop.
/// </summary>
internal interface IFileDialogService
{
    /// <summary>Prompts for one or more PDFs to add. Returns an empty list if the user cancels.</summary>
    IReadOnlyList<string> PromptForPdfFiles();

    /// <summary>Prompts for an output path. Returns null if the user cancels.</summary>
    string? PromptForOutputFile(string suggestedFileName, string title);

    /// <summary>
    /// Prompts for a destination folder, used when compressing several files at once.
    /// Returns null if the user cancels.
    /// </summary>
    string? PromptForFolder(string title);

    /// <summary>Shows a message the user must acknowledge.</summary>
    void ShowError(string message);
}
