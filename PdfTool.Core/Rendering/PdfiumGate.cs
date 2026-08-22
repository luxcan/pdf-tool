namespace PdfTool.Core.Rendering;

/// <summary>
/// Serialises every call into PDFium.
///
/// PDFium keeps global state and is not safe to call from two threads at once. Page thumbnails and
/// compression both reach it and run independently of each other, so the gate has to be shared
/// across the process rather than held per object.
/// </summary>
internal static class PdfiumGate
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    /// <summary>Runs work on a background thread with PDFium held exclusively.</summary>
    public static async Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
    {
        await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>
    /// Runs work on the calling thread with PDFium held exclusively, for callers already on a
    /// background thread and inside a synchronous pipeline.
    /// </summary>
    public static T Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        Lock.Wait(cancellationToken);
        try
        {
            return work();
        }
        finally
        {
            Lock.Release();
        }
    }
}
