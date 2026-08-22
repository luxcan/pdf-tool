namespace PdfTool.Core;

/// <summary>
/// Raised when a PDF operation fails for a reason the user can act on, such as a missing,
/// corrupt or protected file. Callers are expected to surface the message directly.
/// </summary>
public class PdfToolException : Exception
{
    public PdfToolException(string message) : base(message)
    {
    }

    public PdfToolException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
