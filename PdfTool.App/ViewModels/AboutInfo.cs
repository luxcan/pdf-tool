using System.Globalization;
using System.Reflection;

namespace PdfTool.App.ViewModels;

/// <summary>
/// What the About window says about this build. Everything but the repository is read from the
/// assembly, so a build describes itself rather than repeating a version that would have to be kept
/// in step by hand.
/// </summary>
/// <param name="Name">The product name, as a person reads it.</param>
/// <param name="Version">The version, and the commit it was built from when that is known.</param>
/// <param name="BuildDate">The day the build was made, or null when it did not record one.</param>
/// <param name="Description">One line on what the application is for.</param>
/// <param name="RepositoryUrl">Where the source lives.</param>
internal sealed record AboutInfo(
    string Name,
    string Version,
    string? BuildDate,
    string Description,
    string RepositoryUrl)
{
    private const string Repository = "https://github.com/luxcan/pdf-tool";

    /// <summary>The build stamps this in the project file; nothing else records when it was made.</summary>
    private const string BuildDateKey = "BuildDate";

    public static AboutInfo Current { get; } = Read(typeof(AboutInfo).Assembly);

    /// <summary>The one line under the product name, as the About window shows it.</summary>
    public string VersionAndBuild => BuildDate is null ? Version : $"{Version} · Built {BuildDate}";

    /// <summary>
    /// Where releases are listed. The application never fetches this - the About window hands the
    /// address to the shell, which is the whole of its update story.
    /// </summary>
    public string ReleasesUrl => $"{RepositoryUrl}/releases";

    public string IssuesUrl => $"{RepositoryUrl}/issues";

    /// <summary>The address as a person would quote it, without the scheme in front of it.</summary>
    public string RepositoryLabel =>
        new Uri(RepositoryUrl).GetComponents(UriComponents.Host | UriComponents.Path, UriFormat.Unescaped);

    private static AboutInfo Read(Assembly assembly) => new(
        assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "PDF Tool",
        DescribeVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version),
        DescribeBuildDate(assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(metadata => metadata.Key == BuildDateKey)?.Value),
        assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty,
        Repository);

    /// <summary>
    /// The version, carrying the commit it came from when the build recorded one. Builds reach this
    /// application by hand as often as by tag, and "1.0.0" alone does not say which one is installed.
    /// </summary>
    internal static string DescribeVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return assemblyVersion?.ToString(fieldCount: 3) ?? "unknown version";

        // The SDK appends "+<commit sha>". Seven characters identify a commit as well as forty do,
        // and are short enough to read out.
        var parts = informationalVersion.Split('+', 2);

        return parts is [var version, { Length: > 0 } commit]
            ? $"Version {version} ({commit[..Math.Min(7, commit.Length)]})"
            : $"Version {parts[0]}";
    }

    /// <summary>
    /// The stamped build day, written out the way the About window reads it. A build that recorded
    /// nothing, or something that is not a date, says nothing rather than guessing at one.
    /// </summary>
    internal static string? DescribeBuildDate(string? stamped) =>
        DateTime.TryParseExact(
            stamped, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
            : null;
}
