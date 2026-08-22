namespace PdfTool.Core.Splitting;

/// <summary>Outcome of a completed split.</summary>
/// <param name="InputPath">The document that was split.</param>
/// <param name="OutputDirectory">Folder the parts were written to.</param>
/// <param name="OutputPaths">Full paths of the parts, in page order.</param>
public sealed record SplitResult(
    string InputPath,
    string OutputDirectory,
    IReadOnlyList<string> OutputPaths)
{
    public int FileCount => OutputPaths.Count;
}
