using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfTool.App.Services;
using PdfTool.Core;
using PdfTool.Core.Documents;

namespace PdfTool.App.ViewModels;

/// <summary>
/// A list of source PDFs with the add, remove, clear and reorder actions around it. Each tab owns
/// its own instance, so clearing the compression list never disturbs a merge that is being set up.
/// </summary>
internal sealed partial class DocumentListViewModel : ObservableObject
{
    private readonly PdfInspector _inspector;
    private readonly IFileDialogService _dialogs;

    public DocumentListViewModel(PdfInspector inspector, IFileDialogService dialogs)
    {
        _inspector = inspector;
        _dialogs = dialogs;

        Documents.CollectionChanged += (_, _) => RaiseChanged();
    }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>Raised when the contents or the availability of actions change.</summary>
    public event EventHandler? Changed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private DocumentViewModel? _selectedDocument;

    /// <summary>Set by the owner while a merge or compression is running, to keep the list still.</summary>
    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _isAdding;

    public bool HasDocuments => Documents.Count > 0;

    public int TotalPageCount => Documents.Sum(document => document.PageCount);

    /// <summary>Entry point for the Add button, dropped files and command-line arguments alike.</summary>
    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        // The Add button honours the lock through CanModify, but a file dropped on the window
        // reaches this directly. An operation is meant to run against a list that holds still, and
        // on the Merge tab a list that moves underneath one throws the page choices away with it.
        if (IsLocked)
            return;

        var newPaths = filePaths
            .Where(path => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(path => Documents.All(d => !string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (newPaths.Count == 0)
            return;

        IsAdding = true;

        try
        {
            var failures = new List<string>();

            foreach (var path in newPaths)
            {
                try
                {
                    var info = await Task.Run(() => _inspector.Inspect(path)).ConfigureAwait(true);
                    Documents.Add(new DocumentViewModel(info));
                }
                catch (PdfToolException ex)
                {
                    failures.Add(ex.Message);
                }
            }

            if (failures.Count > 0)
                _dialogs.ShowError(string.Join(Environment.NewLine, failures));
        }
        finally
        {
            IsAdding = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private Task PromptForFilesAsync() => AddFilesAsync(_dialogs.PromptForPdfFiles());

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedDocument is not null)
            Documents.Remove(SelectedDocument);
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear() => Documents.Clear();

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private void Move(int offset)
    {
        if (SelectedDocument is null)
            return;

        var from = Documents.IndexOf(SelectedDocument);
        var to = from + offset;

        if (to < 0 || to >= Documents.Count)
            return;

        Documents.Move(from, to);
        SelectedDocument = Documents[to];
    }

    partial void OnIsLockedChanged(bool value) => RaiseChanged();

    partial void OnIsAddingChanged(bool value) => RaiseChanged();

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(TotalPageCount));

        PromptForFilesCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool CanModify() => !IsLocked && !IsAdding;

    private bool CanClear() => CanModify() && HasDocuments;

    private bool CanRemove() => CanModify() && SelectedDocument is not null;

    private bool CanMoveUp() => CanRemove() && Documents.IndexOf(SelectedDocument!) > 0;

    private bool CanMoveDown() => CanRemove() && Documents.IndexOf(SelectedDocument!) < Documents.Count - 1;
}
