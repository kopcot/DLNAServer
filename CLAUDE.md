# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

The solution sits at the repository root - `src/`, `tests/`, the solution file and the deployment
scripts. It was flattened out of a `Current/` subfolder on 2026-09-21, when the repository was
prepared for GitHub; a path of the form `Current/x` in an older document means `x`.

**The server this one replaces is no longer in this repository.** It was a complete, working DLNA
server (v6.1) kept as a READ-ONLY behavioural spec of what must go on the wire, and it was removed
on 2026-09-21 because its own repository, `T:\repos\DLNAServer_Legacy`, carries the identical
source. Documents here still cite it as *the reference*; read it there when a wire-behaviour
question needs settling, and write nothing into it.

`README.md` is the landing page and was cut to that on 2026-09-23; the detail moved to `docs/` -
`architecture.md`, `dlna-compatibility.md` (the behaviours that intentionally differ from that server),
`configuration.md`, `operations.md`, `performance.md` and `troubleshooting.md`, indexed by
`docs/README.md`. There is deliberately no `docs/decisions/` ADR tree: `docs/decisions.md` section 7b is the
decision record and running two of them is how they drift apart. `docs/development.md` covers the build, the two hard rules and the release flow (it was `CONTRIBUTING.md` until 2026-09-26, which is now a short page naming the contributors);
`.github/SECURITY.md` covers reporting.

## The server

> **Start here: `docs/decisions.md`.** It is now the only project document and the single source of truth
> for this rewrite - decisions and their rationale, the memory budget and its measured baselines,
> milestone status and checklists, the conventions and tooling traps that have cost time, and how to
> resume. Read it before doing any work here, and update it as work completes.
>
> **Version `1.1.0923`.** Every change bumps the assembly version - the rule, and who may bump which part,
> is in `docs/decisions.md` section 7 under "Assembly version". Each project under `src/` carries its **own**
> `<Version>` as of 2026-09-23, so bump only the projects the change touched; `DlnaServer.Host` is the
> product version, and test projects take it from `Directory.Build.props`. **`release-notes.md` is updated
> in the same change** - operator-facing release notes, one entry per `Major.Minor`, in their terms, never
> a build log; a change they would not notice adds nothing to it.
>
> **Its section 7b is the one to read first.** On 2026-09-08 the review history (`docs/`), both
> `REVIEW-*-FIXES.md` worklists and both session records were deleted. Everything from them that was
> still live was folded into that section: the **standing decisions** (reporting one of those as a defect
> is a false positive), the **open work**, and the **traps**. Their history was the point of deleting
> them and is gone.
>
> **State.** Running on the NAS, working set 563.4 MB → **177.7 MB** after the memory pass, a television
> plays from it, **789 tests at 0 warnings**, no vulnerable packages. **The NAS is several batches behind
> this tree**: its About page answers and reported `1.0.0` on 2026-09-15, so it carries everything through
> section 6m but not 6n, 6o or `1.1.0917`. See "Where things stand" in docs/decisions.md section 8. Three `/review-all --full`
> passes and a deduplication pass have run and everything they raised is closed except the items listed
> below. The third pass (2026-09-08, whole-tree) found two Blockers in the configuration-reload path -
> a failed or missing re-read blanked every setting and the next scan cascade-deleted the index, and one
> invalid value poisoned `IOptionsMonitor` for the life of the process. Both are fixed and covered by
> tests; see docs/decisions.md section 7 and the `LastGood*` types in `Host/Configuration`.
>
> **The migration history was squashed to a single `InitialSchema` on 2026-09-08**, per an explicit
> decision that this is pre-customer. A deployment carrying an older database will fail to migrate, be
> moved aside as `dlna.sqlite.corrupt-<date>_<time>` and rebuilt empty - but **only when the failure
> surfaces as `SQLITE_CORRUPT` or `SQLITE_NOTADB`**, which is all `DatabaseInitializer.IsCorruption`
> matches. An ordinary migration error (`SQLITE_ERROR`) propagates instead, and the host stops cleanly;
> under `restart: unless-stopped` that is a crash loop, not a self-heal - so the next deploy to the NAS
> costs a full rescan and regenerates every `PublicId`. Thumbnails are adopted rather than rebuilt.
>
> **What is open**, all detailed in section 7b: a steady-state memory reading; the four DIDL fields
> confirmed on a real television; **W9**, whether the two `Language` indexes earn their keep, which needs
> two `EXPLAIN QUERY PLAN` statements against a real database; and from 6n, uploading through a real file
> picker rather than a scripted post, no cap on a whole batch, and the fact that uploading is the first
> endpoint letting a LAN device WRITE to the filesystem - which is why it ships off and needs a restart. Three findings from the 2026-09-08 pass
> were deliberately NOT applied and are recorded where they live: UPnP error 701 for an unknown ObjectID
> (new wire behaviour on the hot path, unverifiable without a television), the SSDP listener resolving
> identity from the remote address (inert on a single-NIC box, and a real fix reworks discovery - the
> limitation is commented at the call site), and the `DLNA.ORG_OP` bit rename, which was a **false
> positive**: the naming is already correct and renaming would have introduced the inversion it claimed.
>
> **The newest work is `docs/history.md` section 6o (2026-09-17, `1.1.0917`)**, which empties the
> backlog's open list again: a **deleted media file now takes its preview image with it** -
> the image sat in a folder scanning skips, so nothing would ever have found it again, and
> `RemoveByPublicIdsAsync` therefore returns the abandoned paths the way `MoveOrRenameAsync` already did.
> On top of that, a file's **type and compatibility profile can be corrected on its own page** (the UPnP
> class is recomputed from the MIME, never chosen separately), a file the byte cache gave up on can be
> **let back in** from either the file or its folder, and *Search files* can list those files. The
> upload-folder *Check* asked for in that batch was already shipped by 6n.
>
> **Section 6n (2026-09-15, `1.1.0915`)** was the previous batch: the whole backlog
> open list closed in one batch - a streamed multipart **file-upload page** (off by default, needs a
> restart, its own security log, a `UploadDevices` table remembering each device's destination), collapsible
> Library listings, **provenance tooltips** behind the Temporary-settings switch, an assembly table on
> About, and `release-notes.md`. Three defects in it were found only by running the server - a route
> collision with the Razor page, MVC consuming the form before the action, and a convention that stopped
> the server booting with uploads OFF - and all three are in section 7's trap list. The tooltip grammar is
> on `Provenance.Source`; follow it for any new annotation.
>
> **Work done 2026-09-07/08** is recorded in `docs/history.md` section 6g: the five customer items from
> the backlog (all closed and deployed) plus an open-ended metadata feature. A file that moves
> or is renamed keeps its row and its `PublicId` instead of being re-inserted; audio files extract their
> embedded cover art; admin tiles carry a media-kind icon and fall back to the `Resources` icons; the
> byte cache no longer records "too large" as permanent; and a `MediaFileTags` table stores every tag a
> file carries about itself - ffprobe for audio and video, **MetadataExtractor for pictures**, since
> ffprobe cannot see an EXIF block - behind `Library.ReadContainerTags`, with purge/rebuild actions on
> Maintenance and per-file.


Layered solution, `net8.0`, pinned via `global.json` to SDK 8.0.414 with `rollForward: latestPatch` - the NAS has 8.0.414, a dev box resolves the newest 8.0.4xx it has, and neither drifts onto the .NET 9/10 SDK.

```
src/DlnaServer.Core/          domain model, DLNA types, options, abstractions — no dependencies
src/DlnaServer.Persistence/   EF Core + SQLite, migrations, repositories
src/DlnaServer.Media/         scanning, ffprobe metadata, SkiaSharp thumbnails
src/DlnaServer.Upnp/          SSDP, description.xml, SOAP, DIDL-Lite — no EF, no HTTP
src/DlnaServer.Admin/         Blazor Server admin UI (Razor Class Library)
src/DlnaServer.Host/          the only executable; nothing references it
tests/DlnaServer.UnitTests/         NUnit + FluentAssertions
tests/DlnaServer.IntegrationTests/  real components wired through DI - no host, no WebApplicationFactory
tests/DlnaServer.ArchitectureTests/ NetArchTest - layering and the entity/DTO boundary
```

**Startup database handling.** A missing database is created from migrations; a corrupt one (failing
`PRAGMA quick_check`, unopenable, or failing to migrate) is moved aside as `dlna.sqlite.corrupt-<date>_<time>`
and rebuilt empty, because the index is derived data a rescan restores. A database merely last opened on a
*different machine* is warned about and left untouched - that is not corruption, and the reference wiped it.

```powershell
dotnet build DlnaServer.sln          # must stay at 0 warnings
dotnet test  DlnaServer.sln
dotnet test  DlnaServer.sln --filter FullyQualifiedName~DlnaOptionsValidatorTest   # single fixture
dotnet run --project src\DlnaServer.Host                                           # needs SourceFolders set
```

`NasBuild.sh` publishes the Host for the QNAP NAS (linux-x64, framework-dependent — the NAS has the .NET 8.0 runtime). `NasBuild.usage.txt` documents arguments, ports, config and troubleshooting.

`Dockerfile` + `docker-compose.yml` are the container path, documented in `Docker.usage.md`; `docker-compose.admin-remote.yml` + `Caddyfile` put **only** the admin pages behind TLS and a password. Built and run 2026-09-06. **Host networking is not optional** — SSDP is UDP multicast and a bridge does not forward it, so on a bridge the admin UI works perfectly and no television ever sees the server. The image installs ffmpeg from Debian so `Thumbnails.DownloadFFmpeg` stays off, redirects the database to `/data` and logs to `/app/logs`, sets `HOME=/data` so Blazor's data-protection keys survive a recreate, and `chmod 0777`s those two directories because the operator picks the uid via `PUID`/`PGID` to match their media — thumbnails are written *into* the media tree, so that mount cannot be read-only.

**Two ports, one process.** One port serves UPnP/DLNA and media, the other the Blazor admin UI. `ServerOptions` defaults to `26851`/`26852`, but the shipped `config.json` sets `26852`/`26853` so this server can run alongside the reference implementation on 26851 — read the live values from `config.json`, not the defaults. Endpoints are bound to one port each via `RequirePortEndpointFilter`, so a renderer on the media port gets a 404 for anything admin. They must differ or startup fails.

**Configuration** is `config.json` next to the binaries, bound to `DlnaOptions` (section `Dlna`) through the normal `IConfiguration` pipeline and consumed via `IOptionsMonitor`, so most edits apply without a restart. The schema is grouped (`Server` / `Library` / `Thumbnails` / `FileCache` / `Compatibility` / `Database` / `Upload`) and is **not** compatible with the reference's flat `config.json`. `DlnaOptionsDefaults` runs between binding and validation: with **no source folder configured it serves the application's own folder**, excluding the thumbnail cache and `Resources` so neither the generated thumbnails nor the device icons are indexed as media. A fresh deployment therefore starts, with a warning naming the folder it fell back to. `DlnaOptionsValidator` then refuses to boot on: a missing source folder, equal ports, a blank `Thumbnails.SubFolderName`, a thumbnail cache inside a source folder that no `ExcludeFolders` entry covers, or an `Upload.DestinationFolder` outside every source folder.

**A first fill dates rows from the filesystem, and only a first fill.** `CreatedUtc` is the row's own indexing time and is what *Recently added* orders by; `FileCreatedUtc` is the file's date and is what Browse's date sort uses, governed by `Library.UseFileCreationDateTime`. When a source folder has no indexed row yet - an empty database, or a folder just added to the configuration - every file found under it takes its `CreatedUtc` from the filesystem instead of from the clock, **regardless of that setting**. A bulk import otherwise stamps every row with one identical timestamp, so *Recently added* degenerates into insertion order; that is a customer-reported bug hit by recreating the database. A file arriving into an already-indexed folder is genuinely new and keeps the current time. The test is "does a row exist for this source folder", not `IsSourceRoot`, so a child promoted to a source folder is not re-dated. `LibraryScanner.ResolveFileSystemDate` falls back to the write time when a filesystem reports no birth time, which is common on Linux and would otherwise date every file to 1970.

**`Library.ExcludeFolders` entries are folder names, partial paths or full paths, matched on whole path-segment boundaries.** `path1` hides `path1` and everything under it and does **not** touch `path1L` or `path10`; `Films/Private` hides one branch without hiding every other `Private`. Either separator may be used and the two are equivalent, so one `config.json` works on the NAS and on a Windows dev box. One rule governs both halves, and as of M1's fix that is structural rather than a convention: `PathExclusion.Canonicalise` is the single canonical form, `PathExclusion.TrimBoundaries` the single definition of an entry's boundaries, and `HiddenPathQuery` the single SQL predicate that all three repository sites call. The three hand-synced copies are gone. Matching is **ASCII-only for case**, deliberately: SQLite's `LIKE` folds ASCII and nothing else, and the NAS runs under `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`, so an entry differing from the folder only in the case of a non-ASCII letter does not match. **This was a customer-reported bug**: hiding used to be a raw substring over the whole path, matching the reference, so `path1` also hid `path1L` while scanning compared single segments and kept importing it, and a partial path was refused by validation outright. The operator got no sign that more was hidden than they had named.

**`Library.ExcludeFolders` carries no default on the property.** `ConfigurationBinder` *adds to* a non-empty `IList<string>` rather than replacing it, so a default declared there appended itself to whatever `config.json` named - and since the admin UI writes the bound list back, the file grew by two entries on every save. `DlnaOptionsDefaults` seeds `.@__thumb` and `@Recycle` after binding, only when configuration names none of its own, de-duplicates case-insensitively either way, and **always adds `Thumbnails.SubFolderName` whether or not it is listed** - a scan that does not skip that folder re-ingests every preview as media. Watch for the same shape anywhere else: a collection property with a non-empty initializer that is bound from configuration.

**Thumbnails are written beside the media**, into `Thumbnails.SubFolderName` (`.@__thumb`), named `<media file name>.jpg` - the reference's layout, so an existing library arrives already previewed and a redeploy does not discard them. An existing thumbnail is adopted rather than regenerated. `SubFolderName` is **required**; `Thumbnails.CacheDirectory` and the central-cache branch in `MediaProcessingHostedService.BuildThumbnailPath` are consequently unreachable, and are kept and marked as such rather than removed.

**ffmpeg is optional and everything it does goes missing quietly.** Indexing, browsing and streaming need none. Video and audio metadata and video thumbnails *do* - durations, resolutions, codecs, audio tracks, subtitle streams, and therefore the search page's language filters. Images use SkiaSharp and are unaffected. `FFmpegProvisioner` looks in an `ffmpeg` folder beside the binaries and downloads only when `Thumbnails.DownloadFFmpeg` is on, which ships **off** because that path executes an unverified third-party binary. Absent binaries are not an error, so this was invisible for a day on a 25,504-file library; `IMediaCapabilities` (`Core.Diagnostics`) now reports what the provisioner resolved and the dashboard warns when the answer is a definite no. It is `bool?` - `null` means nothing has needed ffmpeg yet, and reading it never triggers resolution.

**One library, whether a television or the admin UI is asking.** Every *listing* the repositories answer - source roots, a folder's children, recently added, both searches, and the search page's audio and subtitle language filters - hides anything matching `Library.ExcludeFolders` **and** any folder whose subtree holds no visible file, with no flag to ask for more. Deliberately *not* filtered: lookup by identifier or path (so a renderer mid-stream is not cut off), and the indexer's own reads - `GetExistingPathsAsync` decides what to insert, and `GetIndexedPageAsync` decides what to *delete*, so filtering it would strand a hidden file's row after its file was deleted from disc. `ILibraryScanner.EnumerateDirectories` yields only folders that lead to media, and reconciliation prunes indexed folders that no longer do - **leaves only, repeatedly**, because deleting an empty ancestor cascades and would carry away a subtree `ExcludeFolders` was only meant to hide.

**The Maintenance page has two rebuilds.** *Rebuild index* deletes every row, runs `VACUUM` and asks for a scan - no restart. *Recreate database* deletes the file and restarts. The file is deleted **at startup, not when the button is pressed**: `IDatabaseResetSignal` carries the request across the restart the way `IRestartSignal` does, because the safe moment to remove a database file is when nothing holds it open. `ILibraryScanSignal` is what lets the admin UI start a scan at all, since scanning lives in the host and `DlnaServer.Admin` references only Core and Persistence.

**Uploading is the only path that writes into the media tree, and it ships off.** `Dlna.Upload.Enabled` is read **once at startup** - the endpoint is created then or not at all, which is what makes the setting's "needs a restart" real rather than a note in the page - so with it off there is no route that accepts a file. On, `/admin/upload` is a statically rendered page posting a plain `multipart/form-data` form to `UploadController` at `/admin/upload/files`, which streams each part to disc with `MultipartReader`; nothing travels over the Blazor circuit and there is still no JavaScript. `UploadDestination` re-derives the allowed folders from configuration rather than trusting the form, `UploadFileName` accepts only extensions the library would index, and every file is recorded in `logs/uploadSecurity.log`. See `docs/history.md` section 6n for the three defects that only running it found.

`Dlna.Compatibility` holds the device escape hatches — `SendDlnaResponseHeaders` and `UseLegacyInvertedSearchTargetMatch` restore the reference's behaviour if a TV regresses.

### Conventions for the rewrite

- **Block-scoped namespaces**, per `.editorconfig`. The global `cs-writeguard` hook asks for file-scoped; it carries another project's rules and does not apply here.
- **Protocol DTOs document their wire contract on every property** — carried over from the reference's `BrowseItem.cs`, which is the clearest thing in that codebase. The wire name in bold, then a plain-language description, sitting next to the attribute that carries element name, namespace and order:

  ```csharp
  /// <summary>
  /// <b>upnp:albumArtURI</b><br />
  /// The URL for the media's thumbnail image
  /// </summary>
  [XmlElement(ElementName = "albumArtURI", Namespace = XmlNamespaces.NS_UPNP, Order = 101)]
  public string? ThumbnailUri { get; set; }
  ```

  Applies to every DIDL-Lite type, SOAP request/response contract, and SSDP/HTTP header constant. The point is that a reader never has to cross-reference the UPnP spec to learn what a property becomes on the wire. The `xmldoc-private` hook warning does not override this — these are public protocol contracts.
- **`[LoggerMessage]` source-gen logging**, in `Foo.Log.cs` siblings next to `Foo.cs`. Unique EventIds within a class.
- **One type per file**; interface and implementation in separate files.
- **Entities never leave `DlnaServer.Persistence`.** Entity classes and `DlnaDbContext` are `internal` to that assembly, so no other project can reference one - this is enforced by the compiler, not by convention. Everything crossing the repository boundary is a DTO from `DlnaServer.Core.Contracts`.
- **Every entity has `int Id` and `Guid PublicId`.** `Id` is the surrogate key every foreign key and index is built on and never leaves persistence. `PublicId` is the external identifier - DTOs, DIDL-Lite ObjectIDs and admin URLs all carry it. Repositories take and return `PublicId` and translate to the integer key internally.
- **Architecture rules are tested**, not just documented - `tests/DlnaServer.ArchitectureTests` fails the build if Core gains an EF or ASP.NET dependency, if `Upnp`/`Media` reach into persistence, if an entity turns public or appears on a public signature, or if a DTO gains a settable property.
- **Reads project straight into DTOs** (`.Select(e => new FooDto { ... })`), so the emitted SQL fetches only the DTO's columns and no entity is materialised. Note that `is null` is a compile error inside an expression tree - use `!= null` in projections.
