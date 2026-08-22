using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfTool.App.Imaging;
using PdfTool.Core;
using PdfTool.Core.Merging;
using PdfTool.Core.Rendering;

namespace PdfTool.App.ViewModels;

/// <summary>
/// One page in the page-picker grid. The thumbnail is rendered on first request rather than up
/// front, so a several-hundred-page selection only rasterises what the user actually scrolls to.
/// </summary>
internal sealed partial class PageViewModel : ObservableObject
{
    private readonly IPageRenderer _renderer;
    private readonly string? _password;

    // The widest render asked for, and the widest actually on screen. They differ while a render is
    // in flight, and two zoom steps in quick succession can finish out of order.
    private int _requestedWidthPixels;
    private int _renderedWidthPixels;

    public PageViewModel(string sourcePath, int pageIndex, IPageRenderer renderer, string? password = null)
    {
        SourcePath = sourcePath;
        PageIndex = pageIndex;
        SourceFileName = Path.GetFileName(sourcePath);
        _renderer = renderer;
        _password = password;
    }

    public string SourcePath { get; }

    public int PageIndex { get; }

    public string SourceFileName { get; }

    /// <summary>
    /// Whether this page has ever been asked to render. A page mid-render has nothing to show yet
    /// but still needs driving again when the size changes, so "has a thumbnail" is the wrong test.
    /// </summary>
    public bool HasBeenRequested => _requestedWidthPixels > 0;

    /// <summary>Page number as the user sees it in the source document.</summary>
    public int PageNumber => PageIndex + 1;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private int _rotation;

    [ObservableProperty]
    private BitmapSource? _thumbnail;

    [ObservableProperty]
    private bool _isThumbnailLoading;

    [ObservableProperty]
    private string? _thumbnailError;

    public PageRef ToPageRef() => new(SourcePath, PageIndex, Rotation);

    /// <summary>Rotates the preview a quarter turn clockwise; applied to the page on merge.</summary>
    public void RotateClockwise() => Rotation = (Rotation + 90) % 360;

    /// <summary>
    /// Renders the thumbnail at least as wide as asked for. A request no wider than one already met
    /// does nothing, which is what stops a virtualised container re-rendering every time it scrolls
    /// back into view, and what makes zooming back out cost nothing at all.
    /// </summary>
    public async Task EnsureThumbnailAsync(int widthPixels, CancellationToken cancellationToken)
    {
        if (widthPixels <= _requestedWidthPixels)
            return;

        _requestedWidthPixels = widthPixels;
        ThumbnailError = null;

        // Only say so when there is nothing to look at yet: fetching a sharper copy of a page the
        // user is already reading should not blank it out first.
        IsThumbnailLoading = Thumbnail is null;

        try
        {
            var png = await _renderer
                .RenderPageAsync(SourcePath, PageIndex, widthPixels, _password, cancellationToken)
                .ConfigureAwait(true);

            // An earlier, smaller render finishing late must not replace a sharper one.
            if (widthPixels >= _renderedWidthPixels)
            {
                _renderedWidthPixels = widthPixels;
                Thumbnail = PngBitmapFactory.FromPng(png);
            }
        }
        catch (OperationCanceledException)
        {
            // Left the page view; allow another attempt if the user comes back.
            _requestedWidthPixels = 0;
        }
        catch (PdfToolException ex)
        {
            // Let the next request through: a render that failed at one size may well succeed at
            // another, and leaving the mark set would make every later attempt a no-op.
            _requestedWidthPixels = _renderedWidthPixels;

            // The message shares its cell with the preview. Reporting a failed sharper render would
            // paint it over a picture that is still perfectly good, so say nothing and keep the
            // preview; only a page with nothing to show reports the failure.
            if (Thumbnail is null)
                ThumbnailError = ex.Message;
        }
        finally
        {
            IsThumbnailLoading = false;
        }
    }
}
