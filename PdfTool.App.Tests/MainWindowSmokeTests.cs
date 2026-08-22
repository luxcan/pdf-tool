using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PdfTool.App.Behaviors;
using PdfTool.App.Controls;
using PdfTool.App.ViewModels;
using PdfTool.Core.Compression;
using PdfTool.Core.Documents;
using PdfTool.Core.Merging;
using PdfTool.Core.Splitting;

namespace PdfTool.App.Tests;

/// <summary>
/// Loads and lays out the real window against the real theme. XAML faults - a colour bound where a
/// brush is required, a missing resource key, a broken control template - compile cleanly and only
/// fail when the layout pass runs, which is exactly what these tests force.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class MainWindowSmokeTests(WpfContext wpf)
{
    private const int MergeTab = 0;
    private const int SplitTab = 1;
    private const int CompressTab = 2;

    [Fact]
    public void MergeTab_LoadsAndLaysOut()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                Assert.True(window.IsLoaded);
                Assert.Equal(MergeTab, viewModel.SelectedTabIndex);
                Assert.Same(viewModel.MergeFiles, viewModel.ActiveList);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CompressTab_LoadsAndLaysOut()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                viewModel.SelectedTabIndex = CompressTab;
                ShowAndLayOut(window);

                Assert.Same(viewModel.CompressFiles, viewModel.ActiveList);
                Assert.Equal(CompressionPreset.Balanced, viewModel.SelectedCompressionPreset.Preset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SplitTab_LoadsAndLaysOut()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                viewModel.SelectedTabIndex = SplitTab;
                ShowAndLayOut(window);

                Assert.Same(viewModel.SplitFiles, viewModel.ActiveList);
                Assert.Equal(SplitMode.EveryNPages, viewModel.SelectedSplitMode.Mode);
                Assert.False(viewModel.IsSplittingByRanges);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// The button, the summary line and the split itself are all driven from one plan, so the
    /// summary is the honest way to check what would happen without writing any files.
    /// </summary>
    [Fact]
    public void SplitTab_ReportsWhatTheSettingsWouldProduce()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);
                viewModel.SelectedTabIndex = SplitTab;

                Assert.False(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("Add a PDF", viewModel.SplitSummary);

                viewModel.SplitFiles.Documents.Add(Document("report.pdf", pageCount: 10));
                ShowAndLayOut(window);

                // One page per file is the default.
                Assert.True(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("10 file", viewModel.SplitSummary);

                viewModel.PagesPerFileText = "3";
                Assert.Contains("4 file", viewModel.SplitSummary);
                Assert.Contains("all 10 page", viewModel.SplitSummary);

                viewModel.PagesPerFileText = "0";
                Assert.False(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("at least 1", viewModel.SplitSummary);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SplitTab_ByRanges_ReportsBadInputAndKeepsTheButtonDown()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);
                viewModel.SelectedTabIndex = SplitTab;
                viewModel.SplitFiles.Documents.Add(Document("report.pdf", pageCount: 10));

                viewModel.SelectedSplitMode = viewModel.SplitModes.Single(m => m.Mode == SplitMode.Ranges);
                ShowAndLayOut(window);

                Assert.True(viewModel.IsSplittingByRanges);

                // An empty box is not yet an error the user made, but it is not runnable either.
                Assert.False(viewModel.SplitCommand.CanExecute(null));

                viewModel.PageRangesText = "1-3, 5, 99";
                Assert.False(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("only 10 page", viewModel.SplitSummary);

                viewModel.PageRangesText = "1-3, 5";
                Assert.True(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("2 file", viewModel.SplitSummary);

                // Ranges need not cover the document, and the summary should not pretend otherwise.
                Assert.Contains("4 of 10 page", viewModel.SplitSummary);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SplitTab_MoreThanOneFile_SaysWhyItCannotRun()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);
                viewModel.SelectedTabIndex = SplitTab;

                viewModel.SplitFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                Assert.True(viewModel.SplitCommand.CanExecute(null));

                viewModel.SplitFiles.Documents.Add(Document("b.pdf", pageCount: 4));
                ShowAndLayOut(window);

                Assert.False(viewModel.SplitCommand.CanExecute(null));
                Assert.Contains("one file at a time", viewModel.SplitSummary);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// The whole way through: a real PDF, a real split, real files on disk. It also pins the report,
    /// because releasing the file lists at the end raises their change events, and those describe
    /// the list - which used to replace the outcome before anyone could read it.
    /// </summary>
    [Fact]
    public Task SplitTab_WritesTheFilesAndSaysWhereTheyWent() =>
        InScratchFolderAsync(async temp =>
        {
            var source = Path.Combine(temp, "report.pdf");
            WriteSamplePdf(source, pageCount: 5);
            var output = Path.Combine(temp, "parts");

            await wpf.InvokeAsync(async () =>
            {
                var (window, viewModel) = CreateWindow();
                StubsOf(window).Dialogs.FolderToReturn = output;

                try
                {
                    viewModel.SelectedTabIndex = SplitTab;
                    ShowAndLayOut(window);

                    await viewModel.SplitFiles.AddFilesAsync([source]);
                    viewModel.PagesPerFileText = "2";

                    Assert.True(viewModel.SplitCommand.CanExecute(null));

                    await viewModel.SplitCommand.ExecuteAsync(null);

                    Assert.False(viewModel.IsBusy);
                    Assert.Contains("Split into 3 file(s)", viewModel.StatusMessage);
                    Assert.Contains(output, viewModel.StatusMessage);

                    // Compression was not asked for, so the report should not claim any.
                    Assert.DoesNotContain("compress", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

                    Assert.Equal(
                        ["report-p1-2.pdf", "report-p3-4.pdf", "report-p5.pdf"],
                        Directory.GetFiles(output).Select(Path.GetFileName).Order());
                }
                finally
                {
                    window.Close();
                }
            });
        });

    /// <summary>
    /// Splitting runs every part through the same compressor the Compress tab uses. The parts have
    /// to survive being rewritten in place, and the report has to account for what it did to them.
    /// </summary>
    [Fact]
    public Task SplitTab_WithCompressionOn_CompressesEveryPartItWrote() =>
        InScratchFolderAsync(async temp =>
        {
            var source = Path.Combine(temp, "report.pdf");
            WriteSamplePdf(source, pageCount: 5);
            var output = Path.Combine(temp, "parts");

            await wpf.InvokeAsync(async () =>
            {
                var (window, viewModel) = CreateWindow();
                StubsOf(window).Dialogs.FolderToReturn = output;

                try
                {
                    viewModel.SelectedTabIndex = SplitTab;
                    ShowAndLayOut(window);

                    await viewModel.SplitFiles.AddFilesAsync([source]);
                    viewModel.PagesPerFileText = "2";
                    viewModel.CompressSplitParts = true;

                    await viewModel.SplitCommand.ExecuteAsync(null);

                    Assert.Empty(StubsOf(window).Dialogs.Errors);
                    Assert.Contains("Split into 3 file(s)", viewModel.StatusMessage);

                    // Whether these parts had anything left to give is the compressor's business;
                    // that it ran over them and reported an outcome is this tab's.
                    Assert.True(
                        viewModel.StatusMessage.Contains("compressed from")
                        || viewModel.StatusMessage.Contains("already at its smallest"),
                        $"The split said nothing about compression: {viewModel.StatusMessage}");

                    // Every part is still where it was and still reads as the pages it was given.
                    var inspector = new PdfInspector();

                    Assert.Equal(
                        [2, 2, 1],
                        Directory.GetFiles(output).Order().Select(part => inspector.Inspect(part).PageCount));

                    // "Show in folder" opens on the first part, not on whichever was compressed last.
                    Assert.Equal(Path.Combine(output, "report-p1-2.pdf"), viewModel.LastOutputPath);
                }
                finally
                {
                    window.Close();
                }
            });
        });

    /// <summary>
    /// Segments are labelled from one template on the selector's own style, so that the control the
    /// compress option lives in can reach it. Losing it would not show: every option names itself,
    /// so the segments would still read correctly and only the explanation would be gone.
    /// </summary>
    [Fact]
    public void SegmentedSelectors_CarryEachOptionsExplanationOnHover()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                // Both selectors on this tab are segmented: the split modes and the compress presets.
                viewModel.SelectedTabIndex = SplitTab;
                ShowAndLayOut(window);

                var segments = FindVisualChildren<ListBoxItem>(window)
                    .SelectMany(FindVisualChildren<TextBlock>)
                    .ToList();

                Assert.NotEmpty(segments);
                Assert.All(segments, segment => Assert.NotNull(segment.ToolTip));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Both tabs offer the option through one control, so the hazard is not how it looks but which
    /// setting each instance is tied to - a copy that quietly drove the other tab's flag would leave
    /// a check box that ticks and does nothing.
    /// </summary>
    [Fact]
    public void CompressOption_IsOfferedOnBothTabs_EachBoundToItsOwnSetting()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                // A TabControl only realises the selected tab, so each instance is reached in turn.
                SingleCompressOption(window).IsChecked = true;

                Assert.True(viewModel.CompressMergedOutput);
                Assert.False(viewModel.CompressSplitParts);

                viewModel.SelectedTabIndex = SplitTab;
                ShowAndLayOut(window);

                var splitOption = SingleCompressOption(window);
                Assert.False(splitOption.IsChecked);

                splitOption.IsChecked = true;
                Assert.True(viewModel.CompressSplitParts);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_LaysOutAndRealisesThumbnails()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();
            var renderer = StubsOf(window).Renderer;

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 12));
                viewModel.MergeFiles.Documents.Add(Document("b.pdf", pageCount: 5));

                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                Assert.Equal(17, viewModel.Pages.Count);
                Assert.Equal(17, viewModel.SelectedPageCount);

                // Every visible tile should have asked for a thumbnail - not just the first one -
                // and virtualisation should have spared the rest.
                Assert.InRange(renderer.RenderCount, 2, viewModel.Pages.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_DeselectingPages_UpdatesTheMergeCount()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 6));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                viewModel.Pages[0].IsSelected = false;
                viewModel.Pages[3].IsSelected = false;
                Assert.Equal(4, viewModel.SelectedPageCount);

                viewModel.SelectNoPagesCommand.Execute(null);
                Assert.Equal(0, viewModel.SelectedPageCount);
                Assert.False(viewModel.MergeCommand.CanExecute(null));

                viewModel.InvertPageSelectionCommand.Execute(null);
                Assert.Equal(6, viewModel.SelectedPageCount);
                Assert.True(viewModel.MergeCommand.CanExecute(null));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_ZoomingIn_GrowsTheTilesAndRendersThemSharper()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();
            var renderer = StubsOf(window).Renderer;

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 6));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                var small = viewModel.SelectedPageZoom;
                var smallTile = SingleTileWidth(window);
                var smallestRender = renderer.WidestRenderRequested;

                Assert.Equal(small.TileWidth, smallTile);

                // Zooming has to re-rasterise: scaling a 150px render up to a 440px tile would only
                // give the user a bigger blur, which is the opposite of what they asked for.
                viewModel.SelectedPageZoom = viewModel.PageZoomLevels[^1];
                ShowAndLayOut(window);

                Assert.True(
                    SingleTileWidth(window) > smallTile,
                    $"Tile stayed at {SingleTileWidth(window)}px after zooming in.");

                Assert.True(
                    renderer.WidestRenderRequested > smallestRender,
                    $"Widest render stayed at {renderer.WidestRenderRequested}px after zooming in.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_ZoomingBackOut_CostsNoFurtherRendering()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();
            var renderer = StubsOf(window).Renderer;

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 6));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                viewModel.SelectedPageZoom = viewModel.PageZoomLevels[^1];
                ShowAndLayOut(window);

                var rendersAfterZoomingIn = renderer.RenderCount;

                viewModel.SelectedPageZoom = viewModel.PageZoomLevels[0];
                ShowAndLayOut(window);

                // The sharp bitmaps already in hand scale down perfectly well.
                Assert.Equal(rendersAfterZoomingIn, renderer.RenderCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// WPF hands any TextBox without a menu of its own an internal editing menu built from types
    /// derived from ContextMenu and MenuItem. An implicit style is keyed on the exact type, so it
    /// never reaches those, and the menu arrives in light chrome inside the dark window. The theme
    /// supplies a real menu instead; this is the assertion that it actually arrives.
    /// </summary>
    [Fact]
    public void SplitTab_ThePageCountBox_CarriesAMenuTheThemeCanReach()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.SelectedTabIndex = SplitTab;
                ShowAndLayOut(window);

                var box = FindVisualChildren<TextBox>(window).First();

                Assert.NotNull(box.ContextMenu);
                Assert.Equal(3, box.ContextMenu!.Items.Count);

                // Open it for real: a menu that exists but measures to nothing would still satisfy
                // the assertions above, and the point of the change is what appears on screen.
                box.ContextMenu.PlacementTarget = box;
                box.ContextMenu.IsOpen = true;

                try
                {
                    ShowAndLayOut(window);

                    var items = box.ContextMenu.Items.OfType<MenuItem>().ToList();
                    Assert.Equal(3, items.Count);

                    // 30 DIP is this theme's row; WPF's own menu row is 22. The height is therefore
                    // the evidence that our template is the one being used.
                    Assert.All(items, item => Assert.Equal(30d, item.ActualHeight));
                    Assert.All(items, item => Assert.True(item.ActualWidth > 0, "menu item measured to zero width"));

                    Assert.Same(
                        Application.Current.TryFindResource("SurfaceAltBackground"),
                        box.ContextMenu.Background);
                }
                finally
                {
                    box.ContextMenu.IsOpen = false;
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_SteppingTheZoom_GrowsTheTilesAndStopsAtEitherEnd()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 6));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                var smallestTile = SingleTileWidth(window);

                viewModel.StepPageZoomCommand.Execute(1);
                ShowAndLayOut(window);

                Assert.True(
                    SingleTileWidth(window) > smallestTile,
                    $"Tile stayed at {SingleTileWidth(window)}px after a step in.");

                // Wound past either end it settles there. Wrapping would take a user who is holding
                // the wheel down from the largest tiles straight back to the smallest.
                for (var step = 0; step < viewModel.PageZoomLevels.Count + 2; step++)
                    viewModel.StepPageZoomCommand.Execute(1);

                ShowAndLayOut(window);
                Assert.Equal(viewModel.PageZoomLevels[^1], viewModel.SelectedPageZoom);

                for (var step = 0; step < viewModel.PageZoomLevels.Count + 2; step++)
                    viewModel.StepPageZoomCommand.Execute(-1);

                ShowAndLayOut(window);
                Assert.Equal(viewModel.PageZoomLevels[0], viewModel.SelectedPageZoom);
                Assert.Equal(smallestTile, SingleTileWidth(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_TheWheelWithoutCtrl_LeavesTheTileSizeAlone()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 6));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                var before = viewModel.SelectedPageZoom;

                PageList(window).RaiseEvent(
                    new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta: 120)
                    {
                        RoutedEvent = UIElement.PreviewMouseWheelEvent
                    });

                ShowAndLayOut(window);

                // A bare wheel is how a document of several hundred pages is read; zooming on it
                // would leave the user no way to scroll.
                Assert.Equal(before, viewModel.SelectedPageZoom);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PagePicker_DraggingAPageOntoAnother_ReordersAndKeepsItSelected()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                var moved = viewModel.Pages[0];
                var displaced = viewModel.Pages[2];

                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 2));

                Assert.Same(moved, viewModel.Pages[2]);
                Assert.Same(displaced, viewModel.Pages[1]);
                Assert.Same(moved, viewModel.SelectedPage);

                // Dragging back the other way has to work just as well.
                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(2, 0));
                Assert.Same(moved, viewModel.Pages[0]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Moving a page leaves the same page selected, so nothing about the selection changes to tell
    /// the nudge buttons that their answer has. Both routes to a move have to say so themselves.
    /// </summary>
    [Fact]
    public void PagePicker_MovingAPage_LeavesTheNudgeButtonsAnsweringForItsNewPosition()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);

                // The first page cannot go any further forward.
                viewModel.SelectedPage = viewModel.Pages[0];
                Assert.False(viewModel.MovePageEarlierCommand.CanExecute(null));
                Assert.True(viewModel.MovePageLaterCommand.CanExecute(null));

                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 3));

                // Now last, it can go back but not on.
                Assert.True(viewModel.MovePageEarlierCommand.CanExecute(null));
                Assert.False(viewModel.MovePageLaterCommand.CanExecute(null));

                viewModel.MovePageEarlierCommand.Execute(null);
                Assert.True(viewModel.MovePageLaterCommand.CanExecute(null));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 9)]
    [InlineData(1, 1)]
    public void PagePicker_AnImpossibleDrag_LeavesTheOrderAlone(int from, int to)
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                var before = viewModel.Pages.ToList();

                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(from, to));

                Assert.Equal(before, viewModel.Pages);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Width of a page tile as laid out, read from the template's own border rather than the view
    /// model, so a binding that never reaches the tile is caught.
    /// </summary>
    private static double SingleTileWidth(DependencyObject window) =>
        FindVisualChildren<ListBoxItem>(window)
            .SelectMany(FindVisualChildren<Border>)
            .First(border => border.Width > 0)
            .Width;

    /// <summary>
    /// The page picker's own list, told apart from the segmented selectors by the behaviour only it
    /// carries.
    /// </summary>
    private static ListBox PageList(DependencyObject window) =>
        FindVisualChildren<ListBox>(window).Single(list => WheelZoom.GetCommand(list) is not null);

    [Fact]
    public void SwitchingAwayFromMerge_ClosesThePagePicker()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 3));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                Assert.True(viewModel.IsChoosingPages);

                viewModel.SelectedTabIndex = CompressTab;
                ShowAndLayOut(window);

                Assert.False(viewModel.IsChoosingPages);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TheTwoTabs_KeepSeparateFileLists()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                viewModel.MergeFiles.Documents.Add(Document("merge-me.pdf", pageCount: 4));

                Assert.True(viewModel.MergeCommand.CanExecute(null));
                Assert.False(viewModel.CompressCommand.CanExecute(null));
                Assert.Empty(viewModel.CompressFiles.Documents);

                viewModel.CompressFiles.Documents.Add(Document("squash-me.pdf", pageCount: 9));

                Assert.True(viewModel.CompressCommand.CanExecute(null));
                Assert.Single(viewModel.MergeFiles.Documents);

                viewModel.CompressFiles.ClearCommand.Execute(null);

                Assert.Empty(viewModel.CompressFiles.Documents);
                Assert.Single(viewModel.MergeFiles.Documents);
                Assert.True(viewModel.MergeCommand.CanExecute(null));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ReorderingIsOfferedForMergeOnly()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 2));
                viewModel.MergeFiles.Documents.Add(Document("b.pdf", pageCount: 2));
                ShowAndLayOut(window);

                // A TabControl only realises the selected tab, so each list is checked in turn.
                Assert.True(SingleDocumentList(window).ShowReorderButtons);

                viewModel.SelectedTabIndex = CompressTab;
                ShowAndLayOut(window);
                Assert.False(SingleDocumentList(window).ShowReorderButtons);

                viewModel.SelectedTabIndex = MergeTab;
                ShowAndLayOut(window);

                viewModel.MergeFiles.SelectedDocument = viewModel.MergeFiles.Documents[1];
                Assert.True(viewModel.MergeFiles.MoveUpCommand.CanExecute(null));
                Assert.False(viewModel.MergeFiles.MoveDownCommand.CanExecute(null));

                viewModel.MergeFiles.MoveUpCommand.Execute(null);
                Assert.Equal("b.pdf", viewModel.MergeFiles.Documents[0].FileName);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ThePrimaryAction_IsWiredToTheWindowsCommand()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.SelectedTabIndex = CompressTab;
                ShowAndLayOut(window);

                // The button is hosted inside the file list, whose data context is the file list
                // itself. A binding that fails to reach back out to the window leaves a button that
                // looks clickable and does nothing, which nothing else here would catch.
                var compress = SingleButton(window, "Compress");

                Assert.Same(viewModel.CompressCommand, compress.Command);
                Assert.False(compress.IsEnabled);

                viewModel.CompressFiles.Documents.Add(Document("squash-me.pdf", pageCount: 2));
                ShowAndLayOut(window);

                Assert.True(compress.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// About is the one action on that strip that always applies, so unlike its neighbours it has to
    /// be there before anything has happened.
    /// </summary>
    [Fact]
    public void About_IsAlwaysOfferedOnTheStatusBar()
    {
        wpf.Invoke(() =>
        {
            var (window, _) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                var about = SingleButton(window, "About");

                Assert.Equal(Visibility.Visible, about.Visibility);
                Assert.True(about.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void StatusBarActions_AppearOnlyWhenTheyApply()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                ShowAndLayOut(window);

                var showInFolder = SingleButton(window, "Show in folder");
                var cancel = SingleButton(window, "Cancel");

                Assert.Equal(Visibility.Collapsed, showInFolder.Visibility);
                Assert.Equal(Visibility.Collapsed, cancel.Visibility);

                viewModel.LastOutputPath = @"C:\output\merged.pdf";
                viewModel.IsBusy = true;
                ShowAndLayOut(window);

                Assert.Equal(Visibility.Visible, showInFolder.Visibility);
                Assert.Equal(Visibility.Visible, cancel.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Controls.DocumentListView SingleDocumentList(DependencyObject window) =>
        FindVisualChildren<Controls.DocumentListView>(window).Single();

    private static Controls.CompressOutputOption SingleCompressOption(DependencyObject window) =>
        FindVisualChildren<Controls.CompressOutputOption>(window).Single();

    /// <summary>Runs a test that writes real files against a folder that cleans itself up.</summary>
    private static async Task InScratchFolderAsync(Func<string, Task> test)
    {
        var temp = Path.Combine(Path.GetTempPath(), "PdfTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            await test(temp);
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (IOException)
            {
                // A locked file must not fail an otherwise passing test.
            }
        }
    }

    // ===================== The page arrangement surviving the picker =====================

    /// <summary>
    /// The reported fault, in full: pages dragged into an order, the picker closed, and the merge
    /// started from the file list. The arrangement is what the user is looking at, so it is what the
    /// merge has to write - the file order they deliberately moved away from is not an answer.
    /// </summary>
    [Fact]
    public void PagesReorderedInThePicker_SurviveGoingBackToTheFileList()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 3));
                viewModel.MergeFiles.Documents.Add(Document("b.pdf", pageCount: 2));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                Assert.False(viewModel.HasPageArrangement);

                // a.pdf's first page to the back of the queue.
                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 4));

                Assert.True(viewModel.HasPageArrangement);

                viewModel.BackToFilesCommand.Execute(null);
                ShowAndLayOut(window);

                var merged = viewModel.BuildMergeInput();

                Assert.Equal(5, merged.Count);
                Assert.Equal(new PageRef("b.pdf", 1), merged[3]);
                Assert.Equal(new PageRef("a.pdf", 0), merged[4]);
                Assert.Equal("Merge 5 chosen page(s)", viewModel.MergeButtonLabel);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Rotation and deselection are choices the file list cannot express either, and were being lost
    /// by the same route as the ordering.
    /// </summary>
    [Fact]
    public void RotationAndDeselection_SurviveGoingBackToTheFileList()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                viewModel.SelectedPage = viewModel.Pages[1];
                viewModel.RotatePageCommand.Execute(null);
                viewModel.Pages[3].IsSelected = false;

                viewModel.BackToFilesCommand.Execute(null);

                var merged = viewModel.BuildMergeInput();

                Assert.Equal(3, merged.Count);
                Assert.Equal(90, merged[1].Rotation);
                Assert.DoesNotContain(merged, page => page.PageIndex == 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Opening the picker is not itself a choice. A user who looks at the previews and backs out has
    /// arranged nothing, and must not be told they have - nor be offered a merge of "chosen" pages.
    /// </summary>
    [Fact]
    public void OpeningThePickerAndChangingNothing_LeavesNoArrangementBehind()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 3));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);
                viewModel.BackToFilesCommand.Execute(null);

                Assert.False(viewModel.HasPageArrangement);
                Assert.Equal("Merge all pages", viewModel.MergeButtonLabel);
                Assert.Equal(3, viewModel.MergeInputCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Coming back to the picker shows the arrangement, rather than building over it.</summary>
    [Fact]
    public void ReopeningThePicker_KeepsTheArrangementItWasLeftIn()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 4));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);
                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 3));
                viewModel.BackToFilesCommand.Execute(null);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                Assert.True(viewModel.HasPageArrangement);

                // The first page was dragged to the back, and is still there.
                Assert.Equal(0, viewModel.Pages[3].PageIndex);
                Assert.Equal(1, viewModel.Pages[0].PageIndex);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Reordering the files is what the README teaches as the way to order a merge, and it must not
    /// cost the user a page arrangement. The arrangement already says where every page goes, so the
    /// list order it was built from has nothing left to say about it.
    /// </summary>
    [Fact]
    public void MovingAFileInTheList_DoesNotDisturbThePageArrangement()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 2));
                viewModel.MergeFiles.Documents.Add(Document("b.pdf", pageCount: 2));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);
                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 3));
                viewModel.BackToFilesCommand.Execute(null);

                viewModel.MergeFiles.SelectedDocument = viewModel.MergeFiles.Documents[1];
                viewModel.MergeFiles.MoveUpCommand.Execute(null);

                Assert.True(viewModel.HasPageArrangement);
                Assert.Equal(4, viewModel.BuildMergeInput().Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Adding or removing a file does disturb it - there would be pages in the merge the picker
    /// never showed. The choices go, and the user is told rather than left to notice.
    /// </summary>
    [Fact]
    public void ChangingWhichFilesAreListed_ClearsThePageChoicesAndSaysSo()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 2));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);
                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 1));
                viewModel.BackToFilesCommand.Execute(null);

                Assert.True(viewModel.HasPageArrangement);

                viewModel.MergeFiles.Documents.Add(Document("b.pdf", pageCount: 3));

                Assert.False(viewModel.HasPageArrangement);
                Assert.Contains("page choices", viewModel.StatusMessage);

                // And the merge now covers both files, in file order.
                Assert.Equal(5, viewModel.BuildMergeInput().Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Reset is the only way back to file order once the picker has been used.</summary>
    [Fact]
    public void ResetPages_PutsThePagesBackInFileOrder()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 3));
                ShowAndLayOut(window);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                Assert.False(viewModel.ResetPagesCommand.CanExecute(null));

                viewModel.ReorderPagesCommand.Execute(new ReorderRequest(0, 2));
                viewModel.Pages[0].IsSelected = false;

                Assert.True(viewModel.ResetPagesCommand.CanExecute(null));

                viewModel.ResetPagesCommand.Execute(null);

                Assert.False(viewModel.HasPageArrangement);
                Assert.Equal(3, viewModel.Pages.Count);
                Assert.Equal(0, viewModel.Pages[0].PageIndex);
                Assert.All(viewModel.Pages, page => Assert.True(page.IsSelected));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// The compress option is what sent the user back to the file list in the first place. It is on
    /// the page picker too now, so arranging pages and compressing the result is one visit.
    /// </summary>
    [Fact]
    public void CompressOption_IsOfferedOnThePagePickerAsWellAsTheFileList()
    {
        wpf.Invoke(() =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.Documents.Add(Document("a.pdf", pageCount: 2));
                ShowAndLayOut(window);

                var compress = FindVisualChildren<CompressOutputOption>(window)
                    .Single(option => option.Label == "Compress merged output");

                Assert.Equal(Visibility.Visible, compress.Visibility);

                viewModel.ChoosePagesCommand.Execute(null);
                ShowAndLayOut(window);

                Assert.Equal(Visibility.Visible, compress.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// A file dropped on the window reaches the list directly rather than through the command, so
    /// the lock an operation puts on the list has to be honoured there too - otherwise a merge in
    /// progress can have its own source list changed underneath it.
    /// </summary>
    [Fact]
    public Task AddingFiles_IsRefusedWhileTheListIsLocked() =>
        wpf.InvokeAsync(async () =>
        {
            var (window, viewModel) = CreateWindow();

            try
            {
                viewModel.MergeFiles.IsLocked = true;

                await viewModel.MergeFiles.AddFilesAsync(["nowhere.pdf"]);

                Assert.Empty(viewModel.MergeFiles.Documents);
            }
            finally
            {
                window.Close();
            }
        });

    private static Button SingleButton(DependencyObject window, string content) =>
        FindVisualChildren<Button>(window).Single(button => button.Content as string == content);

    private static DocumentViewModel Document(string name, int pageCount) =>
        new(new PdfDocumentInfo(name, pageCount, 2048));

    /// <summary>The stubs behind a window, parked on its Tag so a test can steer them.</summary>
    private sealed record Stubs(StubPageRenderer Renderer, StubFileDialogService Dialogs);

    private static Stubs StubsOf(Window window) => (Stubs)window.Tag!;

    /// <summary>A real, if empty, PDF, for the tests that go all the way to disk.</summary>
    private static void WriteSamplePdf(string path, int pageCount)
    {
        using var document = new PdfSharp.Pdf.PdfDocument();

        for (var i = 0; i < pageCount; i++)
            document.AddPage();

        document.Save(path);
    }

    private static (MainWindow Window, MainViewModel ViewModel) CreateWindow()
    {
        var renderer = new StubPageRenderer();
        var dialogs = new StubFileDialogService();

        var viewModel = new MainViewModel(
            new PdfInspector(),
            new PdfMerger(),
            new PdfSplitter(),
            new PdfCompressor(),
            renderer,
            dialogs);

        var window = new MainWindow
        {
            DataContext = viewModel,
            Tag = new Stubs(renderer, dialogs),
            Width = 1080,
            Height = 740,

            // Keeps the test from stealing focus or flashing on screen.
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000
        };

        return (window, viewModel);
    }

    /// <summary>
    /// A real Show plus a drained dispatcher queue: container generation for a virtualising panel
    /// only happens once the window has a size and the layout pass has actually run.
    /// </summary>
    private static void ShowAndLayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
