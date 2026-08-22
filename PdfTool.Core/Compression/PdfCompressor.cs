using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTool.Core.Documents;

namespace PdfTool.Core.Compression;

/// <summary>
/// Shrinks a single PDF. Structural rewriting (stream recompression, dropping redundancy) always
/// runs; image downsampling runs when the settings allow it and is where most of the saving comes
/// from on scans and photo-heavy documents.
/// </summary>
public sealed class PdfCompressor
{
    public Task<CompressionResult> CompressAsync(
        string inputPath,
        string outputPath,
        CompressionSettings settings,
        string? password = null,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        return Task.Run(
            () => Compress(inputPath, outputPath, settings, password, progress, cancellationToken),
            cancellationToken);
    }

    private static CompressionResult Compress(
        string inputPath,
        string outputPath,
        CompressionSettings settings,
        string? password,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var originalBytes = new FileInfo(inputPath).Exists ? new FileInfo(inputPath).Length : 0;

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // Written beside the output so the move at the end stays on one volume, and so a failure
        // never leaves a half-written file where the user expects their document.
        var tempPath = outputPath + ".tmp";

        try
        {
            var imagesRecompressed = Rewrite(
                inputPath, tempPath, settings, password, progress, cancellationToken);

            var compressedBytes = new FileInfo(tempPath).Length;

            // Documents that are already optimised routinely come out larger. Handing back a bigger
            // file is never the useful answer, so the original is kept instead.
            if (originalBytes > 0 && compressedBytes >= originalBytes)
            {
                // Compressing in place is supported, and there the original is already exactly where
                // it needs to be. Copying a file onto itself is not a no-op; Windows refuses it.
                if (!IsSameFile(inputPath, outputPath))
                    File.Copy(inputPath, outputPath, overwrite: true);

                return new CompressionResult(
                    inputPath, outputPath, originalBytes, originalBytes, 0, KeptOriginal: true);
            }

            File.Move(tempPath, outputPath, overwrite: true);

            return new CompressionResult(
                inputPath, outputPath, originalBytes, compressedBytes, imagesRecompressed, KeptOriginal: false);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static bool IsSameFile(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static int Rewrite(
        string inputPath,
        string tempPath,
        CompressionSettings settings,
        string? password,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Scoped so the document is closed before the caller moves files around, which matters when
        // the output path is the input path.
        using var document = PdfFileOpener.Open(inputPath, password, PdfDocumentOpenMode.Modify);

        var imagesRecompressed = settings.RecompressImages
            ? PdfImageOptimizer.Optimize(document, settings, progress, cancellationToken)
            : 0;

        cancellationToken.ThrowIfCancellationRequested();

        // The same settings merging and splitting write with, so an output does not depend on which
        // command produced it.
        PdfOutput.ApplyOptions(document);
        PdfOutput.Save(document, tempPath);

        return imagesRecompressed;
    }
}
