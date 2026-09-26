# DlnaServer - status and history

What has been built, what is deployed, and the batch-by-batch record of every change with its reasoning.
Append-only in practice: an entry here describes the moment it was written.

Its sibling is [decisions.md](decisions.md), which carries the conventions, the standing decisions and
the traps - read that one before working. Split out of `PLAN.md` on 2026-09-23.

**Section numbers are unchanged from `PLAN.md`** on purpose, so a reference to "section 6n" still
resolves.

## Where things stand

The server runs on the QNAP NAS and a television plays from it. Milestones 1 to 10 are complete, the
build is at 0 warnings, and **798 tests pass** (375 unit, 402 integration, 21 architecture).

**The NAS is behind this tree.** Checked 2026-09-15 against `http://192.168.1.100:26853/admin/about`,
which answered and reported version `1.0.0` - so it carries everything through section 6m and not 6n
onwards. That one request is the cheapest way to ask again later. Deploying is not a routine redeploy:
see "Deploying is not a routine redeploy" in section 8 of [decisions.md](decisions.md).

What is owed needs a person, a television or a quiet server rather than a patch, and the list lives in
section 7b of [decisions.md](decisions.md).

---

## 0. Status notices, 2026-09-02 to 2026-09-15 - SUPERSEDED, kept for what they record

This was the preamble of `PLAN.md`: eighteen months of "READ THIS FIRST" blockquotes laid down over each
other, several of which already said they were superseded by the ones below them. It is kept because it
records real findings - the stale `config.json` on the NAS, the deployment checks, the reasoning behind
decisions taken at the time - and deleting it would lose them.

**Do not read it as current status.** "Where things stand" above is current; this is the record of how it
got there, newest layer first, and every dated claim in it should be read with its date attached.

# DlnaServer rewrite — working plan

> **START HERE. This file is now the only project document.** On 2026-09-08 the review history, both
> worklists and both session records were deleted; everything from them that was still live - open work,
> standing decisions and traps - is in **section 7b**. Read that before doing anything, and read
> section 7 for the conventions.
>
> **Deployed and measured.** The build runs on the NAS, working set 563.4 MB → **177.7 MB** after the
> memory pass (section 3), and a television plays from it. **789 tests pass at 0 warnings** with no
> vulnerable packages. The NAS still runs the build before `1.1.0915` - section 6n is not deployed yet. The memory work turned on a finding worth remembering: the NAS was running a
> **stale `config.json`**, which `NasBuild.sh` preserves across every redeploy, so every memory fix
> shipped as a config change had never once executed there.
>
> **A third `/review-all --full` and its five-batch fix pass ran on 2026-09-08 - section 6i, read it
> before the older review sections.** It found two Blockers in the configuration-reload path: a failed
> or missing re-read blanked every setting and the next scan cascade-deleted the index, and one
> invalid value poisoned `IOptionsMonitor` for the life of the process. Both are fixed and tested.
> **The `Migrations/` folder was squashed to a single `InitialSchema`**, which means the next deploy
> discards the NAS database and rescans - deliberate, and costed in 6i.
>
> **DEPLOYED 2026-09-08 ~17:43 UTC and verified live on 192.168.1.100** - the server starts, reads the
> real `config.json`, rebuilt its database to 25,596 files, serves a correct SOAP Browse, and an LG
> television plays films, photos and music from it. Section 6i carries what was checked and how.
> A `FileCreatedUtc` anomaly spotted during that check was chased down and is **not a defect** - it
> is what `Library.UseFileCreationDateTime: false` means. Section 6i records the evidence and the
> reasoning so it is not re-investigated.
>
> **What is owed.** Two things need a person or a quiet server, not a patch: confirmation of the four
> DIDL fields **on a real television** (they are on the wire - read off the live server on 2026-09-08 -
> so what is left is whether a set honours them), and a **quiet memory reading** once the metadata pass
> finishes. One needs a database: **W9**, whether the two `Language` indexes earn their keep, which is
> two `EXPLAIN QUERY PLAN` statements written out in section 7b. And one needs the application running:
> **the two Blockers from 6i are config-shaped**, so a green build and a green suite do not verify them -
> section 7b lists the exact steps.
>
> **The preview player now says why a file will not play** rather than showing a dead one - 679 files,
> `.avi` mostly. The server and the configuration were both correct all along; standing decisions 20 and
> 21 carry the proof, so do not go looking for a MIME bug.
>
> **Everything else from the review documents is closed.** The 2026-09-08 pass fixed B3, B4, B6, B7, B8,
> B9, B10 and B12, decided W6 and W10, added the missing `.AsSplitQuery()`, and covered the M2
> unreadable-folder guard that had been called untestable. **It is deployed and live** - the maintainer redeployed
> at 12:34 local (10:34 UTC) on 2026-09-08, confirmed by `/manage/thumbnail` answering with the
> `nextAfter` and `mediaFileFullPath` that only the B8 fix produces.
>
> **Four findings from the third pass were deliberately NOT applied**, and one of them was simply wrong.
> They are standing decisions 22-27 and the closing part of section 6i: UPnP error 701, the SSDP
> local-address resolution, the `LibraryScanner` walk rewrite, the retroactive `GenerateFor*` re-enable -
> and the `DLNA.ORG_OP` rename, which was a **false positive** that would have introduced the very
> inversion it reported. Do not re-raise any of them without reading why.

> **Five admin-UI asks landed 2026-09-12 - section 6m**, with the overlay's zoom corrected the same day
> after the maintainer found it enlarging photographs smaller than the screen and calling that 100%, and **corrected
> again on 2026-09-14**: fitted and zoomed are now mutually exclusive modes rather than one expression
> setting `zoom` and `max-width` together, which moved the cap with the level and squashed a growing
> picture. A photograph opens larger on a click: an overlay
> with zoom on a desktop, `/admin/photo/{id}` on a phone, and **CSS decides which**, so there is no
> user-agent sniffing and no JavaScript. `ProcessingActions` now starts shut on both pages, and on the
> preview page it sits above the file facts. **Reset reopens the filter panel, reversing the 2026-09-10
> decision in section 6k** - do not restore it without reading 6m. Two new pages, `/admin/help` and
> `/admin/about`, and a copyright line at the foot of the sidebar. Nothing on the renderer path changed.

**This file is the single source of truth for the rewrite.** It is written so that work can resume from
this file alone after a disconnect or a lost session. Update the status table and the milestone notes as
each piece of work finishes.

Last updated: 2026-09-23 (sections 6p to 6s; version `1.1.0923`). The line below is from 2026-09-15 and
is part of the superseded block - the newest work is section 6s.

Last updated: 2026-09-15 (section 6n - uploading, folds, provenance tooltips; version `1.1.0915`).
**Read section 6n first for the newest work**, and section 6i for the third
`/review-all --full` and the five-batch fix pass that followed it, including the two configuration-reload
Blockers and the migration squash. **Section 6h** carries the last of the review documents being closed.
Sections
**6c to 6g** carry the detail of the work since 2026-09-03: one
library for both surfaces, folders with no media gone from the index and from every listing, the
exclusion list no longer doubling on every save, the thumbnail sub-folder required and always skipped by
scanning, ffmpeg's absence made visible, the Maintenance page's two rebuilds, and — in **6g** — the
move/rename fix, audio cover art, the admin tile icons and the open-ended file-metadata feature.

**THREE `/review-all --full` passes have run**: two on 2026-09-03 with a deduplication pass the same day,
and a whole-tree one on 2026-09-08 whose findings and fixes are section 6i. Round 1's
53 findings, round 2's 32 and the dedup pass's 12 defects are now **all closed but one**, and round 3's
are closed except the four in section 6i that were deliberately not applied: W9 is what remains, and needs
a query plan read off a real database. W6 and W10 were closed as decisions rather than patches, and both
decisions are recorded in section 7b so they are not re-raised. The reports themselves were deleted on
2026-09-08.

**Do not trust a test count written in a document** — re-run `dotnet test`. It has moved repeatedly
(477 → 488 → 487 within one hour, 534 on 2026-09-04, 586 then 664 then 697 on 2026-09-08).

**The build is deployed and was verified live** — media port 26852, admin 26853, zero errors since
start, `config.json` read correctly, and Browse past the end of a container answers 200/0 instead of
tearing the connection.
**And `ffmpeg` is now present on the NAS**, so the item described below as the largest open one in the
repository is closed - see the note on section 6d.

### Deployment state at the end of 2026-09-03 - READ THIS BEFORE TRUSTING THE NAS

The NAS was deployed **once**, mid-session, so it is running the middle of the day's work, not the end.

**Deployed and confirmed live** (section 6c): the exclusion-list duplication fix, one library for both
surfaces, empty folders pruned at index time and hidden at read time, and the search-files scroller. The
first scan after it removed **7001 directories and 0 files** - `index now holds 25504 file(s) in 1055
directory(ies)`, against 8056 directories before. The unchanged file count is what proves the pruning did
not cascade into content, and `excludeFolders` now reads four entries with no repeats.

**Built, verified locally, NOT deployed:**

- the language-filter empty state and the dashboard's ffmpeg warning, plus `IMediaCapabilities` (6d)
- the Maintenance page's *Rebuild index* and *Recreate database*, plus `IDatabaseResetSignal`,
  `ILibraryScanSignal` and `IIndexMaintenance` (6e)

How to tell which build is answering, without guessing - the older build has no such panel:

```bash
curl -s http://192.168.1.100:26853/admin/maintenance | grep -c 'Rebuild from scratch'
```

**The one thing that matters more than any of it: ffmpeg is missing on the NAS, so the library has no
video metadata at all** - no durations, resolutions, codecs, audio tracks or subtitles across 25,504
files, and that is why the language filters had nothing to show. It needs a decision from the maintainer rather
than code. **Section 6d, "What is still outstanding".**

**A full `/review-all --full` pass was run and its findings fixed (2026-09-02).** 15 reviewers over the
whole solution; every Blocker, Major, Warning and Suggestion is addressed. The changes with consequences
for anything already deployed:

- **`ExcludeFolders` now hides content on READ, not only during import** - a folder can be retired from
  the library without deleting rows or rescanning. Delivery by identifier is unaffected, and the
  indexer's own reads are never filtered (filtering `GetExistingPathsAsync` would make the next scan
  re-insert every hidden path and violate the unique index).
  **The opt-in half of this is SUPERSEDED 2026-09-03** - it read "opt-in per call site: DLNA browse and
  media processing hide, the admin UI and `/manage` still show everything". Hiding is now unconditional
  on every listing, the `excludeHidden` parameter is gone from both repository interfaces, and the admin
  UI is not exempt. See section 6c.
- **Two migrations.** `ThumbnailContentOwnedByThumbnail` inverts the thumbnail/blob foreign key so
  deleting a thumbnail deletes its bytes - the cascade ran the wrong way, so every deleted thumbnail and
  every deleted media file orphaned a blob in a database that is never vacuumed. It backfills, then
  **deletes the orphans already accumulated**.
- **Every mutating `/manage` endpoint is now `POST`.** They were `GET` on the renderer-facing port, so
  `<img src="http://nas:26852/manage/stop">` on any page an operator visited would stop the server, and a
  link unfurler or browser prefetch would do it by accident. No in-repo caller changed; external scripts
  and bookmarks will need the verb.
- **`config.json` changed materially**: `mmap_size` 64 -> 0 MB, `cache_size` 32 -> 8 MiB,
  `temp_store=MEMORY` off, `FileCache` 1024/512 -> 256/32 MB, `DownloadFFmpeg` -> false,
  `UseFileCreationDateTime` -> false. Together those remove ~96 MB of committed native memory and the
  1 GB cache authorisation.
- **`DownloadFFmpeg` now defaults OFF.** Video metadata and video thumbnails need ffmpeg; ship the
  binaries with the deployment or turn it back on knowingly. It downloaded and executed an unverified
  binary from a third-party API.
- **`UseFileCreationDateTime` is honoured** and shipped as `false`, so `CreatedUtc` is indexed-time. That
  changes "recently added" ordering for the existing index until the next rescan - and is what makes it
  work at all for an imported library.
- **`DatabaseInitializerService` is renamed `DatabaseInitializerHostedService`** and derives from
  `BackgroundService`.

**Resuming after a context reset, read these six things in this order:**

0a. **Section 7b FIRST** - the open work, standing decisions and traps inherited from the review
    documents deleted on 2026-09-08.
0b. **Parts of the sections below describe code that has since changed.** 53 review findings were fixed
    after they were written - the source-folder gate, the options validator, the hosted-service ordering,
    the SSDP services, the file watcher, the thumbnail rebuild path and the byte cache all behave
    differently now. **The source is authoritative where they disagree**; the record that used to hold
    the detail was deleted on 2026-09-08.

0. **"Deployment state at the end of 2026-09-03" at the top of this file, and then section 6d.**
   **Both are now superseded on the two points that mattered.** The NAS runs the post-review build, and
   **ffmpeg is present and generating video thumbnails** - it downloaded at 14:08 once
   `Thumbnails.DownloadFFmpeg` was turned on. So the "largest open item needing a decision, not code"
   described below is closed; what remains of it is turning that switch back off, per finding 8. Read
   6d for the reasoning it records, not for the state. Section 8's "Do these next" now starts with it. The older "Deployment state at the
   end of 2026-09-02" note further down is **superseded** - keep it for the traps it records, not for what
   is running.
1. **Section 3b's fifth reading.** The memory question is no longer open in the way this file used to say.
   The cache holds **906 MB in four entries - three films and one icon, with every thumbnail evicted** - so
   it is now defeating its own purpose, not merely costing memory. "Cache thumbnails and static assets
   only" has gone from the cheapest of three options to the only one that does the job. Section 8 item 1 is
   now a confirmation rather than a decision.
2. **Section 7 -> "The admin UI's shared components".** Eight components now carry markup that used to be
   copied - a tile, a stat, a folder list, a form field, a collapsible panel and more. Reach for one before
   writing that markup again.
3. **Section 7 -> "Test traps found the hard way"** before writing a repository test. Three of them cost
   real time in that session, and one is invisible on the NAS because it only shows on Windows.

M1-M8 are complete; the build is at **0 warnings**. The test count is in flux while Admin work is in
flight - re-run `dotnet test` rather than trusting a number here.

---

## 1. Context

The reference implementation is a **working** DLNA/UPnP MediaServer (v6.1) that streams media from a QNAP NAS
to TVs. It is **read-only** — never modify anything under it. It is the behavioural specification: what
goes on the wire must stay compatible so the TVs that work today keep working.

**It no longer lives in this repository.** It was a `Reference/DLNAServer/` folder here until 2026-09-21,
when this repository was flattened and prepared for GitHub; its own repository, `T:\repos\DLNAServer_Legacy`, carries
the identical source. Every `Reference/...` path below is a path inside that repository.

**Always check the Reference implementation before changing behaviour.** It is in production and its
oddities are usually load-bearing. Read first, then decide deliberately whether to keep or diverge.

The rewrite lives at the repository root. It sat in a `Current/` subfolder until 2026-09-21, so a
`Current/x` path in the history below means `x`. It is a redesign, not a port.

---

## 4. Status

| # | Milestone | Status |
| --- | --- | --- |
| 1 | Skeleton — solution, options, logging, two-port Kestrel | **DONE** |
| 2 | Persistence — model, migrations, repositories, DTO boundary | **DONE** |
| 2a | Entity/DTO split, int Id + Guid PublicId | **DONE** |
| 2b | Database corruption recovery | **DONE** |
| 2c | Architecture governance tests | **DONE** |
| 2d | Config resilience — backup + recreate on corrupt `config.json` | **DONE** |
| 3 | Indexing — scanning, reconciliation, file watcher | **DONE** |
| 4 | Media processing — ffprobe metadata, thumbnails, background queue | **DONE** |
| 5 | UPnP core — description.xml, SCPD, SSDP, Browse + DIDL-Lite, golden files | **DONE** |
| 6 | Delivery — streaming with range + DLNA headers, byte cache, icons | **DONE** |
| 7 | Remaining SOAP + GENA | **DONE** |
| 8 | Admin UI + `/manage` endpoints incl. `/manage/memory` | **DONE** - all 24 endpoints plus the Blazor admin UI on the admin port |
| 9 | Memory tuning pass — measure against the targets above | **DONE 2026-09-06, PROVEN ON THE NAS. 563.4 -> 177.7 MB working set, below the reference's 248.1 MB on the same box at the same moment.** The byte-cache budget stays at 5120/512 by the operator's explicit choice - do not change it. A reading with the library quiet would close the caveat that the after-row was taken at 255 s - the 2026-09-08 row in section 3b (282.9 MB at 1891 s) does not, because a metadata pass was running through all of it. **No restart is owed for it:** the live byte-cache and database values are deliberate (standing decisions 11 and 14), so the next quiet moment is enough. The history: The answer to "why is this server heavier than the reference" is not code: the NAS was running a **stale `config.json`** carrying `mmap 128` / `cache_size 32` / byte cache `5120`, where the tree ships `0` / `2` / `256`, and `NasBuild.sh:83-98` preserves that file across every redeploy - so the tuning below had never executed on the NAS at all. Live before-reading: **563.4 MB working set against 44.0 MB GC-committed**, i.e. ~518 MB native, versus the reference's 249.0 MB on the same box at the same moment. The four values were corrected live on 2026-09-06 but the process was not restarted, so the after-reading is still owed. **The first admissible heap-limited reading also exists now** (138.7 MB, dev box, `-HeapLimitPercent 25`), which closes this section's long-standing complaint that no row qualified. Two code levers were added because config alone cannot reach them: `IIndexMaintenance.ReleaseMemoryAsync` (`PRAGMA shrink_memory` + `SqliteConnection.ClearAllPools`, because a pooled connection keeps its whole page cache and the pool prunes only after two 120-240 s ticks) and `NativeHeapTrimmer.TryTrim` (`malloc_trim`, because SQLite's page cache is allocated in ~4 KiB chunks that sit *below* the `MALLOC_MMAP_THRESHOLD_` the deployment sets, so it comes from a glibc arena and is never returned without asking). Both are wired into `Empty the memory` on `/admin/cache` and `POST /manage/filecache/clear`, which now reports working set either side. **Remaining: deploy, restart, re-read.** The earlier structural work, unchanged: `MaxFileSizeInMegabytes` 512 -> 32 and `MaxTotalSizeInMegabytes` 1024 -> 256 (so films no longer fit and thumbnails are what the cache holds), `mmap_size` and `temp_store=MEMORY` off, `cache_size` 32 -> 8 MiB, and `System.GC.ConserveMemory` set in a `runtimeconfig.template.json` rather than through an SDK property that was being silently dropped. **`HeapHardLimitPercent` is NOT set in the deployment and should not be** - this row claimed it was, and section 3b now records why the claim was wrong and why the absence is correct. **What remains is the measurement**, re-run through `tools/measure-memory.ps1 -HeapLimitPercent` for a dev-box reading, because every reading in the trend table below was taken without a heap limit and is inadmissible under this section's own rule |
| 10 | Deployment + verification against a real TV | **DONE 2026-09-04** - the LG lists, plays, shows thumbnails and seeks; VLC likewise. Verified against the **2026-09-03 23:11** build. The NAS now runs the **2026-09-08 12:34** build, which carries everything through section 6h - deployment confirmed live via `/manage/thumbnail`, but **no television has seen it**, so the four DIDL fields and the B6 transfer-mode change are still unproven against a real set. **Everything through 6m is on the NAS; section 6n is not** - the deployed About page answers and reports `1.0.0`, which means it carries 6m (About arrived with it) and predates the version property added in 6n. Checked 2026-09-15; see "Where things stand" in section 8 |

Current test count: **798** (375 unit, 402 integration, 21 architecture), as of 2026-09-23 after the
batches in sections 6p to 6s. The 2026-09-17 figure this line used to carry was **789**. The 2026-09-15 figure this line used to carry was **772** after the
section 6n batch. Build must stay at **0 warnings** - and verify that with a CLEAN rebuild, because an
incremental one hid a real warning during that pass.
This line read 308 for some time after the count had moved twice, which is exactly the drift the review
flagged: section 8 step 3 below was telling a cold-start session to expect a number two revisions old.

**The server is live on the NAS.** It builds, deploys, starts, and serves media to VLC and to the LG
television. Work has moved from building milestones to fixing what real devices and real operators reveal -
see "Where things stand" in section 8 for the ordered list of what to do next, and read the deployment-state
note there first: **part of the current tree is not on the NAS.**

---

## 5. What exists now

```
DlnaServer.sln, Directory.Build.props, Directory.Packages.props, global.json (SDK 8.0.414, latestPatch)
NasBuild.sh, NasBuild.usage.txt, .editorconfig, .gitignore
Dockerfile, docker-compose.yml, docker-compose.admin-remote.yml, Caddyfile, Docker.usage.md
README.md                                the landing page; the detail is under docs/
CONTRIBUTING.md, LICENSE, THIRD-PARTY-NOTICES.md, release-notes.md
.github/                                 workflows (dotnet, release), dependabot, SECURITY.md, templates
tools/measure-memory.ps1                 memory trend capture (section 3b)

docs/                                    README.md (index), architecture, dlna-compatibility,
                                         configuration, operations, performance, troubleshooting,
                                         and this file's sibling decisions.md

src/DlnaServer.Core/                     no dependencies (CommunityToolkit is now the Host's alone)
  Configuration/    DlnaOptions + Server/Library/Thumbnail/FileCache/Compatibility/Database options
  Contracts/        the repository DTOs, and only those - the set held immutable by the
                    architecture tests
  Contracts/Scanning|Watching|Processing
                    parameter objects, filesystem events, media-processing results
  Dlna/             DlnaMime, DlnaMedia, DlnaItemClass, DlnaMimeCatalog (one lookup table)
  Files/            ContentStamp, PathExclusion (IsExcluded = scan, IsHidden = read), BackupFilePath,
                    ThumbnailSize, VideoCaptureTime
  Hosting/          IRestartSignal/RestartSignal, IDatabaseResetSignal/DatabaseResetSignal,
                    ILibraryScanSignal/LibraryScanSignal, IAdminOperationGate
  Delivery/         IServedFileCache, CachedContentClass, ServedFileCacheReport
  Diagnostics/      IApiBlocker, IMediaCapabilities
  Gena/             ISubscriptionStore, EventSubscription
                    (these four folders hold abstractions the Admin RCL needs and the Host implements -
                    see section 7, "Seams that exist only so the admin UI can reach the host")

src/DlnaServer.Persistence/              entities and DbContext are INTERNAL to this assembly
                    root: IDatabaseInitializer(+Result/Outcome, now with Reset),
                    IIndexMaintenance/IndexMaintenance/IndexClearResult (public, for the admin UI)
  Entities/         MediaFileEntity, MediaDirectoryEntity, Audio/Video/SubtitleStreamEntity,
                    ThumbnailEntity(+Content), ServerInstanceEntity - every entity ends in Entity
  Configurations/   EntityBaseConfiguration<T> + one per entity, named for it
                    (MediaFileEntityConfiguration), found by ApplyConfigurationsFromAssembly
  Repositories/     IMediaFileRepository, IMediaDirectoryRepository (+ impls)
  Interceptors/     SqlitePragmaInterceptor, SlowQueryInterceptor
  Migrations/       InitialSchema
  DlnaDbContext, DatabaseInitializer, PersistenceServiceCollectionExtensions

src/DlnaServer.Media/                    no EF, no ASP.NET
  Scanning/         ILibraryScanner, LibraryScanner
  Watching/         IFileSystemChangeWatcher, FileSystemChangeWatcher
  Processing/       IMediaProcessor, MediaProcessor, MediaProcessingSettings
  Processing/Thumbnails/    ImageThumbnailGenerator, ThumbnailRequest
  Processing/Provisioning/  IFFmpegProvisioner, FFmpegProvisioner - NOT named FFmpeg, that
                            namespace shadows Xabe's FFmpeg class

src/DlnaServer.Upnp/                     no EF, no ASP.NET, no hosting - this is what makes it testable
  Constants/        XmlNamespaces, UpnpServices, DlnaProtocolInfo
  Didl/             DidlDocument, DidlItem, DidlContainer, DidlResource, DidlSerializer,
                    DidlMetadata.md - what each property becomes on the wire, plus the reference's
                    own metadata survey and which of its rows are not real DIDL-Lite properties
  Description/      DeviceDescription, DeviceDescriptionBuilder
  Soap/             CustomEnvelopeMessage - the SoapCore envelope override that carries encodingStyle
  Soap/<Service>/   one namespace per SOAP endpoint (ContentDirectory, AvTransport,
                    ConnectionManager, MediaReceiverRegistrar): its service interface plus that
                    service's response contracts, which map 1:1 onto its actions
  Ssdp/             SsdpMessageBuilder, UpnpServerSignature, UpnpDeviceIdentity,
                    LocalAddressProvider, UpnpDeviceRegistry

src/DlnaServer.Admin/                    Blazor Server admin UI (Razor class library), referenced
                                         by the Host as of M8 and served on the admin port only
  App/Routes/Shared/       root component, router, layout and navigation
  Pages/                   Dashboard, Library, Preview, FileCache, Settings, Maintenance
  wwwroot/admin.css        palette sampled from the server's own device icon

src/DlnaServer.Host/                     the only executable; nothing references it
  Program.cs        two-port Kestrel, config guard, logging, DI, SOAP endpoint
  Configuration/    ConfigurationFileGuard + result types, DlnaOptionsDefaults, DlnaOptionsValidator,
                    SettingsWriter (the admin Settings page writes config.json through this)
  Controllers/      MediaController (description.xml), StaticResourceController (SCPD + icons),
                    FileServerController (media + thumbnails, renderer port),
                    AdminMediaController (/admin/media, admin port - same sources, no DLNA headers),
                    EventController (GENA SUBSCRIBE), ManageController (all 24 /manage endpoints)
  Delivery/         DlnaResponseHeaders, IMediaContentResolver + MediaContentResolver +
                    MediaContentSource (the ONE delivery decision, shared by both ports)
  Delivery/Caching/ ServedFileCache (implements Core.Delivery.IServedFileCache)
  Delivery/Prefetch/IMediaCacheBacklog + MediaCacheBacklog, MediaCacheRequest,
                    MediaCacheFillHostedService
  Diagnostics/      ApiBlocker (expiry-based, implements Core.Diagnostics.IApiBlocker),
                    ApiBlockingMiddleware (503 + Retry-After, /manage exempt),
                    AdminSurfaceMiddleware (confines /admin, /_blazor, /_framework, /_content to the
                    admin port - middleware because an endpoint filter does NOT apply to Blazor),
                    ConnectionLoggingMiddleware - one Debug line per request with remote and local
                    address:port, User-Agent and Range. Replaces the reference's per-action helper
                    calls, and covers the SOAP endpoints too. Silent unless Server.DebugMode is on
  Gena/             ISubscriptionStore + SubscriptionStore, EventSubscription, GenaHeaders,
                    GenaTimeout, GenaCallback, HttpSubscribe/HttpUnsubscribe attributes
  Hosting/          DatabaseInitializerService
  Indexing/         LibraryIndexer, LibraryIndexHostedService, FileWatcherHostedService,
                    MediaProcessingHostedService
  Upnp/             ContentDirectoryService, ConnectionManagerService, AvTransportService,
                    MediaReceiverRegistrarService, DidlMapper, BrowseRequest,
                    SsdpAdvertisement, SsdpNotifierHostedService, SsdpListenerHostedService
  Resources/        images/icons (13 files), xml (4 SCPD) - byte-identical to the reference
  config.json, appsettings.json

tests/DlnaServer.UnitTests/              Dlna/, Media/, Persistence/, Upnp/
tests/DlnaServer.IntegrationTests/       config guard, indexer, SSDP advertisement
tests/DlnaServer.ArchitectureTests/      layering, entity boundary, contract immutability
```

### Verification commands

```bash
dotnet build DlnaServer.sln            # must be 0 warnings, 0 errors
dotnet test  DlnaServer.sln            # all green
dotnet run --project src/DlnaServer.Host
```

Ports come from `config.json` → `Dlna.Server`. Currently **26852** (media) and **26853** (admin).
Health probes: `GET /health` on the media port, `GET /admin/health` on the admin port.

---

## 6. Milestone detail

### M2d — Config resilience (DONE)

`ConfigurationFileGuard` (Host/Configuration) runs **before** `AddJsonFile`, because the configuration
provider throws on malformed JSON while the host is being built — too late to recover, the process just
dies with a parse error.

- Missing file → defaults written.
- Unreadable or malformed → moved to `config.json.corrupt-<yyyy-MM-dd_HHmm>`, defaults written.
- Valid → left byte-for-byte alone.

Deliberate design points:
- **Only JSON well-formedness is checked**, not deserialization. The configuration binder accepts values
  a strict deserializer rejects (`"Port": "26851"` as a string), so binding here would condemn a working
  file. Wrong *values* are caught by options validation, which reports them instead of overwriting them.
- **Writes use `FileMode.Create`.** The reference used `CreateNew`, which throws when the file exists, so
  every save over an existing config fell into its catch and silently wrote defaults over the user's settings.
- **`BackupFilePath.CreateUnique`** (Core/Files) appends a numeric suffix when a backup name is taken.
  Timestamps carry minutes but not seconds by convention, so two failures inside one minute collided and
  the second backup destroyed the first. A test caught this. The database initializer uses the same helper.
- **The outcome is reported before options validation runs.** `ValidateOnStart` throws before hosted
  services start, so a warning raised from a hosted service would never be seen — exactly when the
  operator most needs to know their config was replaced.
- **Serilog now owns console and file.** Previously `AddSimpleConsole` handled the console and Serilog the
  files, so a message written directly to the Serilog logger (startup notices, before the host exists)
  never reached the console.

Verified live: a corrupt `config.json` is backed up, defaults are written, the warning appears on console
and in `logs/`, and startup then fails with a precise message naming `SourceFolders` as the thing to set.

### M3 - Indexing (DONE)

- [x] `ILibraryScanner` / `LibraryScanner` (Media) - streams files and directories, segment-based
      exclusions, extension-to-MIME from configuration.
- [x] `ContentStamp`, `PathExclusion`, `BackupFilePath` (Core).
- [x] `LibraryIndexer` (Host) - adds new files, reconciles removals, re-stamps changed files.
- [x] `LibraryIndexHostedService` - startup pass, off the startup path so the server listens immediately.
- [x] `FileSystemChangeWatcher` (Media) + `FileWatcherHostedService` (Host).
- [x] 8 scanner tests, 8 indexer tests.

**Startup behaviour:** the server walks every source folder on start and adds any file not already in the
database. New and changed files are stored with a null metadata/thumbnail stamp, which is exactly what
`GetPendingProcessingAsync` selects - so M4's processor picks them up with no extra plumbing.

**Watcher timing - both reference bugs fixed:**
- Reference de-duplication compared `lastEventTime - UtcNow` against 100 ms. Always negative, so the guard
  was always true and every repeat of a path was dropped permanently, including the successive writes of
  a file copy. Here events are coalesced per path, newest wins.
- Reference debounce waited a flat 30 s per event instead of the remaining time, capping a single-threaded
  reader at one event per 30 s. Here a path is acted on once it has been quiet for 10 s, measured from its
  own last event.
- The queue is **bounded** (10,000). The reference used an unbounded channel, so a bulk copy queued
  unbounded memory. On overflow - or an OS watcher buffer overflow - a full rescan is requested instead,
  because losing individual events is recoverable and unbounded growth on a NAS is not.

**Three bugs found by running it, not by tests:**
1. Directories inserted with `parent=None` - the batch only flushed at 500 rows, so the key map was empty
   while children were built. Now inserted one depth level at a time.
2. `config.json` overrode environment variables - `AddJsonFile` appended its provider after the default
   environment provider. Environment variables are now re-applied last, restoring normal precedence.
   This would have broken any NAS deployment configured through the environment.
3. Overlapping or duplicated source folders (`/share/Media` plus `/share/Media/Movies`) discovered the
   same file twice in one pass and failed the unique index. Source folders are now normalised - absolute,
   de-duplicated, nested ones dropped - and each insert batch is de-duplicated as defence in depth.

**Verified live:** startup scan added 2 files; a file added while running was indexed within the settle
window (+1); a deleted file was reconciled away (-1); repeated passes added nothing. Zero errors
throughout - including zero `UNIQUE constraint` failures, the error that dominates the reference's
production logs at 1390/day.

**Deliberately deferred:** the watcher currently triggers a full rescan rather than a targeted single-path
update. Correct and cheap at this library size; revisit if a scan becomes slow on the real 20k library.

### M4 - Media processing (DONE)

- [x] `FFmpegProvisioner` - locates or downloads ffmpeg once, caches the result. Never fatal: without it
      the server still indexes, browses and streams; only video metadata and video thumbnails are skipped.
- [x] `ImageThumbnailGenerator` - SkiaSharp, every handle disposed on the statement that creates it.
- [x] `MediaProcessor` - ffprobe metadata, video frame grab, dispatch by media kind.
- [x] `MediaProcessingHostedService` - background queue, **one file at a time**, never on the Browse path.
- [x] Repository: `SaveMetadataAsync`, `SaveThumbnailAsync`, `MarkThumbnailNotApplicableAsync`,
      `RecordProcessingFailureAsync`.
- [x] Reference icons and SCPD XML copied byte-for-byte (see "Static assets" below).
- [x] 15 unit tests for thumbnail sizing and capture-time tiers.

**Verified live** against three generated JPEGs: metadata extracted, thumbnails produced at 120x90
(unchanged - smaller than the box), 180x360 (bounded by height) and 480x270 (bounded by width, 16:9
preserved), all stored on disk and in the database, zero errors.

**A bug found by running it:** thumbnails failed silently three times per file and backed off. The resize
forced `SKColorType.Rgb888x`/`Opaque`, and Skia returned null rather than converting. Now the source's own
colour and alpha types are preserved, and the resize and encode failures log separately so the next such
failure names itself. Debug-level logging is what surfaced it - the failure counter had already hidden it.

**Deliberate differences from the reference:**
- Processing is off the Browse path entirely. The reference generated metadata and thumbnails *inside*
  the ContentDirectory Browse call; its own logs show that phase averaging 23.2 ms on slow requests.
- One file at a time. Skia and ffmpeg allocate natively, so concurrency multiplies resident memory
  without improving throughput on a NAS.
- `ConversionPreset.VeryFast` for the single-frame grab. The reference used `VerySlow`, an encoding-effort
  setting that buys nothing for one still.
- Linear sampling instead of `SKFilterMode.Nearest`, the cheapest and worst resample.
- Thumbnails are written **beside the media**, into `Thumbnails.SubFolderName` (`.@__thumb`), named
  `<media file name>.jpg` - `Films/.@__thumb/Film.mkv.jpg`. This is the reference's layout, adopted after
  the first NAS deployment showed why it matters: a cache under the application folder is discarded every
  time the server is published to a new directory, so an entire library's previews regenerate. Keeping
  them with the media also means an existing library arrives already previewed. Scanning skips the folder
  by name, which is what stops the previews being re-ingested as media.
  **Both halves of that sentence changed on 2026-09-03 - see section 6c.** `SubFolderName` is now
  `[Required]` and is added to `ExcludeFolders` after binding whether or not it is listed, so the
  validation rule that failed startup when the two settings disagreed is gone. Blank `SubFolderName` used
  to select a central `CacheDirectory` with a two-level fan-out (`38/16/3816....jpg`) as the escape hatch
  for a read-only media volume; that path is now unreachable and is kept, marked as such in its XML docs,
  rather than removed. Beside the media, each folder holds only its own previews and no fan-out is
  needed.
- **An existing thumbnail is adopted, never regenerated.** The processor checks the target path first and
  reads the file's dimensions from its header if it is already there, exactly as the reference does. For
  video this is the difference between adopting a file and running ffmpeg across the whole library.
- Audio files are marked thumbnail-not-applicable rather than left pending forever.

### Static assets copied from the reference

Copied **byte-for-byte** and verified by SHA-256, so renderers see no difference between old and new:

| Source | Copied | Why |
| --- | --- | --- |
| `Resources/images/icons` (13 files) | Yes | Advertised in `description.xml`'s `iconList` and used as DIDL-Lite fallback thumbnails |
| `Resources/xml` (4 SCPD files) | Yes | Service contracts renderers fetch and parse |
| `Resources/images/icons/.@__thumb` | No | Reference-generated thumbnails of the icons, not source assets |
| `Resources/configuration/config.json` | No | The reference's flat schema; this server ships its own grouped `config.json` |
| `Resources/executables` | No | Empty; ffmpeg is provisioned at runtime into `ffmpeg/` |

All of it is wired through a `Content Include="Resources\**\*"` item with `PreserveNewest`, so it reaches
the publish output - 17 files confirmed in the build output.

### M5 - UPnP core (DONE)

> **Browse changed after deployment.** The root listing's `parentID` was wrong and hid the library from an
> LG television - see M10, "The television saw the server and none of its content". The rule it broke is
> now written down in section 7, "DIDL-Lite rules that renderers actually enforce".

**Protocol layer** in `DlnaServer.Upnp` - no EF, HTTP or hosting dependency:
- [x] `XmlNamespaces`, `UpnpServices`, `DlnaProtocolInfo` (flags verified by arithmetic: `21F00000`).
- [x] `DidlItem`, `DidlContainer`, `DidlResource` - every property documents its wire name.
- [x] `DeviceDescriptionBuilder`, `UpnpServerSignature`, `SsdpMessageBuilder`.
- [x] `LocalAddressProvider`, `UpnpDeviceRegistry`, `UpnpDeviceIdentity`.
- [x] 39 golden tests over whole datagrams and document bytes.

**HTTP surface** (`DlnaServer.Host/Controllers`):
- [x] `MediaController` - `description.xml` at `/` and `/media/description.xml`,
      `Content-Type: text/xml; charset="utf-8"`.
- [x] `StaticResourceController` - `/SCPD/{file}` and `/icon/{file}`, resolved through
      `Path.GetFileName` and a root-prefix check so a crafted name cannot escape the folder.
- [x] Verified live: SCPD 6430 bytes and icon 3009 bytes, both matching the reference exactly;
      `/SCPD/..%2f..%2fappsettings.json` returns 404.

**SSDP** (`DlnaServer.Host/Upnp`):
- [x] `SsdpNotifierHostedService` - one socket per interface, seven notification types, multicast and
      broadcast, `ssdp:byebye` on shutdown.
- [x] `SsdpListenerHostedService` - answers M-SEARCH, replies unicast.
- [x] `SsdpAdvertisement.ShouldAnswer` - **the inverted match is fixed**, with
      `Compatibility.UseLegacyInvertedSearchTargetMatch` reproducing the old behaviour exactly.

**Verified live against real multicast**, by sending genuine M-SEARCH datagrams:

| ST sent | Replies received |
| --- | --- |
| `ssdp:all` | all 7 targets |
| `urn:schemas-upnp-org:device:MediaServer:1` | exactly that one |
| `upnp:rootdevice` | exactly that one |

The reference would have answered the two targeted searches with the *other six* types while omitting the
one requested. A renderer that validates `ST` on the reply discarded all of them.

**Two deliberate divergences added here:**
- **Stable device identity.** `UpnpDeviceIdentity.CreateStableDeviceId` derives the UUID from machine name
  plus address, so it survives a restart. The reference called `Guid.NewGuid()` at every start, so each
  restart looked like a new device and renderers listed the server twice until the old entry's `max-age`
  lapsed. Deterministic, so nothing is stored.
- **A failed SSDP send no longer restarts the process.** The reference tore down and rebuilt the whole
  host when a datagram failed - its production logs show `SSDPNotifierService` doing exactly that. A
  transient network blip should not cost the index and every open stream.
- Header parsing splits on any line ending. The reference split on `Environment.NewLine`, which on Linux
  is a bare newline while SSDP uses CRLF, leaving a trailing carriage return on every parsed value.

**ContentDirectory Browse - done and verified live:**
- [x] `IContentDirectoryService` (SoapCore), `BrowseResponse`, DIDL-Lite serialisation into `Result`.
- [x] `DidlMapper` - containers and items, `res` URLs built from the advertised address.
- [x] `BrowseRequest` - interprets `BrowseFlag`, `Filter`, `SortCriteria`, `RequestedCount`.
- [x] Root container: source folders plus the synthetic recently-added listing.
- [x] Per-request timing log mirroring the reference's telemetry.

| Behaviour | Reference | Here (verified by real SOAP calls) |
| --- | --- | --- |
| `BrowseMetadata` | Ignored, returned a child listing | Returns the object itself |
| `RequestedCount=0` | Clamped to 1 | Returns all, bounded by the configured maximum |
| `RequestedCount=2` | - | 2 returned, `TotalMatches` still 4 |
| `Filter=dc:title` | Ignored | `albumArtURI` and `dc:date` omitted |
| `SortCriteria` | Ignored | Honoured - **except for the root listing's recently added files**, which always stay in indexing order. See section 6f |

**Three bugs found by running it, not by tests:**
1. **Sync-over-async.** The first implementation blocked on `.GetAwaiter().GetResult()` inside the SOAP
   operation. The `cs-writeguard` hook flagged it and was right - that risks thread-pool starvation under
   concurrent browsing. SoapCore supports `Task`-returning operations, so the contract is async throughout.
2. **SOAP parameters bound nothing.** camelCase parameter names produced `ObjectID: null, BrowseFlag: null`
   on every call - SoapCore binds by matching the parameter name to the SOAP body element, and renderers
   send `<ObjectID>`. Parameters are PascalCase for this reason, against the usual convention, and the
   contract documents why. Only visible because the timing log printed the arguments.
3. **Invalid IPv6 resource URLs.** A request arriving on the IPv6 loopback produced
   `http://::1:26852/...` - unbracketed, unparseable. Resource URLs now come from the device registry, so
   they always use an address the server actually advertised over SSDP.

### M6 - Delivery (DONE)

- [x] Stream media and thumbnails with range support (`206`, `Content-Range`).
- [x] DLNA headers behind the compatibility flag - `transferMode.dlna.org`, `contentFeatures.dlna.org`,
      `realTimeInfo.dlna.org`, built from the same `DlnaProtocolInfo` call that produces `res@protocolInfo`
      so header and browse result cannot drift.
- [x] **No response compression is registered at all** - nothing to scope away, and that is now recorded
      on `FileServerController` so it does not get added later. The reference registers it globally, which
      is hazardous over `206 Partial Content`: compressing a range changes the byte count promised in
      `Content-Range`.
- [x] Byte cache following the serving rule below - `ServedFileCache`, its own `MemoryCache` with its own
      byte budget, three retention classes.
- [x] No forced GC on eviction. No post-eviction callback exists, so there is nothing to schedule a
      collect from.
- [x] `HEAD` answered as well as `GET` on both endpoints (a divergence - see below).
- [x] Static assets routed through the cache as their own retention class.
- [x] 37 new tests (6 protocol, 8 repository, 12 cache, 8 headers, 5 backlog); 183 total.

**Deliberate divergences from the reference:**
- **`HEAD` is answered.** MVC does not route `HEAD` to a `[HttpGet]` action, so the reference replies 405
  to the size-and-type probe several renderers send before committing to a transfer. Both endpoints now
  carry `[HttpHead]`, and ASP.NET suppresses the body while keeping `Content-Length` correct.
- **`Cache-Control` is `private`, not `public`, on thumbnails and static assets.** With `Location = Any`
  the response-caching middleware keeps its own copy of exactly the payloads the byte cache already holds -
  a second, separately-bounded 100 MB of duplicate memory for a 20,000-thumbnail library. The renderer
  still caches for the same duration. `description.xml` keeps `Any`, because it is generated rather than
  byte-cached, so there the middleware is a real saving.
- **Per-response logging is level-split.** A renderer issues one request per byte range, so a single
  playback would fill the log at Information. The start of a transfer (no `Range` header) logs at
  Information; the ranges that follow log at Debug.
- **`JPEG_TN` is one constant.** `DlnaProtocolInfo.ThumbnailProfileName` is now the single source for the
  thumbnail profile, used by `DidlMapper` and by the response header.

**Thumbnail source order is memory -> database -> file**, reproducing the reference: a stored blob is
served rather than a second seek to the thumbnail file. The blob is then stored in the byte cache under
the *file* path, so a later request hits memory whichever source filled it. Verified live: first request
logged `from database`, second `from cache`.

**Two bugs found by running and testing it, not by writing it:**
1. **`BoundedChannelFullMode.DropWrite` silently accepted overflow.** `TryWrite` returns **true** while
   discarding the item, so an overflowing request left its path marked as waiting for a read that never
   happened - and `Release` is only called by the filler, so that file could never be cached again for the
   life of the process. `FullMode.Wait` makes `TryWrite` return false instead, which is what the code
   already assumed. Caught by `MediaCacheBacklogTest`, which was written to check the bound held.
2. **A doc comment was orphaned from its method.** Inserting the static-asset helper split
   `ResolveResourcePath` from its own XML doc, leaving the new method carrying two summaries. Cosmetic,
   but it is the failure mode of inserting by pattern rather than reading the surrounding file.

**Verified live** against a running server and real HTTP:

| Request | Result |
| --- | --- |
| `GET /fileserver/file/{id}`, first touch | `200`, `Accept-Ranges: bytes`, all three DLNA headers; log says `from disc` |
| `HEAD` the same file, immediately after | `200` with `Content-Length` and no body; log says `from cache` - the backlog filled it behind the first response without it waiting |
| `Range: bytes=0-99` | `206`, `Content-Range: bytes 0-99/1722719`, exactly 100 bytes; logged at Debug, not Information |
| `transferMode.dlna.org: Background` sent | Echoed back as `Background` |
| `contentFeatures.dlna.org` | `DLNA.ORG_PN=JPEG;DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=20F0...` - byte-identical to the `res@protocolInfo` the same item was browsed with |
| `GET /fileserver/thumbnail/{id}` | `200`, `CI=1` in `contentFeatures`, `Cache-Control: private,max-age=3600` |
| `GET /icon/folder.jpg` | `200`, 23184 bytes, `Cache-Control: private,max-age=86400` |
| `GET /SCPD/..%2f..%2fappsettings.json` | `404` - the traversal guard still holds after the rewrite to serve from cache |
| Unknown file and thumbnail identifiers | `404` each, one warning line naming the requester |

Zero errors in the log across the whole session; the only warnings were the ones the negative tests
provoked.

#### Serving rule - cache on first touch, never block on it

Reproduce the reference's behaviour exactly. For every media request:

1. **Is the file in the byte cache?**
2. **No** - start caching it in the background, and **stream this request straight from disk meanwhile**.
   Never wait for the cache to fill. The first request is served from disk either way; making it wait
   would add latency and gain nothing.
3. **Yes** - serve it from the cache and do not touch the disk at all.

Only files at or below `FileCache.MaxFileSizeInMegabytes` are cached. Anything larger always streams from
disk, by design.

**Why this matters, and it is not latency.** The NAS uses mechanical drives, and they are **audible**. A
disc that has spun down and is woken again to re-read a file the server already served is noise in the
room. Caching on first touch means a file is read from the platter once; every replay after that is
silent. This is a comfort requirement, not a throughput optimisation, and it changes the design
priorities:

- **Prefer retaining over evicting.** A cached file that survives is the whole point. The sliding
  expiration should be generous enough to cover a viewing session, not tuned for minimum footprint.
- **Never force a collection to reclaim cache memory.** Beyond the pause it causes, dropping cached bytes
  early is the exact outcome this rule exists to prevent.
- **Eviction is bounded by the memory budget, not by time-since-use alone.** Section 3 sets the working
  set target; the cache lives inside it, and `FileCache.MaxTotalSizeInMegabytes` is the knob.
- **The trade-off is explicit:** memory held for cached bytes buys silence. When the budget and this rule
  conflict, say so and let the size limits arbitrate rather than quietly shrinking the cache.

#### Settings - one-to-one with the reference

The four reference settings that govern this already have equivalents, with **identical defaults**, so a
production `config.json` ports across by renaming:

| Reference | Here (`Dlna.FileCache`) | Default | Meaning |
| --- | --- | --- | --- |
| `UseMemoryCacheForStreamingFile` | `Enabled` | `true` | Master switch. Off means every request reads the disc. |
| `MaxUseMemoryCacheInMBytes` | `MaxTotalSizeInMegabytes` | `1240` | Total budget for cached file bytes. |
| `MaxSizeOfFileForUseMemoryCacheInMBytes` | `MaxFileSizeInMegabytes` | `512` | Files larger than this always stream from disc. |
| `StoreFileInMemoryCacheAfterLoadInMinute` | `SlidingExpirationInMinutes` | `10` | Sliding lifetime, refreshed on each access. |

Two further behaviours to reproduce, both implemented in `ServedFileCache`:

- **Clamp the budget against available memory.** The reference sets the cache size limit to
  `Math.Min(availableMemoryBytes / 2, MaxUseMemoryCacheInMBytes)`, so a machine with less RAM than the
  configured budget does not promise memory it does not have. Kept - `ResolveBudgetInBytes`, read once at
  construction because `MemoryCacheOptions.SizeLimit` cannot be changed afterwards. The resolved budget
  is logged at startup (`Served-bytes cache holds up to 1024 MB`), so what the process actually promised
  is visible rather than inferred from configuration.
- **Three retention classes, not one.** The sliding lifetime is chosen per content class, because a value
  that suits a two-gigabyte film is wrong for a three-kilobyte icon:

  | Class | Sliding | Absolute | Setting | Why |
  | --- | --- | --- | --- | --- |
  | Media files | **10 min** | **1 h** | `FileCache.MediaSlidingExpirationInMinutes` | Large; a few films exhaust the budget. Refreshes during playback, so one item stays cached throughout. |
  | Thumbnails | **1 h** | **12 h** | `FileCache.ThumbnailSlidingExpirationInMinutes` | Small, re-requested whenever a renderer redraws a folder. Grows with the library, so kept below the static class. |
  | Static assets (icons, SCPD) | **1 h** | **1 day** | fixed, not configurable | A few hundred KB total, referenced by nearly every response. No deployment benefits from tuning them. |

  This diverges from the reference deliberately. It used a **1-day** sliding lifetime for both thumbnails
  and static assets under a 12-hour absolute cap. One day per thumbnail is generous for a class that has
  one entry per file: at a few KB each, a 20,000-file library is tens of megabytes held for a day. One
  hour keeps the redraw case fast while letting the class fall back out of memory when browsing moves on.

  Only the first two sliding values are configurable. The three absolute caps and the static class's
  sliding value are fixed properties on `FileCacheOptions` - `MediaAbsoluteExpiration`,
  `ThumbnailAbsoluteExpiration`, `StaticAssetSlidingExpiration`, `StaticAssetAbsoluteExpiration` - so all
  six values sit in one place and none of them is a deployment knob nobody has a reason to turn.

  **Eviction order is set by priority, not only by expiry.** Media is `Low`, thumbnails `Normal`, static
  assets `High`, so filling the budget with films cannot purge the small classes: one film is worth
  thousands of thumbnails by size, and trading many cheap hits for one expensive one is the wrong way
  round. Note the caveat on `MediaAbsoluteExpiration` in "Open questions" - 1 hour is what section 6
  specifies and what is implemented, but it interacts badly with a long film.

A per-file opt-out also exists on both sides: the reference's `FileEntity.FileUnableToCache` is
`MediaFileDto.IsExcludedFromCache` here, set when a read into the cache has already failed once so the
attempt is not repeated on every request.

**Open question for M9.** `MediaSlidingExpirationInMinutes` governs media files only. Sliding expiration
refreshes while a file is being read, so a long film stays cached throughout. What 10 minutes does not
cover is the gap *between* items - finishing one episode, browsing, then starting the next twenty minutes
later finds the cache cold and wakes the disc again. Worth measuring against real usage, and worth
deciding with the acoustic goal in mind rather than the memory number alone.

Raising it is a separate decision from thumbnail or static-asset retention. The three classes exist
precisely so one can be tuned without disturbing the others.

### M7 - Remaining SOAP + GENA (DONE)

- [x] ConnectionManager - all five actions, `GetProtocolInfo` populated (see below).
- [x] AVTransport - all seven actions, no side effects, replies reporting an idle transport.
- [x] MediaReceiverRegistrar - `IsValidated` and `RegisterDevice`. The reference declares this interface
      with **no operations at all** while advertising the service and serving its SCPD.
- [x] ContentDirectory - `X_GetFeatureList` and `X_SetBookmark`, the two Samsung extensions the SCPD
      advertises and nothing here implemented.
- [x] GENA at `/event/eventAction/{serviceId}` - `TIMEOUT` as `Second-1800`, UNSUBSCRIBE actually removes.
- [x] 33 new tests (3 protocol, 8 timeout, 8 callback, 10 store, 4 others); 216 total.

**The SCPD was the specification, not the reference implementation.** Answered as a decision: a renderer
fetches the SCPD document and builds its requests and its parsing from that, so where the reference's code
disagreed with the SCPD it serves, the document won.

| Action | SCPD says | Reference code | Here |
| --- | --- | --- | --- |
| `GetCurrentConnectionIDs` | out arg `ConnectionIDs` | emits `ConnectionID` | `ConnectionIDs` |
| `Seek` | in arg `Unit` | parameter named `SeekMode` | `Unit` |
| `GetTransportInfo` | 3 out args, no `InstanceID` | emits `InstanceID`, all state null | the 3 declared args, populated |

The `Seek` one is the M5 defect again: SoapCore binds each argument from the body element of the same
name, so the reference's `SeekMode` binds nothing and a renderer's `<Unit>` arrives null. Verified live -
`Seek` with `<Unit>REL_TIME</Unit>` logs `in units of REL_TIME`.

**`GetProtocolInfo` now answers.** The reference returns `Source` and `Sink` both empty, which reads as
"serves nothing" to a renderer that pre-filters on the list. `Source` is built from the configured
`MediaFileExtensions` through `DlnaProtocolInfo.BuildSourceList`, one entry per distinct MIME type:

```
http-get:*:audio/mp4:*,http-get:*:image/jpeg:*,http-get:*:image/png:*,http-get:*:video/3gpp:*, ...
```

The fourth field is a wildcard rather than a `DLNA.ORG_PN` profile deliberately. A renderer matches an
item's `res@protocolInfo` against these entries, so listing literal profiles would need one entry per
profile and would reject anything not enumerated; `*` says "any profile of this MIME type", which is what
is actually true. `Sink` stays empty - a MediaServer sources content and receives none.

**AVTransport answers with valid state rather than nothing.** A MediaServer has no transport, but the
service is advertised, and an advertised action that faults tells a television the device is broken. Every
action acknowledges without side effects; `GetTransportInfo` reports `STOPPED` / `OK` / `1` and
`GetPositionInfo` reports `0:00:00`, all values the SCPD's own `allowedValueList` permits. The reference
returns empty objects, so its `GetTransportInfo` reply carries no state elements at all. Every call logs
at Information: the reference's own comment on these actions is "not found operation in real usage", so if
a renderer does call one, that is the evidence that would justify implementing the service properly.

#### GENA - four more defects than the two this milestone was scoped for

`description.xml` has always advertised `eventSubURL` for all four services. **Nothing served that path**,
so every SUBSCRIBE was answered with a 404 until now.

| Behaviour | Reference | Here |
| --- | --- | --- |
| `TIMEOUT` header | `00:30:00` - a `TimeSpan.ToString()`, not a value the grammar allows | `Second-1800`, via `GenaTimeout` |
| UNSUBSCRIBE | returns `200` and removes nothing | removes, or `412` if the identifier is unknown |
| Renewal (SID, no CALLBACK) | `400` - it demands CALLBACK on every request, so every renewal fails | `200` with the same SID and the newly granted lifetime |
| `CALLBACK` value | stored verbatim, angle brackets included | parsed to absolute HTTP URLs; unusable header is `412` |
| Response body | the text `subscribed` | empty, with `Content-Length: 0` |
| Status codes | `400` for everything | `400` incompatible headers, `412` missing/unusable, `503` store full |
| Subscription store | the shared `IMemoryCache` with `Size = 1` - the meaningless-size defect section 3 names | its own bounded store, 64 subscriptions, lapsed entries pruned on add |

`NT` is validated only when present. The specification requires it on a new subscription, but the
reference never looked at it, so demanding it could refuse a renderer that works today - a wrong value is
rejected, an absent one is not.

**Still no NOTIFY.** Unchanged decision (section 2). What the endpoint owes a renderer is an honest
handshake: an identifier the server recognises later, a lifetime in the form the grammar defines, and an
UNSUBSCRIBE that forgets. It now does all three.

**Verified live** with real SOAP calls and real `SUBSCRIBE` / `UNSUBSCRIBE` methods:

| Request | Result |
| --- | --- |
| `GetProtocolInfo` | 11 source entries from the configured extensions |
| `GetCurrentConnectionIDs` | `<ConnectionIDs>0</ConnectionIDs>` - the plural name the SCPD declares |
| `GetCurrentConnectionInfo` | `Direction: Output`, `Status: OK`, ids `-1`; the reference sends `"0"` for all of them |
| `GetTransportInfo` | `STOPPED` / `OK` / `1` |
| `Seek` with `<Unit>REL_TIME</Unit>` | bound and logged; the reference's `SeekMode` would arrive null |
| `IsValidated` | `<Result>1</Result>` - previously a fault, the interface had no operations |
| `X_GetFeatureList`, `X_SetBookmark` | both answer; previously faults |
| `SUBSCRIBE` new | `200`, `SID: uuid:…`, `TIMEOUT: Second-1800`, `Content-Length: 0` |
| `SUBSCRIBE` renewal, SID only | `200`, same SID, `TIMEOUT: Second-600` (the reference answers `400`) |
| `UNSUBSCRIBE`, then again | `200`, then `412` - it actually removed |
| `SUBSCRIBE` with SID **and** CALLBACK | `400` |
| Unbracketed CALLBACK / wrong `NT` / no SID on UNSUBSCRIBE | `412` each |
| Browse, `description.xml`, `/fileserver/file`, `/icon` | unchanged - 6 items, `200`, `200`, `200` |

Zero errors in the log; every warning was one a negative test provoked.

**Two analyzer suppressions, both because the name is the wire contract:** `CA1716` on
`IAvTransportService` (`Stop` is a reserved word in some languages) and `CA1707` on
`IContentDirectoryService` (`X_` prefixes carry an underscore). Renaming either would make the action
unreachable, since SoapCore dispatches on the name. Each suppression carries that justification in place.

### M8 — Admin UI and management endpoints

- [x] `/manage/stop` - graceful shutdown, mirroring the reference's `/Manage/stop`. Built ahead of the
      rest of this milestone because deployment needed it. Verified live: the reply is delivered, then the
      process exits. It clears the restart signal first, so a stop arriving after a restart request stops
      rather than silently restarting.
- [ ] `/manage/memory` reporting the same statistics as the Reference (required for comparison).
- [ ] `/manage/configuration`, `/manage/database`, indexing status, clear thumbnails/metadata, rescan.
- [ ] Blazor admin on the admin port: library browser, config editor, indexing status.
- [x] **Decided:** `/manage` stays unauthenticated on the media port, as in the reference. That is now a
      stated choice rather than an inherited one, and `/manage/stop` makes it consequential - any device
      that can browse the library can shut the server down. Recorded on `ManageController` itself. Revisit
      only if the server is ever exposed beyond a trusted LAN.
- [ ] Decide the same for the destructive endpoints still to come (`restart`, rescan, clear thumbnails),
      which are a bigger exposure than `stop`: a rescan can be triggered repeatedly to keep the disks busy.
- [ ] Reference's `/manage/thumbnail` returns every thumbnail **with its blob** — unbounded response.

### M8 - management endpoints (DONE 2026-09-02)

All 24 endpoints, covering the reference's whole `/manage` surface. **Every one is unauthenticated, on the
media port** - confirmed as a decision on 2026-09-02: the application is for a trusted internal network.
That is now a stated position rather than an inherited one, and it is load-bearing, because `stop`,
`restart` and the `clearAll*` family are reachable by anything that can browse the library.

`README.md` -> "Management endpoints" lists them. Four diverge from the reference on purpose, each because
its version does not survive a real library:

- **Listings page by `after`, not offset.** An offset shifts when a row is inserted ahead of it, and a
  full scan inserts thousands. The reference returns whole tables in one response.
- **`thumbnailData` takes an identifier.** The reference dumps every stored blob at once - gigabytes of
  base64 on a previewed library, which on a 2-4 GB machine is an outage rather than a diagnostic.
- **`block/{hours}` returns immediately**, because the block is state with an expiry rather than an
  awaited `Task.Delay` inside the request. The reference's call hangs for hours and loses the block if the
  connection drops. `/manage` is exempt from the block, or it could not be lifted without a restart.
- **`recreateAllFilesInfo` returns once the records are cleared**, leaving regeneration to the background
  pass. The reference holds the request open for the whole rebuild and blocks the API throughout.

New components: `IApiBlocker`/`ApiBlocker` (singleton, expiry-based) and `ApiBlockingMiddleware`
(503 with `Retry-After`, `/manage` exempt). New repository methods: `ClearAllMetadataAsync`,
`ClearAllThumbnailsAsync`, `ResetProcessingAsync`, `GetThumbnailPageAsync` - the two bulk ones use
`ExecuteUpdateAsync`/`ExecuteDeleteAsync`, and both reset the **failure counts** as well as the stamps,
without which the pending-work query would skip precisely the files that had failed three times and most
needed re-reading.

`clearAllThumbnails` leaves the images beside the media alone: they are the reference's layout, a rescan
adopts them, and deleting them turns a cheap reset into a full ffmpeg pass.

### M8 - Blazor admin UI (DONE 2026-09-02)

Blazor Server, in-process, on the admin port at `/admin`, no login - the same trusted-network assumption
the `/manage` endpoints make. Pages: Dashboard, Library, Preview, FileCache, Settings, Maintenance.
`README.md` -> "Admin UI" is the reference for what each does.

**Library search** was added under `/admin/library/search`: name (case-insensitive), kind, exact MIME,
modified-date range, size range, minimum video width, codec. `MediaFileRepository.SearchAsync` builds the
query conditionally and **escapes `%` and `_`** before the `LIKE` pattern - filenames are full of
underscores and an unescaped one matches any character, so the failure mode is wrong rows rather than no
rows. `Media` cannot be compared in SQL because the kind is derived from the MIME rather than stored, so
the matching MIME values are resolved first. Six tests cover it, including the wildcard case.

**Two defects the first deployment found, both invisible locally:**

- **`GetThumbnailByPublicIdAsync` takes the thumbnail's identifier, not the media file's.** The admin
  pages passed `MediaFileDto.PublicId`, which matches no row, so every tile and every video poster 404ed
  and the images silently did not appear - a broken `<img>` looks exactly like a library whose thumbnails
  have not been generated yet, which is why this survived a local run. The identifier is on
  `MediaFileDto.ThumbnailPublicId`; `DidlMapper` had it right all along, so renderers were never
  affected. Verified after the fix: the file's identifier still 404s on that route, the thumbnail's
  returns `200 image/jpeg`. The `/manage/thumbnail/{id}` doc said "the file it belongs to" and was wrong
  in the same way - corrected.
- **`width: 100%` with `max-height` distorts an image.** The width is fixed and the height then clamped
  on its own, so the picture is squashed - a `<video>` cannot suffer this because it letterboxes inside
  its box, which is why only the photos looked wrong. Images now take the page width with `height: auto`,
  so the ratio is the file's own.

The library tiles also fall back the way `DidlMapper` does: an image with no thumbnail yet is its own
preview, so a folder of photos looks right before the thumbnail pass has reached it.

**Four things worth carrying forward from building it:**

- **An endpoint filter cannot confine Blazor to a port.** Endpoint filters run in the routing pipeline
  controllers and minimal APIs use; Razor component endpoints are not dispatched through it. Applying
  `RequirePortEndpointFilter` to `MapRazorComponents` compiles cleanly, reads exactly as though it works,
  and was **measured doing nothing** - every admin page still answered 200 on the media port.
  `AdminSurfaceMiddleware` does the job instead, because middleware runs whoever ends up handling the
  request. It covers `/admin`, `/_blazor`, `/_framework` and `/_content`.
- **`UseAntiforgery()` must sit between `UseRouting()` and the endpoints.** Interactive components carry
  anti-forgery metadata, and with the call in the wrong position every admin page fails at *request* time
  with a 500 while startup reports nothing wrong. This pipeline has an explicit `UseRouting()`, so
  "anywhere in the middleware chain" is not good enough.
- **Controllers now straddle both ports**, so `MapControllers` can no longer be pinned to one:
  `AdminOrMediaPortEndpointFilter` decides per request by path prefix. A controller bound to the wrong
  port answers 404 exactly as a missing file does, which is why this was worth a named type rather than
  an inline lambda.
- **Delivery must be one implementation, not two.** The admin controller was written reading the cache
  but never filling it, reasoning that an operator glancing at a file should not disturb what a renderer
  is streaming. That was wrong: watching a film through the admin page is the same workload as watching it
  on a television, and the difference made the acoustic goal depend on which port the request arrived on.
  `MediaContentResolver` is now the single decision for both, covered by `MediaContentResolverTest`, and
  the responsive layout, the thumbnail identifier and this all came from the maintainer using the deployed UI -
  none of the three was visible from a local run.
- **The admin UI does not link to the media port at all.** `AdminMediaController` serves previews from
  `/admin/media/*`, reading the same sources as `FileServerController` - cache, then database copy, then
  file. A second door onto the same content, not a forwarding hop: an HTTP proxy to the other port would
  leave that port equally reachable and add a round trip. This is also what lets the UI work where only
  the admin port is reachable.

**Abstractions moved into Core** so the Razor class library could consume them without referencing the
Host (nothing references the Host): `IServedFileCache`, `CachedContentClass`, `ServedFileCacheReport`
(-> `DlnaServer.Core.Delivery`), `IApiBlocker` (-> `DlnaServer.Core.Diagnostics`), and
`ISubscriptionStore` with `EventSubscription` (-> `DlnaServer.Core.Gena`). All are interfaces or records
with no implementation dependencies, so Core remains dependency-free and this is where the layering story
already said abstractions belong. `ISettingsWriter` is new in `Core.Configuration`, implemented by
`SettingsWriter` in the Host.

**Settings persistence.** `SettingsWriter` rewrites only the `Dlna` section of `config.json`, so anything
else a deployment keeps in that file survives an edit; it copies the previous file aside first, and writes
through a temporary file that is then moved into place - the configuration provider watches this path, and
a partial write would be read as corrupt by the very reload the write triggers.

**Palette** sampled from `Resources/images/icons/large.png` rather than invented: the icon is
overwhelmingly `#b9e1fa` with `#9cd4f9` as its saturated edge and `#f9f8f6` as its warm white, so the
admin pages and the icon a television shows are recognisably the same product.

### M9 — Memory tuning pass

**Status 2026-09-03: the settings changes are APPLIED and NONE of them is measured.** Review findings
31-35, 39 and 40 landed as one batch, and the build carrying them is
**not on the NAS** - every reading in section 3b predates it. So the next M9 action is a deploy plus a
run under `-HeapLimitPercent`, not another edit. Writing more memory code before that reading would be
guessing, and this section's own rule already says a reading without a heap limit is inadmissible.

- [ ] Measure `/manage/memory` after indexing 20k files and a few days of use. **Partly done 2026-09-04**
      - a 12.9 h reading on 25,504 files, plus a controlled before/after across one forced collect
      (§3b, "Sixth reading"). Neither is admissible: no heap limit, and 12.9 h is not "a few days".
- [x] ~~Compare Server GC vs Workstation GC~~ - **decided: workstation GC, concurrent on**, set in the
      Host csproj (the only place that works - see section 3a) and verified in the generated
      `runtimeconfig.json`. `System.GC.ConserveMemory` stays at 5. **Now MEASURED on the NAS too
      (2026-09-04), so this item is closed rather than merely decided**: `isServerGc: False` read live off
      a 12.9 h process, **20 threads** against the 27-30 every Server-GC row here reports, and the 288.1 MB
      post-collect working set is a workstation-GC figure. The "re-open it if the numbers disagree"
      condition is not triggered - they agree. The `isConcurrentGc: False` worry is closed: that field
      describes the last collection, not the configuration.
- [ ] Check RSS after a large thumbnail batch on Linux, with and without `MALLOC_TRIM_THRESHOLD_`.
      **`MALLOC_ARENA_MAX` is now 2 rather than a no-op 32**, so this measurement means something it did
      not before. **PROMOTED 2026-09-04 to the most valuable open item in this milestone.** After the
      forced collect the managed heap is **19.4 MB against a 288.1 MB working set**, so **~269 MB - 93% of
      the footprint - is native or non-heap**, which is exactly what this item measures and what findings
      33/34/35 target. The GC half of M9 is effectively finished; the remaining gap is native.
- [x] ~~Confirm Gen2 collection rate is far below the Reference's ~60/day~~ - **MEASURED 2026-09-04 and it
      FAILS.** 42 collections in 12.9 h is ~**78/day**, *above* the reference's ~60 and 4x the <20/day
      target. (50 by the time of the second reading, but 8 of those were the operator's `Clear and
      collect`; 42 is the unassisted count.) Two things before this is acted on. The box is the anomaly
      this section keeps warning about - 9,977 MB visible, no heap limit - and **a heap limit will push
      this number UP, not down**, because pressure is what triggers collection. And the <20/day target was
      written to indict the reference's forced blocking collect on *every cache eviction*, not to
      constrain a healthy GC. **So §3's <250 MB working set and <20 Gen2/day targets pull against each
      other on this workload, and this file has never reconciled them** - the low RSS reading came *from*
      collecting. Decide which target is real before treating 78/day as a defect.
- [x] **The cache-architecture decision is settled: whole-file media buffering stays.** The maintainer chose to
      keep `MediaCacheBacklog` / `MediaCacheFillHostedService` / `CachedContentClass.Media` rather than
      take the review's deletion, with the limits at 32 MB per file and a 256 MB budget - which admit no
      film, so a film is measured once, recorded as excluded, and never re-enqueued. The machinery is
      inert rather than churning and media takes the `SendFileAsync` path. **Do not raise
      `MaxFileSizeInMegabytes` back to a film-sized value without measuring**: that is the 5,073 MB
      mechanism, and it needs a replacement for the LOH compaction this server dropped.

### M10 — Deployment

- [x] `NasBuild.sh` run on the NAS for the first time - it failed, and the three defects it exposed are
      fixed and recorded under "Deployment traps" in section 7. The fixed publish command is verified
      locally for linux-x64: only the Host is renamed, every dependency keeps its own name, and
      `libSkiaSharp.so` and `libe_sqlite3.so` are both in the output.
- [x] **The server builds, deploys, starts and serves on the NAS.** It took four runs and six distinct
      defects, every one of them recorded under "Deployment traps" in section 7: a compile failure, a
      publish failure, a publish folder missing a third of its assemblies, and a startup crash. The script
      now verifies its own restore graph *and* its own published output, so each of those fails loudly at
      the step that caused it rather than silently three steps later.
- [x] `nohup` does not exist on this NAS. The run step uses plain redirection plus `disown`, which is
      what the reference does - with the redirect added, so a failed start leaves a `stdout.log` behind
      instead of writing to a terminal that is about to close.
- [x] **VLC**: discovers the server, browses the tree, plays files.
- [x] **LG television**: lists the library, plays a file, shows thumbnails, and seeks - **confirmed by
      the maintainer 2026-09-04** against the 2026-09-03 23:11 build. The listing defect described below is history.
- [x] Verify the rest against real TVs: playback, seek, thumbnails - **confirmed 2026-09-04** on the LG.
      Two limits on that evidence, stated rather than implied: which media *kinds* were played was not
      enumerated, and it was the 2026-09-03 23:11 build, so none of the 2026-09-04 work has been seen by
      a television.

**A deployment with no source folder now starts.** `DlnaOptionsDefaults` (Host/Configuration) serves
`AppContext.BaseDirectory` when `Library.SourceFolders` names nothing usable, and excludes the thumbnail
cache folder and `Resources` from that library. Both exclusions are necessary rather than tidy: the cache
defaults to a subdirectory of the application folder, so without the first the fallback trips the validator
rule it would otherwise satisfy; and `Resources/images/icons` holds 13 JPEGs, so without the second a fresh
deployment offers the server's own artwork to a television as a photo album - observed on the first run,
which indexed exactly those 13 files. Verified live on a fresh database: the server starts, warns which
folder it fell back to, and indexes 0 files.

#### The television saw the server and none of its content

The log separated the two renderers cleanly, which is what made this findable: VLC browses with
`RequestedCount: 5000` and descends into containers; the television browses `ObjectID: 0` with
`RequestedCount: 100`, receives 31 objects, and asks again - three times, never descending.

**Cause: the root listing declared the wrong `parentID`.** The root surfaces the most recently indexed
files alongside the source folders, and each of those files was declaring the folder it physically lives
in. A renderer browsing container `0` was handed objects claiming to belong somewhere else. The
specification requires every object in a `BrowseDirectChildren` listing of C to declare `parentID` = C,
and the reference satisfies it by construction with `isRootFolder ? "0" : realParent`. VLC ignores the
violation; the television rejected the listing.

Fixed by making the parent an argument rather than deriving it: `DidlMapper.MapContainer`/`MapItem` take
the browsed container, and `DidlMapper.ParentIdOf(...)` supplies the object's real parent for
`BrowseMetadata`, which is the one reply where the object describes itself. Covered by `DidlMapperTest`
and verified on the wire - every object in a root listing now reports `parentID="0"`.

**Two hypotheses were tested and discarded first**, both plausible enough to be worth recording: that the
`Filter` implementation was stripping required properties (it only ever removes `albumArtURI`, `icon` and
`dc:date`, never `upnp:class`, `dc:title` or `res`), and that the Samsung `X_` extensions were involved
(they are not LG's). The reference's own mapper settled it.

**Still unconfirmed on the device.** If the television still shows nothing after a redeploy, turn on
`Dlna.Server.DebugMode` and read the new per-request log described below - it names the renderer.

#### Per-request connection logging

`Diagnostics/ConnectionLoggingMiddleware` logs one Debug line per request: method, path, remote and local
`address:port`, `Range`, and **`User-Agent`**. The reference calls a connection-logging helper by hand in
every controller action and SOAP operation; one middleware covers the same ground plus the SOAP endpoints
and anything added later. `User-Agent` is the field the reference does not record and the one that matters
most here - it names the renderer, so a log reads as "the television did this, VLC did that" instead of
being inferred from request shapes. The Browse telemetry line also carries the requester and the `Filter`
now. All of it is silent until `Dlna.Server.DebugMode` turns Debug logging on.

#### Thumbnails moved to the media, and are reused

Changed to the reference's layout after deployment showed why it matters - see the M4 section for the
detail. Two things were verified live: thumbnails land at `Films/.@__thumb/Film.jpg.jpg`, and after wiping
the database and re-indexing from scratch their timestamps were **unchanged**, so existing previews are
adopted rather than regenerated. For a library the old server has already previewed, that means no
thumbnail work at all - and for video, no ffmpeg.

#### FriendlyName

`ServerOptions.FriendlyName` already defaulted to `ZEN DLNA Server ({Environment.MachineName})`, matching
the reference, but the shipped `config.json` pinned it to the bare name so the default never applied. The
pin is gone from the repository copy. **A deployed `config.json` survives redeployment**, so an existing
installation keeps the old value until that line is deleted by hand.

**Open on the NAS, from the first run's log:**
- **A fallback library records empty directories.** Serving the application folder indexes 89 containers
  (`runtimes/linux-x64/native` and friends) with no media in them, so a renderer browsing an unconfigured
  deployment sees a tree of empty folders. Harmless, and the startup warning says what to do about it, but
  a directory that contains no media at any depth arguably should not become a container at all. Pre-dates
  this change - it is how M3 indexing has always worked.
- **`global.json` is pinned to SDK 8.0.414 with `rollForward: latestPatch`.** It was briefly removed so
  the NAS could build, then restored in this form: the NAS resolves exactly 8.0.414, a developer machine
  resolves the newest 8.0.4xx it has (8.0.424 here), and neither can silently drift onto the .NET 9 or 10
  SDK. **Removing it is not free** - with no pin, the dev machine selected SDK 10.0.303 and the solution
  went from 0 warnings to 10, all `CA1873` from a new analyzer in pre-existing M4/M5 code, with no source
  change at all. The 0-warning gate only means something while both machines are on one SDK band.

---

## 6b. Backlog raised 2026-09-02

The maintainer's notes from the night, each checked against the code and the reference before being written down
here. The items that turned out to be defects were promoted into "Do these next" in section 8; what
remains is real work with no urgency attached yet. This section is the source of truth.

| # | Item | Verdict | Effort |
| --- | --- | --- | --- |
| 1 | `soap:encodingStyle` missing, LG could not see content | **Done** - fixed, and now tested | — |
| 2 | No log line when a thumbnail or metadata is created | **Fixed 2026-09-02** - `LogMetadataStored` at Information mirrors the reference; `GeneratedThumbnail.WasAdopted` now separates adoption (Debug) from generation (Information) | — |
| 3 | Log levels differ from the reference | **Fixed 2026-09-02** - per-range serving moved to Debug; the polled `GetTransportInfo`/`GetPositionInfo` split off `LogActionRequested` into a Debug `LogStateQueried` | — |
| 4 | Must keep running through a per-file failure, as the reference does | **Fixed 2026-09-02** - section 8 item 1 | — |
| 5 | Classes too mixed up - whole-project structure, not one namespace | **DONE 2026-09-02** - namespaces split and every suffix reconciled - see below | — |
| 6 | Slow queries belong in their own file | **Fixed 2026-09-02** - `logs/slowQuery.log` via a filtered sub-logger, and excluded from `app.log` | — |
| 7 | Split `HttpHead` from the other verbs | **Fixed 2026-09-02** - `HeadFile` and `HeadThumbnail` are their own actions and touch neither the cache nor the backlog | — |
| 8 | Resource translations, English only for now | **Deferred to M8, deliberately** - see below | medium |
| 9 | Cache eviction only happens on the next access | **Correct** - see below | medium |
| 10 | Why a private `MemoryCache` rather than the DI `IMemoryCache`? | **Answered** - see below | — |
| 11 | `/manage/memory` differs from the reference | **Fixed 2026-09-02** - added the fields that answer the memory question, including per-generation size and fragmentation | — |
| 12 | Music videos can carry an embedded thumbnail or lyrics-as-subtitles | **Needs a sample file first** - see below | medium |
| 13 | Why more than 5 GB on the live server? | **Answered** - section 3b, "Second reading" | — |

**Item 8 is deferred to M8 on purpose, not forgotten.** Nothing user-facing is localisable yet: log
messages are not translated, and DIDL-Lite, `description.xml` and the SCPD documents are protocol text
that must never be. The only real consumer is the Blazor admin UI, which does not exist. Building an
`IStringLocalizer` layer with no caller is the speculative abstraction section 2 rules out, and it is not
free either: `Directory.Build.props` sets `InvariantGlobalization=true` and
`SatelliteResourceLanguages=en`, the second deliberately, to kill 234 `NETSDK1188` warnings and 13 locale
folders on the NAS. Reversing both is a deployment decision. **Do it as part of M8, with the first
component that actually shows a string** - and note that leaving `SatelliteResourceLanguages=en` in place
would silently strip any non-English satellite assembly at publish, so translations would work locally and
never ship.

**Item 12 needs a sample file before any code.** Nothing in the tree touches embedded art or lyrics -
confirmed by grep - so this is new work rather than a defect. Two unknowns decide the shape and neither
can be settled by reading: whether `Xabe.FFmpeg`'s `IMediaInfo` surfaces a stream's `attached_pic`
disposition (if not, the cover has to come out through a conversion, `-map 0:v -frames:v 1`), and whether
the lyrics in the actual files are a text subtitle stream - which the pipeline already maps into
`SubtitleStreamInfo` - or an ID3 `USLT` tag, which nothing reads today. Writing it blind would mean
shipping unverifiable guesses through the media pipeline. **Put one music video with embedded art and
lyrics somewhere readable and name it here**, then the work is small: adoption already short-circuits
thumbnail generation, so embedded art is another source ahead of ffmpeg's frame grab.

**Item 5, first half done: the five namespace splits landed 2026-09-02** as pure moves, no type renamed,
0 warnings and every test still green. `README.md` → "Layout" carries the resulting map. Two things came
out of doing it that were not visible from reading:

- **`Media.Processing.FFmpeg` does not compile as a namespace name.** It shadows Xabe's `FFmpeg` *class*,
  so `FFmpeg.GetMediaInfo(...)` resolves to the namespace and every call under it breaks. Renamed to
  `Media.Processing.Provisioning`. Any namespace segment that matches a type name in a package this code
  calls statically will do the same.
- **`ContractImmutabilityTest` matched the contracts namespace with `==`.** Moving DTOs into
  `Core.Contracts.Scanning` and friends would have quietly exempted them from the immutability rule -
  the governance would have shrunk while the test stayed green. Now `StartsWith`, so every sub-namespace
  is covered. Worth checking any architecture rule that names a namespace before moving types into it.

`SoapContractWireNameTest` was added to hold the assumption the move relied on: all 21 response contracts
pin their element name with `[XmlRoot]`/`[MessageContract]`, so the CLR namespace is not part of the
contract. Verified across all 21 before moving, and mutation-checked afterwards by removing one attribute.

**The rename half is next**, and the entity suffix is done: every EF entity now ends in `Entity`
(`MediaFileEntity`, `ThumbnailEntity`, …), which also frees the navigation properties to keep the bare
name - `MediaFileEntity.Thumbnail` is a `ThumbnailEntity`. That rename is exactly where a blind
find-and-replace does damage, because `MediaFile` and `Thumbnail` were each both a type *and* a
navigation property on the other entity; the replacement was restricted to type positions and the two
property names verified afterwards. **`dotnet ef migrations has-pending-model-changes` is the proof that
matters here** - it reported "No changes have been made to the model" before and after, so the rename
touched no column, key or relationship. Run it after any entity edit; it needs
`--startup-project src/DlnaServer.Persistence` because the Host does not reference the Design package.

**The rename half is now complete too.** The stream entities dropped their redundant `Info`
(`AudioStreamEntity`, `VideoStreamEntity`, `SubtitleStreamEntity` - `*Entity` already says what the type
is); the configurations track the entity type name including its suffix
(`MediaFileEntityConfiguration` maps `MediaFileEntity`), which is safe because
`ApplyConfigurationsFromAssembly` finds them by reflection rather than by name; and the two parameter
objects wearing the config-binding suffix became `BrowseRequest` and `LibraryScanRequest`, so `*Options`
now means exactly one thing: bound from `config.json`.

**`MediaProcessingSettings` deliberately kept its name.** It was renamed to `MediaProcessingRequest` and
reverted, because `*Request` misdescribes it: it is built once per *batch* and handed to every file in the
pass, so it outlives a single call - and `MediaProcessor.GenerateThumbnailAsync` already holds a local
named `request` for its `ThumbnailRequest`, which the parameter would have collided with. `README.md` now
gives `*Settings` its own row - options *resolved* for one operation - so the suffix has a defined role
rather than being an unresolved duplicate of `*Options`.

The renames touched no schema: table names come from explicit `ToTable(...)` calls, and
`has-pending-model-changes` reported "No changes" after each pass.

**The original finding, for reference.** First recorded as "`Upnp.Soap` holds
26 types", which was too narrow: the complaint is that types of unlike kind sit together throughout, so a
folder does not tell you what you are looking at. 173 top-level types across 28 namespaces, and the
crowding was by **role mixing** as much as by count:

| Namespace | Types | What is mixed |
| --- | --- | --- |
| `Upnp.Soap` | 26 | 4 service interfaces + 21 response contracts + the SoapCore envelope. Split by service (`.ContentDirectory`, `.AvTransport`, `.ConnectionManager`, `.MediaReceiverRegistrar`) gives 8/8/6/3 |
| `Core.Contracts` | 19 | Repository DTOs + scanning parameter objects + watching events + processing results. Only the DTOs should be reachable from persistence, which would sharpen the architecture test too |
| `Host.Upnp` | 9 | SOAP control implementations + SSDP discovery services + DIDL mapping - three unrelated jobs, where `DlnaServer.Upnp` already models the `Soap`/`Ssdp` split properly |
| `Host.Delivery` | 8 | The byte cache and the prefetch backlog, two subsystems |
| `Media.Processing` | 7 | ffmpeg provisioning + image encoding + orchestration |

Every response contract pins its own wire name through `[MessageContract]`/`[XmlRoot]`, so the CLR namespace
is not part of any contract and these are pure moves - verified across all 21. The naming half of the same
problem is now written down in `README.md` → "Class name suffixes", which also records the three suffixes
that currently mean two different things (`*Options` as both config and parameter object, `*Settings`
duplicating `*Options`, `*Info` as both entity and value). Fix the names and the folders together: a move
that keeps a misleading suffix has not finished the job. This touches committed code across every project,
so it needs agreeing before it starts, and it wants its own pass rather than riding along with a fix.

**Item 7 is not only tidiness.** `GetFile` carries `[HttpGet]` and `[HttpHead]` on one action, so a HEAD
probe runs the entire GET body - including `_cacheBacklog.TryEnqueue(...)`, which queues a **whole-file
read into memory**. Renderers probe with HEAD for size and type before committing to a transfer, so a
probe the renderer may never follow up on can pull up to `MaxFileSizeInMegabytes` (512) into the LOH. A
separate HEAD action that sets the headers and the content length, and touches neither the cache nor the
backlog, fixes the crowding complaint and one contributor to section 3b at the same time.

**Item 9, and what the reference actually does.** `MemoryCache` expiration is lazy: an entry past its
sliding window is not evicted until something touches the cache or the 30 s `ExpirationScanFrequency`
sweep runs (*it only runs when something touches the cache - see section 6t*), and until then it keeps its payload reachable - so with the byte cache that is up to 512 MB
per entry held past its own expiry. The reference's
`MemoryCacheHelper.ScheduleCacheKeyEviction` is **not** a timer on the entry and never removes the key it
is given. It is a fire-and-forget `Task.Run` that waits `delay + clamp(delay/2, 2s, 10s)`, writes a
*decoy* entry under `_ScheduleCacheKeyEviction <key>`, waits that decoy's own 10 s sliding window, then
removes it - two cache touches whose only purpose is to make the lazy scan notice the real entry has
expired. The eviction callback it thereby triggers is what fires the LOH compaction discussed in section
3b. Worth reproducing the *effect* - prompt release of large payloads - without reproducing the shape:
its `AddOrUpdate(key, addValue: new CancellationTokenSource(), ...)` allocates a `CancellationTokenSource`
on every call and abandons it undisposed on the update path, and the `finally` races the `using` against
another thread's `Cancel()`, which is why `ObjectDisposedException` needs its own catch arm. A timer or a
`PostEvictionCallback` is the honest version.

**Item 10 - why `ServedFileCache` owns a private `MemoryCache`.** Three reasons, in order of weight:
1. **`SizeLimit` is a property of the store, not of an entry.** Setting it on the shared `IMemoryCache`
   would impose it on every other consumer, and once a store has a `SizeLimit`, `MemoryCache` **throws**
   for any entry added without a `Size`. A shared store therefore forces a size contract onto unrelated
   code, and the byte budget would be shared with whatever else caches.
2. **`Clear()` and `Count` are on `MemoryCache`, not on `IMemoryCache`.** The clear-on-disable behaviour
   that makes the section 3b measurement possible cannot be written against the interface.
3. **Lifetime and disposal are ours** - `ExpirationScanFrequency` is tuned for byte payloads, and the
   store is disposed with the cache rather than with the container.

The cost is that the store is not injectable, so tests go through `IServedFileCache`. That is the seam
they should use anyway, so nothing is lost.

## 6c. Backlog raised 2026-09-03 (second batch) - ALL DONE

Four items, each verified against the code **and** against the live NAS before being worked on. Three were
real defects, one was already correct and is now pinned by a test.

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Same response to a TV and to the admin portal | **Fixed** - hiding is unconditional on every listing |
| 1b | A deleted file must leave the database even under an excluded folder | **Already correct** - now covered by a test |
| 2 | A folder with no media under it must not be shown | **Fixed** - at index time and at read time |
| 3 | Every save adds another `.@__thumb` and `@Recycle` | **Fixed** - the binder was appending to a non-empty default |
| 4 | Search files scrolls the whole page, search folders scrolls only its results | **Fixed** - results sit in a `.scroller` |
| 5 | `Thumbnails.SubFolderName` must be required and always skipped by scanning | **Fixed** |

Every one of them was then exercised against a running server on a purpose-built tree, not only against
the test suite - which is how the pruning cascade above was found. The whole set of readings is in the
"Verified by running it" note at the end of this section.

### One library, not two (items 1 and 1b)

**Every call site already passed `excludeHidden: true`** - the claim in section 4's summary that "the admin
UI and `/manage` still show everything" was stale. What was actually leaking was the three methods that had
no such parameter at all: `MediaFileRepository.SearchAsync`, `MediaDirectoryRepository.SearchAsync` and
`GetSourceRootsAsync`. Both admin search pages therefore returned content the browse tree had hidden.

The fix is not another `true` at those three sites. **The `excludeHidden` parameter is gone from both
repository interfaces** and hiding is applied inside the repository, because a flag with one legal value is
a way for the two surfaces to drift apart again. A television and the admin UI are now answered by the same
methods, and there is no argument that can make them differ.

What stays unfiltered, and why each one matters:

- **Lookup by identifier or by path.** A renderer mid-stream is not cut off when its folder is hidden, and a
  hidden file's preview page still opens from a link. `GetByPathAsync` is also how the indexer reads.
- **`GetExistingPathsAsync`.** It decides what to insert. Hiding an indexed row from it makes the next scan
  re-insert that path and violate the unique index.
- **`GetIndexedPageAsync`.** It decides what to **delete** - and this is item 1b. Filtering it would strand
  a hidden folder's rows permanently after the files were deleted from disc. It was already unfiltered, so
  item 1b needed no change; `IndexAsync_WhenAHiddenFolderesFileIsDeleted_StillRemovesItsRow` now pins it,
  because nothing stopped a future change from "tidying" that filter in.

**The exclusion MATCHING rule changed on 2026-09-03, from a customer report**.
An entry is now a folder name, a partial path or a full path as it reads on the server, matched on
**whole path-segment boundaries**, with both separators treated as equivalent. Before, hiding was a raw
substring over the whole path - what the reference does - so an entry of `path1` also hid `path1L` and
`path10`, while scanning compared single segments and carried on importing that folder, and an entry
containing a separator was refused by `DlnaOptionsValidator` outright. So the operator could neither
name one branch precisely nor see that far more was hidden than they had asked for.
**Both halves now share one rule**: `PathExclusion.IsExcluded` in memory, and in SQL a LIKE over
`'/' || replace(FullPath, '\', '/') || '/'` against `%/entry/%`, which is what makes the boundary
alignment expressible in one clause per entry. `PathExclusion.IsHidden` forwards to `IsExcluded`, so the
deliberate scan/read asymmetry the old comments describe is gone. Three predicates carry the SQL form -
one in `MediaFileRepository`, two in `MediaDirectoryRepository` - and they must stay in step with the
matcher; `MediaFileRepositoryTest` asserts the SQL half separately for that reason, since a
segment-aligned rule in C# and a substring in SQL would pass every `PathExclusionTest` and still ship
the bug. `DlnaOptionsDefaults` de-duplicates on a canonical form (separators folded, surrounding ones
trimmed) so the same folder cannot be listed twice through the admin UI, which rewrites the list on
every save.

**One more listing was leaking, found by the 2026-09-03 review and closed the same day (finding 53).**
`GetLanguagesAsync` queried `AudioStreams` / `SubtitleStreams` directly, so a hidden folder's audio and
subtitle languages still populated the search page's filter dropdowns - which both told the operator what
was in there and offered a tick that then matched nothing visible. It now reaches those tables **through
`Files`**, `ExcludeHidden(...)` then `SelectMany` over the navigations, because the predicate is written
against `IQueryable<MediaFileEntity>` and a query rooted at the stream tables could never use it.
**There are now no unfiltered listings left** - only the three deliberate exemptions above.

### Folders with no media, at both ends (item 2)

Live evidence first: the source root's nine children included `/share/Media/.@upload_cache` (0 subfolders,
0 files) and `/share/Media/.streams` (66 subfolders, 0 files). Both were being offered to the television.
The cause is that `LibraryScanner.EnumerateDirectories` walked the volume and yielded **every** folder.

Fixed at **both** ends, deliberately, because neither half covers the other:

**Index time.** `EnumerateDirectories` now derives its set from the files: each folder holding an indexable
file, plus its ancestors up to the source root, which is always yielded. Candidates are matched on the path
alone - no `FileInfo`, so the pass costs no `stat` calls; the size and timestamp reads still happen once, in
`EnumerateFiles`. A new `LibraryIndexer.PruneEmptyDirectoriesAsync` then removes any indexed folder that
set no longer contains. **Because insertion and removal read the same set, the pass is idempotent** - a
folder cannot be pruned on one scan and re-added by the next. Measured on a purpose-built tree: an empty
three-level chain collapsed in one scan (`-1 file(s) and -3 directory(ies) removed`), and the scan after it
reported `-0 and -0`.

**It prunes LEAVES ONLY, repeatedly, and that shape is load-bearing.** The first version removed every
not-discovered folder in one sweep, exempting the ones `ExcludeFolders` hides. It was wrong, and only
**running the server** showed it: excluding `Private` correctly exempted `Films/Private`, then `Films` -
which now led to nothing discovered and was *not* itself excluded - was removed, and the cascade took the
exempt folder and its file with it. The index went from 5 directories and 2 files to 3 and 1. So the
exemption held for one folder and undid itself one level up.

Pruning only folders with nothing indexed under them closes it by construction: the excluded folder is
never removed, so its parent never becomes a leaf, so the exemption actually holds. Each pass collapses one
level, bounded by `MaxPrunePasses` (64) so a cycle in the parent data cannot spin inside a scan.
`IndexAsync_WhenOnlyAnExcludedChildHoldsMedia_KeepsBothItAndItsParent` is the regression guard; note that
the test I wrote *before* finding this passed against the broken code, because it put the excluded folder
directly under the source root, which is always discovered. **A test for an exemption has to put the
exempt thing where its ancestor can also be pruned.**

`ReconcileDirectoriesAsync` keeps only its two original tests - the folder is gone from disc, or
configuration no longer covers it - and is now documented as deliberately not caring about emptiness.

**Read time**, because exclusion is a runtime setting and index-time pruning cannot see it: a directory is
listed only when its subtree holds a **visible** file. That is what covers the reported case - media moved
under an excluded folder leaves its parent an empty container - and it takes effect on the next request
rather than waiting for a rescan.

**The read-time predicate is a path range, not a prefix match, and that was the whole design problem.**
`f.FullPath LIKE d.FullPath || '/%'` cannot use an index: SQLite's LIKE optimisation needs a literal or a
bound parameter, not a value from the outer row, so it would scan all 25,504 file rows once per candidate
folder. Written as `f.FullPath > d.FullPath || '/' AND f.FullPath < d.FullPath || '0'` it is a range, and
`0` is `/` plus one so the range holds exactly the subtree - a sibling named `Films2` sorts above the upper
bound and is excluded, which a lower bound alone would not do. **Both separators are tried** (`\` paired
with `]`), because a stored path carries the separator of whichever host indexed it and reading
`Path.DirectorySeparatorChar` would match nothing at all on a Windows box holding a NAS index, silently.

Verified rather than assumed - `EXPLAIN QUERY PLAN` over the emitted SQL, with the exclusion predicates
present:

```
SCAN d
MULTI-INDEX OR
INDEX 1
SEARCH f USING COVERING INDEX IX_Files_FullPath (FullPath>? AND FullPath<?)
INDEX 2
SEARCH f USING COVERING INDEX IX_Files_FullPath (FullPath>? AND FullPath<?)
```

A covering-index range seek per candidate folder, one per separator.

**Then measured on the real library, because a right plan is not the same as a small constant.** Against
25,504 files in 1,055 directories, over the root browse, all seven top-level folders and fifteen `Films`
subfolders: **not one entry in `logs/slowQuery.log`**, whose threshold is 50 ms. End-to-end request times
over the network were 27-66 ms, the slowest subfolder 50 ms, and the one 320 ms outlier was `Films`
serialising 41 full file DTOs rather than anything the predicate did. The cost is real but it is not on
the order that matters.

### The exclusion list doubled on every save (item 3)

Confirmed live, `GET /manage/configuration`:

```
"excludeFolders": [".@__thumb", "@Recycle", ".@__thumb", "@Recycle", "Personal", "Films/Private"]
```

**`ConfigurationBinder` adds to an existing `IList<string>` instead of replacing it.** `LibraryOptions`
declared `ExcludeFolders = [".@__thumb", "@Recycle"]` as a property initializer, so `config.json`'s two
entries landed on top of the two defaults; the admin UI then saved the four-entry list back, and the next
reload made it six. The "shows `.@__thumb` twice" note under section 8's display defects blamed
`DlnaOptionsDefaults` for this, which was wrong - that method only adds anything when no source folder is
configured.

The initializer is now `[]` and `DlnaOptionsDefaults` seeds the pair **only when configuration names none
of its own**, so an operator who lists their own exclusions gets exactly those and can drop `@Recycle` if
they want to. It de-duplicates case-insensitively either way, which is what heals the file already on the
NAS on its next load. `Apply_AppliedTwice_ChangesNothingTheSecondTime` is the test that matters: binding,
`PostConfigure` and a save through the admin UI form a loop, and any pass that adds something turns that
loop into unbounded growth.

**Watch for this shape anywhere else** - a collection property with a non-empty initializer that is bound
from configuration. `SourceFolders` is already `[]` and is the only other one.

### `Thumbnails.SubFolderName` is required, and always skipped (item 5)

It is now `[Required(AllowEmptyStrings = false)]`, and `DlnaOptionsDefaults` adds it to `ExcludeFolders`
unconditionally - listed or not. The validator rule that **failed startup** when the two settings disagreed
is gone, because there is nothing left for them to disagree about.

Two consequences worth knowing:

- **`Thumbnails.CacheDirectory` is now unreachable.** Blank `SubFolderName` used to select it, for a
  read-only media volume. The setting, its validation and the central-cache branch in
  `MediaProcessingHostedService.BuildThumbnailPath` are all **kept and marked as unreachable in their XML
  docs**, so restoring that path is a one-line change rather than a redesign. Removing them is a decision
  for the maintainer, not a tidy-up.
- The name reaches `ExcludeFolders`, so it is written into `config.json` on the next save and shows on the
  settings page. That is once, de-duplicated, and self-documenting.

### Search files now scrolls like search folders (item 4)

`SearchFolders` renders its results through `FolderList`, which wraps them in `.scroller`
(`max-height: 60vh`); `SearchFiles` rendered a bare `.tiles` grid, so the page itself grew and the `<h1>`
scrolled off the top. The tiles now sit in the same `.scroller`.

Measured in a browser on 61 results, 900 px viewport: the results box is 540 px and scrolls internally
(content 2424 px), and with the filter panel collapsed the **page** overflow is 0 - only the results move.
With the panel expanded 242 px of page scroll remains, because the files page has ten filter fields to the
folders page's two and the panel alone is 444 px of the viewport. The pinned panel keeps the filters on
screen through it. Closing that last 242 px means giving `main` a viewport-height flex column so the
results box takes the space actually left instead of a flat 60vh - that changes the scroll behaviour of
**every** admin page, and section 7's pinned-panel note records that page scroll was the deliberate choice
there, so it is a decision rather than a tidy-up.

### Verified by running it, not only by the suite

Local run on a purpose-built tree - `movies/action/film.mkv`, `Films/Private/hidden.mkv`,
`@Recycle/gone.mkv`, `extras/notes.txt`, `movies/.@__thumb/film.mkv.jpg`, and an empty
`empty/nested/deeper` chain:

| Reading | Result |
| --- | --- |
| First scan | 2 files in **5** directories - `empty/nested/deeper` and `extras` never indexed (would have been 9 before) |
| `excludeFolders` after two runs | 3 entries, no repeats, unchanged between runs |
| Excluding `Private` on the second run | `-0 file(s) and -0 directory(ies) removed`; `Films`, `Films/Private` and `hidden.mkv` all still indexed |
| Root children, same call the TV and the admin UI both make | `['movies']` - `Films` hidden because its only media is hidden |
| Recently added | `['film.mkv']` - `hidden.mkv` filtered out |
| Deleting the only file in the three-level chain | `-1 file(s) and -3 directory(ies) removed`, then `-0 and -0` on the next scan |

**One trap the local run exposed that the NAS does not have.** The read-side exclusion is a plain substring
test, so it is **separator-sensitive**: the entry `Films/Private` matches the stored path
`/share/Media/Films/Private` on the NAS and matches nothing at all on a Windows host, where the same folder
is stored as `...\Films\Private`. It fails silently - the folder simply stays visible. The maintainer's live
configuration is fine because the NAS stores forward slashes. Worth fixing if this ever runs on Windows
for real; the fix is normalising separators on both sides of the comparison, and in SQL that means
`replace()` on the column, which these `instr` predicates already cannot index anyway.

## 6d. ffmpeg is absent on the NAS, and nothing said so (2026-09-03)

> **CLOSED later the same day.** `Thumbnails.DownloadFFmpeg` was turned on, ffmpeg and ffprobe
> downloaded into `publishNAS/ffmpeg/` at 14:08 (~157 MB, 4 seconds), and video thumbnails are being
> generated - one confirmed at 479x269 from an `.mp4`. Everything below describes how the absence was
> diagnosed and made visible, which is still worth reading; it no longer describes the state.
> The residue is a security one, not a capability one: the download switch is still on, and it fetches
> and executes an unverified third-party binary.


**Reported as "in the search-files, I do not see filter for audio/subs languages selections".** The
filters were working exactly as designed; there was nothing to offer. Behind that lay a whole capability
missing from a 25,504-file library, and the only trace of it was one log line from the day before.

The chain, each link confirmed against the live server:

1. `publishNAS/ffmpeg/` is **empty**, and `Thumbnails.DownloadFFmpeg` is `false` - so
   `FFmpegProvisioner` logged, once, at 2026-09-02 23:18:43:
   `ffmpeg is unavailable (not present and downloading is disabled). Indexing, browsing and streaming
   continue; video metadata and video thumbnails will be skipped.`
2. No ffprobe means **no video or audio metadata for anything**. A sampled `.mp4` reads
   `"metadataStamp": null`, `"audioStreams": []`, `"video": null`, `"subtitles": []`, `"duration": null`.
   Its thumbnail is there, adopted from the existing `.@__thumb` image, which is what makes the gap so
   easy to miss - the library *looks* processed.
3. Only images have metadata, because `ImageThumbnailGenerator` uses SkiaSharp and needs no ffmpeg. The
   `Stored metadata for ...` lines in `app20260902.log` are **all** `.jpg` and `.png`.
4. No audio or subtitle streams means `GetLanguagesAsync` returns two empty lists, and `SearchFiles.razor`
   rendered the whole language block only when at least one was non-empty. So the controls vanished.

**Two fixes, and the second is the one that matters.**

- **The empty case now explains itself.** `SearchFiles.razor` always renders a language field; with nothing
  indexed it reads "Nothing to offer yet" and the hint names ffprobe and `Thumbnails.DownloadFFmpeg`. A
  control that disappears is indistinguishable from a feature that was never built - which is precisely how
  this was reported.
- **`IMediaCapabilities` (Core) and a dashboard warning.** `FFmpegProvisioner` now also implements
  `IMediaCapabilities.IsVideoProcessingAvailable`, and the Dashboard raises a bad `Notice` when it is
  `false`. In Core because `DlnaServer.Admin` references only Core and Persistence - the same reason
  `IServedFileCache` and `IApiBlocker` live there.

**It is deliberately three-state, `bool?`, and that is not a technicality.** Availability resolves on
*first use*, so a freshly started server has not asked yet; reporting that as unavailable would put a
false alarm on the dashboard of every restart. Reading the property never triggers resolution either -
otherwise opening a page could start downloading a third-party binary as a side effect of rendering.
Verified all three states by running it: with ffmpeg present, no warning; with it renamed away but before
anything needed it, still no warning (`null`); after forcing a metadata pass with
`/manage/recreateAllFilesInfo`, the warning appears.

**What is still outstanding, and it is the maintainer's call.** Getting the binaries onto the NAS is a deployment
decision, and the reason `DownloadFFmpeg` was turned off in the first place is that it downloads and
executes an unverified binary from a third-party API:

- put `ffmpeg` and `ffprobe` into `publishNAS/ffmpeg/` - QNAP has them in QPKGs, or copy a linux-x64
  static build - and note that `NasBuild.sh` must not wipe that folder on a redeploy; or
- set `Thumbnails.DownloadFFmpeg` to `true` once, knowingly, let it fetch them, and turn it back off.

Either way, **the existing 25,504 files then need their metadata read**: Maintenance -> *Recreate
metadata*, or `/manage/recreateAllFilesInfo`. Expect it to take a long while, one file at a time, and
expect the language lists to stay short even afterwards - a stream contributes a language only if it
carries the tag, and downloaded `.mp4`s frequently do not.

Related, and now visibly the same class of problem: section 8's open question "Metadata extraction logs
nothing on success, at any level. The only evidence ffprobe is working is the absence of a warning."

## 6e. Rebuilding the index from the Maintenance page (2026-09-03)

Two actions, because they answer different problems and only one of them is cheap. Both discard the whole
index and scan again; the index is derived data, so neither touches a byte of media.

| | Rebuild index | Recreate database |
| --- | --- | --- |
| What goes | Every indexed row | The database file itself |
| Mechanism | `DELETE` + `VACUUM` + a scan request | Signal, restart, delete at startup, migrate, scan |
| Restart | No | Yes |
| Page survives | Yes, and reports what it removed | No, it loses its connection |
| Schema | Kept, already migrated | Rebuilt from the migrations |
| Use it when | Any time the index looks wrong | The *file* is the problem - a schema that will not migrate, space a rebuild did not reclaim |

### The file is deleted at startup, not when the button is pressed

This is the whole design, and it is the answer to "do not forget to close connections before deleting the
database". **The safe moment to delete a database file is when nothing holds it open**, and inside a
request handler is the opposite of that: every other scope's `DbContext`, the pooled SQLite connections
(`Pooling=True`, so a connection outlives the context that opened it) and any renderer query in flight
would all be pointing at a file that had just gone - and SQLite would quietly create an empty replacement
with no schema in it.

So `IDatabaseResetSignal` (Core) carries the request instead. `DatabaseResetSignal` is built in
`Program.Main` and handed to `BuildApplication`, exactly like `RestartSignal`, so it outlives the container
it was raised in - **with one difference: the loop does not clear it per iteration.** `DatabaseInitializer`
reads it before its health checks, deletes the file and its `-wal`/`-shm` sidecars, and only then clears
the request - a throw before that point leaves the request standing, so the next start retries rather than
opening the database it was told to discard. New outcome `DatabaseInitializationOutcome.Reset`, distinct
from `Recreated` because nothing was *wrong* with the file: it is deleted rather than kept aside, since an
operator reclaiming space does not want the old copy left behind.

**The sidecars are the part that is easy to miss.** A `-wal` left beside a deleted database is replayed
into the empty file SQLite then creates, so the rows the operator asked to be rid of come back.
`InitializeAsync_WhenAResetIsRequested_RemovesTheWriteAheadSidecars` covers it - and note that writing a
fake `-wal` in that test needs `SqliteConnection.ClearAllPools()` first, or the seed context's pooled
handle blocks the write on Windows.

### `ILibraryScanSignal` - the seam that lets the admin UI start a scan

Scanning lives in the host and the admin project references only Core and Persistence, so this joins
`IRestartSignal` and `IServedFileCache` in Core for the same reason. `LibraryIndexHostedService` now runs
its startup pass and then loops on the signal, so a scan can be asked for without a restart.

Two details that are decisions rather than details:

- **Requests collapse.** `LibraryScanSignal` is a `SemaphoreSlim(0, 1)` and `RequestScan` swallows
  `SemaphoreFullException` - that throw *is* the answer. A pass serialises against every other pass
  through `LibraryIndexer`'s process-wide gate, so queueing one per click would make an impatient operator
  wait through all of them and every pass after the first would find nothing to do. Checking
  `CurrentCount` first and releasing only when it is zero reads tidier and is a race.
- **The click does not await the scan.** A pass over 25,000 files takes minutes and would hold the
  circuit's `DbContext` for the duration. Fire and forget; progress is in the log.

### Two things found by running it

- **`ExecuteDelete` under-reports a cascade.** `DELETE FROM Directories` over a parent and its child
  returned **1**, not 2: SQLite implements a foreign-key cascade as a trigger, and trigger-deleted rows do
  not reach `changes()`. The operator would have been told half the truth about what had just happened, so
  `IndexMaintenance` counts both tables *before* deleting.
- **`VACUUM` is allowed to fail.** It needs the database to itself and this server is still serving, so
  `SQLITE_BUSY` is an expected outcome, never a reason to report the clear as failed - `IndexClearResult`
  carries `SpaceReclaimed` and the page says "the file kept its size because another connection was using
  the database". It also cannot run inside a transaction, which is why it follows the deletes.

### Verified end to end, and one pre-existing rough edge confirmed

Driven in a browser against a running server on a six-file tree. *Rebuild index* reported
`Removed 6 file(s) and 4 folder(s) and reclaimed the space`, the log showed `+6 added` moments later, and
the page stayed connected throughout. *Recreate database* restarted the host and the log read
`Applying migration 'InitialSchema'` ... `Applied 4 pending database migration(s)` then `+6 added` - four
pending migrations is the proof the file was genuinely gone, since a surviving database would have had
none. Each panel's confirm arms independently: arming a rebuild left `Restart server` / `Stop server`
untouched.

**One `[ERR] An error occurred using the connection to database 'main'` appears on shutdown when the
metadata pass is mid-write.** It is **pre-existing and not caused by this work** - confirmed by control
experiment, not by argument: a plain `/manage/restart` with pending processing reproduced it. Harmless
(the file's stamp is simply not updated, so the next pass retries it) but it is an ERR-level line that
looks alarming in a log. Worth a `catch` around the shutdown path in `MediaProcessingHostedService`
sometime.

## 6f. Backlog raised 2026-09-03, closed 2026-09-04 (third batch)

Three items. One was the maintainer's own fix needing verification, two were real gaps.

| # | Item | Verdict |
| --- | --- | --- |
| 1 | Recently added must stay in indexing order, not be re-sorted | **The maintainer's fix is correct and live** - verified on the NAS, then pinned by a test |
| 2 | Some `config.json` settings are not on `/admin/settings` (Database, for example) | **Fixed** - 11 settings added plus a full editor for the file-type map |
| 3 | No way to search for files with no metadata or no preview | **Fixed** - two filters and a status column |

### Recently added: the fix was right, the invariant was unprotected

Verified live rather than by reading: the root listing comes back `CreatedUtc` strictly descending, titles
not alphabetical, `FileCreatedUtc` not monotonic. `GetRecentlyAddedAsync` orders by `CreatedUtc`
descending and `Paginate` preserves order through `GetRange`.

**But the ordering was carried by the order of two statements** - the sort runs while the file list is
still empty, and the recently added files are appended after it. Moving the append above the sort restores
a customer-reported bug with nothing failing to compile. So the composition is now
`ContentDirectoryService.ComposeRoot`, `internal` for the same reason `Paginate` is, and
`ContentDirectoryRootListingTest` pins it under a title sort **and** a date sort. Mutation-checked: the
reintroduced defect fails 2 of its 3 cases.

**A date-sort case is not redundant with a title-sort case**, and the first version of that fixture proved
it - built with the file date ascending along the list, an ascending date sort reproduced the given order
by coincidence and the case could not see the defect. Both dates now descend along the list.

**Deliberate divergence:** for the root listing only, `SortCriteria` orders the folders and is ignored for
the recently added files. M5's table is annotated.

**Still open, not applied:** `Sort` is now only ever called with an empty file list, so its file-sorting
half is unreachable. Dropping it would remove the trap entirely rather than pin it - a change to committed
code that wants the maintainer's say-so, not a tidy-up.

### The settings page had eleven settings missing, and one of them can empty the library

Found by diffing every options class against the page rather than by looking: the whole **`Database`**
section, three server identity strings, `Library.UseFileCreationDateTime`, `Thumbnails.DownloadFFmpeg`,
`Compatibility.AlsoNotifyBroadcastAddress`, and `Library.MediaFileExtensions`.

**The four `FileCacheOptions` absolute expirations are deliberately still absent** - M6 states they are
not deployment knobs, so adding them would contradict a decision rather than close a gap.

**`Library.MediaFileExtensions` got a real editor** (`Shared/ExtensionMapEditor.razor`), with the
conversion and validation in `Configuration/ExtensionMap.cs` - a plain class, because a Razor component
cannot be unit tested in this solution and this is the half that can be got wrong. It refuses an empty
map, a duplicate extension and a line with no type chosen, each naming the line. **An empty map is the
one edit on that page that empties the whole library**: the map is read in exactly two places -
`LibraryIndexer` deciding what counts as media, and `ConnectionManagerService.GetProtocolInfo` advertising
what the server serves - and nothing seeds a default. A misspelt type is now impossible, where before
`Enum.TryParse` failed and the indexer skipped that extension in silence.

**The four `Database` ranges also gained plain-language `ErrorMessage`s.** They carried DataAnnotations'
own wording, so a refused value printed `The field CacheSizeInMegabytes must be between...` on a page
whose rule is that no property name reaches an operator. Those messages had been unreachable from the UI
until the fields went on the page.

### A blank profile and a null profile are the same thing on the wire

Worth stating because it is easy to conclude otherwise from the two `??` operators in the indexer, and
doing so produces a false alarm about changing what televisions receive.

`DlnaProtocolInfo.ResolveProfile` tests `!string.IsNullOrWhiteSpace(...)` before falling back to the
catalog, so a stored `""` and a stored `null` both resolve to the type's main profile and **emit identical
bytes**. The indexer's `configured.ProfileName ?? mime.ToMainProfileName()` does keep `""` as `""` in the
database - but the wire layer then treats it as absent anyway, so nothing downstream can tell.

The one place that uses `??` rather than `IsNullOrWhiteSpace` is `ContentFeaturesForThumbnail`, where a
`""` would emit a bare `DLNA.ORG_PN=`. It is unreachable: all three call sites -
`DidlMapper` and `FileServerController` twice - pass the fixed `ThumbnailProfileName`, never a file's own
profile. Checked, because it is the only path where the inconsistency would show.

### The whole reference MIME table, and profiles you can pick from

The catalog carried **36** of the reference's **138** MIME types, and three of the lists it did carry were
truncated - `.mp4` offered 8 of the reference's 33 profiles. Fine while a profile was a free-text box
nobody filled in; wrong once the editor offers a menu. All of it is now here: **102 types added, 138
total**, generated from `Reference/.../DlnaMime.cs` rather than typed.

**Two things made this dangerous, and both are now pinned by `DlnaMimeCatalogGrowthTest`.**

`MediaFileEntity.Mime` is stored with `HasConversion<int>()`, so **every existing number is in the
database** for 25,504 rows. New members therefore take fresh numbers at the top of their own band and
nothing was renumbered - verified by diffing the enum before and after, not by inspection. The test pins
all 37 original values.

**And a new type can steal an extension from an existing one.** `CreateExtensionIndex` is
first-writer-wins ordered by enum value, and the bands run Video &lt; Audio &lt; Image - so a *new video*
type outranks an *existing audio* one. `.mp3` briefly resolved to `VideoXMpeg` instead of `AudioMpeg3`,
which the existing suite caught. A new entry now never claims an extension that already resolves
somewhere; it keeps the rest of its list. The 16 contested extensions are pinned.

Three of the reference's members were **not** added: `AudioXmswma`, `SubtitleTtmlxml` and
`SubtitleXsubrip` are the same types this project already carries under corrected PascalCase and the same
MIME string. Adding them produced three `CA1708` case-collision warnings, which is how they were spotted.

### `DLNA.ORG_*` fields are computed from named flags now

`FlagsStreaming = "21F00000000000000000000000000000"` said nothing about what was being claimed. The
fields are built from `DlnaOrgFlags`, `DlnaOrgOperation` and `DlnaOrgContentIndex` - one documented bit
per capability, in the reference's own shape - and `DlnaProtocolInfo.Format` renders each in its own
notation: hex for flags, binary for the operation pair, decimal for the content index.

**The wire output is byte-identical**, which the golden tests already prove and
`DlnaProtocolInfoFlagsTest` now states outright: `21F00000...` and `20F00000...`, `01`, `0` and `1`, plus
a case per bit position. A flag added or removed by accident fails there rather than on a television.
`CA1711` objects to a type ending in `Flags`; suppressed in place, because `DLNA.ORG_FLAGS` is the name
of the field and renaming it away from the wire name is the only thing that would make it harder to read.

One thing the change makes visible that a comment had been carrying: **byte-seek is deliberately
under-declared**. `DlnaOrgOperation.ByteSeekSupported` exists, is understood, and is left out because the
televisions in use were validated against `01` - so the omission now reads as a decision rather than a
gap.

### Searching for what still needs work

Two filters, *Details read* and *Preview made*, each any / yes / not yet, plus a status row saying why -
waiting its turn, turned off, or a count of failed attempts.

**"Missing" is the absence of a stamp**, which covers a file never reached and one that failed every
attempt, and excludes a file that can never have a preview: `MarkThumbnailNotApplicableAsync` stamps audio
without producing an image, so reading the thumbnail row instead would hand an operator a whole music
collection as outstanding work.

`ExcludeFolders` needed no work - `SearchAsync` applies hiding before any filter - and a test pins that
for the new filters, because a filter added later could be written against an unfiltered query.

### Verified by running it, and the browser caught what the tests could not

520 tests pass at 0 warnings, and the filters still did nothing: `@bind` discards a `bool?` on a
`<select>` (section 7, "Blazor binding traps"). Live readings on a throwaway instance afterwards:

| Reading | Result |
| --- | --- |
| Settings page | 7 panels, 14 extension rows, 37 type options per row, all 11 new fields present |
| Every extension removed, then Save | refused - "At least one file type is needed..." and **nothing written to disc**, confirmed by re-reading the file |
| A line with no type, then Save | refused, naming `'.foo'` |
| A real save | `.foo` written with a null profile; `Database` and the server identity untouched |
| `Database` lookup cache set to 99999 | refused with "Lookup cache must be between 0 and 4096 MB per connection" - no property name |
| *Details read* = not yet | the two unreadable videos |
| *Details read* = yes | the photo, which SkiaSharp could read without ffmpeg |
| Preview page status rows | "no - 3 attempts failed" |
| Console | no errors, no warnings |

## 6g. Backlog raised 2026-09-06, closed 2026-09-08 (fourth batch)

All five items are implemented, deployed and verified on the NAS. A sixth piece of work - open-ended
file metadata - was asked for during the same session and is described below with them.

### 1. A file over the cache limit was recorded as permanently uncacheable

`IsExcludedFromCache` is written to the database and cleared only when the file's content changes, so a
film that did not fit under `FileCache.MaxFileSizeInMegabytes` stayed on the platter for good - even
after the operator raised the limit. An earlier fix had narrowed the flag to *only* the size case, on the
reasoning that size was "the one permanent reason". That is exactly backwards: the limit is the one
reason an operator can change.

Size now records nothing. `MediaContentResolver` compares `SizeInBytes` against the **live** limit, so
raising it is enough on the very next request, with no rescan and nothing to clear. The flag keeps the
meaning its name implies - set only when reading the file genuinely fails, proven by opening it.

### 2. Moving a file created a second row

Identity is `FullPath` and nothing else, so a move was an insert plus a delete in the same pass, `~ms`
apart, with nothing correlating them. Lost with the old row: the `PublicId` - and therefore the DLNA
ObjectID, the media URL and any renderer bookmark - the extracted metadata, the suppression flags, and
`CreatedUtc`, so a moved file also jumped to the top of *Recently added*. The old preview was orphaned on
disc forever.

Reconciliation now pairs a vanished row against a path **this pass inserted**, matching on `ContentStamp`,
and carries the original row across. The insert-set test is what makes it safe: a stamp is size and mtime,
not a hash, so two byte-identical copies share one - and without it, deleting one copy would "move" the
deleted row onto the survivor and destroy an untouched file's identity. A test pins exactly that.

Named after the watcher's own vocabulary, as the maintainer asked: `MoveOrRenameAsync`, and the log says *moved*
when the folder changed and *renamed* when only the name did.

### 3. Audio cover art was never extracted

Embedded artwork is carried as a single-frame video stream with the `attached_pic` disposition, so an MP3
with a cover reports one video stream and one without reports none - which is what tells them apart, and
why no tag reader is needed. Behind `Thumbnails.GenerateForAudio` (on by default). A file with no cover is
marked not-applicable rather than counted as a failure, decided from the stored metadata so it costs no
probe.

**This shipped inert and had to be fixed twice.** Every audio file already carried
`ThumbnailStamp == ContentStamp`, written by the old "nothing to make" branch, and the new code is gated
on that stamp differing - so not one existing file was ever revisited. The
`RequeueAudioThumbnails` data migration clears it once. Proven on the NAS afterwards: a real 360x360 JPEG
cover, and 354 thumbnails created in one pass.

### 4 and 5. Admin tiles - fallback icon and media-kind badge

`MediaTile` falls back to the existing `Resources/images/icons/file{Audio,Movie,Image}.jpg` - the same
images `DidlMapper` already hands televisions - instead of printing the bare word "audio". Those icons
404ed on the admin port because `AdminOrMediaPortEndpointFilter` keys on the `/admin` prefix, so `GetIcon`
gained a second `/admin/icon/{fileName}` route, exactly as the favicon already does. Each tile also
carries a 20px circular badge with an inline SVG glyph, driven by `Mime.ToMedia()` and never by whether a
preview exists, so a track showing its cover art still reads as music.

### 6. All metadata a file carries about itself

A `MediaFileTags` table (`StreamIndex` / `Name` / `Value`), shown in a collapsed **All metadata** panel on
the file page, gated by `Library.ReadContainerTags`, with purge/rebuild on Maintenance and per-file in
`ProcessingActions`.

- **Audio and video** go through ffprobe (`-show_entries format_tags:stream=index:stream_tags -of json`),
  which the Xabe object model cannot replace - it surfaces only the handful of tags it has properties for.
- **Pictures go through MetadataExtractor**, a new package. ffprobe reports a JPEG as an `image2`
  container holding an `mjpeg` stream and walks straight past the EXIF block, and SkiaSharp exposes only
  dimensions, colour type and EXIF *orientation*. Verified on the NAS: a Nikon photograph returns 107
  tags including make, model, software, date taken and exposure time. Reading a picture spawns no process
  at all.
- **The table is purely additive** - the relationship is declared from the tag entity's side with
  `HasOne<MediaFileEntity>().WithMany()`, so no existing entity or configuration was touched and the
  migration contains only `CreateTable`/`CreateIndex`.
- `RequeueImageMetadata` re-queues pictures that hold no tags, for the same reason as
  `RequeueAudioThumbnails`.

**The lesson worth carrying:** this is three instances in one session of the same trap - *a feature gated
on a stamp or flag that existing rows already carry does nothing until something clears it.* Ship the
backfill migration in the same change; do not rely on the operator knowing which button to press.

---

## 6h. The last of the review documents, closed 2026-09-08

Everything section 7b listed as open, except the one item that needs a database. **Deployed the same
day**, at 12:34 local, and verified live rather than assumed: `/manage/thumbnail` answers with the
`nextAfter` and `mediaFileFullPath` that only the B8 fix produces. The B10 fallback is therefore live on
the real library - see standing decision 16 for what that widens, and note that a scan after the
redeploy had **not** changed the file count (25,595) as of 11:24 UTC.

**Two rules that existed twice each now exist once.** `StoredPath.IsSeparator` is the single definition of
"this character separates two path segments", and the indexer's own copy - built from
`Path.DirectorySeparatorChar`, so on Linux it did not recognise `\` at all - is gone (**B3**).
`FileSystemDate.Resolve` is the single definition of the birth-time plausibility rule, moved out of
`LibraryScanner` into `DlnaServer.Core.Files` because the folder half of the first-fill rule lives in the
host and could not reach a private method in another project.

**Folders are dated like their files now (B4), and nothing else had ever found this.**
`MediaDirectoryCreateDto` carried no date at all, so every row took the one `nowUtc` that
`StampTimestamps` computes per `SaveChanges` - a 500-row batch shared a single timestamp while its files
correctly took filesystem dates, and Browse's date sort over containers therefore degenerated into
insertion order after any bulk import. It now mirrors the file mechanism exactly: an optional
`IndexedUtc` on the DTO, `CreatedUtc = IndexedUtc ?? default` in the repository so the conditional stamp
still fills an ordinary arrival, and `firstFillRoots` threaded into `IndexDirectoriesAsync` - the value
already existed one line above the call and was simply not passed. One `DirectoryInfo` stat per **new**
folder, only on a first fill; `EnumerateDirectories` still yields bare strings and touches no disc.
**Verified by mutation:** forcing `IndexedUtc = null` fails two of the new tests, so they are not passing
vacuously.

**A television is no longer told two different things about the same file (B6).** The `DLNA.ORG_FLAGS`
switch and the `transferMode.dlna.org` switch were keyed off different sets of `DlnaMedia` members and
disagreed for `Subtitle` and `Unknown`: the flags claimed the streaming bit, the header answered
`Interactive`, and a renderer that finds those contradicting refuses to play with no diagnostic. The
fix went the **opposite way from what the review implied** - see standing decision 15 - because
`ContentFeaturesFor` matches the reference byte for byte and the header is the net-new half. Both now
read `DlnaProtocolInfo.IsStreamed`.

**The MIME catalog is a real fallback (B10), which three doc comments had been describing for weeks.**
`LibraryScanner.TryResolveMime` consulted only configuration, so `.webm`, `.flac` and `.ts` were not media
unless an operator had typed them into `config.json`, and nothing said so. Configuration still wins
outright; the fallback admits video, audio and images only, and `ConnectionManager`'s `Source` list
widened to match so the server cannot serve a MIME it never claimed. **This is the one change here with a
large blast radius - 137 extensions become media** - and standing decision 16 carries the count, the
reasoning, the deliberate subtitle exclusion and the short list of extensions that are not media in any
useful sense.

**The B9 reorder had a second half, and only the simplification pass found it.** Moving `File.Exists`
after the cache in `GetFile` left `HeadFile` still checking it first, so a deleted-but-still-cached film
answered **404 to a HEAD probe and 200 to the GET behind it** - and most renderers HEAD before they GET,
so the fix would have stopped playback through the other verb. `HeadFile` now falls back to the cache
too, and returns the payload rather than `PhysicalFile` in that case, because `PhysicalFile` takes its
`Content-Length` from a directory entry the file no longer has. It still never touches the backlog, which
is the cost that action exists to avoid. **The lesson: reordering one verb's guard is not done until the
sibling verb is read.**

**Four smaller ones.** `/manage/thumbnail` echoes a `NextAfter` and is pageable at last (**B8**) - the
blocker was that `ThumbnailDto` carried only the `.@__thumb` image path while the read orders and seeks
on the media path, so it gained a `MediaFileFullPath` and the endpoint pages on that; its keyset predicate
is also spelled the way its two siblings are. `FileServerController` checks `File.Exists` **after** the
cache now (**B9**), matching `/admin/media` and `IServedFileCache.Evict`'s promise that deletion is not a
reason to evict - checking first turned a still-cached, still-streamable film into a 404 on the one port a
television uses. The Settings page validates through `ISettingsPreflight` (**B7**), which applies the
startup defaults to a *copy* and then validates, so the page no longer refuses configurations the server
boots from happily - the copy is what keeps the seeded exclusions out of the file it is about to write.
And both entities' `FullPath` docs said "compared case-insensitively by database collation" where the
configuration says `// Case-SENSITIVE` in as many words (**B12**); the docs were wrong, not the code.

**`GetWithDetailsAsync` splits its query.** Found in the live log rather than by a test - EF warns at
runtime, and the warning was sitting in the NAS log at 10:00:19. Two projected collections mean a file
with 5 audio tracks and 4 subtitles built 20 rows instead of 9, each repeating a `FullPath` declared at
4096 chars. `SaveMetadataAsync` had already split for the same reason.

**The M2 unreadable-folder guard is covered, and this file was wrong to call it untestable.** It said the
test needs a folder that exists and refuses to enumerate, which is not portable between Windows and the
NAS. It does not: `FindUnusableSourceFolders` asks the injected `ISourceFolderChecker`, so a checker
reporting the folder unusable exercises the guard anywhere. The test deletes both indexed files and
asserts neither row goes.

**W6, W10 and the `Ordinal` finding were closed as decisions**, recorded as standing decisions 17, 18 and
19 so they are not raised again. W10's evidence is worth keeping: a full day of live logs over 25,532
files holds **zero** "Slow database command" entries against a registered 50 ms threshold.

**A new finding, from reading the artifact rather than the source.** This file claimed twice that
`System.GC.HeapHardLimitPercent` was set permanently in `runtimeconfig.template.json`. It never was -
see the correction in section 3b. The absence is correct; the document was not.

**Test count 586 → 664**, 0 warnings throughout.

**The preview page says why a file will not play, instead of showing a dead player.** Reported as "why
will this not play" against an AVI, and the answer was that nothing was wrong - the browser has no AVI
demuxer, the server was serving it correctly, and the page had no way to say so (standing decision 20).
`BrowserPlayback` (`DlnaServer.Admin/Playback`) now decides from the **container**, never the MIME,
because Chrome ignores `Content-Type` for media and sniffs the bytes - proved by this server's own
`.mp3 → audio/mp4` workaround playing perfectly. Video and audio use a **deny**-list so an unmeasured
container still gets a player: wrongly hiding one breaks a file that worked, wrongly showing one is only
the behaviour that already shipped. Images use an **allow**-list, the opposite way round, because the web
image formats are a short closed set while the ones a browser refuses are not - and the B10 fallback
above makes TIFF, PICT, PCX, CMU raster and AutoCAD drawings indexable without anyone asking, each of
which would have rendered as a broken image.

Measured in Chrome 152 against the live server, which is what the lists are built from rather than
opinion: `.mp4`, `.mkv`, `.mov`, `.m4v`, `.3gp` and `.flac` play; `.avi`, `.wmv`, `.flv` and `.mpg` fail
with `DEMUXER_ERROR_COULD_NOT_OPEN`. **`.3gp` playing is why the count is 679 and not 688** - an earlier
census had assumed those 9 files were unplayable. So: 666 `.avi`, 9 `.wmv`, 2 `.flv`, 2 `.mpg`.

**Verified by running it, not by reading the markup** (recurring check 2, and check 7 - a rendered
element is not a working one). A local host on spare ports over a folder of three placeholder files:
`.avi` and `.wmv` render the message with the kind icon and no `<video>` element, `.mp4` still renders a
player with no message. Screenshot taken, server stopped, scratch folder and database removed.

**Two duplications left standing, both in committed code, both needing a decision rather than a patch.**
`MediaFileRepository.GetPlayableNeighboursAsync` hand-rolls
`s.Mime.ToMedia() is DlnaMedia.Video or DlnaMedia.Audio or DlnaMedia.Image`, which is character-for-character
the body of the new `DlnaMimeCatalog.IsPresentableMedia` - so a rule introduced as "one definition" already
has a third copy. It is in-memory LINQ after `ToListAsync`, so the extension translates fine there.
And `DlnaOptionsDefaults.Canonicalise` is a second definition of `PathExclusion.Canonicalise`, whose own
remarks argue that both halves now derive from one place; they are equivalent today (trim-then-fold and
fold-then-trim agree on every input either can see), so it is a drift risk rather than a defect - but
`SettingsPreflight` now makes that rule reachable from the Settings page as well as from startup.

## 6i. Third `/review-all --full` pass and its fix pass, 2026-09-08

A whole-tree audit - 355 `.cs` files, 14 agents and 3 skills - then five staged batches of fixes, each
gated on `dotnet build` at 0 warnings and the full suite. **664 tests → 697**, all green, clean rebuild.

**Read this before reading the findings below: the tree did not compile when the pass started, and the
audit never noticed.** `ServedFileCache.cs` used `GCSettings` with no `using System.Runtime;`, added at
09:34 that morning with the per-eviction `GC.Collect` block. Every reviewer had been told not to build,
so twenty of them read the file and none of them found it. The claim of "0 warnings" in this document was
false for most of that day. **A review that never compiles the code cannot report that it compiles**, and
an incremental build hid a second warning later in the same pass - only `Remove-Item obj\Debug` then a
rebuild surfaced it. That is the same class as the `runtimeconfig.json` trap in section 7.

### The two Blockers, and why one mechanism could not fix both

Both live in the configuration-reload path, and between them they could destroy the index.

**A failed or missing re-read blanked every setting.** `FileConfigurationProvider.Load(reload: true)`
replaces `Data` with an empty dictionary **before** it consults `OnLoadException`, and raises its change
token afterwards regardless - so `context.Ignore = true` suppressed the rethrow and nothing else. This
was proved by experiment during the audit, not inferred: rewriting the file as `{ this is not json` made
`configuration["Dlna:Server:Port"]` return null, and deleting the file returned null **with no callback at
all**. With the section blank, `DlnaOptionsDefaults` falls `SourceFolders` back to the application folder,
**validation passes**, the watcher moves onto the publish folder, `logs/` churns, a scan runs, and
`ReconcileDirectoriesAsync` cascade-deletes every indexed directory - `PublicId`s and `CreatedUtc`
included. The log line said the server was "still running on the values it started with", which was false.

Fixed by `LastGoodJsonConfigurationSource`/`LastGoodJsonConfigurationProvider`, which capture `Data`
before `base.Load()` (every framework path assigns a *new* dictionary, so the reference stays valid),
restore it when the read was lost, and raise the token a second time so consumers rebind onto the
restored values rather than the defaults they were just given. Three cases, deliberately distinguished:
unparseable → restore; **missing → restore** (the silent variant, which an ordinary `vim` save reaches
because `backupcopy=auto` renames a temporary over the target); `{}` → **do not** restore, because an
operator emptying the file is asking for defaults. All three are tested.

**An invalid-but-parseable value poisoned every reader for the life of the process.**
`OptionsCache.GetOrAdd` stores a `Lazy<TOptions>` in `ExecutionAndPublication`, which caches the
exception and rethrows the same instance forever. One `"Port": 0` therefore made every `CurrentValue`
read throw - every database connection, every listing, every Browse, every media response, SSDP
announcements - **including the Settings page that would have repaired it**, leaving SSH as the only way
back. `LastGoodDlnaOptionsMonitor` serves the last options that validated instead. Startup is
deliberately unprotected: there is no last good value yet, so `ValidateOnStart` still refuses the boot.

**An options-level decorator alone could not have fixed the first one**, which is worth stating because
it looks like it should: blanked options are *valid* options, so nothing at that layer can tell them from
a deliberate minimal configuration. The provider-level fix is not redundancy.

### Everything else that changed

**Configuration.** `ConfigurationFileGuard` and `SettingsWriter` were **stricter than the parser they
protect** - default `JsonDocumentOptions` reject comments and trailing commas that
`JsonConfigurationFileParser` accepts, so a hand-edited file was condemned, moved to `backup/` and
replaced with defaults. Both now share `DlnaConfigurationJson`, which also ends the two `_writeOptions`
disagreeing about enum encoding (the guard wrote names, the admin UI wrote numbers, into one file).
`Path.GetFullPath` no longer throws out of validation; the minimum-length rule no longer blames the
operator for the `SubFolderName` entry `DlnaOptionsDefaults` injects; the last `Directory.Exists` inside
validation is gone, which `ValidateSourceFolders`' own remark had argued against for the rest of the
class. `SettingsWriter` gained a lock around its read-mutate-write and its first tests.

**The watcher, where three defects made it permanently deaf.** `WatchTargets` recorded the folders that
were *asked for*, not the ones that *attached*, and `Start` skipped a missing folder silently - so the
comparison was request-against-previous-request, always matched, and a source folder absent at startup
stayed unwatched for the life of the process. With `UsePeriodicRescan` shipping off, nothing would ever
have noticed. `Start` now returns what it attached to and the loop retries on an interval. A throwing
`Restart` no longer loops: `watched` is assigned *before* the rebuild. A rename **into** an excluded
folder now publishes the departure - that is what a QNAP delete looks like, and dropping it left the row
listed and unplayable. A successful pass consumes a pending resync instead of paying for a second full
walk. And `ReadWatchTargets` normalises through `LibraryIndexer.NormaliseSourceFolders`, so the watch and
the index finally agree on what a source folder is.

**The settle and coalesce windows moved to a monotonic clock.** They were wall-clock, so an NTP step - which
a NAS without a battery-backed clock takes on every boot - either stalled indexing for the length of the
step or marked everything settled at once and **indexed files mid-copy**, the one thing
`FileSettleSeconds` exists to prevent. This touched three files: the watcher, `FileChangeEvent`, and
`MutableTimeProvider`, which overrode only `GetUtcNow` and would otherwise have stopped being able to
drive the window at all.

`FileWatcherHostedService` had **no test file**. It has one now, driven through `CollectSettled` and
`ScanIfDueAsync` directly - the loop is paced by `Task.Delay` and `MutableTimeProvider` does not override
`CreateTimer`, so it cannot be fast-forwarded. Making those two `internal` follows the precedent
`ResolveIdleDelay` and `FileSystemChangeWatcher.Publish` already set, and avoided adding a package.

**Delivery and persistence.** `ResolveMaximumFileSizeInBytes` is clamped to `int.MaxValue`, which makes
the `(int)info.Length` cast provably safe and `ResolveBucketSize`'s comment true - and an existing test's
own expected value proved the overflow was reachable, resolving to **8,507,281,408 bytes** on a
development machine. `ObjectDisposedException` joined the read's catch filter, closing the shutdown race
the `_isDisposed` remark already claimed was caught, and `TryGet` honours `_isDisposed` as that remark
also claimed. A `DatabaseReadyMiddleware` answers 503 + `Retry-After` while migrations run, instead of
Kestrel serving bare 500s. `ResolveScopeAsync` uses the half-open range this same file already uses in
`AnyUnderPathAsync`. `FileCache.razor` inherits `AdminPageBase` and gates its `DbContext` work, so it
stops being the one admin page that can tear the Blazor circuit.

**UPnP.** The video-thumbnail ffprobe runs under the ffmpeg timeout - `FromSnippet.Snapshot` takes no
token and probed internally anyway, so building the conversion by hand costs the same one probe and makes
it cancellable. The audio path's leftover-`.frame.png` pre-delete now applies to video too. A PNG is no
longer advertised as `JPEG_TN`. DIDL serialization happens when the document is assigned rather than in a
property getter during the body write. `SecurityElement.Escape` strips illegal XML control characters, and
`description.xml` ends in a fixed `"\n"` rather than `Environment.NewLine`. `ObjectID` echoes the parsed
GUID, because `Guid.TryParse` trims `\v` and `\f` - whitespace to it, illegal in XML - and the raw string
went into `parentID`.

**Security.** `RejectRemoteManagementEndpointFilter` refuses `/manage` from a non-private remote address.
See standing decision 22 for why this was reachable at all.

**Tests added beyond the two Majors:** a reflection test that every `DlnaOptions` section is actually
validated (`Database` and `Compatibility` were both missing once, which took the library offline for
every renderer), one pinning `AdminSurfaceMiddleware`'s media-only list against the `ControlPath`
constants it now uses instead of re-typed literals, three layering rules `CLAUDE.md` states that
nothing enforced, and `ContractImmutabilityTest` extended to arrays, dictionaries and sets.

### Migrations squashed to one

`Migrations/` was replaced with a single `20260908162737_InitialSchema`, on an explicit decision that this
is pre-customer. It made the unused-index work free: `EntityBaseConfiguration`'s `CreatedUtc` index is
opt-in now, so **`IX_Files_CreatedUtc` is the only one that exists** and the other eight are never
created rather than created and dropped. The two `Requeue*` data migrations went with it - there is
nothing to re-queue in a database built from scratch. The covering indexes survive because they were
always declared in the entity configurations; **W9's two `Language` indexes survive too**, because W9 is
still open and nothing is dropped on speculation.

**The cost, and it is not optional: the live NAS database will be discarded and the library rescanned.**
Its `__EFMigrationsHistory` does not contain the new migration, so `MigrateAsync` fails against existing
tables and the documented corruption path moves the file aside and rebuilds it empty. Every `PublicId` is
regenerated, so a renderer holding a cached listing gets 404s until it re-browses. Thumbnails are adopted
rather than rebuilt, so that part is cheap; ffprobe metadata and tags are not.

### Deployed and verified live, 2026-09-08 ~17:43 UTC

The maintainer deployed 6i to the NAS (`192.168.1.100`, media 26852, admin 26853) and it came up. Checked over
HTTP while the metadata and thumbnail pass was still running, so every memory figure here is a **loaded**
reading and none of it closes the quiet-reading item.

**Verified working:**

- **The server starts.** Both Blockers touched startup; `/health` and `/admin/health` both answer 200.
- **`/manage/configuration` reads the real file** - `sourceFolders: ["/share/Media"]`, **not** the publish
  folder. That is the single most useful signal available: had the last-good provider misfired on a real
  start, the source-folder fallback is exactly what would have shown up here.
- `excludeFolders` is exactly `[".@__thumb", "@Recycle"]` - two entries, so the `ConfigurationBinder`
  list-doubling fix is still holding after a fresh first fill.
- **The port split survives the `AdminSurfaceMiddleware` change to `UpnpServices.ControlPath`.**
  `/manage/database` answers 200 on 26852 and 404 on 26853; `/admin` answers 200 on 26853 and 404 on
  26852.
- **`RejectRemoteManagementEndpointFilter` admits a private address** - the LAN check reached `/manage`
  normally. The refusal half is still unproven; it needs a request from a non-private source.
- **The migration squash worked.** The database rebuilt and re-indexed to **25,596 files / 1,069
  directories**, and thumbnails are being written with `hasStoredContent: true`.
- **The wire is intact after batch 4.** A live SOAP Browse returns 200 with the envelope's
  `encodingStyle` present, `parentID="0"` on root children, and `DLNA.ORG_OP=01` - which is the value
  standing decision 25 predicts and is the concrete proof that the rename that decision refuses would
  have been wrong.
- **An LG television lists and plays films, photos and music.** Photos matter most here: that is the arm
  where `ResolveThumbnail` now advertises the image's real MIME instead of asserting `JPEG_TN` over it.
- Byte cache live at the operator's 5120/512 (decisions 11 and 14), so the new `int.MaxValue` clamp is
  not binding on this deployment and changes nothing here.

**Loaded memory readings**, for the trend only: 295.4 MB working set at 248 s uptime, 309.6 MB at 384 s;
managed heap 20.3 → 53.0 MB; gen2 23 → 29. Do not compare these to the 177.7 MB figure in section 3.

**`isServerGc: false` and `isConcurrentGc: false` are both correct and neither is a finding.** Workstation
GC is set deliberately in `DlnaServer.Host.csproj`, and `GCMemoryInfo.Concurrent` describes the *last
collection* rather than the configured setting - `ManageController` already documents that a 2026-09-03
review misread it. The generated artifact says `System.GC.Concurrent: true`, which is the value that
counts.

### Checked and NOT a defect: `FileCreatedUtc` holding the scan instant is what `UseFileCreationDateTime: false` means

Raised during the live check on 2026-09-08 and resolved the same session. **Recorded because the
evidence looks alarming and someone will find it again.**

What was seen, from `/manage/file/...` with the process started at 17:43:19 - and two files in one
Browse differing only in the seventh decimal, which is `DateTime.UtcNow`, not a filesystem value:

```
fileCreatedUtc    2026-09-08T17:43:30.9102193Z   <- the scan instant
fileModifiedUtc   2026-09-08T16:13:23.4108896Z
createdUtc        2026-09-08T16:13:23.4108896Z   <- the filesystem date, via first-fill dating
```

**That is the configured behaviour.** `LibraryScanner.TryDescribe` sets
`CreatedUtc = options.UseFileCreationDateTime ? fileSystemCreatedUtc : DateTime.UtcNow`, and
`LibraryIndexer` maps `ScannedFile.CreatedUtc` onto `MediaFileCreateDto.FileCreatedUtc`. The live
`config.json` has `useFileCreationDateTime: false`, so the scan instant is the correct value.
`LibraryOptions` says as much where the setting is declared.

The reason the default is off is in `TryDescribe`'s own comment: on a filesystem that records no birth
time - common on Linux, and true of this NAS - turning it **on** pins every file to 1970 in the one
field Browse's date sort uses. So the scan instant is the deliberate lesser evil, and a `dc:date` of
"when the library was indexed" is a consequence of that choice rather than a bug.

**Both fields were behaving correctly**, which is the part worth keeping: `createdUtc` took the
filesystem date through the first-fill rule (so *Recently added* survived the rebuild, which is exactly
what that rule exists for), and `fileModifiedUtc` matched `contentStamp`'s ticks.

If a renderer's date column is ever the complaint, the lever is `Library.UseFileCreationDateTime` - and
the 1970 trap above is what to expect on this hardware. **It is not a code defect and there is nothing
to fix.**

### Not applied, and why - reporting one of these as an oversight is wrong

- **UPnP error 701 for an unknown `ObjectID`.** There is no SOAP-fault mechanism in this codebase, so
  adding one is new wire behaviour on the hottest path, against renderers that were validated against the
  reference's behaviour. Not shippable without a television to test it.
- **The SSDP listener resolving identity from the local address.** `UdpReceiveResult` carries no local
  address and `UpnpDeviceIdentity` carries no subnet mask, so a real fix needs
  `SocketOptionName.PacketInformation` or one listener per interface - it reworks discovery. Inert on a
  single-NIC box. The limitation is now commented at the call site so it is not misread as working.
- **The `LibraryScanner` exclusion-before-recurse rewrite.** `EnumerateFiles` with recursion cannot skip a
  subtree, so the fix is a hand-rolled walk of the most performance-sensitive enumeration here - on a
  claim ("roughly double the filesystem calls") that is arithmetic rather than measured. Standing
  decision 19's own standard applies.
- **Making `GenerateFor*` re-enable retroactive.** Not stamping the disabled kinds would leave them
  permanently pending, so the queue never drains and `SettleAsync` never fires - worse than the bug. The
  remedy already exists as *Recreate thumbnails* on Maintenance, and the Settings page now says so.

## 6j. Browse warmed previews from the wrong place, 2026-09-09

Browse queues a folder's previews into the byte cache as it replies (`ContentDirectoryService.WarmPreviews`
-> `MediaCacheBacklog` -> `MediaCacheFillHostedService`). The drain reached the cache only through
`IServedFileCache.LoadAsync`, **which reads the filesystem and nothing else** - while the serve path,
`FileServerController.GetThumbnail`, prefers the copy the database holds and only falls back to the image
beside the media. With `Thumbnails.StoreInDatabase` defaulting to **true**, the database branch is the
normal one, so the prefetch was waking the platter for bytes already in SQLite. That is the exact opposite
of the acoustic goal the prefetch exists for, and it was invisible: both paths fill the same cache entry
under the same key, so the only symptom was disc activity nobody was measuring.

Not a false positive against section 7b - warming from disc only was never a standing decision, and the
`WarmPreviews` remark that argues for a *derived* path is about keeping Browse free of queries, which it
still is.

**The fix, in three parts.**

- `MediaCacheFillHostedService.LoadAsync` is new and owns the source decision: anything that is not a
  preview goes straight to the filesystem as before, and a preview goes cache -> database -> disc, which is
  `GetThumbnail`'s order. The lookup is `IMediaFileRepository.GetThumbnailContentAsync`, already existing,
  and `PublicId` carries a unique index on every entity (`EntityBaseConfiguration`), so it is a seek.
- `MediaCacheRequest.PublicId` now means the **thumbnail** row for a `Thumbnail` request and the media file
  for a `Media` one, and `WarmPreviews` passes `ThumbnailPublicId` accordingly. This is the load-bearing
  half and it fails **silently** when wrong - the media file's identifier matches no thumbnail row, the
  lookup returns nothing, and the warm quietly falls back to disc with no error anywhere. Covered by
  `ContentDirectoryPreviewWarmingTest`.
- `LogFilled` gained a `{Source}` field carrying `cache` / `database` / `disc`, mirroring
  `FileServerController`'s own constants, because otherwise there is no way to see from the outside which
  branch ran.

`WarmPreviews` became `internal static` taking `(files, options, backlog)` so it can be tested directly -
the shape `ContentDirectoryService.Paginate` already uses in the same class, for the same reason.

**Verified**: 702 tests at 0 warnings, and the new coverage was mutation-checked - disabling the database
branch fails exactly the one test that asserts it, while the disc-fallback and media-file tests stay green.

**Still duplicated, deliberately not collapsed.** `GetThumbnail` and this drain now both encode
"cache -> database -> disc". The shared piece is only the ordering; the controller must additionally build
an `IActionResult`, log a source, and hand a missing file to `PhysicalFile`. `IMediaContentResolver` is the
precedent for extracting it - it exists precisely so `/fileserver` and `/admin/media` cannot drift - and a
`ThumbnailContentResolver` shaped the same way would be the honest end state. Left as a suggestion rather
than applied, because it reshapes a committed controller on the hot path for a three-line ordering rule.

### The Recently-served page said "from disc" for reads that came from the database

The same day, and the reason the fix above was hard to see from the outside. `/admin/cache` showed a tile
labelled *From memory / from disc* carrying `Hits / Misses` straight off `MemoryCache.GetCurrentStatistics()`
- and a preview taken from SQLite still **misses** the memory cache on its way past, so it was counted as a
disc read. Every one of the three paths that prefer the database copy (`FileServerController`,
`AdminMediaController`, and now the Browse prefetch) was reported as touching the platter.

`ServedFileCacheReport.DatabaseHits` is the new counter, and the tile is now
*Memory / disc / database* = `Hits / (Misses - DatabaseHits) / DatabaseHits`. `/manage/filecache` carries
the raw field alongside `Misses` rather than the subtraction, because that endpoint reports what was
counted.

**Counted inside `IServedFileCache.Store`, not at the call sites.** Every production caller of the public
method is handing over bytes it read out of the database; the cache's own disc reads go to a new private
`StoreCore` and are not counted. So a fourth such site is counted without having to remember to, which
matters given `AdminMediaController`'s own comment already records that its ladder and
`FileServerController`'s had drifted once.

**The disc figure is an upper bound, deliberately.** A serve that finds neither memory nor the database
probes the cache twice on its way to disc - once directly, once inside `LoadAsync` - so one such read
registers two misses. Making it exact needs a cache-probe-skipping overload on a committed public
interface; the caveat is documented on the property instead.

**Verified by running it, and the browser caught what the suite could not.** Three generated PNGs, a real
SOAP Browse, then `/admin/cache`: `0 / 0 / 3` - all three previews warmed from the database, no disc reads
- and a following thumbnail request over the media port moved memory to 1 while disc and database stayed
put, which is the whole point of the prefetch. The first attempt labelled the tile
*Served from memory / disc / database*, which wrapped onto two lines and dropped *Reading now* onto a row
of its own; the label is short for that reason and there is a comment saying so.

### The dashboard rate counted a database read against itself

`Dashboard.razor`'s *Served from memory* tile was `Hits / (Hits + Misses)`, and a preview taken from SQLite
lands in `Misses` - so the one figure on the dashboard that exists for an **acoustic** reason was counting
a read that never touched a drive as though it had. The tile is now **Served without the disc** =
`(Hits + DatabaseHits) / (Hits + Misses)`. The denominator is unchanged on purpose: the database reads are
already inside `Misses`, so adding them there too would count each one twice.

Measured live on the three-file library: after one Browse the rate reads **50%** where the old formula said
0%, and after serving a warmed preview **70%** against the old 40%.

### Audit - nothing reads a preview off the disc before the database

Asked for after the fix above, and the answer is clean. Every path that turns a thumbnail into bytes goes
cache -> database -> disc:

| Path | Order | Note |
| --- | --- | --- |
| `FileServerController.GetThumbnail` | cache -> database -> disc | the renderer's path |
| `AdminMediaController` thumbnail | cache -> database -> disc | its own comment already records that this ladder and the one above had drifted once |
| `MediaCacheFillHostedService` | cache -> database -> disc | fixed above |
| `ManageController.GetThumbnailDataAsync` | database only | deliberate - it exists to inspect the **stored** blob, and 404s when there is none |
| `ImageThumbnailGenerator.Describe` / `Generate` | disc only | generation and adoption, where the disc is the only source there is |

The remaining `PhysicalFile(thumbnail.FilePath, ...)` calls are the last-resort fallbacks, correctly
positioned after both. `ImageThumbnailGenerator` already materialises the encoded bytes once and uses them
for both destinations, so the write-then-read-back is gone too.

One asymmetry is real but harmless and left alone: the two controllers test `HasStoredContent` before
querying, the drain queries unconditionally. That is the `StoreInDatabase`-off cost noted below.

**Not addressed**: with `StoreInDatabase` off, every warm now costs one query that returns null before
falling back to disc. Gating on the option was rejected - rows written while it was on would then never be
found - and the service does not inject `IOptionsMonitor<DlnaOptions>` today. A unique-index seek per
preview on a background thread is not worth the guard.

## 6k. Two operator asks, 2026-09-10

**`FileCache.WarmPreviewsOnBrowse` turns the prefetch of section 6j off.** On by default, so nothing
changes for a deployment that does not set it. Deliberately separate from `FileCache.Enabled`: that one
decides whether anything is held in memory at all, while this one decides only whether the cache is filled
*ahead* of the request - which is the half that spends disc reads on previews a renderer may never draw,
since a television paging straight through a folder pays for every image in it. The guard sits at the top
of `WarmPreviews` rather than at the call site, so the switch is covered by the same direct test the rest
of that method is. `MediaCacheFillHostedService` still checks `IServedFileCache.IsEnabled` itself, so the
cache-off case was already handled and is untouched.

**Both search pages shut their filter panel when a search runs.** The panel is tall - the file search
carries fourteen controls - and it was costing the results below it that height on every search.
`CollapsiblePanel.Collapse()` is new and renders itself, because the caller is a parent event handler and
a parent re-render does not reach a child whose parameters have not changed; the fold is internal state,
by that component's own design. `SearchPanel` forwards it, and each page calls it from `RunAsync` rather
than from the Search button, because Enter in a filter box runs the same search without going through the
button - `RunAsync` is the one entry point both paths share, which is what its existing re-entrancy
comment already says. Placed after the `try`/`finally` so the re-entrancy guard turning a call away leaves
the filters where the operator had them. Reset deliberately does **not** reopen the panel; the Footer keeps
Search and Reset reachable while it is shut, and the heading carries the filter summary.
**Reversed 2026-09-12 on the operator's ask - see section 6m.** Reset now reopens it.

Verified by running the server, not only by the tests: the button and the Enter key both collapse on both
pages, the summary reads back the live filter, the new checkbox renders under *Recently served files*,
and saving it wrote `"WarmPreviewsOnBrowse": false` into `config.json` and read back off through
`IOptionsMonitor` with no restart and no validation fallback.

**The admin port's root redirects to `/admin`.** It answered **404**, because confinement keys on the path
as well as the port: `AdminOrMediaPortEndpointFilter` sends anything not under `/admin` to the media port,
and `/` is mapped there to `description.xml` - some renderers fetch the root instead of the LOCATION that
SSDP advertises. So the one URL an operator actually types was refused on the one port whose only purpose
is the admin UI, and behind Caddy's password prompt that reads as a broken deployment rather than a wrong
URL. `AdminSurfaceMiddleware` does it, because it already knows the admin port and runs *before* routing -
a `MapGet("/")` would have collided with `MediaController`'s existing `[HttpGet("/")]` at request time.
302 rather than 301: a browser keeps a permanent redirect until its cache is cleared, so it could not be
taken back. The media port's root is untouched.

Verified against the running server rather than inferred, since the last two guards to ship here compiled
cleanly and did nothing: `26853/` answers `302 Location: /admin` and follows to the Dashboard in a real
browser; `26852/` still answers 200 `text/xml` with the description; `/admin` and `/_blazor/negotiate`
still 404 on the media port; `/manage/database` still 200 on the media port and 404 on the admin port.

## 6l. Temporarily hidden folders, and a runtime-only settings group, 2026-09-10

`Library.ExcludeFolders` does two jobs at once - it stops the scanner descending into a folder **and**
hides what is already indexed under it. That is right for a recycle bin, and wrong for content an operator
wants kept current but normally out of sight: an excluded folder is never enumerated, so its files get no
metadata, no thumbnails, and no notice when they change.

**`Library.TemporarilyHiddenFolders`** is the other half. Same entry form, same segment-boundary rule, same
`PathExclusion` / `HiddenPathQuery` implementation - but **indexed normally**. It is absent from every
listing, and refused by delivery; the scanner, the watcher and the processing queue never see it.

**Three sets, and the table is the design.** `ITemporaryFolderVisibility` (`Core/Diagnostics`, implemented
in `Host/Diagnostics`, modelled on `IApiBlocker`) composes them once so the two lists cannot drift the way
the three hand-synced exclusion copies did before M1:

| Consumer | Set | Why |
| --- | --- | --- |
| The 8 file + 5 directory listing methods, and `ExcludeWithoutVisibleMedia` | `HiddenFromListings` = excluded + temporarily hidden | One library, whether a television or the admin UI is asking |
| `GetByPublicIdAsync` (files and directories), `GetWithDetailsAsync`, `GetThumbnailByPublicIdAsync` | `HiddenFromDelivery` = temporarily hidden only | **The deliberate departure.** Excluded folders stay exempt so a renderer mid-stream is not cut off; here the exemption would defeat the setting, since a television keeps the identifiers it saw in an earlier listing |
| `GetPendingProcessingAsync` | `ExcludeFolders` only | The one place they differ the *other* way - these files must still get metadata and thumbnails, or revealing shows a wall of blank tiles |
| `LibraryScanRequest`, `FileWatcherHostedService`, `PruneEmptyDirectoriesAsync` | `ExcludeFolders` only | The whole point: scanned and watched normally. Nothing changed here |
| `GetByPathAsync`, `GetExistingPathsAsync`, `GetIndexedPageAsync`, every write | nothing | Unchanged. `GetByPathAsync` is the indexer's, so it cannot join the delivery row above - hiding it re-inserts the file against the unique index |

`HiddenPathQuery` gained a third arm, `ExcludeHiddenThumbnails`, keyed off the media file's path rather than
the preview's own - the preview lives in a sub-folder that is itself excluded from scanning, so matching on
`ThumbnailEntity.FilePath` would test the wrong path against the wrong rule.

**The reveal window is not in `DlnaOptions`, and that is structural rather than tidiness.** `SettingsWriter`
serialises the whole options graph with `JsonIgnoreCondition.Never`, so any property reachable from
`DlnaOptions` is written to `config.json` on every save - there is no per-property opt-out. A setting that
must never be persisted therefore has to live outside the tree entirely, which is what `IApiBlocker`,
`IRestartSignal` and the other runtime signals already do. Absolute UTC expiry cleared lazily on read, no
timer, `TimeProvider` for the clock.

**It reads the options monitor on every access rather than caching off the change token.** The first
implementation cached and recomputed in `IOptionsMonitor.OnChange` - which is wrong here, and the existing
suite caught it. `LastGoodDlnaOptionsMonitor` serves the last options that *validated* while its `OnChange`
forwards whatever was last **bound**, so a cache built on the notification would hide by rules an invalid
`config.json` had introduced and that nothing else in the server agrees with - the same failure class as
the section 7 configuration-reload Blockers. Reading through costs nothing on an ordinary library: with no
temporarily hidden folders the answer is the `ExcludeFolders` instance itself, pinned by a `BeSameAs` test.

**A new page, `/admin/settings/temporary`**, holds the minutes field and *Show now* / *Hide now*. It is a
navigation **sub-item under Settings** - `<li class="sub">` in `NavMenu`, the same shape the two search
pages use under Library - rather than a panel on the Settings page, so that everything reachable from
Settings is written by its Save button and everything on this page is not. `Settings`' own `NavLink` gained
`Match="NavLinkMatch.All"` for the same reason Library's has it: without it the parent stays highlighted on
the child page. It is the home for anything similar later - note that Maintenance's "Television traffic"
block is the same class of setting and is the obvious candidate to move here.

**Verified against the running server**, on a three-file library with `Private` temporarily hidden:
`/manage/database` counted all 3 files including the hidden one; a SOAP `Browse` of the root returned
`dlna-media, open2, open1` and, after *Show now*, `dlna-media, secret1, open2, open1`; direct delivery
answered 404 on both `/FileServer/file/{id}` and `/admin/media/{id}` while hidden, 200 while shown, and 404
again once the window lapsed at the time the panel had displayed; and Save round-tripped
`"TemporarilyHiddenFolders": ["Private"]` into `config.json` with no trace of the window anywhere in the file.
`LibraryIndexerTest` covers the part that cannot be seen from a listing - a file added to a hidden folder is
indexed and one deleted from it is reconciled away, while the folder stays absent from every listing.

**Open, and stated rather than fixed:** `SystemUpdateID` and every container `UpdateID` are hardcoded to
`1`, so a television sitting on a folder listing is never told the library changed and will not notice a
reveal or an expiry until it browses again. And a stream in progress stops when the window lapses, which
follows directly from blocking delivery. Hiding remains a listing-and-delivery control, not an access
control: anything with filesystem access to the machine still sees the files.

## 6m. Five operator asks, 2026-09-12

All five are admin-UI only. Nothing on the renderer path changed - no DIDL-Lite, no SSDP, no delivery, no
schema - so this batch cannot regress a television.

### A photograph opens larger, and "larger" depends on the screen

**Desktop: an overlay.** Clicking the picture on the preview page opens a fixed, full-screen viewer with the
photograph **at the width of the screen**, a toolbar carrying zoom out / level / zoom in / *Fit to screen* /
*Close*, and the stage scrolling when the picture is larger than it. Escape closes.

**Phone: `/admin/photo/{fileId}`** (and `/admin/photo/{directoryId}/{fileId}`), a page holding the picture, a
*Back to the file* link and nothing else. A toolbar costs height a phone has none of, and the browser's own
pinch-zoom does the job better than anything that could be built here.

**Which of the two a click reaches is decided by CSS, not by the server, and that is the load-bearing part.**
The markup is one `<img>` inside an `<a href="/admin/photo/...">`, with a transparent `<button>` absolutely
positioned over it. On a desktop the button covers the picture and takes the click; at `max-width: 760px` the
button is `display: none` and the click lands on the link underneath. The alternative - rendering a link and
a button and hiding one - **fetches the photograph twice**, because a browser still loads an `<img>` inside a
`display: none` subtree. One element, one request, no user-agent sniffing, no JavaScript.

The wrapper is `width: fit-content; margin-inline: auto` so the button matches the *picture's* box rather
than the panel's - a landscape photograph in a tall panel would otherwise be clickable well outside itself.

**Fitted and zoomed are two mutually exclusive MODES, and the viewer opens fitted.** Fitted is `max-width`
alone: the picture at its own size, brought down only if it is wider than the stage. Any change of level
leaves that mode for `zoom` alone - a percentage of the file's own pixels with nothing capping the width, so
the stage scrolls and the panning is the browser's. *Fit to screen* is what returns, and levels are steps of
25 between 25 and 400.

**One custom property, and a class picks which of the two spends it:**

```css
.lightboxstage img { display: block; height: auto; margin-inline: auto; }
.lightboxstage img.fitted       { max-width: var(--zoom, 100%); }
.lightboxstage img:not(.fitted) { zoom: var(--zoom, 100%); }
```

The page passes `style="--zoom:N%"` plus the class and spends no arithmetic of its own. **This is what makes
the viewer possible with no JavaScript** (section 7, "no JS interop"): the fit is expressed in CSS rather
than measured, so the browser resolves the screen's width and the page never learns it. `zoom` and not
`transform: scale()`, which leaves the layout box behind and would cost the stage its scrollbars and the
arrow-key panning that comes with them.

**The fitted state is a MODE, not a level**, and the toolbar says so rather than showing a number. The level
readout reads **`Fitting`** while fitted and `N%` otherwise, and the `Fit to screen` button is **not rendered
at all** while fitted rather than rendered disabled - it is the one control with nothing to do in that state.
Both were asked for on 2026-09-14, and the readout carries `min-width: 7ch` so the zoom buttons do not
shuffle as the text swaps between the word and a level (measured: the `+` button sits at the same x in every
state).

A number would be a lie there, which is the point: levels are percentages of the file's own size, and fitted
is that size *capped to the stage*, so a picture wider than the screen is not at 100% of anything the operator
can see. **Fitted is decided by the mode, never by measuring** - the maintainer chose this explicitly over "the photo
currently fits inside the stage", which would have needed JavaScript to measure and would have flipped the
label back to `Fitting` on a big picture zoomed far enough out. So `25%` stays `25%` even when the result
happens to fit, and `Fit to screen` is gated on `_viewerFitted` and never on `_zoomPercent == 100`: zooming
back out to 100% is the file's own size uncapped, a different rendering from fitted whenever the picture is
wider than the stage, so the button must still be there.

**Setting both properties at once was a defect, corrected 2026-09-14.** The 2026-09-12 version was
`zoom: var(--zoom); max-width: var(--zoom);` on one element, on the reasoning that `max-width`'s percentage
resolves against the containing block *divided by* the zoom and so always works out to the stage's width -
making every level a percentage of `min(natural, stage)`. The arithmetic is real and the rendered *width* is
right at every level, which is exactly what the verification measured; what it never looked at was the
*shape*. The cap moves with the level, so a picture growing past the stage is squashed rather than panned.
The maintainer reported it as "incorrect showing a scale". The lesson is recorded in the personal log: measuring the
one number a hypothesis is about is not verifying the rendering.

Verified 2026-09-14 twice. First against the CSS alone on a 600-pixel stage with a 2,000x1,000 and a 320x160
picture: fitted renders 600 (capped, `zoom` unset) and 320 (true size, not blown up), neither scrolling; at
125% `max-width` is `none`, the big one renders 2,500 and the stage scrolls; zoomed back to 100% the big one
renders 2,000, still scrolling, which is the case that proves the button must follow the mode.

Then against a running instance on 27852/27853 over a scratch library of five generated photographs
(4000x3000, 2400x900, 900x2400, 320x240, 120x90), each carrying a drawn square grid so a distortion is
visible rather than inferred - **the check the 2026-09-12 pass skipped**. At 1440px the 4000x3000 opens
`Fitting` at 1,425 with square cells and no `Fit to screen` button; `+` gives `125%`, the button, 5,000
rendered pixels, both scrollbars and an aspect ratio of 1.3333 against the file's 1.3333; `-` walks 75% then
50%; `Fit to screen` returns to `Fitting` at 1,425. Fitted widths across the library: 1,425 / 1,425 / 900 /
320 / 120, so every picture narrower than the stage is at its true size and neither of the small ones is
enlarged.

**Three things this got wrong first, each found by running it rather than by the suite:**

- **The page behind scrolled.** The overlay is `position: fixed`, so a wheel over a picture that does not
  overflow scrolled the document underneath, and closing landed the operator somewhere they never chose.
  `html:has(.lightbox) { overflow: hidden; }` is the fix - `:has` is what reaches *upward* from the overlay
  to the root scroller, and the folder list already depends on it.
- **Escape stopped working after any toolbar click.** The handler was on the stage, which is what takes the
  focus when the overlay opens; clicking a toolbar button moved the focus out of that element. It is on the
  overlay root now, and `keydown` bubbles, so it catches the key whatever is focused inside.
- **Closing dumped the keyboard at the top of the page.** Focus now moves into the stage on open - which is
  also what makes the arrow keys pan a zoomed picture, with no handler of our own - and back onto the
  picture on close. One flag per direction, cleared in `OnAfterRenderAsync`, so a later render cannot steal
  the focus back from wherever the operator put it.

### `ProcessingActions` starts shut, on both pages that host it

Six buttons that throw stored work away are not what either page is for - the preview page is for looking at
the file, the library page for walking the tree - and open by default they sat between the operator and
that. It is a `CollapsiblePanel` now with no parameter: both call sites want the same thing, and a
destructive-action panel folded by default is the right default for any third.

On the preview page it also moved **above** `MediaFileFacts`, which the ask named explicitly. Shut, it is one
line, so it names what the page can do to this file without costing the facts underneath it any height.

### Reset reopens the filter panel

This **reverses** the 2026-09-10 decision recorded in section 6k. The reasoning there was that the Footer
keeps Search and Reset reachable and the heading carries the summary - true, but it misses that after a Reset
there is nothing left to read on a shut panel: no results, and a summary that says *no filters set*. The
panel stayed folded over an empty page, so Reset looked like it had done nothing. `CollapsiblePanel.Expand()`
mirrors `Collapse()` exactly, including rendering itself for the same reason, and `SearchPanel` forwards it.

### Help and About

**`/admin/help`** is the operator's manual: what the server is, getting media in (source / excluded /
temporarily hidden folders), previews and file details and the ffmpeg dependency, the four ways of finding
things, memory, what each Maintenance button costs, why a television might not see the server, why a file
plays on a television but not in the browser, and where the settings, index, previews and logs live.

Deliberately **plain `Panel`s rather than `CollapsiblePanel`**: a manual is read straight through and searched
with the browser's own Ctrl+F, and text inside a shut `CollapsiblePanel` is not in the DOM to be found. Also
paragraphs with bold lead-ins rather than `MetaList`, even where the shape is label-and-value - `dl.meta dd`
carries `word-break: break-all` so a file path never widens a table, and on a sentence that breaks words at
every line end.

**`/admin/about`** carries the build (product, informational version, runtime, machine, OS) and, more usefully,
**what the televisions are told** - friendly name, model, manufacturer, contact and both ports, straight off
`ServerOptions`. Some renderers key their own quirks off the model and manufacturer strings, so an operator
chasing a television that behaves oddly needs to see the exact values it was given.

Both pages are **statically rendered**. Neither has a control, so neither opens a SignalR circuit - the rule
`App.razor` states, and the same reason `Dashboard` is static.

### The copyright line

`Copyright.Notice` in `DlnaServer.Admin`, shown at the foot of the sidebar and on About. Held there by the
menu claiming the column's spare height (`.sidebar` is a flex column, `.nav` is `flex: 1 1 auto`), so it sits
at the bottom of the screen on a short menu and below the last item on a long one - never floating mid-column.
Hidden in the phone layout, where the sidebar is a bar across the top; About carries it there.

**Deliberately not taken from `ServerOptions.ManufacturerName`**, which looks like the same fact and is not:
that string is the identity the device advertises, an operator is free to change it, and some televisions key
quirks off it. Who holds the copyright does not change when a deployment renames its device. The year is read
once when the type is first touched, so a server running across New Year shows the old year until it restarts
- stale by a few days beats a notice that has to be edited every January and will not be.

### Verified by running it

A throwaway instance on 27851/27852 against a scratch library (three generated JPEGs - 1600x900, 800x1400 and
120x90 - plus random-byte `.mp4`, `.mp3` and `.avi`), driven in a real browser. 0 build warnings, 721 tests
green, no console errors, no `ERR`/`FTL` in the server log.

Checked at 1280px: the zoom button's box matches the picture's box exactly on all three photographs; the
overlay opens fitted; 150% gives both scrollbars and enables *Fit to screen*; Escape closes
**with the focus on a toolbar button**, the focus returns to the picture and `html` goes back to
`overflow: visible`; *This folder* and *This file* are shut on arrival and open on one click; the six buttons
and *Include every subfolder* come back intact; Search collapses and Reset reopens on **both** search pages.
Checked at 390px: `.photozoom` and `.sitefoot` are both `display: none`, and tapping the picture navigates to
`/admin/photo/{id}`. *This file* was confirmed shut above *File* on an image, on audio, and on an unplayable
`.avi`, so the ask's "all media-kind" is covered rather than assumed.

**This paragraph used to record the upscaling as "one consequence worth knowing, not a defect" - reading the
ask's "scale of the picture up to the width of the screen" as licence to enlarge a small photograph. The maintainer
called it on 2026-09-12 and he was right**, which is why the zoom paragraph above now describes a fitted
default and percentages of the picture. Worth keeping as a reminder that "the ask literally says so" is a weak
defence for behaviour that looks wrong on screen.

Re-verified 2026-09-12: a 320x240 photograph opened at 320 rendered pixels labelled 100%, and zoomed out to
240 / 160 / 80 against 75 / 50 / 25%. A 4000x3000 one opened at 1361 in a 1376 stage, 75% rendered 1021, and
175% rendered 2382 with the stage pannable. Escape closed and returned the focus to the picture. **Those
numbers were all correct and the rendering was still wrong** - the widths were right while the picture was
being squashed, which is the 2026-09-14 correction above. The current mode split is verified in that section;
the overlay has not been re-checked against a running instance since, only against the CSS in a browser.

**A row of buttons between panels needed a panel's own gap below it.** `.actions` carried no margin at all, so
the preview page's *Previous / Next / Download* row sat flush against *This file* underneath while every panel
around it was spaced by 18px - reported the same day. `.content > .actions` now carries `margin-bottom: 18px`,
scoped to a direct child of the page so the rows that live INSIDE a panel keep their container's spacing. The
only other page with a top-level row is Settings, where the row is last and the margin is trailing space.

## 6n. Uploading, folds and provenance tooltips, 2026-09-15 (`1.1.0915`)

The whole of the backlog's open list, closed in one batch: collapsible listings (1), a file
upload page with its own settings, remembered destination, security log, overwrite/skip and a report
(2 and 2a-2f), provenance tooltips behind a temporary switch (3, 3a), an assembly table on About (4), and
`release-notes.md` (5). Three of the five are admin-UI only and cannot regress a television. **Uploading is
not**: it is the first code in this server that writes into the media tree, and the first HTTP endpoint
outside SOAP that reads a request body.

### Uploads are a streamed form post, not a circuit

The files never touch the Blazor circuit. `Pages/Upload.razor` is **statically rendered** and posts a plain
`multipart/form-data` form to `UploadController`, which walks it with `MultipartReader` and streams each
part straight to disc. A circuit moves a body in 32 KB SignalR messages through this process's memory,
which is the wrong shape for a film; a form post is also what makes the page work identically from an
Android phone, a Windows laptop and a Linux desktop, with the browser drawing its own progress.

The form's fields sit **before** its file input, deliberately: a multipart body arrives in document order,
so the destination is known by the time the first byte of the first file does. Otherwise every file would
have to be buffered before the endpoint could learn where to put it.

Three things had to be got right for this to work at all, and each was found by running it:

- **The endpoint cannot share the page's path.** A Razor component endpoint answers `POST` as well as
  `GET`, so `/admin/upload` matched both the page and the controller and every upload was an
  `AmbiguousMatchException` and a 500 - while the `GET` carried on working, because the controller has no
  `GET`. The route is `/admin/upload/files`.
- **MVC reads the whole form before the action runs.** The form value providers call `ReadFormAsync` for
  any request with a form content type, whether or not the action binds anything from it, so the body was
  already consumed and spooled to temporary files and `MultipartReader` threw *"Unexpected end of Stream,
  the content may have already been read by another component"*. `DisableFormValueModelBindingAttribute`
  removes those factories for this one action. Without it an upload cannot stream at all.
- **Turning uploads OFF must remove the controller, not silence it.** The first version cleared the
  controller's selectors, which left an action carrying `[ApiController]` with no attribute route - and
  `MapControllers` refuses that outright, so **the server did not start in its default configuration**,
  since uploads ship off. `DisabledUploadConvention` is an `IApplicationModelConvention` that removes the
  controller from the model. `DisabledUploadConventionTest` pins it.

None of the three produced a build warning or a failing test.

### What the endpoint refuses, and why the page is not trusted

The form is a text field a browser can be made to say anything in, so `UploadDestination` re-derives the
allowed folders from configuration and checks the choice against them: the root must be one this server
offers, the sub-folder must be relative, free of `.` and `..`, and must resolve to somewhere under that
root, and the result must not be a folder `ExcludeFolders` hides - a file written into `.@__thumb` would be
accepted and then never appear. `UploadFileName` reduces the name to its last segment (a directory upload
sends `Holiday/DSC_0001.jpg`, an old browser the whole local path) and accepts only extensions the library
would index, configuration first and then the catalog, which is the same two tiers the scanner resolves a
MIME with. Each file is written as `<name>.uploading` and moved into place, so the watcher sees one arrival
rather than a growing file and a scan running mid-upload walks past it.

`Upload.MaxSizeInMegabytes` is enforced **while copying**, per file, and Kestrel's global 256 KB body cap
is lifted for this one request only. A per-request total would fail a batch of ten legitimate files for
being large together.

**`UploadDestination.Roots` answers full paths.** It did not at first, and the symptom was not a refusal:
everything worked and the destination was simply never offered again, because the page showed the root as
configured while the endpoint recorded the resolved path. A short 8.3 path on Windows is what exposed it.
`UploadRootsTest` is that case.

### Remembering where the files went

Two places, and the second is the point: a cookie carries the destination per browser, and
`UploadDevices` - one row per device, keyed by a hash of address and user agent - answers when the cookie
has been cleared. The row also records the address, user agent, accepted language, last destination and a
running file count. It is the **second migration** on top of `InitialSchema`, additive, so no rescan.

`logs/uploadSecurity.log` is the lasting record: one line per file with everything known about the sender.
It is a **security** record rather than a diagnostic one - someone looking at a file that should not be in
the media folders needs to see how it arrived - which is why it holds more about the sender than the
feature needs and why it is filtered out of `app.log` by source category, exactly as the slow-query log is.
The `ByExcluding` arm on `app.log` had to learn the new category too, or every line would be in both files.

### Provenance tooltips

`Shared/Provenance.razor` wraps a value and, while the period is running, renders it as
`<span class="tech" data-tech="...">`; the tooltip is a CSS `::after`, since this project ships no
JavaScript and a `title` attribute cannot be styled, waits a second and never appears on a touch screen.
`StatTile` and `MetaRow` took an optional `Source` parameter, which is what made annotating every page one
attribute per value rather than a rewrite.

The bubble is `position: fixed` with no offsets, and that is not a preference. A panel is
`overflow-x: auto`, which makes its other axis scrollable too, so an absolutely positioned bubble would be
clipped by its own panel and put a scrollbar on it; a fixed box with auto offsets is laid out at its static
position and no overflow ancestor clips it.

**Every annotation follows one grammar**, and the first pass did not - the three in the ask were bare
identifiers while a dozen of mine had grown an explanatory clause (`... - every indexed row`,
`..., read by ffprobe`). Reported as a defect, and it is one: a tooltip that explains itself in prose on
one page and names a column on the next leaves the reader unable to tell which they will get. The rule,
written on `Provenance.Source` where the next annotation will find it:

| Kind of value | Form | Example |
| --- | --- | --- |
| Runtime figure | its metric name | `Working Set (MB)`, `GC Collections Gen0/Gen1/Gen2` |
| Setting | key path under the file holding it | `config.json Dlna.Server.ManufacturerUrl` |
| Indexed value | `database <Table>.<Column>` | `database Files.SizeInBytes (MB)` |

A unit or rendering goes in brackets at the end - `(MB)`, `(UTC)`, `(%)`. No clause after a dash or a
comma; the page's own label and hint are where prose belongs. `ShowMore` takes the source from its caller
for this reason: the component counts whatever it is given, so only the caller can name it.

The switch is `ITechnicalTooltips`, a copy of `ITemporaryFolderVisibility`'s shape - a singleton outside
`DlnaOptions` holding one expiring window, cleared lazily on read. Outside the options tree because
`SettingsWriter` serialises the whole graph, so anything reachable from `DlnaOptions` reaches `config.json`
on the next save.

### Verified by running it

Both defects above were found this way, and so was the root-spelling one. On a live server: a two-file
upload wrote the `.jpg` and refused the `.txt` with a reason; skip left a 3,009-byte file alone and
overwrite replaced it with 5,002 bytes; `..`, an unoffered root and an absolute sub-folder were each
refused with a 400 and nothing was written outside the library; the scan that follows an upload indexed the
new file (+1, 5 files in 4 directories); the destination prefilled from the cookie and, with no cookie
sent, from the database; `logs/uploadSecurity.log` carried a line per file including a real browser's user
agent and language; with uploads off the server started, the endpoint answered 404 and the nav entry was
gone. In a browser: Folders collapsed to *"1 folder(s)"*, the assembly group listed 171 rows including
`DlnaServer.Host 1.0.915.0 / 1.0.0915`, and the three tooltips from the ask read *Working Set (MB)*,
*GC Collections Gen0/Gen1/Gen2* and *config.json Dlna.Server.ManufacturerUrl*.

### Checking the upload folder

*Upload folder* on Settings has the *Check* button *Source folders* has, wired to the same
`ISourceFolderChecker` and rendering through the same `.checkrow` / `.checks` markup. Nothing new was
built for it: the checker already takes a collection and answers per path, so the field passes its single
value.

Two things it deliberately does not do. It does not check that the folder sits inside a source folder -
that is `DlnaOptionsValidator.ValidateUploadFolder`'s rule, it never touches the disc, and the save is
already refused with a message naming it. Keeping the split is the point: the button answers "is it
there", the save answers "is it allowed". And a **blank value is answered in words rather than handed to
the checker**, which would return `"Blank."` and paint a legitimate setting as a fault - empty means every
source folder is offered, which is what the hint under the field already says.

`.checkrow` had to learn about `input` as well as `textarea`: `.field input[type=text]` pins a 320 px
width, so without the added selector the box sat stubby beside the button instead of filling the row.
Verified in a browser at 914 px in a 994 px row, identical to the Source folders textarea above it.

### The 403 the `fetch` probe could not see

Reported from the NAS on 2026-09-16: every upload answered **403**. The whole feature was dead, and the
tree was green - 0 warnings, 772 tests. `1.1.0916` fixes it with one word.

Two facts have to be held at once, and neither is visible from the source.

**Fetch metadata never arrives here.** `Sec-Fetch-Site` is sent only to potentially-trustworthy origins,
and this server is plain HTTP on a LAN address. So `RejectCrossSiteEndpointFilter`'s primary check never
runs in any browser - the `Origin` branch, written as a fallback for Safari 15.x, decides every request.
Confirmed against Chrome 153: no `Sec-Fetch-*` on either a form post or a `fetch` to `:26853`.

**This server's own response header then blanked the `Origin`.** A navigation-mode `POST` serializes its
`Origin` as the literal `null` when the document's referrer policy is `no-referrer` - which `Program.cs`
set on every response. `Uri.TryCreate("null", …)` fails, so the filter refused the operator's own form.
The fix is `Referrer-Policy: same-origin`, which nulls the `Origin` only when the request really is
cross-origin - exactly the signal the filter wants - and still lets no referrer leave this origin.

**Why the earlier verification missed it, and the lesson.** A `fetch` runs in cors mode, and cors mode
always sends the true serialized origin whatever the referrer policy says. So the one probe that had been
used passed while every real form failed. The open item above named this gap in plain words and it was
still not the thing that got checked. **A guard is verified only by the exact request shape a user makes** -
for a form, a form, submitted by a browser, over the origin it will actually be reached on. Testing it on
`localhost` would also have passed, because localhost is trustworthy and does send fetch metadata.

Verified 2026-09-16 on a live server reached over a LAN IP: the picker took a real file and the browser
posted it (303, `Uploaded`, 160 bytes on disc byte-identical), while a form posted from a different origin
still got its 403. `Post_WithAnOpaqueOrigin_Is403` pins `Origin: null` as refused, so the 403 cannot later
be "fixed" by relaxing the filter - a sandboxed frame and a `data:` URL send that value too.

The filter is the only thing in the server that answers 403; every other guard answers 404. A 403 from
this server therefore always means this filter, which is worth knowing before reading anything else.

### Help

`Pages/Help.razor` gained *Adding files from a browser* and *Where a number on these pages comes from*,
and the Library folds, the assembly group and `logs/uploadSecurity.log` are named in the sections that
already covered their pages. It was missed in the first pass and reported: the manual is the one page an
operator reads to find out a feature exists, so a feature absent from it is, for them, not shipped.

### Open

- ~~A real browser has not driven the file picker.~~ Closed 2026-09-16, and closing it is what found the
  403 below. `fetch` was the wrong instrument precisely because it is not a form: see "The 403 the `fetch`
  probe could not see".
- **Nothing limits how much can be uploaded in total**, only how large one file may be. A disc fills.
- **The endpoint is as unauthenticated as the rest of the admin surface.** Decision 1 in section 7b still
  holds - trusted LAN - but this is the first endpoint where that decision lets a device on the network
  write to the filesystem rather than read from it. `Upload.Enabled` shipping off is the control.

## 6o. Four operator asks, 2026-09-17 (`1.1.0917`)

The backlog's open list, closed. One is a defect with a real cost on disc; three are admin-UI
features. The fifth item on that list - a *Check* button for the upload folder - was **already built** on
2026-09-16 and is described in 6n under "Checking the upload folder"; the inbox row was stale, and nothing
was written for it.

### A deleted file left its preview behind forever

**Reported by a customer**, and the cost is unbounded rather than cosmetic: previews live beside the media
in `Thumbnails.SubFolderName`, which scanning is configured to skip, so an image whose media file is gone
is not merely untidy - it is unreachable. Nothing enumerates that folder, nothing adopts an image with no
file to belong to, and no rebuild or rescan ever revisits it. A library that churns files accumulates them
until the disc is full.

The move path had been right since 6g: `MoveOrRenameAsync` returns the abandoned image's path and
`LibraryIndexer.TryDeleteAbandonedThumbnail` removes it, evicting it from the byte cache first and
swallowing `IOException`/`UnauthorizedAccessException` so housekeeping can never cost the pass. The delete
path never learned the same trick, because `RemoveByPublicIdsAsync` answered a count and the thumbnail rows
vanished by cascade with their paths still in them.

So `RemoveByPublicIdsAsync` now answers `(int Removed, IReadOnlyList<string> AbandonedThumbnailPaths)` -
deliberately the shape `MoveOrRenameAsync` already uses in the same interface, rather than a second method
a caller can forget to call. It projects the one column over the same set the delete matches, **before**
the delete, because the cascade is what destroys the evidence. The indexer destructures and loops the
existing helper.

The split is the one persistence keeps everywhere: the repository names the files, the caller deletes them.
Persistence does not touch the disc.

**Not changed, and reporting it as an oversight is wrong**: `ClearThumbnailsAsync` and
`ClearAllThumbnailsAsync` still leave the images alone. That is the standing decision in section 7 under
"Clearing versus recreating" - the media is still there, a rescan adopts the image, and that is what makes
a redeploy cheap. Only a file that is *gone* orphans its preview.

### Correcting one file's type and profile

`Files.Mime`, `Files.DlnaProfileName` and `Files.UpnpClass` are written once, when the file is first
indexed, from the extension map - and nothing re-derives them. That is what makes an edit here durable
rather than fragile: it survives every rescan - though **not** *Rebuild index* or *Recreate database*,
which delete every row unconditionally and re-derive these three columns from the extension map. It is
also the only way to correct a single file without
changing the map for every file sharing its extension.

`UpdateDlnaMappingAsync` takes the MIME and the profile and **recomputes the UPnP class itself**, from
`DlnaMimeCatalog.ToDefaultItemClass` - the same call `LibraryIndexer` makes at insert. The class is not an
independent choice, and a picture retyped as video that kept `imageItem` would advertise one thing in its
class and another in its type, which is precisely the disagreement that hides content from a television.
Confirmed in a browser: retyping a `.mkv` to `image/jpeg` moved the *Kind* row from *Video* to *Photo*
without the page asking for it. A blank profile is stored as `null`, because empty and "the standard
profile for this type" are the same thing in `res@protocolInfo`, and an empty string would be advertised
as a profile named nothing.

`Shared/FileTypeEditor.razor` is its own component for the reason `ProcessingActions` is one: it writes to
the library, and it is hosted by `MediaFileFacts`, which also states facts for listing tiles and must stay
read-only and repository-free. So `MediaFileFacts` gained one optional `RenderFragment Actions` rendered
under its `MetaList`, and the preview page passes the editor into it - the correction sits **inside** the
group it corrects, under the two rows it rewrites.

Two details worth keeping. The controls seed **once per file**, keyed on `PublicId`, not on every parameter
set: the preview page re-renders on every sibling step and every completed action, and re-seeding there
would throw away a half-made edit. And the profile control is a `datalist`, not a `select`, exactly as the
Settings extension map does it - the known profiles for the chosen type are offered, the list follows the
type, and a renderer wanting something not on the list can still be given it by typing.

### Letting a file back into memory

`Files.IsExcludedFromCache` is a latch. `MediaCacheFillHostedService` sets it when reading a file has
genuinely failed - it proves the failure by opening the file rather than inferring it from an empty answer -
and the only thing that ever cleared it was `UpdateContentAsync`, when the file's own bytes changed. A file
that was briefly unreadable, because a share was unplugged or a permission was wrong, therefore stayed
barred from the cache for the life of its row however healthy it had since become, with the page saying
*Kept in memory: no* and offering no way out.

`ResetCacheExclusionAsync(MediaFileScope)` is the way out, and it takes a scope rather than an identifier
because the plumbing for that already exists and a wrongly-mounted share bars a folder, not a file. It is
narrowed to `f.IsExcludedFromCache`, so the number reported is what was let back in rather than how many
files the scope covered - the admin UI shows that number to the operator, and counting untouched rows would
claim a folder of healthy files had just been repaired.

It is a seventh button on `ProcessingActions`, which means it appears under *This folder* on Library as
well as *This file* on Preview. That was chosen deliberately. It is the one button there that throws
nothing away, so the panel's lead paragraph says so.

### Searching for the files it happened to

`MediaFileSearchRequest.IsKeptInMemory` is written in the operator's direction, which is the **opposite** of
the column's. The negation lives in exactly one place, the SQL predicate, so nothing else has to reason
about a double negative, and both directions are pinned by a `TestCase` pair - a single inverted comparison
would otherwise pass one of them.

The filter is **not** paired with *Details read* / *Preview made*, though it looks like it belongs there.
Those two ask what has not been produced yet; this asks what the server has already failed to read, which
is a different question with a different remedy - the button above. Pairing them would invite the wrong
conclusion, which is the exact reasoning `FieldPair` exists for.

### The second review pass, and what it changed (2026-09-17)

A second `/review-all --full` ran once six reviewers that had been project-scoped to another repository
were installed globally. They found four things twelve reviewers had not, and all four are fixed here.

**Retyping across media KINDS now re-queues the file.** `UpdateDlnaMappingAsync` wrote the three mapping
columns and nothing else, so the stamps still matched and the file was never re-probed - and both the
metadata probe and the thumbnail generator dispatch on the kind. A video retyped to a picture kept
advertising `res@duration` and `upnp:videoCodec` on an `imageItem`, which is the same class of DIDL
self-contradiction that once hid a library from an LG television. Worse, `FileServerController` takes its
`Content-Type` from the stored MIME, so the bytes went out labelled as the new type. A same-kind
correction (a container or profile fix) deliberately still costs nothing.

**The editor now offers only what the server advertises.** It enumerated `DlnaMimeCatalog.All`, which
includes subtitle types - these collapse the item to a bare `object.item` - and types with no file
extension, which `GetProtocolInfo` never lists, so a renderer filtering on `SourceProtocolInfo` drops the
item. `DlnaMimeCatalog.IsServable` is now the one definition, read by the editor and by
ConnectionManager; `DlnaMimeCatalog.Servable` is the sorted list, built once, which also retired the
per-render sort the two search/editor dropdowns were each doing. The repository gates on the same
predicate plus `Enum.IsDefined`, because the dropdown is client-side - `LibraryIndexer.BuildScanOptions`
already carries that guard, and its comment names the admin editor as the validation that was assumed and
was not enough.

**`DlnaProfileName` is validated.** It is interpolated verbatim into `DLNA.ORG_PN={profile};DLNA.ORG_OP=`,
so a `;` appends fields of its own. `HasMaxLength(128)` bounds nothing: SQLite ignores a declared text
length and EF Core validates none. Letters, digits and underscores, 128 at most, checked at the write.

**The tracked read became load-bearing, so it had to go.** A Blazor circuit holds one DI scope for its
whole life, so the pooled context's identity map accumulates across every click and a tracked read
returns the cached copy rather than the row. Comparing the old kind reads a *value*, and the
`ExecuteUpdate` siblings bypass the tracker entirely - so the comparison is now an `AsNoTracking`
projection and the write is `ExecuteUpdateAsync`, matching every sibling.

**Two writers of `DlnaProfileName` disagreed.** Insert stored the catalogue default eagerly, the update
stored null. Invisible on the wire (`ResolveProfile` re-derives it) but visible in the admin facts row,
where two files identical on the wire read differently. The update now matches the insert.

**Still open, deliberately.** The delete path is not atomic: `ExecuteDeleteAsync` commits and the
abandoned-path list then lives only in process memory, so a crash in that window re-orphans a preview
with no log trace. A two-phase commit between SQLite and the filesystem is not available, and per-file
logging on a 25,000-file pass costs more than it is worth. The realistic fix is a Maintenance sweep of
`SubFolderName` for images whose row is gone - which is also the only thing that would reclaim previews
orphaned *before* `1.1.0917`, since those have no row to name them. `release-notes.md` says so plainly
rather than promising a sweep that does not exist.

### Verified by running it

The suite was 783 at 0 warnings when this batch first shipped, up 11 from 772; the second review pass in
this section took it to 789. But three of these are UI and the fourth is a filesystem
side effect, so all four were driven in a browser against a live server on a throwaway tree, with a
**genuinely barred file** rather than a seeded flag: `icacls /deny` on one `.mkv`, then a request for it,
which made `MediaCacheFillHostedService` record the exclusion through its own code path.

- The search filter found 3 files for *yes* and 0 for *no*, then exactly the barred file once there was one -
  and the choice survived collapsing and reopening the panel, so it is hidden rather than cleared. That is
  the check that matters on this page: `@bind` on a `select` does not round-trip a `bool?`, which is why the
  page's `Presence` enum exists, and it fails silently.
- Retyping a file moved *File type*, *Compatibility profile* and *Kind* together.
- *Allow in memory again* reported `1 file(s)` and flipped the facts row to *yes*, on the file panel and on
  the folder panel.
- A real JPEG was indexed, given a preview at `.@__thumb/photo.jpg.jpg`, then deleted from disc. The
  watcher-driven pass removed the row and the image with it, and the thumbnail folder ended empty.

Nothing in the repository was touched by the run: the database, the thumbnail cache, the source folder and
both ports were all overridden to the scratchpad, per section 7's "Running a throwaway instance".

## 6p. Upload containment and a split package pair, 2026-09-23 (`1.1.0923`)

Two items, both small, neither a feature. The first is the only genuine defect the external review pass of
2026-09-23 produced; the second is why pull request #11 had been sitting red.

### An upload could be written through a symlink, out of the library

`UploadDestination` did every containment check on the spelling of the path. `Path.GetFullPath` makes a
path absolute and collapses `.` and `..` **as text** - it never reads the filesystem - so the ordinal
prefix test in `TryCombine` compared two strings that both began with the root and passed. A sub-folder
that happened to be a symlink was an ordinary segment to every guard in the file: not rooted, no dot
segment, inside the root by prefix. The write then followed the link.

The fix is one filesystem check, `HasLinkedSegment`, walking the segments between the root and the resolved
destination and refusing the first one carrying `FileAttributes.ReparsePoint`. The root itself is
deliberately exempt - an operator may configure a link as a source or upload folder, and the string it
resolves to is shared by both sides of the comparison anyway.

**Refusing links here is consistent with the scanner rather than in tension with it**, and that is what
makes the fix safe to take. `LibraryScanner` puts `ReparsePoint` in `AttributesToSkip`, so a folder reached
through a link is never enumerated and never indexed. A file uploaded behind one would not have appeared in
the library under any circumstances, so nothing an operator wants is lost. It was briefly read the other
way while planning this batch - as though the scanner followed links and the fix would break a normal QNAP
layout - which is worth recording because the comment at that call site explains at length why skipping is
a *downside*, and it reads as though it describes the behaviour rather than the cost of it.

The file-name half needed the same treatment for a narrower reason: on Linux
`Path.GetInvalidFileNameChars()` is only `{'\0', '/'}`, so a name is not a way out, but `FileMode.Create`
follows a link sitting at that name and writes through it. `UploadController` now clears whatever is at the
`.uploading` path first - deleting a link removes the link, not its target - and opens with
`FileMode.CreateNew`, so anything still there is an error rather than a target.

**Severity, recorded so it is not re-litigated:** uploading ships off, needs a restart to enable, is
refused off-LAN, and planting the link needs write access to the media tree already, which the upload
endpoint itself cannot grant. Defence in depth, not a remote hole.

Covered by `UploadDestinationLinkTest` in the integration project rather than beside the nine existing
cases in `UploadDestinationTest`: that fixture works on strings that are never created on disc, which is
exactly why it could not have caught this. One of its assertions pins the premise - the lexical check still
passes for the linked path - so a later reader can see which check is doing the refusing. Creating a
symbolic link needs developer mode or elevation on Windows, so the helper calls `Assert.Ignore` with the
reason rather than failing; on this machine the three cases ran rather than skipping.

### SkiaSharp 3 to 4, both halves of it

Pull request #11 bumped `SkiaSharp` to `4.152.1` and left
`SkiaSharp.NativeAssets.Linux.NoDependencies` at `3.119.1`. SkiaSharp checks the native library's version
the first time anything touches it, so the build was clean and two tests died at run time in
`ImageTagReaderTest.CreateJpeg` constructing an `SKBitmap`: *the version of the native libSkiaSharp library
(119.0) is incompatible ... supported versions are in the range [152.0, 153.0)*.

Both versions now move together and a comment in `Directory.Packages.props` says why, since the failure
mode is a green build. Note what the local suite does **not** prove: a Windows run never loads the Linux
native package, so the half that was actually broken is verified by CI, not here.

## 6q. A library breakdown and a dashboard that refreshes itself, 2026-09-23 (`1.1.0923`)

Two operator asks, and the second one reverses a decision this project had made on purpose.

### What the library is made of

The Dashboard's *Library* panel had two figures, *Files* and *Folders*. It now splits the files by kind -
video, music, photos, and an *Other* tile that appears only when something lands in it.

The kind is **not stored**. `DlnaMedia` is derived from `DlnaMime` through `DlnaMimeCatalog.ToMedia()`,
which is a dictionary lookup and cannot be translated to SQL, and neither `Mime` nor `UpnpClass` is
indexed. `CountByKindAsync` therefore does what the media filter in `SearchAsync` already does: it groups
on the stored MIME in SQL and folds the groups to kinds in memory. One query, no new index, no new
abstraction.

**The trap here was consistency, not the query.** `CountAsync` counts every row and does **not** hide
`Library.ExcludeFolders`, while every listing does - so the old *Files* tile could already disagree with
the Library page. Had the new per-kind counts filtered while the total did not, the tiles would visibly
fail to add up and read as a bug. `CountByKindAsync` returns the total as the sum of its own parts, so the
panel is consistent by construction, and the tile now agrees with the Library page for the first time.
`CountAsync` itself was deliberately left alone: `LibraryIndexer` reconciles against it and needs every
row, so the two answers are allowed to differ and now do so on purpose.

### The Dashboard refreshes itself

Both pages read everything once in their initialise handler and never again. They now tick.

**The Dashboard was statically rendered, and `App.razor` argues for that**: static by default, interactive
per page, because a circuit owns a DI scope holding a `DbContext` and a pooled SQLite connection - and the
Dashboard is the landing page, the one most likely to sit open in a tab. It is now
`@rendermode InteractiveServer`, which is that cost accepted rather than overlooked.

What makes it affordable is that the two halves refresh at different rates. `IServedFileCache.Describe()`
is synchronous, touches no database and no disc - counters and `_cache.Count` - so the memory and
cache tiles tick every five seconds. The counts are a database round trip and refresh on every sixth tick,
through `AdminPageBase.RunGatedAsync`, because on a timer the race the gate exists for is no longer
hypothetical.

*Superseded 2026-09-24: the counts now re-read only when `ILibraryChangeSignal` has moved or the hidden folders changed - see the trap on it in `docs/decisions.md` section 7.*

*Recently added* is not a page: it is a `CollapsiblePanel` on `Library.razor`, which was already
interactive, so it costs nothing new. It re-reads **only** that list. Reloading the page would rebuild
`_folders` and `_files` and throw away every page the operator had pulled in with *Show more*.

**No push seam was built, deliberately.** There is no backend-to-UI notification path today and adding one
was not warranted: `ILibraryScanSignal` is a single-consumer `SemaphoreSlim`, so a UI waiter would steal
the indexer's wake-up, and `IDatabaseReadySignal` latches rather than repeating. These are counters, not
events. During a scan they change continuously, so a push per indexed row would be thousands of messages a
second and the interval is what coalesces them.

The timers hang off the existing `AdminPageBase` plumbing rather than new lifetime code: the loop waits on
`PageToken`, which the base already cancels on dispose, and each page overrides `Dispose(bool)` to call
base first - so the token is cancelled and the loop unwinding before the timer it waits on goes away.

## 6r. What is being served, and what the configuration file did, 2026-09-23 (`1.1.0923`)

Two logging asks. Neither adds a mechanism; both make something the server already knew say so.

### Serving a media file is now Information

`MediaContentResolver.Resolve` already returned `MediaContentSource.IsCached`, and
`FileServerController.LogServed` already fanned out to a transfer line and a range line carrying a
`cache` / `disc` / `database` label. Only the level changed.

**On the maintainer's decision, both lines moved to Information** - the range line as well as the
transfer. The two costs recorded at those declarations were put to him and are unchanged by the decision:
one line per file served, kept for a rolling week, is a viewing history in plaintext on a share anyone on
the LAN can read; and a renderer issues one request per byte range, so a single film produces hundreds of
the range line, which is how 32 MB of `app.log` filled in a day the last time this was at Information.

Thumbnails stay at Debug, as asked, and that is the only reason `LogServedThumbnail` exists as a second
fan-out: `[LoggerMessage]` fixes the level at compile time, so one shared pair of declarations could not
carry two levels. A browsing television asks for a preview per tile, so those outnumber the media lines by
orders of magnitude and none of them says anything about what is being watched.

**The retention note this forces.** `app.log` rolls daily at a 32 MB cap with
`retainedFileCountLimit: 14` and `retainedFileTimeLimit: 7 days`, and Serilog applies both. The comment on
that constant said the count "only bounds a pathological day" - which was true only because the measured
32 MB day had this logging ON. Several segments a day is now the ordinary case, so **the count can reach
back less than seven days on a busy one**. Left at 14 rather than raised: 14 x 32 MB is already 448 MB in
a publish folder on an SMB share, so the trade taken is a shorter window on heavy days, not more disc.
Raising the count, lowering the per-file cap, or accepting the shorter window is the open choice.

### The configuration file says what happened to it

`ConfigurationFileGuard` already backed up and replaced an unusable `config.json` (Warning, naming the
backup) and already wrote defaults when none existed (Information). Two cases were missing.

**A clean read said nothing at all**, so "the file was read and taken" was indistinguishable from the
guard never having run. `ConfigurationFileStatus.Valid` now logs at Information.

**A file that parses but carries no `Dlna` section** bound to nothing and the server ran entirely on
defaults looking healthy. The likeliest cause is named in `CLAUDE.md`: the reference server's
`config.json` is flat, every key at the root, and the two schemas are not compatible. The new
`Unrecognised` status reports it and **leaves the file exactly as it was** - nothing is wrong with it as a
file, so replacing it would destroy settings the operator may only have mis-shaped, which is what makes
this a different outcome from `Replaced` rather than another reason for it.

An empty object is deliberately still `Valid`: it carries no settings to lose, and the missing source
folder it produces is already reported by `ReportSourceFolderFallback`. Two warnings for one situation is
noise.

## 6s. The README split into docs/, 2026-09-23 (`1.1.0923`)

Both external reviews of 2026-09-23 reached the same conclusion independently, and it was already open in
the inbox: a 494-line README was carrying architecture, conventions, an operator manual, protocol
compatibility, performance rationale and implementation history at once. It is now 94 lines and links out.

### What moved, and what deliberately did not

Six documents under `docs/`, indexed by `docs/README.md`: `architecture.md`, `dlna-compatibility.md`,
`configuration.md`, `operations.md`, `performance.md` and a new `troubleshooting.md`.

**The moved prose was moved, not rewritten.** The sections were relocated by line range so the committed
text survives byte for byte - the point of the exercise is where a reader finds something, not a fresh
pass over wording that was already good. One thing genuinely changed place: a blockquote about the
grouped configuration schema had been sitting at the end of the library-filtering section, and it now
sits under `## Configuration` where it belongs.

**The review proposed about 35 files across six folders plus nine ADRs. That was cut to six documents and
no ADRs at all.** `docs/decisions.md` section 7b is already the decision record; a second one in `docs/decisions/`
would drift from it, and the drift would be silent. For a single-maintainer repository the 35-file tree is
a maintenance liability rather than an information architecture.

`troubleshooting.md` is the only document that is new writing. Its seven symptoms are the failure modes
this project has actually hit - bridge networking swallowing SSDP, ffmpeg absent and silent, cache churn
in the working set, a setting that needs a restart - not a generic checklist.

### The rules that keep it from growing back

Two paragraphs were added to `CONTRIBUTING.md` rather than to a document nobody reads before writing code.

**Documentation explains *why*, tests enforce *what*.** Anything `ArchitectureTests`, a golden wire test
or a migration already guarantees is stated in `docs/` as a fact with its reasoning and never restated as
a rule. A rule written twice eventually disagrees with itself, and the prose copy is the one that goes
stale without anything failing.

**The abstraction threshold**, which is the one recommendation from the review pass worth taking wholesale:
a new abstraction needs a real boundary, more than one implementation, an independent lifetime,
independently testable behaviour, a dependency violation it prevents, or a meaningful domain concept. It
is a brake on adding rules, not a loosening of the existing ones.

### Version statements, and the ones that must not be swept

Inbox item 3a asked for "updating all other notes, readme-files and other md-files" on a version bump.
Taken literally that corrupts the record: `history.md`'s batch headings, `decisions.md`'s trap entries, and
`release-notes.md`'s section headings, are **historical references** and are correct as written.

Exactly two statements track the current build - the version line near the top of `README.md`, which was
stale at `1.1.0917` against `1.1.0922` when this started, and the example release tag in
`CONTRIBUTING.md`. `.github/SECURITY.md` carries a `Major.Minor` support table that moves on a Minor bump
only. That distinction is now written into `CONTRIBUTING.md` under "Versioning and releases".

### THIRD-PARTY-NOTICES.md

The one commercial-legal item that applies to a public MIT repository. **Every row was read from the
`<license>` element of the package's own `.nuspec` in the local NuGet cache rather than asserted from
memory**, which is what caught the three that do not declare a plain SPDX expression: SQLitePCLRaw ships
a licence file, Xabe.FFmpeg points at a URL, and NetArchTest declares nothing at all.

Two are not simply permissive and are called out: **Xabe.FFmpeg**'s free tier is non-commercial, and
**FluentAssertions** became paid for commercial use at version 8 - test-only, so not distributed, but a
commercial build pipeline is still a use of it. ffmpeg itself is not distributed at all.

**Not done, and a judgement call left open:** `DlnaServer.Host.csproj` copies `release-notes.md` and
`LICENSE` into the output so a deployment carries them. `THIRD-PARTY-NOTICES.md` is not in that list. It
arguably should be if the server is ever redistributed.

## 6t. Operator asks raised after 2026-09-25 (`1.1.0926`)

The inbox batch in `NOTES.local.md` headed "after 25.09.2026". This section records it as it closes.

### Columns, labels, a page title and a phone menu (items 1-4, 6, 8)

**The tile grid has four fixed columns** (`.grid` in `admin.css`), two at 1200 px of viewport and below. It was
`repeat(auto-fit, minmax(180px, 1fr))`, which stretched every panel's tiles across its own row: a two-tile
panel put its second figure half-way across the page, so no figure lined up with the one above it. The
operator had started on this with an empty `<div/>` padding the Dashboard's kinds row to four cells when
*Other* is absent. It is kept at the operator's request, but under fixed tracks it has no visual effect -
the grid now does what it was reaching for. With fixed columns the pinned panel on the *Recently served
files* page (`/admin/cache`) falls into exactly the layout asked for - *In use / Limit / Largest file kept
/ Files kept* on the first row, the served counts and *Reading now* on the second.

**The breakpoint is 1200 px, not 1000 px.** It is a viewport width, and the viewport carries the 220 px
sidebar and 64 px of padding the grid does not get: at 1000 px four columns came to about 160 px each, which
wraps a figure such as `2026-09-26 09:22` and puts its first line above its neighbours' - the misalignment
the change was for. At 1200 px each column still gets about 200 px.

**A tile is now a flex column with its figure at the foot**, so a label that wraps to two lines no longer
pushes its figure below its neighbours'. That is what made a longer label affordable:
*Memory / disc / database* became *Times served from memory / disc / database*, with a sentence under the
grid saying what the three counts are. The comment that had kept the label short "so Reading now does not
drop onto its own row" was only true under auto-fit, and was updated with the change.

*Tidy-ups so far* became *Memory tidy-ups so far*.

**Page title.** `App.razor` carried a static `<title>`; it is gone, `MainLayout` renders a default
`<PageTitle>` ahead of `@Body`, and the preview page and the phone's photo page render
`<file title> - ZEN DLNA Server`. The static element had to go, not merely be overridden: once any
`<PageTitle>` renders, `HeadOutlet` adds a `<title>` of its own, and with two in the head the browser shows
the first. **Preview is interactive while `HeadOutlet` is static**, so its title reaches the tab through the
prerender and through enhanced page loads, which is also how *next* and *previous* arrive; with
prerendering off the tab would keep the default. Verified on the running server - one `<title>` element,
the file's name in the tab, and the name following a step to the next file.

**The phone menu is a checkbox and a label, with no JavaScript**, in keeping with the rest of the admin UI.
The label is the button; the stylesheet shows the menu while the checkbox is ticked. The layout is
statically rendered, so an interactive toggle would have cost a SignalR circuit on every page - the
reason `App.razor` gives for keeping the router static. **Enhanced navigation re-renders the checkbox
unticked, so choosing a page closes the menu** - verified in the browser rather than assumed, since
`checked` is a property and not every DOM-patching scheme resets one. A reload or a back-forward restore
does not go through enhanced navigation, and Firefox restores a checkbox's state on both, so it carries
`autocomplete="off"`. On a phone the checkbox is visually hidden rather than `display:none`, so it stays
reachable from a keyboard; on a wide screen it is `display:none`, so it is not an invisible tab stop.

### `CONTRIBUTING.md` became `docs/development.md` (items 5, 5a)

The operator's reading was right: the file was a developer guide - setup, the two hard rules, test layout,
versioning and releases - under a name that promises something else. It moved to `docs/development.md`,
and **`CONTRIBUTING.md` was recreated as a short page
naming who contributes** - the maintainer, and Claude Code as the assistant the maintainer works with.
The name is kept at the root rather than dropped because GitHub links to that exact file from its
new-issue and new-pull-request pages; a plain rename would have removed the link. Because the old name
still exists, git records the move as a new file, so the guide's earlier history is in the log of
`CONTRIBUTING.md`.

Every other Markdown file was checked name against content (5a). None needed renaming. What the audit did
find was an index with gaps: `docs/README.md` now lists `development.md` and the two deployment guides,
`Docker.usage.md` and `NasBuild.usage.txt`, which it had never mentioned. Moving those two into `docs/` was
considered and left alone - about twenty references across the `Dockerfile`, both compose files, the
solution, `.dockerignore`, `NasBuild.sh` and a C# comment, for a gain of one folder. The README version line
and the example release tag were both still `1.1.0923` against a host at `1.1.0924`, and were corrected.

Left as found: this file carries a second `#` heading, *DlnaServer rewrite — working plan*, which opens the
archived plan inside it. Demoting it would put it on the same level as the `##` sections it contains.

### Memory the served-bytes cache kept after it was done with it (metrics pass)

The live server was read on 2026-09-26 at `1.1.0924`, 14 h 38 min after its deploy: **1310.4 MB** working
set, **1088.9 MB** of it in the served-bytes cache - 24,305 entries, 24,298 of them previews, loaded by one
television paging through every folder with `WarmPreviewsOnBrowse` on, **12.5 hours after its last
request**. Thumbnails slide out after 60 minutes and films after 10; nothing had left.

**Expired entries were never removed from an idle server.** `MemoryCache` looks for expired entries only
when it is read or written; `ExpirationScanFrequency` rate-limits that look, it does not schedule one.
Section 6h item 9 described the 30 s scan as a sweep that "runs", which was the misreading. `Describe()`
reads `Count` and `Keys`, neither of which triggers the scan, so the dashboard reported the held bytes
faithfully and nothing released them. One request for `contentDirectory.xml` dropped the cache from
24,305 entries to 1. **`ServedFileCache` now runs a timer every minute that removes a key it never
stores**, which is the cheapest call that starts `MemoryCache`'s own scan - and that scan, unlike
`Compact(0)`, allocates nothing: `Compact` sorts every live entry into three lists before removing the
expired ones, about 1.5 MB of large-object garbage a minute at the NAS's 24,305 entries. The scan runs on
the cache's own task, so nothing it could throw reaches the timer, where an unhandled exception would end
the process. The store's clock now comes from the injected `TimeProvider`, so a test can move past an
expiry rather than wait an hour for it.

**The collection an evicted film triggers could never free that film.** The media post-eviction callback
forced its compacting collection from inside the callback, and `MemoryCache` holds the evicted entry - so
its buffer - until the callback returns. Read on the NAS afterwards: **264.9 MB on the large object heap
with 0 MB and 2 entries in the cache**, 733.9 MB working set. *Empty the memory*, which collects outside
any callback, took it to **0.5 MB and 200.2 MB**. The collection now waits a second and then runs once:
by then the callback has returned, and any other film the same sweep evicted is released by that one
collection rather than each paying for its own. It replaces two back-to-back blocking compacting
collections, the first of which ran while the entry was still held and so paid the pause for nothing. The
`SemaphoreSlim` beside the in-progress flag went with it - the flag already admitted one collection at a
time, and the semaphore's `Wait` result was ignored before `Release`, which on a timeout would have thrown
from a task nobody observes. A film evicted while a collection is already running sets a second flag, and the running
collection goes round once more rather than leaving that film for some later, unrelated eviction.

**The sweep also empties a cache that has been switched off.** Only `IsEnabled` releases the payloads when
`FileCache.Enabled` goes false, and only the request path read it - so switching the cache off to reclaim
memory did nothing on a server nobody was using. The sweep reads it first. `ExpirationScanFrequency` is now
derived as half the sweep interval rather than written as its own 30 s, because the sweep only works while
it is shorter: the scan a tick asks for runs only if the last one is older than that frequency.

Both are covered by tests that fail on the old code - verified by putting the collection back inside the
callback and removing the sweep: 2 of 21 `ServedFileCacheTest` cases fail, and all pass with the fix. A
third covers the switched-off cache. The repeated collection has no test of its own: provoking an eviction
inside the one-second window of a running collection is a timing test, and the code is five lines.
The sweep test stores both of its entries before moving the clock, because any write after the clock
has moved lets the cache start its own scan and would make the test pass or fail on thread timing. The eviction test holds only a `WeakReference` to a 4 MB payload and waits for it to die without
forcing a collection of its own.

**The startup log names the build.** `Starting ZEN DLNA Server <informational version> on <runtime>`, on
every start including an in-process restart; until now a deploy could only be dated from the DLL
timestamp.

**Looked into and left alone, with the reason:**

- **The nightly 01:00 slow query** - 452.6 ms on 09-16, 585.9 ms on 09-26, once a night and never
  otherwise. It is `GetPendingProcessingAsync`, the idle poll of `MediaProcessingHostedService`, which walks
  every row by design. Warm, it stays under the 50 ms threshold; the one slow run a night is the first poll
  after something on the NAS around 01:00 pushes `dlna.sqlite` out of the operating system's cache. Rows
  grew 0.2% over the period while the time grew 29%, which fits a cold walk through a 909 MB file whose
  `Files` pages sit ever further apart between stored preview images, not a query that got worse. Half a
  second a night on a background thread nobody waits for; if it ever matters, a partial index matching the
  pending predicate, checked with `EXPLAIN QUERY PLAN` alongside W9.
- **Capping preview warming** - not needed at these numbers. 580 MB is 11% of the NAS's 5120 MB budget;
  thumbnails outrank films in eviction priority, so warming could only push a film out once about nine
  films were live at the same moment. What made it look expensive was the retention fixed above: with the
  sweep, a full crawl costs about 580 MB for an hour and then nothing. Lowering
  `ThumbnailSlidingExpirationInMinutes` shortens that hour without code. Revisit if the budget drops below
  about 1.5 GB. One consequence belongs with the maintainer rather than with this change: films are cached at
  `Low` priority and previews at `Normal`, deliberately, so if warming ever does fill the budget, the film
  being watched is what gives way first - its response is unaffected, but its next range request reads the
  disc.

### Test projects version themselves (maintainer's correction)

Review found the `Tests`-conditioned `<Version>` in `Directory.Build.props` still at `1.1.0923` while
`docs/development.md` said test projects take the product version from it. The maintainer's ruling: they do
not track the product version at all - every assembly carries its own version and is bumped only when that
project is touched. Each test `.csproj` now sets its own `<Version>`: `DlnaServer.IntegrationTests` at
`1.1.0926`, since this batch changed it, and the other two unchanged at `1.1.0923`. `docs/decisions.md`
section 7, `docs/development.md` and `CLAUDE.md` say so.

### Screenshots for the repository (item 9)

Seven pictures of the admin UI now sit in `docs/screenshots/`, and the README shows them under *What it looks like*: Dashboard, Library, a film's page and its subtitle section, *Search files*, Settings, and the phone menu. Nothing in them comes from the NAS. The library is generated:
- painted landscapes (Pillow);
- tones encoded to tagged MP3s, with a lyrics file;
- `testsrc`/`smptehdbars`/`mandelbrot` colour-bar videos, the films with two dubbed audio tracks and two subtitle files each.

The library was served from a throwaway server at `C:\DemoMedia`, a neutral path, since the pages show folders. ffmpeg was downloaded for that run only, on the maintainer's go-ahead, so that videos have previews and the dashboard shows no ffmpeg warning.

**The machine name must never appear in a committed picture or note** (maintainer ruling). *Friendly name* shows it when left blank, so the server ran with `Dlna.Server.FriendlyName` set to `ZEN DLNA Server`. Every page used was checked for the host name before it was captured. The About page names the machine and is deliberately not among the shots.

Shooting found one defect: on a phone, the menu button reaches the screen's right edge, so its keyboard focus outline lost its right side. The outline is now drawn inside the button (`outline-offset: -2px`).

### An architecture picture that matches the code

The maintainer supplied a generated diagram (`diagram.png`, gitdiagram.com, 21.09.2026) to be put on the architecture page if it was right. Most of it was, but three arrows were not:
- The admin UI does not call `/manage`. Its pages inject the repositories directly and use the restart and scan signals; `/manage` is plain HTTP for an operator or a script.
- `LibraryScanner` does not write the index. It only reads the disc, and `LibraryIndexer` writes.
- `EventController` keeps subscriptions in the in-memory `SubscriptionStore`, not in the database.

The picture also predated the subtitles and left out the repositories' own database and the cache-fill service. `docs/architecture.md` therefore gained *How the pieces talk*: a Mermaid flowchart drawn from the code, in the same colours as the original, with the picture rendered from it as `docs/architecture.png`. The Mermaid sits under the picture as its source, so a change to one is a change to both. The original `diagram.png` was then deleted from the repository root and from the solution items, on the maintainer's instruction, since it had been replaced and was wrong. The ready signal is drawn as each background service "waits for" it: pointing the edges the other way pulled the persistence group to the top of the layout.

### Subtitles: linked by name, served, edited, found (items 7-7j)

The batch's largest item, and the first time anything about subtitles goes on the wire: the reference
declared `SubtitleFileExtensions` and marked it "not implemented", and this server indexed the tracks
inside a file without ever advertising them. **None of it is confirmed on a television yet**; that is the
open item it leaves.

**Where a link lives.** A table of its own, `SubtitleFiles` (migration `AddSubtitleFiles`), rather than
rows in `SubtitleStreams`, which every metadata re-read clears - an operator's link or language edit has
to survive re-reading the file. The path is stored relative to the media file's folder with forward
slashes, so a film moved together with its subtitles keeps them. `Source` is `Automatic`, `Manual`, or
`Removed`: the last is an automatic link the operator took away, kept as a marker so the next scan does not
add it back.

**Matching** (`SubtitleMatcher`, pure, in Core). A subtitle belongs to a media file when its name without
the extension is the media's, or the media's followed by a dot and anything. Candidates are filtered by
kind first - `.lrc` only for music, the rest only for video - and the longest media name then wins, which
is rule 7b: `film.1.en.srt` goes to `film.1.mkv` when there is one, and a `film.1.mp3` cannot take it. Two
videos with one name both get the link. The language is read only from what follows the media's name,
right to left past `forced`/`sdh`/`cc`/`hi`/`default`, so `The.Office.US.srt` does not become `us`. A
`.sub` with an `.idx` beside it is VobSub - pictures - and is left alone.

**Found by the scan that was already running.** The scanner reported only media and dropped everything
else before it left the walk; it now reports subtitle-like files too, flagged `IsSubtitle`, from the same
walk, so no second pass wakes the discs. Folder discovery is untouched, so a folder holding only subtitles
does not become a container. The indexer keeps those out of the insert batch - a `Subs` folder has no
directory row and would otherwise log a missing parent for each file - and links them **after
reconciliation**, because a move keeps the old row and deletes the new one, and **under the same gate**,
so an unmounted volume cannot look like every subtitle deleted. The match is tried in the subtitle's own
folder first and then in the folder directly above, never further - the maintainer asked for both one
folder down and for a subtitle that arrives a week after its video to be linked by itself, which the
watcher's rescan does. An automatic link is dropped only when its media no longer matches it or its file is
definitely gone. The indexer test for an unreachable folder passes even without the gate, because the
definitely-gone check alone keeps a link whose file is still there; the gate is what covers a volume whose
files really have vanished from the mount point.

**On the wire**, behind `Compatibility.SendSubtitles`, on by default: an extra `res` per linked file after
the media `res` (so a renderer reading the first `res` still gets the film), Samsung's
`sec:CaptionInfoEx` for one of them - an SRT when there is one - and a `CaptionInfo.sec` header on the
media response when the request carries `getCaptionInfo.sec`. Browse makes one batched lookup per page,
and none on a page where no file has a linked subtitle file. The file is served from `fileserver/subtitle/{id}.{ext}`
on the media port; the extension is there for televisions that judge a URL by its ending. The stored path
is re-checked before serving, because it may have come from what an operator typed. Golden wire files are
unchanged: an item with no subtitles is exactly what it was.

**One defect only running it found**: `XmlSerializer` writes an attribute without its prefix when the
attribute's namespace is the element's own, so the first Browse came out as `<sec:CaptionInfoEx
type="srt">`. `Form = XmlSchemaForm.Qualified` makes it `sec:type`, and `DidlSerializerTest` pins the
exact string.

**The file's page** has a *Subtitle files* group for video and music: each link with its language
(editable), whether it was found by name or added by hand, and *Remove*; and an add row with *Check* and
*Add*. A path is accepted in the file's folder or one folder below, never climbing out, never into a folder
the library hides - a temporarily hidden one only while it is shown. Adding one replaces the automatic
links in one transaction, and its language is guessed from the name the same way. Hand-made links are
operator data that *Rebuild index* discards with the files; the Maintenance page says so.

**Has subtitles** (7i, 7j) means a track inside the file or a live linked file. It is a `required` member of
`MediaFileDto`, so none of the three hand-kept projections could leave it silently false - the compiler
named exactly those three the first time it built. *Search files* has a *Subtitles* filter, and the Library
tiles carry a second badge under the kind badge when it is true.

Verified on a throwaway server over a library built for the rules: four links from the first scan
(same folder, one below, the 7b pair, lyrics) and none for the VobSub pair; the DIDL above read back from a
real Browse; the subtitle served as `text/srt`; the header answered; an unknown id and the admin port both
404; in the browser the badges, both sides of the filter, every *Check* refusal, a manual add replacing the
automatic links, a language edit, and an automatic link removed and still removed after a rescan.

**What the review changed before it was committed.** The `/review-all --full` pass on this diff raised
these, all applied:
- **A subtitle swapped for a symlink was served.** `SubtitlePath` is lexical, and both the page's check
  and the file server then followed links on disc: an automatic `film.en.srt` replaced by a link to the
  database stayed linked - the scanner skips links, so the next scan saw the name vanish while
  `IsDefinitelyAbsent` still found the link entry - and was served on the media port without a password.
  `SubtitlePath.ExistsWithoutLinks` now refuses a link as the file and as the one sub-folder, and both
  callers use it. Covered by `SubtitleFileCheckerTest`, which creates real links.
- **`CaptionInfo.sec` was built from the `Host` header**, which the caller chooses, while the DIDL used the
  advertised address. `DidlMapper.ResolveEndpoint` is now the one resolution both use.
- **Windows spellings.** A segment ending in a dot or a space, or containing `~`, is refused: Windows drops
  the first two and resolves an 8.3 name, so each could reach a hidden folder under a name the hidden-folder
  test does not read.
- **At most 8 subtitle resources per item**, so a folder of language variants does not lengthen every
  Browse of it. `CaptionInfoEx` and the header still choose from all of them.
- **`HasSubtitleFiles`** is a second `required` flag beside `HasSubtitles`, linked files only. Browse and
  the header query the links on it: `HasSubtitles` includes embedded tracks, so on an MKV library almost
  every page had queried for nothing. The badge and the search filter keep `HasSubtitles`.
- Smaller: the matcher reads each media file's name stem once per folder rather than once per subtitle
  and media pair; the scanner no longer stats a subtitle file, since linking needs only its name - so an
  empty subtitle file is now linked like any other; the sync looks names up in a set; the "SRT first"
  choice is one `DidlMapper.PreferredCaption` for both callers; and the page's language guess is
  `SubtitleMatcher.GuessLanguage(subtitle, media)`, on the same `BelongsTo` rule the scan uses.
- The Browse-time lookup, `GetForFilesAsync`, now resolves the files' ids first and filters on
  `MediaFileId`, so its plan cannot depend on join order.

**The second full review, before the commit.** A second `/review-all --full` over the fixed diff, with all
17 agents and all 5 skills, found no blocker. Its findings were in the rules for which links a scan keeps,
drops and brings back. Every item below except the last is covered by `SubtitleRepositoryTest`, which
exercises the real repository:
- **A hand-picked link and an automatic one could end up side by side for good.** The sync kept every
  automatic link that still matched before it asked whether its media had a manual one. A scan that read the
  table just before the operator pressed *Add* therefore re-inserted the automatic link, and no later scan
  took it away. The manual test now comes first.
- **Removing a hand-added link could undo itself.** *Remove* deleted a manual row outright. If that file also
  matched by name, the next scan linked it straight back as automatic. *Remove* now leaves the same
  `Removed` marker for both kinds, and the marker goes once its file does. Removing the last hand-picked link
  still lets the automatic ones come back, which is the point of having removed it.
- **A folder excluded after its subtitles were linked kept them for good**, and still offered and served
  them. The scan never walks an excluded folder, so its files are never seen. Their links were therefore
  kept, and probed on disc on every pass, waking that disc to change nothing. The sync now takes the scan's
  `ExcludeFolders` and drops an automatic link into one without probing. A `Removed` marker there stays,
  for when the folder is shown again.
- **A first fill wrote every link in one save**, where the indexer writes in 500s. It now inserts in 500s.
- **A language set on a linked file could not be searched for.** The *Search files* subtitle-language filter
  and its dropdown read only the tracks inside files. Both now include live linked files.
- **`.smi` went out as SMIL** (`application/smil`), an unrelated format. It is SAMI, now `application/x-sami`;
  whether a television accepts that is part of the open television check.
- **The file's page:** a path the filesystem refuses, such as one containing a NUL, used to throw in *Check*
  and end the operator's session. It is now a refusal. A failed write no longer clears what was typed or
  announces a change, and an *Add* for a file that has left the library says so.
- Smaller:
  - `MediaFileDto.HasSubtitles` is computed from a `required` `HasSubtitleTracks` and `HasSubtitleFiles`,
    so the two can no longer disagree, and each projection makes one EXISTS where it made two. The tracks
    half keeps `Count != 0` because CA1860 flags `Any()` there.
  - `SubtitlePath.TryLocate` is the one lexical-then-disc check for both callers.
  - `.Cut`, `.DC`, `.HD` and `.UHD` no longer read as languages.
  - `.ttml` stays unlinkable, now with the reason in the code: it would be served as XML, which a browser
    renders.
  - Stale `using`s in the touched files are gone, and `CaptionInfo.sec` has its own wire-doc.
  - `DlnaServer.UnitTests` moved to `1.1.0926` with this work, after the paragraph above recorded it
    unchanged.
- **The maintainer's rulings on what the review left open:**
  - **A hand-added link no longer outlives its file.** A manual link used to be kept whatever became of its file, so a film moved without its subtitle, or a subtitle deleted by hand, left a link that 404s. A scan now drops a manual link whose file is definitely gone, or which points into an excluded folder, which such a link may never do. Until that scan runs, the file's page marks the link with the reason it cannot be served, for hand-added links only. The film's automatic links then come back one scan later, not in the same pass.
  - **The check-then-serve gap is accepted**, and recorded in `docs/decisions.md` 7b.
  - **No shared expression** for the "not removed" predicate: the hand-kept projections stay as they are.
  - The whole-table read in the sync is recorded as W10 in `docs/decisions.md` 7b.
