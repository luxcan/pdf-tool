using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace PdfTool.Core.Documents;

/// <summary>
/// Reads dictionary entries the way a PDF is allowed to write them rather than the way it ideally
/// would.
///
/// PDFsharp's own accessors assume the ideal shape: <c>GetName</c> throws on an entry that is not a
/// name, and any entry at all may legitimately be written indirectly. Code that has to survive
/// whatever a producer emitted needs an answer instead of an exception, so everything here reports
/// null and lets the caller decide.
/// </summary>
internal static class PdfElements
{
    /// <summary>The dictionary an item stands for, following an indirect reference, or null.</summary>
    public static PdfDictionary? Resolve(PdfItem? item) => Dereference(item) as PdfDictionary;

    /// <summary>The name an entry holds, or null when it holds anything else.</summary>
    public static string? ReadName(PdfDictionary dictionary, string key) =>
        NameOf(dictionary.Elements[key]);

    /// <summary>
    /// The name an item is, or null. Worth going through for /ColorSpace and /Filter especially,
    /// which are arrays as often as they are names.
    /// </summary>
    public static string? NameOf(PdfItem? item) => Dereference(item) switch
    {
        PdfName name => name.Value,
        PdfNameObject name => name.Value,
        _ => null
    };

    public static PdfItem? Dereference(PdfItem? item) =>
        item is PdfReference reference ? reference.Value : item;
}
