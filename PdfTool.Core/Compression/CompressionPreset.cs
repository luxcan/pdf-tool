namespace PdfTool.Core.Compression;

/// <summary>How aggressively to shrink a document.</summary>
public enum CompressionPreset
{
    /// <summary>Rewrite structure and streams only. Nothing is re-encoded, so nothing degrades.</summary>
    Lossless,

    /// <summary>Downsample only very large images, at high JPEG quality. Safe for printing.</summary>
    HighQuality,

    /// <summary>Sensible default for documents that will be read on screen or emailed.</summary>
    Balanced,

    /// <summary>Smallest output. Visible softening on photographs and scans.</summary>
    Smallest
}
