# PDF Tool

Merge, split and compress PDFs on your own computer. Your files never leave it.

## Why this exists

Most people who need to join two PDFs together, pull three pages out of one, or compress one small
enough to email find the same thing: the free options want your personal documents uploaded to a
website first.

That is a poor trade for a medical report, a bank statement, a signed contract or a photograph of a
passport. Once a file is sitting on somebody else's server you are trusting a promise about what
happens to it next, and in plenty of workplaces that upload is against the rules to begin with.

PDF Tool does the same work on your own machine. It reads your file from your disk, writes the
result back to your disk, and does nothing else with it. There is no networking code in the
application at all: you can disconnect from the internet entirely and every feature still works.

It is especially good at compressing scanned documents, which are very often far larger than they
need to be. The software that does that well costs money, and the free way to do it is the website
that wants your file.

### Why scans are so big

A scanner does not read the words on a page. It photographs them. A twenty-page scanned contract is
twenty photographs in a wrapper, and photographs taken at scanner resolution are enormous — which is
why a document you can comfortably read on a phone arrives as a 40 MB attachment your email refuses
to send.

Shrinking one means making those photographs smaller, which is what most of this tool does.

## Getting your files in

Three ways, whichever suits: drag them onto the window, use **Add files...**, or drop them on
`PdfTool.exe` itself — "Open with" from Explorer works too, and anything opened that way lands on
whichever tab is showing.

Each tab keeps its own list, so clearing the compression list never disturbs a merge you are part
way through setting up.

## What it does

Three tabs, one job each.

### Merge — several files into one

Put your files in the order you want with **Move up** and **Move down**, then press
**Merge all pages** and choose where to save the result.

If you want only some of the pages, or a different order, **Choose pages...** opens a grid of
previews where you can:

- tick and untick individual pages, or use **Select all**, **Select none** and **Invert** — the
  ticks decide which pages go into the merged file
- turn a page a quarter turn with **Rotate**, which is how you fix a page that scanned sideways
- drag a page to where you want it, or nudge it with **Move left** and **Move right**
- put everything back with **Reset**: pages in file order, all ticked, none turned
- switch between four preview sizes — **S**, **M**, **L**, **XL** — for when the pages look alike
  and the small tiles are not enough to tell them apart. Holding **Ctrl** and turning the mouse
  wheel over the grid does the same, a step at a time.

**Rotate**, **Move left** and **Move right** act on the page you have clicked — the one with the
highlighted tile — rather than on everything ticked, so ticking a box never loses your place.

Previews are drawn only as you scroll to them, so opening a merge of several hundred pages does not
leave you waiting for pictures you have not looked at yet.

You can merge from the grid itself, or press **Back** for the file list. **What you arranged is
kept either way.** The button on the file list changes to read **Merge 12 chosen page(s)** so you
can see it is about to write the pages you picked rather than every page in the list, and opening
**Choose pages...** again brings the same grid back. **Reset** is the way back to a plain merge.

Adding or removing a file does clear the arrangement, and the status bar says so — there is no
sensible way to keep choices about pages that are no longer there, or to guess where new ones
belong. Moving a file up or down leaves it alone: the arrangement already says where every page
goes.

You can have the merged file compressed in the same pass by ticking **Compress merged output**. It
sits above both the file list and the page grid, so it is there whichever you are looking at, and
the quality setting beside it is the same one the Compress tab uses — change it in either place and
both follow.

### Split — one file into several

Add one PDF and choose how to cut it:

- **Every N pages** — parts of a fixed size, taken from the front. Left at 1, you get a file per
  page. The last part is short whenever the pages do not divide evenly: ten pages at three gives you
  three files of three and one of one. Asking for more pages than the document holds is not an
  error — you simply get the whole thing back as a single file.
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
save it; several ask for a folder.

Outputs are named after their originals with `-compressed` on the end — `report.pdf` becomes
`report-compressed.pdf` — so a batch can safely be written back into the folder the originals came
from. For a single file that is only the suggested name and you can change it. Two files with the
same name from different folders do not collide; the second becomes `report-compressed (2).pdf`.

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

How much you save depends entirely on what you started with. A scan has a great deal of room in it;
a document that is already well compressed has very little, and the tool tells you when it could not
improve on what you gave it.

## While it is working

The strip along the bottom of the window is the running commentary. Before you start it says what is
loaded — `3 file(s), 40 page(s) ready to merge.` While something is running it counts through the
work with a progress bar, and **Cancel** appears beside it. Cancelling takes effect at the next page
or the next file rather than part way through writing one, so nothing is ever left half-written. A
cancelled merge writes nothing at all, because the file is only saved once every page is in; a
cancelled split or batch compression keeps whichever parts it had already finished.

When it finishes, the strip says what was written and how big it came out, and **Show in folder**
appears to open Explorer with the result already highlighted. That message stays put until you do
something else, so a saving figure does not vanish before you have read it.

If a file cannot be opened or a page cannot be drawn, the tool says which file and why rather than
failing silently.

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
  colour, which costs three times the data for no benefit. Where an image carries no colour worth
  keeping, it is stored as greyscale instead. The test allows for a little chroma noise, because a
  grey page that has already been through a JPEG encoder is never perfectly grey — but it is
  deliberately hard to pass, since draining the colour out of a logo or a stamp is a real loss where
  a few unsaved bytes are not.
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

## What it does not do

- **Encrypted PDFs are not handled yet.** There is no prompt for a password, so a protected file
  cannot be opened.
- **Bookmarks and form fields do not survive a merge or a split.** Both build a new document and
  pour pages into it, which carries the pages themselves but not the document-level structure
  wrapped around them.
- **64-bit Windows only.** The interface is WPF, which does not run anywhere else, and the download
  is built for x64.

## Installing

Download `PdfTool.exe` from the [releases](https://github.com/luxcan/pdf-tool/releases) page and put
it wherever you like — your desktop is fine. There is nothing to install, no administrator rights
needed, and no separate runtime to fetch alongside it. It is a single large file because everything
it needs is inside it, .NET included.

The file is not code-signed, because a signing certificate costs more per year than this tool is
worth. Windows SmartScreen may therefore warn you the first time you run it, and some antivirus
software flags single-file applications of this kind on sight. If you would rather not take that on
trust, the source is all here and the build instructions below produce the same tool from it. It
will not be a byte-for-byte copy of the download — a build stamps the day it was made, and a release
carries the version from its tag — but nothing else differs.

The **About** button at the bottom left of the window tells you which build you are running: the
version, the commit it was built from, and the day it was built. That is the first thing worth
checking if something behaves oddly. It also links to the releases page, so you can see whether
there is a newer build and what changed in it — the application does not check for you, because
checking would mean the networking code it deliberately does not have.

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
[SkiaSharp](https://github.com/mono/SkiaSharp) for image encoding,
[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) for the view models, and
[VirtualizingWrapPanel](https://github.com/sbaeumlisberger/VirtualizingWrapPanel) for the page grid —
WPF ships no panel that both wraps and virtualises, and the grid has to do both to stay usable on a
document of several hundred pages.
