using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;

namespace PdfTool.Core.Documents;

/// <summary>
/// Drops the images an assembled document lists but no page draws.
///
/// A page carries its resources in a listing it may share with every other page of the document it
/// came from, so taking one page takes the whole listing along with it. A part cut from a scan
/// written that way holds every image in the original, and a document split into four comes to four
/// times the size of the one it came from.
///
/// Deciding that nothing draws an image is the entire risk here, because being wrong takes a picture
/// out of someone's document. So this does not attempt to understand every route a PDF has to an
/// image. It prunes a listing only when it can account for every page that uses it completely -
/// pages that draw images by name and do nothing else - and leaves every other listing exactly as it
/// found it. That is the shape scanners produce, which is where the wasted size is.
///
/// What that rules out is listed in <see cref="CanAccountFor"/>. Being refused costs a larger file;
/// being wrong costs a blank page, so the two are not weighed equally.
/// </summary>
internal static class PdfResourcePruner
{
    public static void Prune(PdfDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Which pages share which listing is settled first, and cheaply. Most documents this tool is
        // given list no images at all, and discovering that by parsing every content stream costs
        // seconds on a long one.
        foreach (var (listing, pages) in GroupPagesByListing(document))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DrawnFrom(listing, pages, cancellationToken) is { } drawn)
                KeepOnly(listing, drawn);
        }
    }

    /// <summary>
    /// The pages of the document grouped by the /XObject listing they read from, skipping any listing
    /// that cannot be holding more than a page needs. A listing of one entry is not worth a content
    /// stream to judge, and skipping those is what keeps an ordinary document free.
    /// </summary>
    private static Dictionary<PdfDictionary, List<PdfPage>> GroupPagesByListing(PdfDocument document)
    {
        var listings = new Dictionary<PdfDictionary, List<PdfPage>>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < document.PageCount; i++)
        {
            var page = document.Pages[i];
            var resources = page.Elements.GetDictionary("/Resources");

            if (PdfElements.Resolve(resources?.Elements["/XObject"]) is not { } listing)
                continue;

            if (listing.Elements.Count < 2)
                continue;

            if (!listings.TryGetValue(listing, out var sharers))
                listings[listing] = sharers = [];

            sharers.Add(page);
        }

        return listings;
    }

    /// <summary>
    /// Every name drawn out of this listing, or null when the listing must be left alone. Null is the
    /// answer to anything unaccounted for, not merely to anything that failed.
    /// </summary>
    private static HashSet<string>? DrawnFrom(
        PdfDictionary listing, List<PdfPage> pages, CancellationToken cancellationToken)
    {
        // Only images. A form draws through a content stream of its own, against whichever resources
        // it is painted with, and following that safely enough is the part not worth attempting.
        foreach (var key in listing.Elements.Keys)
        {
            if (PdfElements.Resolve(listing.Elements[key]) is not { } xObject)
                return null;

            if (PdfElements.ReadName(xObject, "/Subtype") != "/Image")
                return null;
        }

        var drawn = new HashSet<string>(StringComparer.Ordinal);

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CanAccountFor(page))
                return null;

            if (DrawnNames(page) is not { } names)
                return null;

            // A page that lists images and draws none is not worth telling apart from one that could
            // not be read: PDFsharp answers a failed decode with no bytes and a truncated stream with
            // whatever it managed, and neither raises anything. Both arrive here as "draws nothing".
            if (names.Count == 0)
                return null;

            foreach (var name in names)
            {
                // Drawn but not listed means the drawing was written against resources other than the
                // ones being judged, so what those do list cannot be accounted for either.
                if (!listing.Elements.ContainsKey(name))
                    return null;

                drawn.Add(name);
            }
        }

        return drawn;
    }

    /// <summary>
    /// Whether a page's own content stream is the whole account of what it draws.
    ///
    /// Each of these paints from a content stream this never reads: a tiling pattern, a Type 3 font's
    /// glyph procedures, the form behind a soft mask, and an annotation's appearance. Any of them can
    /// draw an image without naming it anywhere this can see, so a page carrying one is not a page to
    /// judge.
    /// </summary>
    private static bool CanAccountFor(PdfPage page)
    {
        var resources = page.Elements.GetDictionary("/Resources");

        return !HasEntries(resources, "/Pattern")
            && !HasType3Font(resources)
            && !HasSoftMask(resources)
            && !HasAppearance(page);
    }

    private static bool HasEntries(PdfDictionary? resources, string key) =>
        PdfElements.Resolve(resources?.Elements[key]) is { } entries && entries.Elements.Count > 0;

    private static bool HasType3Font(PdfDictionary? resources)
    {
        if (PdfElements.Resolve(resources?.Elements["/Font"]) is not { } fonts)
            return false;

        foreach (var key in fonts.Elements.Keys)
        {
            // A font that cannot be read is treated as one that could have been Type 3.
            if (PdfElements.Resolve(fonts.Elements[key]) is not { } font)
                return true;

            if (PdfElements.ReadName(font, "/Subtype") == "/Type3")
                return true;
        }

        return false;
    }

    private static bool HasSoftMask(PdfDictionary? resources)
    {
        if (PdfElements.Resolve(resources?.Elements["/ExtGState"]) is not { } states)
            return false;

        foreach (var key in states.Elements.Keys)
        {
            if (PdfElements.Resolve(states.Elements[key]) is not { } state)
                return true;

            // /None is the ordinary way to say there is no mask; anything else names a form.
            if (state.Elements.ContainsKey("/SMask") && PdfElements.ReadName(state, "/SMask") != "/None")
                return true;
        }

        return false;
    }

    private static bool HasAppearance(PdfPage page)
    {
        if (page.Elements.GetArray("/Annots") is not { } annotations)
            return false;

        foreach (var item in annotations.Elements)
        {
            if (PdfElements.Resolve(item) is not { } annotation)
                return true;

            if (annotation.Elements.ContainsKey("/AP"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The XObject names a page draws, or null when its content cannot be accounted for.
    /// </summary>
    private static HashSet<string>? DrawnNames(PdfPage page)
    {
        if (ContentOf(page) is not { } content)
            return null;

        var names = new HashSet<string>(StringComparer.Ordinal);

        return Collect(content, names) ? names : null;
    }

    /// <summary>
    /// A page's content, parsed, or null when it cannot be read - which has to include the ways
    /// PDFsharp declines to say so. A stream whose filter fails comes back as no bytes at all rather
    /// than as an error, so bytes going in and nothing coming out is a failure however quietly it was
    /// reported.
    ///
    /// The bytes are read and concatenated here rather than through the overload that takes a page,
    /// because that one rewrites /Contents into a single decompressed stream as a side effect - and a
    /// document this declines to prune has to be left as it was found.
    /// </summary>
    private static CSequence? ContentOf(PdfPage page)
    {
        try
        {
            using var content = new MemoryStream();

            foreach (var stream in ContentStreamsOf(page))
            {
                var decoded = stream.Stream!.UnfilteredValue;

                if (stream.Stream.Value.Length > 0 && decoded.Length == 0)
                    return null;

                content.Write(decoded);

                // Separated, so the last token of one stream cannot run into the first of the next.
                content.WriteByte((byte)'\n');
            }

            return ContentReader.ReadContent(content.ToArray());
        }
        // Deliberately including OutOfMemoryException: PDFsharp's content lexer allocates without
        // bound on an unterminated string, and a few malformed bytes should cost this document its
        // pruning rather than cost the user the parts that were written before it.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>A page's content is one stream or an array of them, written inline or referenced.</summary>
    private static IEnumerable<PdfDictionary> ContentStreamsOf(PdfPage page)
    {
        var contents = page.Elements["/Contents"];

        var items = PdfElements.Dereference(contents) is PdfArray array
            ? array.Elements.ToList()
            : [contents];

        foreach (var item in items)
        {
            if (PdfElements.Resolve(item) is { Stream: not null } stream)
                yield return stream;
        }
    }

    /// <summary>
    /// Collects the name each Do draws. False means an operator drew something this cannot attribute,
    /// which is a reason to leave the listing alone rather than to treat it as drawing nothing.
    /// </summary>
    private static bool Collect(CObject content, HashSet<string> names)
    {
        switch (content)
        {
            case CSequence sequence:
                foreach (var child in sequence)
                {
                    if (!Collect(child, names))
                        return false;
                }

                return true;

            case COperator { OpCode.OpCodeName: OpCodeName.Do } drawing:
                if (drawing.Operands.Count != 1 || drawing.Operands[0] is not CName name)
                    return false;

                names.Add(name.Name);
                return true;

            default:
                return true;
        }
    }

    private static void KeepOnly(PdfDictionary listing, HashSet<string> drawn)
    {
        var unused = listing.Elements.Keys.Where(key => !drawn.Contains(key)).ToList();

        foreach (var key in unused)
            listing.Elements.Remove(key);
    }
}
