namespace PdfTool.Core.Compression;

/// <summary>
/// What the compressor is allowed to do. Structural rewriting always happens; the image settings
/// decide whether pictures are also downsampled and re-encoded, which is where the real savings are
/// and the only part that loses quality.
/// </summary>
public sealed record CompressionSettings
{
    /// <summary>Whether images may be downsampled and re-encoded as JPEG.</summary>
    public required bool RecompressImages { get; init; }

    /// <summary>Longest edge an image may keep, in pixels. Larger images are scaled down to fit.</summary>
    public required int MaxImageEdgePixels { get; init; }

    /// <summary>JPEG quality applied when re-encoding, 1 to 100.</summary>
    public required int JpegQuality { get; init; }

    /// <summary>
    /// Whether an image whose pixels are all grey may be stored as greyscale rather than colour.
    /// Scanners produce these constantly and the saving costs nothing visible.
    /// </summary>
    public required bool ConvertGrayscaleImages { get; init; }

    /// <summary>
    /// How much smaller a re-encoded image must come out, as a fraction of its original size,
    /// before it is worth keeping. Re-encoding always spends a generation of quality, so taking a
    /// saving of a few bytes for it is a bad trade.
    /// </summary>
    public required double MinimumImageSavingFraction { get; init; }

    public static CompressionSettings FromPreset(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Lossless => new CompressionSettings
        {
            RecompressImages = false,
            MaxImageEdgePixels = int.MaxValue,
            JpegQuality = 100,
            ConvertGrayscaleImages = false,
            MinimumImageSavingFraction = 0
        },
        CompressionPreset.HighQuality => new CompressionSettings
        {
            RecompressImages = true,
            MaxImageEdgePixels = 2400,
            JpegQuality = 88,
            ConvertGrayscaleImages = true,
            MinimumImageSavingFraction = 0.15
        },
        CompressionPreset.Balanced => new CompressionSettings
        {
            RecompressImages = true,
            MaxImageEdgePixels = 1700,
            JpegQuality = 75,
            ConvertGrayscaleImages = true,
            MinimumImageSavingFraction = 0.05
        },
        CompressionPreset.Smallest => new CompressionSettings
        {
            RecompressImages = true,
            MaxImageEdgePixels = 1100,
            JpegQuality = 58,
            ConvertGrayscaleImages = true,
            MinimumImageSavingFraction = 0.02
        },
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown compression preset.")
    };

    /// <summary>Throws if the values would produce nonsense output.</summary>
    public void Validate()
    {
        if (MaxImageEdgePixels < 1)
            throw new ArgumentOutOfRangeException(
                nameof(MaxImageEdgePixels), MaxImageEdgePixels, "Image edge must be at least 1 pixel.");

        if (JpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(
                nameof(JpegQuality), JpegQuality, "JPEG quality must be between 1 and 100.");

        // A threshold of 1 would reject every possible result, leaving the setting silently
        // equivalent to not recompressing at all.
        if (MinimumImageSavingFraction is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(
                nameof(MinimumImageSavingFraction),
                MinimumImageSavingFraction,
                "Minimum image saving must be at least 0 and below 1.");
    }
}
