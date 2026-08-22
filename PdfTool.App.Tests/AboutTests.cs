using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using PdfTool.App.ViewModels;

namespace PdfTool.App.Tests;

/// <summary>
/// The About window is the only place this application says which build it is, and a build reaches
/// it by hand as often as by tag - so the version it reports has to be the one that is running.
/// </summary>
[Collection(WpfCollection.Name)]
public sealed class AboutTests(WpfContext wpf)
{
    [Theory]
    // What the SDK stamps on a build from a git checkout.
    [InlineData("1.0.0+c54f44bcd43811515c17f16a6961a9cc48015f51", "Version 1.0.0 (c54f44b)")]
    [InlineData("1.2.3-beta+abcdef0123", "Version 1.2.3-beta (abcdef0)")]
    // A commit shorter than the seven characters asked for must not be cut short of itself.
    [InlineData("1.0.0+abc", "Version 1.0.0 (abc)")]
    // Nothing after the plus is no commit at all, rather than an empty pair of brackets.
    [InlineData("1.0.0+", "Version 1.0.0")]
    [InlineData("1.0.0", "Version 1.0.0")]
    public void DescribeVersion_CarriesTheCommitWhenTheBuildRecordedOne(string informational, string expected) =>
        Assert.Equal(expected, AboutInfo.DescribeVersion(informational, new Version(9, 9, 9)));

    /// <summary>A build with no informational version still has to say something truthful.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescribeVersion_WithoutAnInformationalVersion_FallsBackToTheAssemblys(string? informational) =>
        Assert.Equal("4.5.6", AboutInfo.DescribeVersion(informational, new Version(4, 5, 6, 7)));

    [Fact]
    public void DescribeVersion_WithNothingToGoOn_SaysSoRatherThanShowingNothing() =>
        Assert.Equal("unknown version", AboutInfo.DescribeVersion(null, null));

    /// <summary>The build stamps a day; anything else it might stamp is not one.</summary>
    [Theory]
    [InlineData("2026-08-12", "12 August 2026")]
    [InlineData("2026-01-01", "1 January 2026")]
    public void DescribeBuildDate_WritesTheStampedDayOut(string stamped, string expected) =>
        Assert.Equal(expected, AboutInfo.DescribeBuildDate(stamped));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12 August 2026")]
    [InlineData("not a date")]
    public void DescribeBuildDate_WithoutADay_SaysNothingRatherThanGuessing(string? stamped) =>
        Assert.Null(AboutInfo.DescribeBuildDate(stamped));

    [Fact]
    public void Current_ReadsThisBuild()
    {
        var about = AboutInfo.Current;

        Assert.Equal("PDF Tool", about.Name);
        Assert.StartsWith("Version ", about.Version);
        Assert.NotEmpty(about.Description);
        Assert.Equal("https://github.com/luxcan/pdf-tool", about.RepositoryUrl);
        Assert.Equal("https://github.com/luxcan/pdf-tool/releases", about.ReleasesUrl);
        Assert.Equal("https://github.com/luxcan/pdf-tool/issues", about.IssuesUrl);
        Assert.Equal("github.com/luxcan/pdf-tool", about.RepositoryLabel);
    }

    /// <summary>The build this project produces stamps a date, so the window has one to show.</summary>
    [Fact]
    public void Current_CarriesTheDayThisBuildWasMade()
    {
        var about = AboutInfo.Current;

        Assert.NotNull(about.BuildDate);
        Assert.Equal($"{about.Version} · Built {about.BuildDate}", about.VersionAndBuild);
    }

    [Fact]
    public void VersionAndBuild_WithoutABuildDate_IsJustTheVersion() =>
        Assert.Equal(
            "Version 1.0.0",
            new AboutInfo("PDF Tool", "Version 1.0.0", null, "", "https://example.com/x").VersionAndBuild);

    /// <summary>
    /// Lays the real window out against the real theme. A window resolves its resources when it
    /// loads, so a key that is only in the main window's scope fails here and nowhere else.
    /// </summary>
    [Fact]
    public void AboutWindow_LaysOutAndShowsTheVersionAndWhereANewerBuildWouldBe()
    {
        wpf.Invoke(() =>
        {
            var window = new AboutWindow
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);

                var about = AboutInfo.Current;
                var shown = Texts(window);

                Assert.Contains("PDF Tool", shown);
                Assert.Contains(about.VersionAndBuild, shown);
                Assert.Contains(about.Description, shown);
                Assert.Contains(about.RepositoryLabel, shown);

                // Every address the window offers has to be a link, not written out as plain text.
                var links = FindVisualChildren<TextBlock>(window)
                    .SelectMany(text => text.Inlines.OfType<Hyperlink>())
                    .Select(link => link.NavigateUri?.AbsoluteUri)
                    .ToList();

                Assert.Equal(3, links.Count);
                Assert.Contains(about.ReleasesUrl, links);
                Assert.Contains(about.IssuesUrl, links);
                Assert.Contains(about.RepositoryUrl, links);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static List<string> Texts(DependencyObject window) =>
        [.. FindVisualChildren<TextBlock>(window).Select(text => text.Text).Where(text => text.Length > 0)];

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
