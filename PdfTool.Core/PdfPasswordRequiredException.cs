namespace PdfTool.Core;

/// <summary>
/// Raised when a PDF cannot be opened because it is encrypted and no valid password was supplied.
/// The UI treats this differently from other failures because it can prompt and retry.
/// </summary>
public sealed class PdfPasswordRequiredException : PdfToolException
{
    public PdfPasswordRequiredException(string filePath)
        : base($"'{Path.GetFileName(filePath)}' is password protected.")
    {
        FilePath = filePath;
    }

    public PdfPasswordRequiredException(string filePath, Exception innerException)
        : base($"'{Path.GetFileName(filePath)}' is password protected.", innerException)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
