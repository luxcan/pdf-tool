namespace PdfTool.Core.Compression;

/// <summary>Outcome of compressing one document.</summary>
/// <param name="InputPath">The file that was compressed.</param>
/// <param name="OutputPath">Where the result was written.</param>
/// <param name="OriginalBytes">Size of the input on disk.</param>
/// <param name="CompressedBytes">Size of the output on disk.</param>
/// <param name="ImagesRecompressed">How many images were re-encoded smaller.</param>
/// <param name="KeptOriginal">
/// True when compression produced a larger file and the original was written instead. Already
/// optimised documents hit this routinely, and growing a file is never the useful answer.
/// </param>
public sealed record CompressionResult(
    string InputPath,
    string OutputPath,
    long OriginalBytes,
    long CompressedBytes,
    int ImagesRecompressed,
    bool KeptOriginal)
{
    public long SavedBytes => OriginalBytes - CompressedBytes;

    public double SavedFraction => OriginalBytes == 0 ? 0 : (double)SavedBytes / OriginalBytes;
}
