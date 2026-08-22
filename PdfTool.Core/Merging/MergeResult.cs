namespace PdfTool.Core.Merging;

/// <summary>Outcome of a completed merge.</summary>
/// <param name="OutputPath">Full path of the file that was written.</param>
/// <param name="PageCount">Pages in the merged document.</param>
/// <param name="SizeBytes">Size of the merged file on disk.</param>
public sealed record MergeResult(string OutputPath, int PageCount, long SizeBytes);
