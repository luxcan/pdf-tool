namespace PdfTool.App.Formatting;

/// <summary>Formats byte counts for display in the file list and the completion message.</summary>
internal static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB"];

    public static string Format(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Size cannot be negative.");

        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {Units[unit]}"
            : $"{size:0.#} {Units[unit]}";
    }
}
