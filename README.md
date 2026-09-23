# DlnaServer

A UPnP/DLNA MediaServer (DMS) that streams media from a QNAP NAS to TVs on the local network.

This is a ground-up redesign of an earlier DLNA server (v6.1), kept outside this repository. That reference is **read-only** and
serves as the behavioural specification: what goes on the wire must stay compatible so the TVs that work
today keep working. The internals are new.

> **How the two compare, measured** (2026-09-03, on a tree that has since grown): the rewrite is
> **2.5x the raw lines but only 1.02x the hand-written logic** — the rest is tests, XML docs, generated
> migrations and a real admin UI. The report those figures came from was deleted on 2026-09-08 along with
> the rest of the review history; the ratios are what mattered and are recorded here, the absolute counts
> are a dated snapshot.
>
> **Deployed and measured.** This build runs on the NAS, its working set fell from 563.4 MB to
> **177.7 MB** under the memory pass, and a television plays from it. 789 tests at 0 warnings, no
> vulnerable packages, no open Majors. What is still owed is a *steady-state* memory reading — the
> 177.7 MB was taken 255 s after a restart — and confirmation on a real television of the four DIDL
> fields added on 2026-09-06.
>
> **Version `1.1.0923`.** It is `Major.Minor.MonthDate`, and each project carries its own, so a number
> says which assembly changed. The one on the About page is the host's, which is the product version;
> the assembly table on that page lists the rest. `release-notes.md` says what changed for whoever runs the server,
> one entry per `Major.Minor`; `PLAN.md` holds everything internal. `release-notes.md` and `LICENSE` are
> both copied into the build and publish output, so a deployment carries them beside the binaries.
>
> **Licence: [MIT](LICENSE).** Free to use, change and pass on, with no warranty.
>
> **`PLAN.md` is the single source of truth**, and its section 7b carries the open work, the standing
> decisions and the traps inherited from the deleted documents.

## Layout

| Project | Role |
| --- | --- |
| `src/DlnaServer.Core` | Domain model, DLNA types, configuration options, abstractions. No dependencies. |
| `src/DlnaServer.Persistence` | EF Core over SQLite: DbContext, entity configuration, migrations, repositories. |
| `src/DlnaServer.Media` | Library scanning, ffprobe metadata extraction, SkiaSharp thumbnails. |
| `src/DlnaServer.Upnp` | SSDP, `description.xml`, SCPD, SOAP services, DIDL-Lite. No EF, no HTTP. |
| `src/DlnaServer.Admin` | Blazor Server admin UI, as a Razor Class Library. Referenced by the Host as of M8; its components are served on the admin port only. |
| `src/DlnaServer.Host` | The ASP.NET Core host. The only executable; nothing references it. |
| `tests/DlnaServer.UnitTests` | NUnit + FluentAssertions. One type at a time, including the tests that pin the protocol bytes, plus the repository tests — those run against a throwaway on-disk SQLite database built by the real migrations (`SqliteTestDatabase`), because the in-memory provider does not enforce the collations, foreign keys and unique indexes they check. |
| `tests/DlnaServer.ArchitectureTests` | NetArchTest and reflection rules that fail the build rather than a review: the layering, the entity/DTO boundary, contract immutability, unique `[LoggerMessage]` EventIds, and the two-key rule on every entity. |
| `tests/DlnaServer.IntegrationTests` | Several real components wired together through DI — real persistence and SQLite, real temporary media folders — with fakes only where a test needs to control something awkward (the clock, options reload, a processor that throws). **No host is booted and nothing uses `WebApplicationFactory`**; the HTTP surface is covered by testing controllers and helpers directly. |

`DlnaServer.Upnp` depends on neither the database nor ASP.NET Core. That is deliberate: it turns plain inputs
into protocol strings, which is what makes byte-exact assertions possible without running a server.

Within those projects, a namespace holds one role rather than everything a folder happened to accumulate.
The folder path always mirrors the namespace:

| Namespace | Holds |
| --- | --- |
| `Core.Contracts` | The repository DTOs, and only those — this is the set the architecture tests hold immutable |
| `Core.Contracts.Scanning` / `.Watching` / `.Processing` | Parameter objects, filesystem events, and media-processing results |
| `Upnp.Soap` | `CustomEnvelopeMessage` alone |
| `Upnp.Soap.ContentDirectory` / `.AvTransport` / `.ConnectionManager` / `.MediaReceiverRegistrar` | One service interface plus that service's response contracts. Actions map 1:1 onto responses, so each namespace is exactly one SOAP endpoint's surface |
| `Host.Upnp.Control` / `.Discovery` | The SOAP service implementations and DIDL mapping, against SSDP advertisement and its two hosted services |
| `Host.Delivery.Caching` / `.Prefetch` | The served-bytes cache, against the backlog that fills it behind a response |
| `Media.Processing.Thumbnails` / `.Provisioning` | Image encoding, against ffmpeg provisioning |
| `Core.Uploads` / `Host.Uploads` | What may be uploaded and where it may land, against the endpoint plumbing that receives it - the split is what lets the admin page ask the same question the endpoint answers |

Moving a response contract between namespaces cannot change the wire, because every one of them pins its own
element name with `[XmlRoot]`/`[MessageContract]` — and `SoapContractWireNameTest` fails the build if a new
one forgets to, since that would silently make the CLR namespace part of the protocol.

`Media.Processing.Provisioning` is deliberately not called `.FFmpeg`: a namespace of that name shadows
Xabe's `FFmpeg` class, so `FFmpeg.GetMediaInfo(...)` stops compiling in every file under it.

## Class name suffixes

The suffix is a contract, not decoration: it says what the type is allowed to do and where it may live. A
type whose suffix does not match its behaviour is a defect, whichever half is wrong. One type per file,
interface and implementation in separate files, and the folder path mirrors the namespace.

| Suffix | What it is | What it must do — and must not |
| --- | --- | --- |
| `*Options` | Configuration bound from `config.json` (`DlnaOptions`, `ServerOptions`, `LibraryOptions`, `ThumbnailOptions`, `FileCacheOptions`, `DatabaseOptions`, `CompatibilityOptions`, `UploadOptions`) | Mutable properties with defaults and `DataAnnotations`; lives in `Core/Configuration`; read through `IOptionsMonitor<T>`, never captured in a field. No behaviour, no dependencies |
| `*Settings` | Options **resolved** for one operation and passed down a pipeline — `MediaProcessingSettings` | Immutable, built once from `*Options` and handed to every item in a pass, so the values cannot shift mid-batch under a config reload. Never bound to configuration itself: that is what separates it from `*Options`, and why it is not a `*Request` either — it outlives a single call |
| `*Dto` | A contract crossing a layer boundary | `sealed record`, `required`, `init`-only — the architecture tests fail the build on a settable property. Lives in `Core/Contracts`. What repositories project into and return |
| `*Entity` | An EF entity — `MediaFileEntity`, `MediaDirectoryEntity`, `ThumbnailEntity`, `ServerInstanceEntity`, `UploadDeviceEntity` | `internal` to `DlnaServer.Persistence`, deriving `EntityBase`; `int Id` internal plus `Guid PublicId` external. May never appear on a public signature. The suffix is what tells an entity from the `*Dto` that mirrors it, and it keeps a navigation property free to take the bare name — `MediaFileEntity.Thumbnail` is of type `ThumbnailEntity` |
| `*Configuration` | EF `IEntityTypeConfiguration<T>` | Named for the entity it configures, suffix included — `MediaFileEntityConfiguration` maps `MediaFileEntity`. One per entity, `internal` to persistence; only mapping — keys, indexes, converters, relationships, and an explicit `ToTable(...)` so the table name never depends on a CLR name |
| `*Repository` | The only way to reach the database | Takes and returns `PublicId` and DTOs, never entities; projects straight into DTOs (`is null` is a compile error in an expression tree — use `!= null`); async with a `CancellationToken` |
| `*HostedService` | A `BackgroundService` registered in `Program.cs` | Guards **per item** first and per pass second, so one bad file cannot end the loop. Resolves scoped services through `IServiceScopeFactory`, never by constructor injection |
| `*Service` | A UPnP SOAP control service — `ContentDirectoryService`, `ConnectionManagerService`, `AvTransportService`, `MediaReceiverRegistrarService` | Reserved for the four SOAP endpoints. **Not** a general-purpose "business service" suffix; operation parameters are PascalCase, deliberately, because SoapCore binds them by element name |
| `*Controller` | An MVC controller | Explicit binding attributes, inline route constraints (`{id:guid}`), and one port via `AdminOrMediaPortEndpointFilter`, which classifies by path prefix - `RequirePortEndpointFilter` now guards only the two health endpoints. Thin: no domain logic |
| `*Response` | A SOAP response contract, one per action | `sealed class` with `[MessageContract]`/`[XmlRoot]` pinning the wire name, and every property documenting what it becomes on the wire |
| `*Result` | The outcome of an operation, success or failure | An immutable record carrying enough for the caller to decide; no exceptions for expected outcomes |
| `*Request` | A parameter object for one call — `BrowseRequest`, `LibraryScanRequest`, `ThumbnailRequest` | Immutable; groups arguments that travel together. Not `*Options`: nothing here is bound from configuration |
| `*Builder` | Turns inputs into a protocol document — `DeviceDescriptionBuilder`, `SsdpMessageBuilder` | Pure and static where possible: inputs to string, no I/O, so the bytes can be asserted |
| `*Mapper` | Translates between layers — `DidlMapper` | Pure functions only. No I/O, no repository calls |
| `*Serializer` | Writes a type to its wire form — `DidlSerializer` | Deterministic bytes; anything it emits is golden-file tested |
| `*Interceptor` | An EF Core interceptor | Observes commands; never changes results |
| `*Converter` | An EF value converter | Pure and lossless in both directions |
| `*Provider` | Supplies a value that cannot be a constant — `LocalAddressProvider` | Read-only; the value may change between calls |
| `*Scanner` / `*Indexer` / `*Watcher` | Discovers files, reconciles them into the index, reacts to filesystem events | Scanner reads the filesystem only; indexer owns the reconciliation; watcher only reports |
| `*Processor` / `*Generator` / `*Provisioner` | Media work: metadata extraction, thumbnail encoding, ffmpeg provisioning | Returns `null` for "could not", never throws for bad input — they are fed arbitrary user files |
| `*Cache` / `*Store` / `*Registry` / `*Backlog` | Holds state for longer than a request | Registered as a singleton; bound by real bytes where it holds payloads; thread-safe |
| `*Validator` / `*Guard` / `*Defaults` | Startup correctness — refuse to boot, recover a broken file, fill a blank setting | Run in that order: defaults, then validation. Failure messages name the setting and what to do |
| `*Middleware` / `*Filter` / `*Attribute` | ASP.NET pipeline pieces | `Host` only; no domain logic |
| `*Extensions` | Extension methods, in a `static` class | Pure and deterministic only. Anything touching I/O, time or DI belongs on an injectable type instead |
| `*Catalog` | One lookup table — `DlnaMimeCatalog` | Static and immutable; the single source for that mapping |
| `*.Log.cs` | The `[LoggerMessage]` partial beside its type | Same folder and namespace as `Foo.cs`; unique `EventId` per class; the declared level must match what the doc comment claims |
| `*Test` | A test fixture, named for its subject | `internal sealed`, `[TestFixture]`, Arrange/Act/Assert, and every assertion carries a `because` reason |

**`*Options` now means one thing only: bound from `config.json`.** The two parameter objects that used to
wear it are `BrowseRequest` and `LibraryScanRequest`, and the stream entities have dropped their redundant
`Info` — `AudioStreamEntity` rather than `AudioStreamInfoEntity`, since `*Entity` already says what the
type is. `DlnaMimeInfo` keeps `*Info` legitimately: it is a plain value, not an entity.

## Build and run

```bash
dotnet build DlnaServer.sln     # must stay at zero warnings
dotnet test  DlnaServer.sln
dotnet run --project src/DlnaServer.Host
```

Set `Dlna.Library.SourceFolders` before the first run. The shipped `config.json` sets `/share/Media`, and
with no source folder configured at all `DlnaOptionsDefaults` falls back to the application's own folder and
warns rather than refusing to start — so a fresh deployment comes up serving the wrong thing rather than
not coming up. See "Configuration" below for what the validator does refuse.

Deployment to the NAS is `./NasBuild.sh`; see `NasBuild.usage.txt`.

## Ports

| Serves | Code default | Shipped `config.json` |
| --- | --- | --- |
| UPnP/DLNA and media streaming. Advertised to renderers. | 26851 | **26852** |
| Blazor admin UI. | 26852 | **26853** |

The shipped file is what actually runs: `config.json` sets `Dlna.Server.Port` and `Dlna.Server.AdminPort`
one above the `ServerOptions` defaults, so this server can run alongside the reference implementation on
26851. Read the live ports out of `config.json` rather than assuming the defaults.

One process, two Kestrel endpoints. Every endpoint is bound to exactly one port, so a TV on the media port
receives a 404 for anything on the admin surface. The two ports must differ or startup fails.

## Configuration

`config.json` sits beside the binaries, binds to `DlnaOptions` (section `Dlna`) through the normal
`IConfiguration` pipeline, and is consumed via `IOptionsMonitor` so most edits apply without a restart.

With **no source folder configured the application's own folder is served**, so a fresh deployment starts
rather than refusing to boot. The thumbnail cache and `Resources` are excluded from that fallback library,
because otherwise the generated thumbnails and the device icons would be indexed as media. A warning names
the folder it fell back to.

`DlnaOptionsValidator` then refuses to start the server on: a source folder that does not exist, equal media
and admin ports, a per-file cache limit above the total cache budget, a blank `Thumbnails.SubFolderName`, a
thumbnail cache directory inside a source folder that no `ExcludeFolders` entry covers, or an
`Upload.DestinationFolder` that is not inside one of the source folders.

`Library.ExcludeFolders` carries no default on the property itself. `ConfigurationBinder` **adds to** a
non-empty `IList<string>` rather than replacing it, so a default declared there appended itself to whatever
`config.json` named - and because the admin UI writes the bound list back, the file grew by two entries on
every save. `DlnaOptionsDefaults` seeds `.@__thumb` and `@Recycle` after binding, only when configuration
names none of its own, and de-duplicates case-insensitively either way, so an already-doubled file heals on
its next load.

Thumbnails are written beside the media they describe, into `Thumbnails.SubFolderName` (`.@__thumb`) - so
`Films/Film.mkv` is previewed by `Films/.@__thumb/Film.mkv.jpg`, which is where the reference puts them and
where an existing library already has them. One that is already there is adopted rather than regenerated.
The name is **required**, and it is added to `ExcludeFolders` after binding whether or not an operator
listed it: a scan that does not skip that folder re-ingests every preview as media and then previews those.

## Uploading

**Off unless `Dlna.Upload.Enabled` is on, and turning it on needs a restart** - the endpoint is created
while the application is being built or not at all, so a server with uploads off has no route that accepts
a file rather than a route that checks a flag. It is the only part of this server that writes into a source
folder.

With it on, `/admin/upload` posts a plain `multipart/form-data` form to `/admin/upload/files`, which
streams each part straight to disc. There is no JavaScript and no SignalR circuit in that path, so it works
the same from an Android phone, a Windows laptop and a Linux desktop, and a film is not moved through the
server's memory in 32 KB messages.

| Setting | What it does |
| --- | --- |
| `Upload.Enabled` | Whether the endpoint exists. Needs a restart |
| `Upload.DestinationFolder` | Empty means any source folder may be chosen; a full path pins uploads to that one folder, which must be inside a source folder |
| `Upload.MaxSizeInMegabytes` | Refuses one file larger than this. Per file, not per upload |

A file is taken only if its extension is one the library would index - `Library.MediaFileExtensions` first,
then the built-in catalog - so nothing can land in the media tree that the server would not serve. The
destination is re-derived from configuration rather than trusted from the form: the root has to be one of
the offered folders, and a sub-folder has to be relative, free of `.` and `..`, inside that root, and not a
folder `ExcludeFolders` hides. Each file is written as `<name>.uploading` and moved into place once every
byte is down, so a scan running mid-upload walks past it. The operator chooses per upload whether an
existing file is replaced or left alone, and the result page lists every file and what happened to it.

The upload page prefills the folder last used - from a cookie, and from a row in `UploadDevices` keyed by
the device when the cookie is gone. **Every file is also written to `logs/uploadSecurity.log`**: address,
user agent, language, device, destination, name, size and outcome. That file exists for security review
rather than diagnosis, which is why it records more about the sender than the feature needs.

## ffmpeg is optional, and everything it does goes missing quietly

Indexing, browsing and streaming need no ffmpeg. **Video and audio metadata and video thumbnails do** -
durations, resolutions, codecs, audio tracks and subtitle streams all come from ffprobe, and the search
page's language filters are built from what those streams reported. Image metadata and image thumbnails
use SkiaSharp and are unaffected.

`FFmpegProvisioner` looks for `ffmpeg` and `ffprobe` in an `ffmpeg` folder beside the binaries, and
fetches them only when `Thumbnails.DownloadFFmpeg` is on - it ships **off**, because that path downloads
and executes an unverified binary from a third-party API. Absent binaries are therefore not an error and
not fatal; the server runs, and the library simply has no video metadata in it.

That silence cost a whole library once, so it is now visible in two places: `IMediaCapabilities`
(`Core.Diagnostics`) reports what the provisioner resolved, and the dashboard raises a warning when the
answer is a definite no. It is `bool?` on purpose - availability resolves on first use, so `null` means
nothing has needed ffmpeg yet, and reading the property never triggers the resolution.

After putting the binaries in place, the already-indexed files need their metadata read: *Recreate
metadata* on the Maintenance page, or `POST /manage/recreateAllFilesInfo`.

## Rebuilding the index

The Maintenance page carries two rebuilds, each behind a two-click confirm. Both throw the index away and
scan the source folders again - the index is derived data, so no media is at risk; what they cost is the
metadata, which ffprobe reads again over every file. Thumbnails already beside the media are adopted.

- **Rebuild index** deletes every row, runs `VACUUM`, and asks for a scan. No restart, and the page stays
  connected to report what it removed. This is the one to reach for.
- **Recreate database** deletes the database file and restarts the server, which rebuilds the schema from
  the migrations and scans on the way up. For when the file itself is the problem.

**The file is deleted at startup, not when the button is pressed.** The safe moment to remove a database
file is when nothing holds it open, and a request handler is the opposite of that - other scopes'
`DbContext`s, the pooled SQLite connections and any renderer query in flight would all point at a file
that had just gone, and SQLite would quietly create an empty replacement with no schema. So
`IDatabaseResetSignal` carries the request across the restart the way `IRestartSignal` does, and
`DatabaseInitializer` acts on it before it opens anything - deleting the `-wal` and `-shm` sidecars too,
because a stale write-ahead log is replayed into the new file and brings the old rows back.

`ILibraryScanSignal` is what lets the admin UI start a scan at all: scanning lives in the host, and the
admin project references only Core and Persistence. Repeated requests collapse into one pass, and the
click never awaits it - a pass over a large library takes minutes.

## One library, whether it is a television or the admin UI asking

Every **listing** the repositories answer - source roots, a folder's children, recently added, and both
searches - hides the same two things, and there is no flag to ask for more:

- anything whose path matches an entry in `Library.ExcludeFolders`, and
- any folder whose subtree holds no visible file.

So the admin pages and a renderer cannot disagree about what the library holds. The second rule is what
keeps QNAP's own `.streams` and `.@upload_cache` off a television, and it also covers the folder left empty
by hiding the only child that held media - which takes effect on the next request, with no rescan.

Two things are deliberately **not** filtered. Lookup by identifier or by path still returns hidden content,
so a renderer mid-stream is not cut off and a hidden file's page still opens from a link someone already
has. And the indexer's own reads are never filtered: `GetExistingPathsAsync` decides what to insert, so
hiding an indexed row from it would make the next scan re-insert the path and violate the unique index -
and `GetIndexedPageAsync` decides what to delete, so filtering it would strand a hidden file's row after
its file was deleted from disc.

`ILibraryScanner.EnumerateDirectories` yields only the folders that lead to media - each folder holding an
indexable file, plus its ancestors up to the source root, which is always yielded. An indexed folder the
scan no longer offers is therefore removed by reconciliation, and because the same set decides insertion,
nothing is pruned on one pass and re-added by the next. A folder `ExcludeFolders` hides is exempt from that
removal: hiding is meant to retire a folder without discarding its rows, so adding a name to that setting
never costs the subtree's metadata and thumbnails.

> The schema is **not** compatible with the reference's flat `config.json`. Settings are grouped into
> `Server`, `Library`, `Thumbnails`, `FileCache` and `Compatibility`. Port values across by hand.

## The served-bytes cache

Its purpose is acoustic, not throughput: the NAS drives are mechanical and audible in the room, so a file
already sent should not wake a spun-down disc to be read again.

**It is sized against the machine, not against a constant.** `FileCache.MaxTotalSizeInMegabytes` (1 GB) is
additionally clamped to **half** of what the machine reports, and `MaxFileSizeInMegabytes` (512 MB) to half
of whatever budget that yields. Production servers have 2-4 GB in total, so on the smallest supported
machine the clamp and the configured value agree at 1 GB rather than one silently overriding the other.
This is a deliberately generous share: the acoustic goal is what the memory buys, and a spun-up drive is
the cost of being frugal here.

The startup line reports all three figures - configured, available and resolved - because the resolved
budget is not always the configured one, and without the comparison a clamped value looks like a bug:

```
Served-bytes cache holds up to 1024 MB (configured 1024 MB, machine reports 39907 MB)
```

A file above the per-file cap is never read into memory at all; it streams from the disc with range
support. `IServedFileCache` exposes `BudgetInBytes` and `MaxFileSizeInBytes` so the resolved values are
observable rather than inferred.

A cached film is one contiguous 512 MB array on the large object heap, and that churn is what took the
working set to 5073 MB on a box reporting 40 GB free, where the GC had no reason to compact. The budget and
the per-file share bound how many such payloads can be held at once; whether that is enough on a 2 GB
machine is a question for a measured constrained run, not for a default - see `PLAN.md` section 3b.

**Payloads are `ReadOnlyMemory<byte>` and are served with `AsStream()`** from
`CommunityToolkit.HighPerformance`. There is no `File(ReadOnlyMemory<byte>, …)` overload, and the
alternative — `ToArray()` — would copy the entire payload on every range request; a renderer issues one of
those per range. The cache therefore hands out a view over its own buffer, which is also what leaves it
free to hold a slice of a larger buffer later without changing a single caller.

**Concurrent callers for one path share one read.** The first starts it, the rest await the same operation,
so 32 renderers asking for one thumbnail at the same moment cost one disc read and one allocation instead
of 32 of each — measured, and the reason it matters on a 2-4 GB box is that duplicate payloads multiply
the transient peak. The shared read deliberately ignores the individual caller's `CancellationToken`, since
one request going away must not abandon the read the others are waiting on.

**What it holds is observable.** `GET /manage/filecache` reports the resolved budget, bytes held, entry
count, hit and miss counts, reads in flight, and **every cached path** - the last of these being the point,
because a byte total cannot distinguish a cache doing its job from a leak. `GET /manage/filecache/clear`
drops every payload and forces one compacting collection: cached payloads above 85 KB live on the large
object heap, which is reclaimed only on a Gen2 collection and never compacted by default, so clearing
without collecting would report success and hand nothing back to the operating system. That is the only
place in the application that forces a collection, and only ever on an operator's request.

Do **not** pool these payloads. Handing MVC a stream over a rented buffer would let eviction return that
buffer to the pool while a response is still writing it; GC-owned memory cannot fail that way, because the
stream holds a reference.

## Admin UI

Blazor Server, in the same process, on the admin port at **`/admin`**, which that port's root redirects
to. No login: like the `/manage`
endpoints, it assumes a trusted internal network.

| Page | What it does |
| --- | --- |
| `/admin` | Memory in use, the server's own share, large-item share, tidy-up counts, cache occupancy, library counts. Every figure is kept; the labels are operator-facing rather than CLR vocabulary |
| `/admin/library` | Browses source folders and their contents as thumbnail tiles, with an Up button and a clickable breadcrumb trail. The root also lists the newest files in the library, below a rule - the same set a renderer is offered beside the source folders. Inside a folder, a panel clears or recreates metadata and thumbnails for that folder, optionally through every subfolder, and each file tile carries the same four actions for itself. Folders, files and recently-added each fold away, keeping their count in the heading, and a section shut stays shut while walking the tree |
| `/admin/preview/{folder}/{file}` | Plays a video or track, shows an image at its own size, lists metadata - including **every audio track** with its language, title and default flag - and steps to the next or previous **playable** item in the folder |
| `/admin/library/search/files` | Finds files by name (case-insensitive), kind, exact MIME type, modification date range, size range, minimum video width, codec, and **audio or subtitle language** - the languages offered are those the index holds, several may be selected, and filters combine with AND. The filter panel collapses to a single bar, naming what is still set, so a long result list gets the height back. Also answers on the original `/admin/library/search`, so an existing link still resolves |
| `/admin/library/search/folders` | Finds folders by name or by any part of their full path, both case-insensitive - so an ancestor's name finds everything beneath it. The filter panel collapses, as on the file search |
| `/admin/cache` | Titled *Recently served files*. Cache occupancy, how much was served from memory, every file held, and *Empty the memory* (which also compacts) |
| `/admin/upload` | Titled *Add files*. Copies media from the browser into a library folder - destination, optional sub-folder, replace-or-skip, and a report of every file afterwards. Only listed when uploads are on |
| `/admin/settings` | Every setting with a description of what it does, and Save. **Check** beside the source-folder box reports every path it is given - missing, a file, not absolute, or present but unreadable - rather than stopping at the first bad one |
| `/admin/maintenance` | Rebuild file details or previews for the whole library, pause or resume television traffic, list waiting devices, and restart or stop the server - the last two behind a two-click confirm. To discard without making it again, use Clear in the Library |
| `/admin/settings/temporary` | The settings that are **never written to `config.json`**, each running for a number of minutes and then lapsing: reveal the temporarily hidden folders, and label every figure on these pages with where it came from. A restart clears both |
| `/admin/help` | The operator's manual - what each page does, what each Maintenance button costs, and what to try when a television cannot see the server. Plain panels rather than folding ones, so the browser's own Ctrl+F finds everything |
| `/admin/about` | What this build is - version, runtime, machine - what the televisions are told about it, and every program library loaded, with versions, in a group that starts shut |

**Any figure can say where it came from.** While the period on `/admin/settings/temporary` is running,
hovering a value shows its source in one grammar across every page: a runtime metric by its name
(`Working Set (MB)`), a setting by its key path (`config.json Dlna.Server.ManufacturerUrl`), an indexed
value by its column (`database Files.SizeInBytes (MB)`). It is CSS only - the admin UI still ships no
JavaScript - and off by default, because it underlines a large part of every page.

**Delivery is identical on both ports.** `MediaContentResolver` is the single implementation behind
`/fileserver` and `/admin/media`, so a film watched through the admin page is served from memory when it is
held and queued to be read into memory when it is not - exactly as one watched on a television. Having the
two differ would make the acoustic goal depend on which door the request came through. Thumbnails likewise
go through the cache from either port.

**The UI never links to the media port.** `AdminMediaController` serves previews and thumbnails from
`/admin/media/*` on the admin port, reading the same sources as the renderer-facing controller — the cache,
then the database copy, then the file. So an operator's browser never learns that the renderer surface
exists, and the UI keeps working where only the admin port is reachable. It is a second door onto the same
content rather than a forwarding hop: proxying over HTTP to the other port would leave that port just as
reachable while adding a round trip.

**Isolation is enforced by middleware, not by an endpoint filter.** `AdminSurfaceMiddleware` 404s
`/admin`, `/_blazor`, `/_framework` and `/_content` when they arrive on the media port. An endpoint filter
cannot do this job for Blazor: filters run in the routing pipeline that controllers and minimal APIs use,
and Razor component endpoints are not dispatched through it — applying one to `MapRazorComponents`
compiles, reads as though it works, and was measured doing nothing at all, with every admin page still
answering 200 on the media port.

Search escapes `%` and `_` before building its `LIKE` pattern. Filenames are full of underscores, and an
unescaped one matches any single character - so without that a search would return rows not containing the
term at all, which is worse than returning none. Case folding is SQLite's own, which covers ASCII: `naruto`
finds `Naruto`, but an accented capital is not folded.

Folders in the library are plain bold links rather than tiles - a folder has no image, so a card gave a
placeholder the same visual weight as a real preview.

Thumbnails are addressed by the **thumbnail's** own identifier
(`MediaFileDto.ThumbnailPublicId`), not the media file's - the repository looks them up by
`Thumbnails.PublicId`, and passing the file's identifier produces a 404 that looks exactly like a
thumbnail that has not been generated yet. An image with no thumbnail yet is shown as its own preview.

**Settings are written to `config.json`**, which the configuration provider watches — so a save both
persists the change and applies it. The previous file is copied aside first. Settings that cannot take
effect until a restart (the two ports, and the cache's total budget) say so on the page rather than
implying otherwise.

The palette is sampled from the server's own device icon (`Resources/images/icons/large.png`), which is
overwhelmingly `#b9e1fa` with `#9cd4f9` as its saturated edge — so the admin pages and the icon a
television shows for this server read as the same product.

## Management endpoints

Every `/manage` endpoint is **unauthenticated**, on the media port, and that is a stated decision rather
than an oversight: the server is for a trusted internal network. On anything else this controller needs a
guard before anything else does — `stop`, `restart` and the `clearAll*` family are all reachable by
anything that can browse the library.

| Endpoint | Purpose |
| --- | --- |
| `memory` | Process and GC memory, including per-generation size and fragmentation |
| `filecache` | Budget, bytes held, entries, hits/misses, and **every cached path** |
| `filecache/clear` | Drops every payload and forces one compacting collection |
| `configuration` | The `DlnaOptions` actually in force, after defaults, env vars and hot reload |
| `database` | Row counts for files and directories |
| `subscriptions` | Live GENA subscriptions |
| `file?after=&take=` | A page of indexed files, ordered by path |
| `file/{id}` | One file with its audio, video, subtitle and thumbnail records |
| `fileLast?count=` | The most recently indexed files |
| `directory?after=&take=` | A page of indexed directories |
| `directory/{id}` | One directory with its children and its files |
| `videoMetadata/{id}` | The stream metadata of one file |
| `thumbnail?after=&take=` | A page of generated thumbnails |
| `thumbnail/{id}` | One thumbnail's record |
| `thumbnailData/{id}` | The stored bytes of one thumbnail |
| `dlnaMime` | The compiled-in MIME catalogue with profiles and extensions |
| `clearAllMetadata` | Forgets extracted metadata so it is read again |
| `clearAllThumbnails` | Removes thumbnail records so they are produced again |
| `recreateAllFilesInfo` | Both of the above, in one call |
| `recreateFilesInfo/{id}` | The same for one file |
| `block/{hours}` / `unblock` | Refuses renderer traffic for a period, and lifts it |
| `stop` / `restart` | Stops the process, or rebuilds the host in place |

Five places diverge from the reference deliberately, each because its version does not survive a real
library:

- **Listings are paged by `after`, not by offset.** An offset shifts when a row is inserted ahead of it,
  and a full scan inserts thousands. The reference returns every row in one response, which on 25,501
  files is tens of megabytes.
- **`thumbnailData` takes an identifier.** The reference dumps every stored blob in the library at once —
  on a previewed library that is gigabytes of base64, and on a 2-4 GB machine it is an outage, not a
  diagnostic.
- **`block/{hours}` returns immediately.** The reference awaits `Task.Delay(hours)` *inside* the request,
  so the call hangs for hours and the block is lost if that connection drops. Here the block is state with
  an expiry, and `/manage` is exempt from it — otherwise a block could not be lifted without restarting.
- **`recreateAllFilesInfo` returns as soon as the records are cleared.** Regeneration is the background
  processing pass's job, one file at a time. The reference holds the request open for the whole rebuild
  and blocks the entire API while it runs.

`clearAllThumbnails` leaves the image files beside the media in place. They are the reference's layout, a
rescan adopts them, and deleting them would turn a cheap reset into a full ffmpeg pass over the library.

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
  not the source template.
