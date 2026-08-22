using PdfSharp;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfTool.Core.Documents;

/// <summary>
/// Opens source PDFs and translates PDFsharp's failure modes into <see cref="PdfToolException"/>s
/// the UI can present. Shared by inspection and merging so both report the same failures the same way.
/// </summary>
internal static class PdfFileOpener
{
    public static PdfDocument Open(string filePath, string? password, PdfDocumentOpenMode mode)
    {
        if (!File.Exists(filePath))
            throw new PdfToolException($"File not found: {filePath}");

        // PDFsharp invokes the provider only when the document is encrypted and the supplied
        // password is missing or wrong, which is a more reliable signal than matching on messages.
        var passwordRequired = false;

        void OnPasswordRequired(PdfPasswordProviderArgs args)
        {
            passwordRequired = true;
            args.Abort = true;
        }

        try
        {
            var document = password is null
                ? PdfReader.Open(filePath, mode, OnPasswordRequired)
                : PdfReader.Open(filePath, password, mode, OnPasswordRequired);

            if (passwordRequired || document is null)
                throw new PdfPasswordRequiredException(filePath);

            return document;
        }
        // PDFsharp signals malformed input with a plain InvalidOperationException as well as with
        // its own exception types, so both have to be translated.
        catch (Exception ex) when (ex is PdfSharpException or InvalidOperationException)
        {
            if (passwordRequired)
                throw new PdfPasswordRequiredException(filePath, ex);

            throw new PdfToolException(
                $"'{Path.GetFileName(filePath)}' could not be read. It may be corrupt or not a PDF.", ex);
        }
        catch (IOException ex)
        {
            throw new PdfToolException(
                $"'{Path.GetFileName(filePath)}' could not be opened: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfToolException($"Access to '{Path.GetFileName(filePath)}' was denied.", ex);
        }
    }
}
