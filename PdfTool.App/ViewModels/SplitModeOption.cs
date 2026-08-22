namespace PdfTool.App.ViewModels;

/// <summary>
/// A way of splitting, worded for the person choosing it. The name is short enough to sit in a
/// segment; the description carries what it actually does.
/// </summary>
internal sealed record SplitModeOption(SplitMode Mode, string Name, string Description)
{
    public static IReadOnlyList<SplitModeOption> All { get; } =
    [
        new(SplitMode.EveryNPages, "Every N pages",
            "Cuts the document into consecutive parts. Leave it at 1 for a file per page."),
        new(SplitMode.Ranges, "Page ranges",
            "Writes one file per range you list, in the order you list them.")
    ];

    /// <summary>What a screen reader announces for the segment, in place of the record's own dump.</summary>
    public override string ToString() => Name;
}
