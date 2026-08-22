# PDF Tool

Merge, split and compress PDFs on your own computer. Your files never leave it.

## Why this exists

Most people who need to join two PDFs together, pull three pages out of one, or make a scan small
enough to email end up on a website that asks them to upload the file first.

That is a poor trade for a medical report, a bank statement, a signed contract or a photograph of a
passport. Once a file is sitting on somebody else's server you are trusting a promise about what
happens to it next, and in plenty of workplaces that upload is against the rules to begin with.

PDF Tool does the same work on your own machine. It reads your file from your disk, writes the
result back to your disk, and does nothing else with it. There is no networking code in the
application at all: you can disconnect from the internet entirely and every feature still works.

It is also built for a second job — shrinking scanned documents, which are very often far larger
than they need to be. The software that does that well costs money, and the free way to do it is the
website that wants your file.

### Why scans are so big

A scanner does not read the words on a page. It photographs them. A twenty-page scanned contract is
twenty photographs in a wrapper, and photographs taken at scanner resolution are enormous — which is
why a document you can comfortably read on a phone arrives as a 40 MB attachment your email refuses
to send.

Shrinking one means making those photographs smaller, which is what most of this tool does.

## What it does

Three tabs, one job each.

### Merge — several files into one

Drag your files onto the window, or use **Add files...**. Put them in the order you want with
**Move up** and **Move down**, then press **Merge all pages**.

If you want only some of the pages, **Choose pages...** opens a grid of previews where you can:

- tick and untick individual pages, or use **Select all**, **Select none** and **Invert**
- turn a page a quarter turn with **Rotate**, which is how you fix a page that scanned sideways
- drag a page to where you want it, or nudge it with **Move left** and **Move right**
- switch between four preview sizes — **S**, **M**, **L**, **XL** — for when the pages look alike
  and the small tiles are not enough to tell them apart

Previews are drawn only as you scroll to them, so opening a merge of several hundred pages does not
leave you waiting for pictures you have not looked at yet.

You can have the merged file compressed in the same pass by ticking **Compress merged output**.

### Split — one file into several

Add one PDF and choose how to cut it:

- **Every N pages** — equal parts of a fixed size. Left at 1, you get a file per page.
- **Page ranges** — a list such as `1-3, 5, 8-10`, which gives one file per range in the order you
  list them. The ranges do not have to cover the whole document, so this is also how you pull a few
  pages out and ignore the rest.

A line beside the controls tells you what you are about to get before anything is written — how many
files, and how many pages of the original they use. If you mistype a range it explains what is wrong
rather than simply refusing to run.

The parts go into a folder you choose and are named after the pages inside them — `report-p01.pdf`,
`report-p04-06.pdf` — with the numbers padded so the folder sorts into reading order instead of
putting page 10 in front of page 2. Splitting takes one file at a time, because page numbers only
mean something against a single document.

Each part can be compressed as it is written, using the same settings as the Compress tab.

**Parts only carry their own pictures.** This sounds obvious and is not. A PDF page keeps a list of
the images it can draw, and that list is often shared with every other page in the document — so a
part cut out of a 21 MB scan can arrive holding all 21 MB, and splitting into four can leave you
with more than you started with. PDF Tool leaves the other pages' images behind.

Occasionally a document is put together in a way the tool cannot completely account for. When that
happens it does not guess: it keeps every image and the parts come out larger than they strictly
need to be. A file bigger than necessary is a nuisance. A page that has lost its picture is a
problem, and the tool is built never to make the second trade.

### Compress — make files smaller

Add one file or many, choose how hard to squeeze, and press **Compress**. One file asks you where to
save it; several ask for a folder and keep their own names.

## Choosing how hard to squeeze

| Setting | What it does | Use it when |
| --- | --- | --- |
| **Lossless** | Tidies the file's structure. No picture is touched, so nothing loses quality. | You want a guaranteed-identical document. Savings are modest. |
| **High quality** | Shrinks only very large images, barely. | The document may be printed. |
| **Balanced** | The sensible default. | Reading on screen and emailing. |
| **Smallest** | Squeezes hardest. | Size matters more than looks. Photographs and scans go visibly soft. |

Balanced limits pictures to 1700 pixels along their longest edge, which on an A4 page works out at
about 150 dots per inch — the usual target for something meant to be read on a screen rather than
printed. High quality allows 2400 and Smallest 1100.

As a rough guide, fifteen scanned medical documents totalling 13.4 MB came out **48.7% smaller** on
Balanced. A document that is already well compressed will save far less, and the tool tells you when
it could not improve on what you gave it.

## How the compression works

For readers who want the detail. Most of a scanned PDF is picture data, so that is where the savings
are: the tool reduces the size of each embedded image, re-encodes it as a JPEG, and rewrites the
file structure around it.

The exact settings behind the four choices above:

| Setting | Longest image edge | JPEG quality | Minimum saving before an image is re-encoded |
| --- | --- | --- | --- |
| Lossless | unchanged | not applied | not applicable |
| High quality | 2400 px | 88 | 15% |
| Balanced | 1700 px | 75 | 5% |
| Smallest | 1100 px | 58 | 2% |

Four things it does beyond the obvious:

- **Grey pictures are stored as grey.** Scanners routinely record a black-on-white page in full
  colour, which costs three times the data for no benefit. Where every pixel in an image is grey, it
  is stored as greyscale instead.
- **Awkward encodings still get compressed.** Some images are stored in formats the compressor
  cannot read directly — CCITT fax, JBIG2 and JPEG 2000 among them. Rather than skip those, it hands
  them to PDFium, the rendering engine that already ships inside the application, and works from
  what comes back.
- **Small gains are refused.** Re-encoding a picture always costs a little quality. An image that
  would only shrink slightly is left exactly as it was, because the trade is not worth making.
- **The output is never bigger.** If compressing produces a larger file than the original — which
  already-optimised documents do routinely — the original is kept and the tool says so.

Running through all of it: anything that cannot be interpreted with confidence is left alone.
Skipping an image costs you some saving. Corrupting one costs you the document.

The same principle governs what a split part carries. A part keeps every image unless the tool can
account for every page that shares the list — which it declines to do when a page draws through
patterns, Type 3 fonts, soft masks, annotation appearances or forms, or when a content stream will
not read. Each of those can put a picture on the page without naming it anywhere the tool can see.

Encrypted PDFs are not handled yet: there is no prompt for a password.

## Installing

Download `PdfTool.exe` from the [releases](https://github.com/luxcan/pdf-tool/releases) page and put
it wherever you like — your desktop is fine. It is one self-contained file of about 78 MB. There is
nothing to install, no administrator rights needed, and no separate runtime to install alongside it.

The file is not code-signed, because a signing certificate costs more per year than this tool is
worth. Windows SmartScreen may therefore warn you the first time you run it, and some antivirus
software flags single-file applications of this kind on sight. If you would rather not take that on
trust, the source is all here and the build instructions below produce the identical file.

The **About** button at the bottom of the window tells you which build you are running, which is the
first thing worth checking if something behaves oddly.

Every push to `main` also publishes the executable as a build artifact, reachable from the
[Actions](https://github.com/luxcan/pdf-tool/actions) tab.

## Building from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download) and Windows. The interface is
built with WPF, which does not build anywhere else.

```
dotnet build PdfTool.sln
dotnet test PdfTool.sln
```

To produce the single-file executable, into `PdfTool.App/bin/publish/win-x64/`:

```
dotnet publish PdfTool.App -c Release -p:PublishProfile=win-x64
```

Tagging a commit `v*` and pushing the tag stamps that version into the executable and attaches it to
a GitHub release, using the tag's own annotation as the release notes. Moving an existing tag and
force-pushing it republishes that release against the commit it now points at.

## How the project is laid out

| Project | Contains |
| --- | --- |
| `PdfTool.Core` | Merging, splitting, compression, inspection and rendering. No reference to WPF, so the logic stays testable without a window. |
| `PdfTool.App` | The WPF shell: view models, theme and controls. |
| `PdfTool.Core.Tests` | Tests that run against real PDFs built by the fixtures, rather than against mocks. |
| `PdfTool.App.Tests` | Smoke tests that show and lay out the real window, catching interface faults that compile cleanly and only fail on screen. |

Built on [PDFsharp](https://github.com/empira/PDFsharp) for document structure,
[PDFtoImage](https://github.com/sungaila/PDFtoImage) (PDFium) for rendering,
[SkiaSharp](https://github.com/mono/SkiaSharp) for image encoding, and
[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).
