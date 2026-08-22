using PdfTool.Core.Compression;

namespace PdfTool.App.ViewModels;

/// <summary>
/// A compression preset with wording aimed at the person choosing it rather than at the encoder.
/// The name is short enough to sit in a segment; the description carries the trade-off.
/// </summary>
internal sealed record CompressionPresetOption(CompressionPreset Preset, string Name, string Description)
{
    public static IReadOnlyList<CompressionPresetOption> All { get; } =
    [
        new(CompressionPreset.Lossless, "Lossless",
            "Rewrites the file structure and leaves every image untouched. Nothing degrades, but savings are modest."),
        new(CompressionPreset.HighQuality, "High quality",
            "Downsamples only very large images. Safe for printing."),
        new(CompressionPreset.Balanced, "Balanced",
            "Good default for reading on screen and emailing."),
        new(CompressionPreset.Smallest, "Smallest",
            "Largest savings. Photographs and scans will look visibly softer.")
    ];

    /// <summary>What a screen reader announces for the segment, in place of the record's own dump.</summary>
    public override string ToString() => Name;
}
