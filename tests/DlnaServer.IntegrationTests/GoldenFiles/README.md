# Golden files

Captured from the **reference server actually running**, not written by hand and not derived from this
codebase. That distinction is the whole point: a golden file written to match the current output proves
only that the code agrees with itself, and `[WIRE-1]` is a claim about agreeing with the reference.

## How these were produced

1. The reference server was copied out of its read-only tree, then at `Reference/DLNAServer` here and
   since 2026-09-21 in its own repository at `T:\repos\DLNAServer_Legacy` (its source is never modified, and
   running it in place would create a database, a `logs/` folder and an ffmpeg download inside it).
2. The copy's `config.json` was pointed at a one-file media folder holding a 70-byte PNG, with
   `ServerPort` moved to 26861 and every video/audio processing flag turned off - an image needs only
   SkiaSharp, so nothing reaches out to download ffmpeg.
3. The server was started, the endpoints below were requested over HTTP, and it was stopped.

| File | How |
| --- | --- |
| `reference-description.xml` | `GET /Media/description.xml` |
| `reference-browse-response.xml` | `POST /ContentDirectoryService.asmx`, `Browse` on `ObjectID=0`, `BrowseDirectChildren`, `Filter=*`, `RequestedCount=10` |
| `reference-sort-capabilities.xml` | `POST /ContentDirectoryService.asmx`, `GetSortCapabilities` |
| `reference-search-capabilities.xml` | `POST /ContentDirectoryService.asmx`, `GetSearchCapabilities` |

## Why the comparison normalises

A literal byte comparison is **not achievable**, and that is most likely why `[WIRE-1]` was recorded as
done while nothing was ever implemented. Object identifiers are `PublicId` GUIDs and the reference mints
its own, so the same media file is a different id in each server, on every rebuild. The same applies to
the device UDN, to `dc:date`, and to the host and port in every URL.

So the tests mask exactly those volatile fields and compare the rest of the document in full. What
survives normalisation is everything that actually breaks a television: element order, namespace
prefixes and declarations, attribute presence and spelling, XML escaping, and the DLNA `protocolInfo`
strings.

## What these captures already proved

Each of these was a review finding that had been reasoned about but not verified. The captures settle
them:

- **`res@class` is real.** The reference writes `class="image"` on the media `res`; `DidlResource` here
  has no such property.
- **The root-listing title carries a folder suffix.** `dc:title` came back as
  `Sample.png (C:\...\refmedia)`, not `Sample.png`.
- **A second `res` element is emitted** for the thumbnail, with `size="0"`,
  `DLNA.ORG_OP=00;DLNA.ORG_CI=1` and no `class`, alongside `upnp:albumArtURI` **and** `upnp:icon`.
- **`dc:date` has no timezone suffix.** The reference emits `2026-09-02T20:43:23.5389560`; this server's
  `ToString("O")` on a UTC value produces a trailing `Z`.
- **`GetSortCapabilities` returns `<SearchCaps>`** in the reference - the wrong element name, a
  copy-paste bug in it. This server returns the spec-correct `<SortCaps>`, which is therefore a genuine
  divergence and a decision rather than an oversight.

## Re-capturing

Only re-capture from a reference build that is known good, and say in the commit which one. A golden
file quietly re-captured from a regressed reference silently moves the target these tests exist to hold
still.
