namespace PdfTool.Core.Tests;

/// <summary>A scratch folder that deletes itself when the test finishes.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PdfTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>A path inside the folder; several segments build a nested one that need not exist.</summary>
    public string Combine(params string[] segments) => System.IO.Path.Combine([Path, .. segments]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A locked file must not fail an otherwise passing test; the OS reclaims temp folders.
        }
    }
}
