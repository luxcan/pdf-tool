using PdfTool.App.Services;

namespace PdfTool.App.Tests;

/// <summary>Answers dialog prompts from a script, so tests never open a window.</summary>
internal sealed class StubFileDialogService : IFileDialogService
{
    public IReadOnlyList<string> FilesToReturn { get; set; } = [];

    public string? OutputFileToReturn { get; set; }

    public string? FolderToReturn { get; set; }

    public List<string> Errors { get; } = [];

    public IReadOnlyList<string> PromptForPdfFiles() => FilesToReturn;

    public string? PromptForOutputFile(string suggestedFileName, string title) => OutputFileToReturn;

    public string? PromptForFolder(string title) => FolderToReturn;

    public void ShowError(string message) => Errors.Add(message);
}
