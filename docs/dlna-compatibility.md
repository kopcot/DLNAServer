# DLNA compatibility

What this server puts on the wire, and every place it differs from the
reference implementation it replaces.

## Deliberate divergences from the reference

Everything not listed here reproduces the reference exactly, including its oddities — **except the wire
details below**, which were found by capturing the reference's own responses (see
`tests/DlnaServer.IntegrationTests/GoldenFiles/`) and are divergences this server keeps deliberately:

- **`res@class`** — the reference writes `class="image"` on the media `res`; `DidlResource` has no such
  property. Non-standard in DIDL-Lite, and no renderer has been observed needing it.
- **Root-listing title suffix** — the reference renders `dc:title` as `Sample.png (C:\path	oolder)`.
  Putting a filesystem path inside every title is noise on a television, so titles here are just the title.
- **Thumbnail `DLNA.ORG_PN`** — hard-coded `JPEG_TN` where the reference resolves `JPEG`. Note `JPEG_TN`
  is the ≤160×160 profile while `Thumbnails.MaxWidth`/`MaxHeight` default to 480×360, so the advertised
  profile and the bytes disagree; kept for now because televisions accept it, but it is a real mismatch.
- **`res@size` on the thumbnail resource** — omitted here, `size="0"` in the reference.
- **`GetSortCapabilities`** — the reference answers with `<SearchCaps>`, which is the wrong element name
  and a copy-paste defect in it. This server answers with the spec-correct `<SortCaps>`, and
  `WireGoldenFileTest` pins that so the divergence cannot be "fixed" away by accident.
- **`dc:date`** — the reference emits `2026-09-02T20:43:23.5389560` with no timezone suffix; this server
  emits a UTC value with the trailing `Z`, which is unambiguous rather than local-and-unmarked.
- **SSDP `DATE`** — UTC here.


| Area | Reference | Here |
| --- | --- | --- |
| SSDP M-SEARCH | Answers for every service type **except** the one requested (inverted match) | Correct matching; `Compatibility.UseLegacyInvertedSearchTargetMatch` restores the old behaviour |
| Browse `BrowseFlag` | Ignored — `BrowseMetadata` returns a child listing | Honoured |
| Browse `Filter` | Ignored | Honoured |
| Browse `SortCriteria` | Ignored | Honoured |
| Browse `RequestedCount` | Clamped to `[1,100]`; `0` becomes `1` | `0` means all, bounded by `Compatibility.MaxBrowseRequestedCount` |
| DLNA HTTP headers | `contentFeatures.dlna.org` / `transferMode.dlna.org` never sent | Sent; `Compatibility.SendDlnaResponseHeaders` turns them off |
| GENA `TIMEOUT` | `00:30:00` | `Second-1800` |
| GENA `UNSUBSCRIBE` | Acknowledged, never enacted | Actually removes the subscription |
| MediaReceiverRegistrar | SCPD advertises two actions, contract implements neither | Both implemented |
| GENA `NOTIFY` | Never sent | Still not sent — known gap, out of scope for now |
| Thumbnail location | Inside the media tree, kept out of scans only by `ExcludeFolders` | **Also inside the media tree**, in `Thumbnails.SubFolderName` (`.@__thumb`), so an existing library arrives already previewed and a redeploy keeps its previews. The name is required, and is added to `ExcludeFolders` after binding whether or not it is listed there |
| Folders with no media | Every folder on the volume becomes a container, QNAP's `.streams` and `.@upload_cache` included | Only folders leading to media are indexed, and a folder whose subtree holds nothing visible is not listed |
| Metadata/thumbnails | Generated synchronously inside the Browse call | Background queue; Browse returns immediately |
| Schema changes | `EnsureCreated`, wiping the database on a machine-name mismatch | EF Core migrations |

### Preserved deliberately

These look like bugs and are not:

- `.mp3` is advertised as `audio/mp4` with `DLNA.ORG_PN=MP4` — an LG TV workaround.
- `DLNA.ORG_OP=01` (time-seek only), even though byte-range serving works.
- `DLNA.ORG_FLAGS` of `21F00000…` for streaming and `20F00000…` for images.
- Two different `sec:` namespaces: `http://www.sec.co.kr/dlna` in `description.xml`,
  `http://www.sec.co.kr/` in DIDL-Lite.
- Duplicate `X_DLNADOC` entries (`DMS-1.50` and `M-DMS-1.50`).
- SSDP announcements to the limited broadcast address as well as the multicast group.
- `description.xml` served through an `XmlDocument` round-trip — the wire bytes are the re-serialised form,
