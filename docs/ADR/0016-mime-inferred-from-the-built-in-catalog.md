# 0016. Scanning infers a MIME from the built-in catalog

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 16

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, while closing the last of the review documents deleted that day.

## Decision

**Scanning infers a MIME from the built-in catalog when configuration names no mapping**, and the fallback admits **video, audio and images only** (`DlnaMimeCatalog.IsPresentableMedia`). Subtitles are deliberately excluded: the catalog knows `.srt`, `.vtt`, `.sub` and `.ttml`, and indexing them would put every sidecar beside the films as a `Generic` item on the television. `ConnectionManager`'s `Source` list widened to match, deliberately over-approximating - under-claiming a MIME costs playback, over-claiming costs nothing. Configuration still wins outright, which is what keeps `.mp3 -> audio/mp4` working.

## Consequences

**Know the size of this before the next scan: 137 extensions become media that were not.** Against the shipped 14, the catalog adds 137 video/audio/image extensions - and most of them are the point (`.wav`, `.flac`, `.ogg`, `.m4a`, `.aac`, `.webm`, `.ts`, `.mka`, `.gif`, `.bmp`, `.tif`, `.webp`, `.mid` were all silently not media before, on a real library). A handful are not media in any useful sense and will appear on a television if the library holds them: **`.m3u`** (a playlist, classified Audio), **`.svg` / `.ico` / `.wmf`** (icons and vector art - `.wmf` is classified *Video*), **`.dwg` / `.dxf` / `.svf`** (CAD drawings, classified Image), and the ambiguous **`.pm`**, **`.la`** and **`.ts`**, which are also a Perl module, a libtool archive and a TypeScript file.

Nothing has been narrowed to exclude them, because that list is a judgment call rather than a defect - if any of them shows up in a listing, `Library.ExcludeFolders` does not help and the right lever is a deny-list beside `IsPresentableMedia`. **The first scan after deploying this will insert a lot of rows.**
