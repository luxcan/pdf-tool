using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfTool.App.Behaviors;
using PdfTool.App.Formatting;
using PdfTool.App.Services;
using PdfTool.Core;
using PdfTool.Core.Compression;
using PdfTool.Core.Documents;
using PdfTool.Core.Merging;
using PdfTool.Core.Rendering;
using PdfTool.Core.Splitting;

namespace PdfTool.App.ViewModels;

/// <summary>
/// Hosts the three tabs. Merge, Split and Compress each own their own file list; everything below
/// the tabs - progress, status, cancellation - is shared, because only one operation runs at a time.
/// </summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private const string DefaultMergeFileName = "merged.pdf";
    private const string CompressedFileSuffix = "-compressed";
    private const int MergeTabIndex = 0;
    private const int SplitTabIndex = 1;

    private readonly PdfMerger _merger;
    private readonly PdfSplitter _splitter;
    private readonly PdfCompressor _compressor;
    private readonly IPageRenderer _renderer;
    private readonly IFileDialogService _dialogs;

    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _thumbnailCancellation;

    /// <summary>
    /// What the split settings currently work out to, or null while they do not work out to
    /// anything. Held so the button, the summary and the split itself all agree.
    /// </summary>
    private IReadOnlyList<PageRange>? _plannedRanges;

    public MainViewModel(
        PdfInspector inspector,
        PdfMerger merger,
        PdfSplitter splitter,
        PdfCompressor compressor,
        IPageRenderer renderer,
        IFileDialogService dialogs)
    {
        _merger = merger;
        _splitter = splitter;
        _compressor = compressor;
        _renderer = renderer;
        _dialogs = dialogs;

        MergeFiles = new DocumentListViewModel(inspector, dialogs);
        SplitFiles = new DocumentListViewModel(inspector, dialogs);
        CompressFiles = new DocumentListViewModel(inspector, dialogs);

        MergeFiles.Changed += (_, _) => OnActiveListChanged();
        CompressFiles.Changed += (_, _) => OnActiveListChanged();

        // Deliberately the collection and not Changed: Changed also fires when the list is locked
        // and unlocked, which happens at the start of the very merge that is consuming the page
        // choices. Only an actual change of contents may disturb them.
        MergeFiles.Documents.CollectionChanged += OnMergeDocumentsChanged;
        MergeFiles.PropertyChanged += OnMergeFilesPropertyChanged;

        // The split settings are only meaningful against a document, so they are worked out again
        // whenever the list changes as well as whenever a setting does.
        SplitFiles.Changed += (_, _) =>
        {
            OnActiveListChanged();
            UpdateSplitPlan();
        };

        UpdateSplitPlan();
        StatusMessage = DescribeActiveList();
    }

    public DocumentListViewModel MergeFiles { get; }

    public DocumentListViewModel SplitFiles { get; }

    public DocumentListViewModel CompressFiles { get; }

    public IReadOnlyList<CompressionPresetOption> CompressionPresets => CompressionPresetOption.All;

    /// <summary>
    /// Pages built from the merge list when the user opens the page picker, and the sole authority
    /// on what a merge writes once it holds choices the file list cannot express. It is kept in step
    /// with the file list rather than rebuilt on each visit, so closing the picker no longer throws
    /// away the arrangement made in it.
    /// </summary>
    public ObservableCollection<PageViewModel> Pages { get; } = [];

    /// <summary>
    /// Whether the picker holds something the file list cannot say for itself - a page moved,
    /// rotated, or left out. Worked out by comparing the two rather than recorded in a flag: a flag
    /// is a second thing that can disagree with the collection, which is the fault this exists to
    /// close. Opening the picker and closing it again therefore leaves nothing behind.
    /// </summary>
    public bool HasPageArrangement => Pages.Count > 0 && !MatchesFileOrder();

    /// <summary>How many pages the next merge would write, from whichever source currently speaks.</summary>
    public int MergeInputCount => HasPageArrangement ? SelectedPageCount : MergeFiles.TotalPageCount;

    /// <summary>
    /// What the merge button says. A button that reads "Merge all pages" while a page arrangement
    /// is standing behind it would be describing the one thing it is not about to do.
    /// </summary>
    public string MergeButtonLabel => HasPageArrangement
        ? $"Merge {MergeInputCount} chosen page(s)"
        : "Merge all pages";

    /// <summary>Tile sizes the page picker offers.</summary>
    public IReadOnlyList<PageZoomOption> PageZoomLevels { get; } = PageZoomOption.All;

    [ObservableProperty]
    private PageZoomOption _selectedPageZoom = PageZoomOption.All[0];

    /// <summary>The list the current tab operates on; dropped files and arguments land here.</summary>
    public DocumentListViewModel ActiveList => SelectedTabIndex switch
    {
        MergeTabIndex => MergeFiles,
        SplitTabIndex => SplitFiles,
        _ => CompressFiles
    };

    /// <summary>Ways of splitting, offered as segments.</summary>
    public IReadOnlyList<SplitModeOption> SplitModes { get; } = SplitModeOption.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplittingByRanges))]
    private SplitModeOption _selectedSplitMode = SplitModeOption.All[0];

    /// <summary>
    /// Held as text rather than a number so that what the user typed can be judged and explained.
    /// A binding that silently keeps the last good value leaves them staring at a disabled button.
    /// </summary>
    [ObservableProperty]
    private string _pagesPerFileText = "1";

    [ObservableProperty]
    private string _pageRangesText = string.Empty;

    /// <summary>Whether the ranges box applies, rather than the chunk size.</summary>
    public bool IsSplittingByRanges => SelectedSplitMode.Mode == SplitMode.Ranges;

    /// <summary>What the current settings would produce, or why they cannot be used.</summary>
    [ObservableProperty]
    private string _splitSummary = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RotatePageCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageEarlierCommand))]
    [NotifyCanExecuteChangedFor(nameof(MovePageLaterCommand))]
    private PageViewModel? _selectedPage;

    [ObservableProperty]
    private bool _isChoosingPages;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChoosePagesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SplitCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedPageCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowOutputFolderCommand))]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    private string? _lastOutputPath;

    /// <summary>Gates the Show in folder button, which is hidden until there is something to show.</summary>
    public bool HasOutput => LastOutputPath is not null;

    /// <summary>Applied by the Compress tab and by compressing what a merge or a split wrote alike.</summary>
    [ObservableProperty]
    private CompressionPresetOption _selectedCompressionPreset =
        CompressionPresetOption.All.Single(option => option.Preset == CompressionPreset.Balanced);

    [ObservableProperty]
    private bool _compressMergedOutput;

    [ObservableProperty]
    private bool _compressSplitParts;

    /// <summary>Routes dropped files and command-line arguments to whichever tab is showing.</summary>
    public Task AddFilesAsync(IEnumerable<string> filePaths) => ActiveList.AddFilesAsync(filePaths);

    // ===================================== Merge =====================================

    /// <summary>
    /// The one place a merge input is produced. With an arrangement held it is that arrangement,
    /// verbatim; without one it is every page of every file in list order. Nothing else builds
    /// PageRefs, so no screen can merge in an order the user is not looking at.
    /// </summary>
    internal IReadOnlyList<PageRef> BuildMergeInput() => HasPageArrangement
        ? [.. Pages.Where(page => page.IsSelected).Select(page => page.ToPageRef())]
        : [.. MergeFiles.Documents.SelectMany(document => Enumerable
            .Range(0, document.PageCount)
            .Select(index => new PageRef(document.FilePath, index)))];

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private Task MergeAsync() => WriteMergedFileAsync(BuildMergeInput());

    [RelayCommand(CanExecute = nameof(CanChoosePages))]
    private void ChoosePages()
    {
        // Only when there is nothing to show. Rebuilding here is what used to discard an arrangement
        // the moment the user came back to look at it again.
        if (Pages.Count == 0)
            BuildPages();

        IsChoosingPages = true;
        StatusMessage = "Reorder, rotate or deselect pages, then merge.";
    }

    /// <summary>
    /// Puts the pages back the way the file list has them: original order, none left out, none
    /// turned. The only way back to a plain merge once the picker has been used for something.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasPageArrangement))]
    private void ResetPages()
    {
        DiscardPages();
        BuildPages();
        StatusMessage = "Page choices reset to the file order.";
    }

    [RelayCommand]
    private void BackToFiles()
    {
        CancelThumbnailLoading();
        IsChoosingPages = false;

        // A merge started from inside the picker keeps reporting its progress; leaving the picker
        // while it runs must not paint over it.
        if (!IsBusy)
            StatusMessage = DescribeActiveList();
    }

    [RelayCommand]
    private void SelectAllPages() => SetAllPagesSelected(true);

    [RelayCommand]
    private void SelectNoPages() => SetAllPagesSelected(false);

    [RelayCommand]
    private void InvertPageSelection()
    {
        foreach (var page in Pages)
            page.IsSelected = !page.IsSelected;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPage))]
    private void RotatePage() => SelectedPage?.RotateClockwise();

    [RelayCommand(CanExecute = nameof(CanMovePageEarlier))]
    private void MovePageEarlier() => MoveSelectedPage(-1);

    [RelayCommand(CanExecute = nameof(CanMovePageLater))]
    private void MovePageLater() => MoveSelectedPage(1);

    /// <summary>
    /// Applies a page dragged onto another's position. The moved page is left selected, so the
    /// rotate and nudge buttons keep acting on whichever page the user was just handling.
    /// </summary>
    [RelayCommand]
    private void ReorderPages(ReorderRequest? request)
    {
        if (request is null || request.From == request.To)
            return;

        if (request.From < 0 || request.From >= Pages.Count)
            return;

        if (request.To < 0 || request.To >= Pages.Count)
            return;

        Pages.Move(request.From, request.To);
        SelectedPage = Pages[request.To];
        NotifyPagePositionChanged();
    }

    /// <summary>
    /// Invoked as each virtualised thumbnail container is realised. A screenful of tiles is realised
    /// in one layout pass, so the command has to stay executable while earlier renders are still in
    /// flight - otherwise every tile after the first is turned away and stays blank. The renderer
    /// serialises the actual work itself.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadThumbnailAsync(PageViewModel? page)
    {
        if (page is null)
            return;

        _thumbnailCancellation ??= new CancellationTokenSource();

        await page.EnsureThumbnailAsync(SelectedPageZoom.ThumbnailPixels, _thumbnailCancellation.Token)
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Re-renders what is already on screen at the new size. Only pages with something to show are
    /// asked: the rest are still virtualised away and will render at the current size the first
    /// time they are realised.
    /// </summary>
    partial void OnSelectedPageZoomChanged(PageZoomOption value)
    {
        // Every page that has been asked for, not just those that have answered: one still rendering
        // has no thumbnail yet, and skipping it leaves it stretched at the old size with nothing
        // left to trigger it again. The width guard makes the requests that are already big enough
        // free.
        foreach (var page in Pages.Where(page => page.HasBeenRequested))
            LoadThumbnailCommand.Execute(page);
    }

    /// <summary>
    /// Moves the tile size one step, and stops at either end rather than wrapping: a wheel held down
    /// at the largest size should settle there, not drop back to the smallest.
    /// </summary>
    [RelayCommand]
    private void StepPageZoom(int direction)
    {
        var index = PageZoomLevels.TakeWhile(level => level != SelectedPageZoom).Count();

        SelectedPageZoom = PageZoomLevels[Math.Clamp(index + direction, 0, PageZoomLevels.Count - 1)];
    }

    // ===================================== Split =====================================

    /// <summary>
    /// Writes one file per range into a folder the user picks. Parts are named after the pages
    /// inside them, so the folder can be read without opening anything.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSplit))]
    private async Task SplitAsync()
    {
        if (_plannedRanges is not { Count: > 0 } ranges || SplitFiles.Documents.Count != 1)
            return;

        var input = SplitFiles.Documents[0].FilePath;

        var folder = _dialogs.PromptForFolder("Choose where to save the split files");
        if (folder is null)
            return;

        BeginOperation();

        var progress = new Progress<SplitProgress>(report =>
        {
            ProgressFraction = report.Fraction;
            StatusMessage = $"Writing {report.CurrentFileName} ({report.FilesWritten} of {report.TotalFiles})...";
        });

        try
        {
            var result = await _splitter
                .SplitAsync(
                    input, ranges, folder,
                    progress: progress, cancellationToken: _operationCancellation!.Token)
                .ConfigureAwait(true);

            // Points at the first part, so "Show in folder" opens the folder with it selected.
            LastOutputPath = result.OutputPaths.Count > 0 ? result.OutputPaths[0] : null;

            StatusMessage = $"Split into {result.FileCount} file(s) in {result.OutputDirectory}.";

            if (CompressSplitParts && result.OutputPaths.Count > 0)
                await CompressSplitPartsAsync(result, _operationCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Split cancelled.";
        }
        catch (PdfToolException ex)
        {
            StatusMessage = "Split failed.";
            _dialogs.ShowError(ex.Message);
        }
        // A folder the user picks carries no writability check, unlike a save dialog, so being
        // refused is an ordinary outcome here rather than a fault worth a crash report.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Split failed.";
            _dialogs.ShowError($"The split files could not be written: {ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    private bool CanSplit() => !IsBusy && _plannedRanges is { Count: > 0 };

    partial void OnSelectedSplitModeChanged(SplitModeOption value) => UpdateSplitPlan();

    partial void OnPagesPerFileTextChanged(string value) => UpdateSplitPlan();

    partial void OnPageRangesTextChanged(string value) => UpdateSplitPlan();

    /// <summary>
    /// Works out what the settings mean against the document on the tab, and says so. The plan, the
    /// summary and whether the button is live are all decided here so they cannot disagree.
    /// </summary>
    private void UpdateSplitPlan()
    {
        _plannedRanges = null;

        if (SplitFiles.Documents.Count == 0)
        {
            SplitSummary = "Add a PDF to split.";
        }
        else if (SplitFiles.Documents.Count > 1)
        {
            // Ranges are written against one document's page numbers, so several at once would be
            // ambiguous rather than merely unimplemented.
            SplitSummary = "Split takes one file at a time. Remove the others.";
        }
        else
        {
            var pageCount = SplitFiles.Documents[0].PageCount;

            try
            {
                _plannedRanges = BuildRanges(pageCount);
                SplitSummary = DescribeSplitPlan(_plannedRanges, pageCount);
            }
            catch (PdfToolException ex)
            {
                SplitSummary = ex.Message;
            }
        }

        SplitCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<PageRange> BuildRanges(int pageCount)
    {
        if (SelectedSplitMode.Mode == SplitMode.Ranges)
            return PageRange.Parse(PageRangesText, pageCount);

        // Read the same way page numbers are read in a range list: digits only, no sign, no
        // separators, and no dependence on the machine's regional settings.
        if (!int.TryParse(PagesPerFileText, NumberStyles.None, CultureInfo.InvariantCulture, out var pagesPerFile)
            || pagesPerFile < 1)
            throw new PdfToolException("Pages per file must be a whole number of at least 1.");

        return PageRange.EveryNPages(pageCount, pagesPerFile);
    }

    private static string DescribeSplitPlan(IReadOnlyList<PageRange> ranges, int pageCount)
    {
        var pagesKept = ranges.Sum(range => range.PageCount);

        var summary = ranges.Count == 1
            ? "Will write 1 file"
            : $"Will write {ranges.Count} files";

        // Ranges may skip pages or repeat them, so what happens to the rest is worth stating.
        return pagesKept == pageCount
            ? $"{summary}, covering all {pageCount} page(s)."
            : $"{summary}, using {pagesKept} of {pageCount} page(s).";
    }

    // ===================================== Compress =====================================

    /// <summary>
    /// Compresses every file on the Compress tab into its own output. A single file is saved
    /// wherever the user says; several go into a folder they pick, keeping their names.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAsync()
    {
        var destinations = ResolveCompressionDestinations();
        if (destinations.Count == 0)
            return;

        BeginOperation();

        try
        {
            var results = await CompressAllAsync(destinations, _operationCancellation!.Token)
                .ConfigureAwait(true);

            StatusMessage = DescribeSaving(results);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Compression cancelled.";
        }
        catch (PdfToolException ex)
        {
            StatusMessage = "Compression failed.";
            _dialogs.ShowError(ex.Message);
        }
        // Compressing several files also writes into a folder the user picked, with the same lack of
        // any writability check behind it.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Compression failed.";
            _dialogs.ShowError($"The compressed file could not be written: {ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    // ===================================== Shared =====================================

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _operationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void ShowOutputFolder()
    {
        if (LastOutputPath is null || !File.Exists(LastOutputPath))
            return;

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LastOutputPath}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task WriteMergedFileAsync(IReadOnlyList<PageRef> pages)
    {
        if (pages.Count == 0)
        {
            _dialogs.ShowError("No pages are selected.");
            return;
        }

        var outputPath = _dialogs.PromptForOutputFile(DefaultMergeFileName, "Save merged PDF");
        if (outputPath is null)
            return;

        BeginOperation();

        // Created on the UI thread, so reports marshal back to it automatically.
        var progress = new Progress<MergeProgress>(report =>
        {
            ProgressFraction = report.Fraction;
            StatusMessage = $"Merging page {report.PagesWritten} of {report.TotalPages} ({report.CurrentFileName})...";
        });

        try
        {
            var result = await _merger
                .MergeAsync(pages, outputPath, progress: progress, cancellationToken: _operationCancellation!.Token)
                .ConfigureAwait(true);

            LastOutputPath = result.OutputPath;
            StatusMessage =
                $"Merged {result.PageCount} page(s) into {Path.GetFileName(result.OutputPath)} " +
                $"({FileSizeFormatter.Format(result.SizeBytes)}).";

            if (CompressMergedOutput)
                await CompressMergedFileAsync(result, _operationCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Merge cancelled.";
        }
        catch (PdfToolException ex)
        {
            StatusMessage = "Merge failed.";
            _dialogs.ShowError(ex.Message);
        }
        catch (IOException ex)
        {
            StatusMessage = "Merge failed.";
            _dialogs.ShowError($"The merged file could not be written: {ex.Message}");
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>Squeezes the file the merge just produced, in place.</summary>
    private async Task CompressMergedFileAsync(MergeResult merge, CancellationToken cancellationToken)
    {
        var results = await CompressAllAsync([(merge.OutputPath, merge.OutputPath)], cancellationToken)
            .ConfigureAwait(true);

        StatusMessage =
            $"Merged {merge.PageCount} page(s) into {Path.GetFileName(merge.OutputPath)}, " +
            $"{DescribeSizeChange(results)}.";
    }

    /// <summary>Squeezes the parts the split just produced, each in place.</summary>
    private async Task CompressSplitPartsAsync(SplitResult split, CancellationToken cancellationToken)
    {
        var results = await CompressAllAsync(
                [.. split.OutputPaths.Select(path => (path, path))], cancellationToken)
            .ConfigureAwait(true);

        // Back to the first part, which compressing the rest of them moved off.
        LastOutputPath = split.OutputPaths[0];

        StatusMessage =
            $"Split into {split.FileCount} file(s) in {split.OutputDirectory}, " +
            $"{DescribeSizeChange(results)}.";
    }

    /// <summary>
    /// Runs each pairing through the compressor in turn, driving the progress bar across the batch
    /// as a whole. This is the single path behind every compression the tool performs, so the
    /// Compress tab, a merge and a split all shrink a file by exactly the same means.
    /// </summary>
    private async Task<IReadOnlyList<CompressionResult>> CompressAllAsync(
        IReadOnlyList<(string InputPath, string OutputPath)> files,
        CancellationToken cancellationToken)
    {
        var settings = CompressionSettings.FromPreset(SelectedCompressionPreset.Preset);
        var results = new List<CompressionResult>(files.Count);

        ProgressFraction = 0;

        foreach (var (inputPath, outputPath) in files)
        {
            // Said before the work starts rather than on the first report, because a document with
            // no images to examine never reports at all.
            StatusMessage = DescribeCompressionStep(inputPath, results.Count + 1, files.Count);

            var finished = results.Count;

            // Weighted across the batch, so the bar does not restart on every file.
            var progress = new Progress<CompressionProgress>(
                report => ProgressFraction = (finished + report.Fraction) / files.Count);

            var result = await _compressor
                .CompressAsync(
                    inputPath, outputPath, settings,
                    progress: progress, cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            results.Add(result);

            LastOutputPath = result.OutputPath;
            ProgressFraction = (double)results.Count / files.Count;
        }

        return results;
    }

    /// <summary>
    /// Names the file being compressed, and where it sits in the batch when there is a batch. A lone
    /// file counted as "1 of 1" only invites the reader to wonder what the other one was.
    /// </summary>
    private static string DescribeCompressionStep(string inputPath, int position, int total) =>
        total == 1
            ? $"Compressing {Path.GetFileName(inputPath)}..."
            : $"Compressing {Path.GetFileName(inputPath)} ({position} of {total})...";

    private void BeginOperation()
    {
        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        ProgressFraction = 0;
        LastOutputPath = null;
    }

    private void EndOperation()
    {
        // Releasing the lists raises their change events, and those describe the list in the status
        // bar. Without holding onto it, whatever the finished operation just reported - how much was
        // saved, where the parts went - would be replaced before the user could read it.
        var outcome = StatusMessage;

        ProgressFraction = 0;
        IsBusy = false;
        _operationCancellation?.Dispose();
        _operationCancellation = null;

        StatusMessage = outcome;
    }

    /// <summary>Pairs each listed file with where its compressed copy goes; empty if the user backs out.</summary>
    private List<(string InputPath, string OutputPath)> ResolveCompressionDestinations()
    {
        if (CompressFiles.Documents.Count == 1)
        {
            var input = CompressFiles.Documents[0].FilePath;
            var output = _dialogs.PromptForOutputFile(SuggestCompressedName(input), "Save compressed PDF");

            return output is null ? [] : [(input, output)];
        }

        var folder = _dialogs.PromptForFolder("Choose where to save the compressed files");
        if (folder is null)
            return [];

        // Files with the same name from different folders are a normal batch - scans filed by year,
        // say - and they would otherwise all land on one output, leaving the user with one document
        // where they asked for several and a saving figure describing bytes that are not on disk.
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return [.. CompressFiles.Documents.Select(document =>
            (document.FilePath, Path.Combine(folder, UniqueCompressedName(document.FilePath, taken))))];
    }

    private static string UniqueCompressedName(string path, HashSet<string> taken)
    {
        var name = SuggestCompressedName(path);

        if (taken.Add(name))
            return name;

        for (var copy = 2; ; copy++)
        {
            var candidate =
                $"{Path.GetFileNameWithoutExtension(path)}{CompressedFileSuffix} ({copy}){Path.GetExtension(path)}";

            if (taken.Add(candidate))
                return candidate;
        }
    }

    private static string SuggestCompressedName(string path) =>
        $"{Path.GetFileNameWithoutExtension(path)}{CompressedFileSuffix}{Path.GetExtension(path)}";

    /// <summary>The whole outcome of the Compress tab, which is a batch and says so.</summary>
    private static string DescribeSaving(IReadOnlyList<CompressionResult> results)
    {
        if (results.Count == 0)
            return "Nothing to compress.";

        var (originalBytes, compressedBytes) = TotalBytes(results);
        var saved = originalBytes - compressedBytes;

        if (saved <= 0)
        {
            return $"Compressed {results.Count} file(s). Already as small as this tool can make them, " +
                   "so the originals were kept.";
        }

        return $"Compressed {results.Count} file(s): {FileSizeFormatter.Format(originalBytes)} to " +
               $"{FileSizeFormatter.Format(compressedBytes)}, saving {FileSizeFormatter.Format(saved)} " +
               $"({(double)saved / originalBytes:P0}).";
    }

    /// <summary>
    /// What compression did, as a clause the merge and split reports finish with. Those already say
    /// how many files they wrote, so this describes the bytes and leaves the counting to them.
    /// </summary>
    private static string DescribeSizeChange(IReadOnlyList<CompressionResult> results)
    {
        var (originalBytes, compressedBytes) = TotalBytes(results);
        var saved = originalBytes - compressedBytes;

        return saved <= 0
            ? $"already at its smallest at {FileSizeFormatter.Format(compressedBytes)}"
            : $"compressed from {FileSizeFormatter.Format(originalBytes)} to " +
              $"{FileSizeFormatter.Format(compressedBytes)} ({(double)saved / originalBytes:P0} smaller)";
    }

    private static (long OriginalBytes, long CompressedBytes) TotalBytes(
        IReadOnlyList<CompressionResult> results) =>
        (results.Sum(result => result.OriginalBytes), results.Sum(result => result.CompressedBytes));

    /// <summary>Fills an empty page list from the file list, in file order, with nothing left out.</summary>
    private void BuildPages()
    {
        foreach (var document in MergeFiles.Documents)
        {
            for (var index = 0; index < document.PageCount; index++)
            {
                var page = new PageViewModel(document.FilePath, index, _renderer);
                page.PropertyChanged += OnPagePropertyChanged;
                Pages.Add(page);
            }
        }

        _thumbnailCancellation = new CancellationTokenSource();
        UpdateSelectedPageCount();
    }

    /// <summary>
    /// Empties the page list and lets go of everything hanging off it - the subscriptions, the
    /// selection, and the thumbnails, which are decoded bitmaps and worth releasing.
    /// </summary>
    private void DiscardPages()
    {
        if (Pages.Count == 0)
            return;

        CancelThumbnailLoading();

        foreach (var page in Pages)
            page.PropertyChanged -= OnPagePropertyChanged;

        // Before the clear: nothing may be left pointing into an emptied collection.
        SelectedPage = null;
        Pages.Clear();
        UpdateSelectedPageCount();
    }

    /// <summary>
    /// A file moving does not disturb a page arrangement - the arrangement already says where every
    /// page goes, and the order it was built from stopped having a say the moment it existed. Adding
    /// or removing one does: there would be pages in the merge the picker never showed, or pages in
    /// the picker with no file behind them. Those choices are dropped, and the drop is announced.
    /// </summary>
    private void OnMergeDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move || Pages.Count == 0)
            return;

        var wasArranged = HasPageArrangement;

        DiscardPages();

        // Files arrive one at a time, so a multi-file drop waits for the last of them rather than
        // rebuilding the grid - and throwing its thumbnails away - once per file.
        if (IsChoosingPages && !MergeFiles.IsAdding)
            BuildPages();
        else if (wasArranged && !IsBusy && !IsChoosingPages)
            StatusMessage = "The file list changed, so the page choices went back to the file order.";

        NotifyMergeInputChanged();
    }

    private void OnMergeFilesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentListViewModel.IsAdding)
            && !MergeFiles.IsAdding
            && IsChoosingPages
            && Pages.Count == 0)
        {
            BuildPages();
        }
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Rotation counts as much as selection: both are choices the file list cannot express, and
        // both decide whether an arrangement is being held.
        if (e.PropertyName == nameof(PageViewModel.IsSelected))
            UpdateSelectedPageCount();
        else if (e.PropertyName == nameof(PageViewModel.Rotation))
            NotifyMergeInputChanged();
    }

    private void UpdateSelectedPageCount()
    {
        SelectedPageCount = Pages.Count(page => page.IsSelected);
        NotifyMergeInputChanged();
    }

    /// <summary>
    /// Everything the merge button reads is worked out from the page list rather than stored, so
    /// anything that moves, turns or unticks a page has to say that those answers have changed.
    /// </summary>
    private void NotifyMergeInputChanged()
    {
        OnPropertyChanged(nameof(HasPageArrangement));
        OnPropertyChanged(nameof(MergeInputCount));
        OnPropertyChanged(nameof(MergeButtonLabel));

        MergeCommand.NotifyCanExecuteChanged();
        ResetPagesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Whether the page list still says exactly what the file list says: every page of every file,
    /// in order, all kept, none turned. This is the comparison behind <see cref="HasPageArrangement"/>.
    /// </summary>
    private bool MatchesFileOrder()
    {
        var index = 0;

        foreach (var document in MergeFiles.Documents)
        {
            var path = document.FilePath;

            for (var page = 0; page < document.PageCount; page++)
            {
                if (index >= Pages.Count)
                    return false;

                var candidate = Pages[index++];

                // Cheapest tests first: the comparison runs on every keystroke of a select-all, and
                // only a page that matches in every other respect is worth comparing paths for.
                if (!candidate.IsSelected
                    || candidate.Rotation != 0
                    || candidate.PageIndex != page
                    || !string.Equals(candidate.SourcePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return index == Pages.Count;
    }

    private void SetAllPagesSelected(bool isSelected)
    {
        foreach (var page in Pages)
            page.IsSelected = isSelected;
    }

    private void MoveSelectedPage(int offset)
    {
        if (SelectedPage is null)
            return;

        var from = Pages.IndexOf(SelectedPage);
        var to = from + offset;

        if (to < 0 || to >= Pages.Count)
            return;

        Pages.Move(from, to);
        SelectedPage = Pages[to];
        NotifyPagePositionChanged();
    }

    /// <summary>
    /// Whether a page can move further depends on where it sits, not on which page is selected, and
    /// moving one leaves the selection pointing at the same page throughout. Nothing else would tell
    /// the buttons their answer has changed, so a page dragged to the front keeps offering to move
    /// forward until something else happens to refresh them.
    /// </summary>
    private void NotifyPagePositionChanged()
    {
        MovePageEarlierCommand.NotifyCanExecuteChanged();
        MovePageLaterCommand.NotifyCanExecuteChanged();

        // Moving a page is what turns a plain list into an arrangement, so the merge button has to
        // hear that both its label and its answer may have changed.
        NotifyMergeInputChanged();
    }

    private void CancelThumbnailLoading()
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = null;
    }

    partial void OnIsBusyChanged(bool value)
    {
        MergeFiles.IsLocked = value;
        SplitFiles.IsLocked = value;
        CompressFiles.IsLocked = value;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ActiveList));

        // Leaving the Merge tab mid-selection would strand the page picker over the wrong list.
        if (value != MergeTabIndex && IsChoosingPages)
            BackToFiles();
        else
            StatusMessage = DescribeActiveList();
    }

    private void OnActiveListChanged()
    {
        MergeCommand.NotifyCanExecuteChanged();
        ChoosePagesCommand.NotifyCanExecuteChanged();
        CompressCommand.NotifyCanExecuteChanged();

        // The file list is half of what the merge button counts when no arrangement is held.
        NotifyMergeInputChanged();

        if (!IsBusy && !IsChoosingPages)
            StatusMessage = DescribeActiveList();
    }

    private string DescribeActiveList()
    {
        var list = ActiveList;

        if (!list.HasDocuments)
            return "Drop PDF files here, or use Add files.";

        return SelectedTabIndex switch
        {
            MergeTabIndex => $"{list.Documents.Count} file(s), {list.TotalPageCount} page(s) ready to merge.",
            // The split settings have their own summary beside them; the status bar describes the
            // list, the same as the other tabs do.
            SplitTabIndex => $"{list.Documents.Count} file(s), {list.TotalPageCount} page(s) ready to split.",
            _ => $"{list.Documents.Count} file(s) ready to compress."
        };
    }

    private bool HasSelectedPage() => SelectedPage is not null;

    private bool CanMovePageEarlier() => SelectedPage is not null && Pages.IndexOf(SelectedPage) > 0;

    private bool CanMovePageLater() => SelectedPage is not null && Pages.IndexOf(SelectedPage) < Pages.Count - 1;

    private bool CanChoosePages() => !IsBusy && MergeFiles.HasDocuments;

    private bool CanMerge() => !IsBusy && MergeInputCount > 0;

    private bool CanCompress() => !IsBusy && CompressFiles.HasDocuments;
}
