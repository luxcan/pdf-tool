namespace PdfTool.Core.Tests;

/// <summary>
/// Reports on the calling thread. <see cref="Progress{T}"/> posts to the captured synchronisation
/// context, which would let assertions run before the callbacks arrive.
/// </summary>
internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
