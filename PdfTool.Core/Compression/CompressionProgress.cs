namespace PdfTool.Core.Compression;

/// <summary>Progress of a single document's compression, reported as each image is handled.</summary>
/// <param name="ImagesProcessed">Images examined so far.</param>
/// <param name="TotalImages">Images the document contains.</param>
/// <param name="ImagesRecompressed">Of those examined, how many were actually made smaller.</param>
public sealed record CompressionProgress(int ImagesProcessed, int TotalImages, int ImagesRecompressed)
{
    public double Fraction => TotalImages == 0 ? 0 : (double)ImagesProcessed / TotalImages;
}
