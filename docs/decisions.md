# DlnaServer - decisions, conventions and traps

The half of the old `PLAN.md` with a long life: why the server is built the way it is, the rules that
apply while changing it, and the things that have cost time. Read this before working.

Its sibling is [history.md](history.md) - status, milestones and the batch-by-batch record of what
changed when. Split out of `PLAN.md` on 2026-09-23, which had reached 5,312 lines and stopped being a
plan long before that; every milestone in it reads DONE.

**Section numbers are unchanged from `PLAN.md`** on purpose. A reference to "section 7b" in a source
comment still means section 7b - only the file it lives in has changed.

| Need | Section |
| --- | --- |
| Why something was decided | 2, and the standing decisions in 7b |
| The memory budget, and how to measure against it | 3, 3b |
| How logging compares to the reference | 3c |
| The conventions and the tooling traps | 7 |
| Open work, and findings deliberately not applied | 7b |
| Picking the work up from cold | 8 |

---

## 2. Decisions already taken

| Decision | Choice |
| --- | --- |
| Approach | Redesign; Reference is a behavioural spec, not a code template |
| Target framework | `net8.0` (QNAP has the .NET 8.0 runtime installed — confirmed) |
| Structure | Layered `src/` projects + 3 test projects |
| Admin UI | Blazor Server, in-process, on its own Kestrel port |
| Wire compatibility | Byte-identical, golden-file tested |
| Config | `config.json`, `IOptionsMonitor` hot-reload, clean restart path |
| Test stack | NUnit + FluentAssertions |
| Packages | Reference versions, minus the unused ones |
| DLNA response headers | Sent, behind `Compatibility.SendDlnaResponseHeaders` |
| SSDP M-SEARCH | Fixed, with `Compatibility.UseLegacyInvertedSearchTargetMatch` to restore old behaviour |
| Browse | `BrowseFlag`, `Filter`, `SortCriteria`, `RequestedCount=0` all honoured |
| GENA / MediaReceiverRegistrar | Cheap correctness fixes only; no GENA event push this round |
| Entity/DTO boundary | Entities `internal` to Persistence — compiler-enforced. DTOs cross the boundary |
| Keys | Every entity has `int Id` (internal PK/FK) + `Guid PublicId` (external identifier) |
| Mapping | LINQ projection in the query — no mapper library, no entity materialised on reads |
| DTO shape | `sealed record` with `init` properties |
| Architecture tests | NetArchTest.Rules 1.3.2; layering + entity/DTO boundary |
| Database corruption | Missing → create. Corrupt → move aside, rebuild empty. Wrong machine → warn only |

---

## 3. Memory budget — the hard constraint

The server must run in **minimal RAM** while still answering renderers quickly.

### The target machine has 2-4 GB, and the test NAS does not

**Production boxes have between 2 and 4 GB of RAM in total.** The development NAS reports
`totalAvailableMemoryMb` of **39,907** - roughly 40 GB - so it is not a proxy for a customer machine in
the one dimension this whole section is about. Recorded 2026-09-02, after the first like-for-like
comparison against the reference.

> **SUPERSEDED 2026-09-02 by section 3b's fifth reading - read that before acting on the two bullets
> below.** They record a decision taken while the evidence said the cache was merely *expensive*. The
> evidence now says it is *counter-productive*: at 906 MB it held three films and had evicted every
> thumbnail, so it was spending a gigabyte to avoid platter reads for content nobody re-requests while
> forcing them for content that is re-requested constantly. The budget numbers below are kept for the
> reasoning that produced them, not as current policy - the plan of record is to cache thumbnails and
> static assets only.

Two consequences. **Both were resolved as deliberate decisions rather than as defects** - see the cache
rework in section 8, item 3 - and they are recorded here because the reasoning has to survive:

- **The total budget is 1024 MB, clamped to `TotalAvailableMemory / 2`.** On a 2 GB machine that resolves
  to 1 GB, so the configured value and the clamp agree there. This is a generous share of a small box,
  taken knowingly: the acoustic goal is what the memory buys.
- **`MaxFileSizeInMegabytes` stays 512 MB**, clamped additionally to half the budget. One cached film is
  therefore still a single 512 MB `byte[]` on the large object heap, which is the allocation shape behind
  the 5073 MB reading. What changed is that the budget bounds how many can be held at once, that an
  oversized file is refused before it is read, and that concurrent requests for one path no longer each
  allocate their own copy.

**The GC behaves differently here than it will in production, and that invalidates measurements taken at
face value.** With 40 GB visible the runtime sizes its allocation budgets generously and feels no pressure
to collect or decommit - which is the mechanism behind both the 5 GB reading and the 80%-fragmented large
object heap seen at 16 minutes uptime. **Validate any memory change under an emulated limit**
(`DOTNET_GCHeapHardLimit` / `DOTNET_GCHeapHardLimitPercent`) so this NAS behaves like the smallest
supported machine. A number measured without that is not evidence.

The acoustic goal in M6 survives this. The live log shows what is actually re-served is **thumbnails** -
small, hot, and worth caching. Whole-film buffering is what generates the LOH churn, and dropping it costs
one extra platter read per film, not the silence the cache exists to buy.

### Measured baseline (Reference, live, 11 days uptime, ~20,000 files)

Source: `http://192.168.1.100:26851/manage/memory`

| Metric | Value |
| --- | --- |
| Working set | 394.9 MB |
| Private memory | 466.2 MB |
| **Managed heap** | **42.5 MB** |
| Total committed (GC) | 52.6 MB |
| Gen0 / Gen1 / Gen2 collections | 6511 / 1764 / **670** |
| Threads | 26 |
| Server GC | true, concurrent GC false |

### What this tells us

**The managed heap is not the problem.** 42.5 MB managed against a 395 MB working set means roughly
**350 MB is native or fragmentation**, not C# objects. Optimising LINQ allocations would barely move it.
The real drivers are:

1. **SkiaSharp native allocations** — thumbnail decode/encode buffers held by the native allocator.
   The Reference documents needing `MALLOC_TRIM_THRESHOLD_` on Linux or RSS grows without bound.
2. **SQLite** — page cache, WAL, memory-mapped region.
3. **Allocator fragmentation** — long-running process, many short-lived large buffers.
4. **670 Gen2 collections** — the Reference fires a forced, blocking, compacting
   `GC.Collect(MaxGeneration, Forced, blocking: true)` on *every* cache eviction, via an unbounded
   `Task.Run` chain. That is pure cost: it does not reduce RSS meaningfully and it stalls responses.

### Evidence from production logs

Source: `T:\repos\DLNAServer\DLNAServer\bin\NAS\publish\logs` (the live NAS server, read-only).

**Error volume is dominated by one bug.** Across the retained error logs:

| Count | Error |
| --- | --- |
| 1390 | `SQLite Error 19: UNIQUE constraint failed: DirectoryEntities.LC_DirectoryFullPath` |
| 556 | logged by `DlnaDbContext` (the same failures, logged again) |
| 278 | `FileWatcherManager` operation failures |
| 278 | `Microsoft.EntityFrameworkCore.Update` |
| 96 | `ImageProcessor` |
| 26 | `FFmpegService` |
| 2 | `SSDPNotifierService` - **each of these restarts the whole process** |

The failing statement is always the same insert, with `@p3='/'` and `@p4='/'`: the server repeatedly tries
to insert a directory row for the **filesystem root**. `GetNewDirectoryEntities` walks `DirectoryInfo.Parent`
upwards until it finds a known directory or hits the root, so every watcher event re-attempts `/`, which
already exists, and the unique index rejects it. It retried roughly every 100 ms and produced a 6.4 MB
error log in a single day.

**Consequences for the rewrite:**
- **Never walk parent directories above a configured source root.** A source folder is the top of the tree;
  there is nothing above it worth indexing.
- **Resolve directories against the index before inserting**, and de-duplicate within a batch.
  `IMediaDirectoryRepository.GetExistingPathsAsync` exists for this.
- Directory creation must be idempotent - a repeated scan or watcher event must be a no-op, not an error.

**SQLite is not the bottleneck.** The slow-query log records only ~106 entries in a month, and the single
slowest was **101.85 ms** - the Browse query. Its text selects roughly **70 columns across 6 tables**,
including every duplicated `LC_*` column, because the repository `Include`s every navigation by default.
DTO projection removes most of that read by construction.

**Browse telemetry does exist** - `ServerShowDurationDetailsBrowseRequest` is on in production, and every
Browse logs a total plus a per-phase breakdown. Measured over 1875 requests:

| Percentile | Total Browse duration |
| --- | --- |
| p50 | 16.8 ms |
| p90 | 123.3 ms |
| p99 | 218.6 ms |
| max | 2158.4 ms |

For the slow requests (>100 ms, n=280) the phase breakdown says where the time goes:

| Phase | avg ms | share |
| --- | --- | --- |
| **Get data from database** | **131.9** | 78% |
| Fill empty data (inline metadata/thumbnails) | 23.2 | 14% |
| Refresh found files from directory | 10.6 | 6% |
| Get directory by ObjectID | 1.0 | <1% |
| Sort / filter / check | <1 each | negligible |

Two conclusions. The database read dominates, and it is the query that `Include`s every navigation and
selects ~70 columns across 6 tables - which DTO projection removes by construction. And "Fill empty data"
is the inline metadata and thumbnail generation, confirming that moving it off the Browse path (M4) is
worth real milliseconds even on its average case.

**Targets to beat:** p50 under 16.8 ms and p90 under 123 ms, measured the same way. Reproduce the same
per-phase log line in M5 so the comparison is like for like.

### Targets

**The <200 MB figure below was not achievable and has been restated.** Section 3b's own M5-M7 readings
measured **350.6 MB with the byte cache empty** - 1.75x the old target, and within 11% of the 394.9 MB
baseline this rewrite exists to beat - so restricting what the cache stores could never close the gap,
because a cache holding nothing is already over. What actually held the memory was native and structural,
not a managed cache: ~96 MB of SQLite pragmas (`mmap_size` 64 MB + `cache_size` 32 MiB), an unbounded
`temp_store=MEMORY`, a 1 GB `FileCache` authorisation, and Server GC with no heap limit. All four are now
changed (config.json and `runtimeconfig.template.json`), which is what makes the restated figures below
reachable rather than aspirational.

| Metric | Target | Notes |
| --- | --- | --- |
| Working set, 20k files, idle | **< 250 MB** | vs 395 MB today; 350.6 MB was measured before the native floor was cut |
| Working set, 20k files, under playback | **< 400 MB** | the byte cache is now capped at 256 MB, not 1024 |
| Managed heap | < 60 MB | already close |
| Gen2 collections per day | < 20 | vs ~60/day today |
| Browse response (cached folder) | < 200 ms | renderers time out |
| First byte on a media stream | < 300 ms | |

### Rules that follow from this

- **Never force a GC in normal operation.** No automatic `GC.Collect`, no eviction-triggered collection.
  **One deliberate exception:** `/manage/filecache/clear`, where an operator has explicitly asked for the
  memory back and cached payloads above 85 KB sit on the large object heap. That exception is revised into
  this rule later in this section - it is stated here so a session reading only section 3 does not delete
  it as a violation.
- **Bound every cache by real bytes.** The reference gave entity caches `Size = 1` regardless of payload,
  so the byte limit was meaningless. Cache file bytes only, sized by actual length.
- **Do not shrink the byte cache quietly to make a number look better** - but on a 2-4 GB machine its
  *shape* is wrong, not just its size, and that is a separate matter. The cache exists to stop mechanical
  drives spinning up a second time for a file already served, and those drives are audible in the room
  (see the serving rule under M6). That goal is real and stays. What does not survive the 2-4 GB
  constraint is holding **whole films** to achieve it: a 512 MB `byte[]` is a quarter of the smallest
  supported machine, and it is the large-object churn behind both the 5 GB reading and an 80%-fragmented
  LOH. The live log shows the payloads actually re-served are thumbnails, which are small and hot and
  worth every byte. **So: keep caching, cap the per-file size so payloads stay off the LOH, and derive the
  total budget from available memory.** Tuning `FileCache.MaxTotalSizeInMegabytes` remains a conscious
  acoustic decision - it is simply no longer allowed to be a constant larger than half the machine.
- **No entity-level query caching.** Already decided; the boundary makes it easy to keep.
- **Stream, do not buffer.** Media responses stream from disk with range support (`PhysicalFile`, so the
  kernel's `sendfile` does the work and no managed buffer is involved). The **one** exception is the
  served-bytes cache described in the bullet above, which reads a whole file deliberately and only within
  its byte budget and per-file ceiling - never as part of serving a request that missed the cache.
- **One thumbnail at a time**, disposed promptly, with bounded concurrency. Skia buffers are native.
- **`ArrayPool<byte>`** for transient buffers above ~1 KB; `stackalloc` below.
- **Workstation GC - DECIDED, applied and verified in the artifact 2026-09-03; not yet measured on the
  NAS.** The Reference uses Server GC, which reserves a heap and a GC thread per core; the NAS reports 4.
  That is the mechanism behind the reading where the GC had essentially stopped running (6 gen0
  collections in 42 minutes) - no single heap ever filled.
  **The setting lives in `src/DlnaServer.Host/DlnaServer.Host.csproj`, and it must.** `Directory.Build.props`
  had carried a `ServerGarbageCollection` PropertyGroup for a long time and **it had never once applied**:
  `Microsoft.Common.props` imports that file before the Web SDK defines `OutputType`, so its
  `Condition="'$(OutputType)' == 'Exe'"` tested an empty string. It looked correct because
  `Microsoft.NET.Sdk.Web` defaults `ServerGarbageCollection` to `true` anyway, so the dead setting agreed
  with reality; `ConcurrentGarbageCollection` evaluated to **empty**. `runtimeconfig.template.json` alone
  does not work either - the SDK writes `System.GC.Server` from the MSBuild property over the template's
  value. See the MSBuild-condition trap in section 7b; this is the third instance of that class here.
  **Verify by reading the artifact, never the source:**
  `bin/Debug/net8.0/DlnaServer.Host.runtimeconfig.json` now says `"System.GC.Server": false`, and a
  running server reports `isServerGc: False`. Delete the generated file before rebuilding - an
  incremental build does not always regenerate it.
  **Local effect on a 20-core dev box, same workload:** managed heap **29.2 -> 10.0 MB**, working set
  **153 -> 130 MB**, GC committed **15.3 MB**. Not an M9 datapoint - wrong machine, no heap limit - but
  it confirms the mechanism. `System.GC.ConserveMemory` stays at 5.
  **`isConcurrentGc: False` was a misreading, not a second bug.** `GCMemoryInfo.Concurrent` reports
  whether the *last* collection was a background one, and gen0/gen1 never are, so it reads `False` on a
  healthy server regardless of configuration. `ManageController` now says so at the field.
- **`MALLOC_ARENA_MAX` was a no-op and is now 2.** glibc's own default is 8 x the core count, so the
  earlier `=32` asked for exactly what glibc would have done unasked - each arena retaining up to 64 MB
  of freed-but-untrimmed heap. `MALLOC_TRIM_THRESHOLD_` / `MALLOC_MMAP_THRESHOLD_` are unchanged.
  **And the exports now actually reach a hand-started server:** they lived in `NasBuild.sh`'s own shell
  and reached the app only through its `run` branch, so an init-script start had different memory
  behaviour, silently. `NasBuild.sh` emits `PUBLISH_DIR/run.sh` and prints that as the manual command.
  Review finding 33.
- **`/manage/memory` must exist** and report the same statistics as the Reference, so before/after is
  directly comparable. This is a required deliverable, not a nice-to-have.

---

## 3b. Memory measurement protocol (run after every milestone)

**After each milestone - or each substantial part of one - measure and append a row.** A single reading
says nothing; the trend is what shows whether the budget holds as features land.

```powershell
./tools/measure-memory.ps1 -Label "M6 (streaming)"
```

The script builds, starts the server, waits for it to settle, reads `/manage/memory`, prints a Markdown
row and stops the server. Paste the row into the table below.

**Emulate the target machine, or the reading is inadmissible.** The test NAS reports ~40 GB, production
has 2-4 GB, and the GC sizes its budgets against what it is told it has - so a working set measured here
says nothing about a customer box. Constrain the runtime for the run:

```bash
DOTNET_GCHeapHardLimit=0x30000000 ./DlnaServerMew          # 768 MB, emulates a 2 GB machine's share
DOTNET_GCHeapHardLimitPercent=20 ./DlnaServerMew           # or a share of whatever the box reports
```

Note this is a **measurement** tool, not a deployment setting. On a real 2-4 GB machine the runtime already
reads the machine's memory and sizes itself accordingly; it is the 40 GB test box that is the anomaly. So
GC settings are configured in `src/DlnaServer.Host/DlnaServer.Host.csproj` (server GC, concurrent GC - **not** in `Directory.Build.props`, where an `OutputType` condition made them silently inert; see the MSBuild-condition trap in section 7b) and in `src/DlnaServer.Host/runtimeconfig.template.json` (`ConserveMemory` - **and nothing else**) - the template exists because an SDK property set in the props file can be silently dropped, which is what happened to `GarbageCollectionAdaptationMode` - the fix for the 5 GB reading was the cache asking for
less, not the GC being told to allow less. Revisit only if a constrained measurement says otherwise.

**`HeapHardLimitPercent` is set nowhere in the deployment, and that is correct.** This file claimed twice
that the template carried it permanently. It does not: the template holds `ConserveMemory` alone, the
deployed `publishNAS/DlnaServer.Host.runtimeconfig.json` has no such property, and `NasBuild.sh` exports
no `DOTNET_GCHeapHardLimit*`. The only place it is ever set is `tools/measure-memory.ps1`, as the
environment variable of a single measurement run - which is exactly what the paragraph above prescribes.
The **document** was wrong, not the configuration. Corrected 2026-09-08, and it is the same trap the
round-1 list records twice: a prescribed file location is a claim until an artifact is read.

**Conditions must match or the numbers are not comparable.** Fix these three, and state them in Notes:
1. **The same library.** An empty folder and a 20,000-file library are different programs.
2. **The same settle time.** 60 s minimum; startup indexing skews anything shorter.
3. **The same configuration.** Release runs lower than Debug. The NAS runs Release.

Working set is the figure that matters. Managed heap alone misleads: the reference shows 42 MB managed
against a 395 MB working set, so most of its footprint is native (SkiaSharp, SQLite, allocator
fragmentation) and never appears in GC statistics.

| After | Uptime | Working set | Private | Managed heap | Gen0/1/2 | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| M5 (discovery + Browse) | 302 s | 157.3 MB | 97.6 MB | 29.8 MB | 0 / 0 / 0 | Debug, Server GC, 40 threads. Library: 3 images, thumbnails generated. |
| M5 + logging parity | 65 s | 135.5 MB | 85.2 MB | 23.2 MB | 0 / 0 / 0 | Debug, Server GC, 36 threads. Library: **empty**. |
| M6 (delivery) | 60 s | 126.0 MB | 79.1 MB | 17.3 MB | 0 / 0 / 0 | Debug, Server GC, 38 threads. Library: 4 files (2 images, 2 videos), thumbnails generated. Shipped cache defaults (1240 / 512 MB), but **no media was served during the settle, so the byte cache is empty** - this row measures the server, not the cache. |
| M7 (SOAP + GENA) | 60 s | 132.6 MB | 84.1 MB | 19.6 MB | 0 / 0 / 0 | Debug, Server GC, 39 threads. Same library and empty cache as the M6 row. The +6.6 MB covers three more SoapCore endpoints and their contract serialisers, which are built once at startup. |
| **NAS, real library** | 1013 s | **391.1 MB** | 452.9 MB | **179.2 MB** | 13 / 10 / 9 | Release, Server GC, 27 threads, linux-x64 on the QNAP. First measurement against the real library and real renderers (VLC browsing, an LG television discovering). Read live from `http://192.168.1.100:26852/manage/memory`. **This is the number that matters and it misses both targets** - see the analysis below. |
| **NAS, real library, after playback** | 2530 s | **5073.0 MB** | 5174.2 MB | **1128.2 MB** | 6 / 5 / 4 | Release, Server GC, 25 threads. Same box, index now 25,501 files in 1,070 directories. GC total committed **4901.8 MB**. Read again 316 s later: working set flat at 5073.5 MB, managed heap up to 1182.6 MB, **collection counts unchanged at 6/5/4**. See the analysis below - this is 13x the row above and the mechanism is understood. |
| **NAS, post-fix, cold reprocess** | 200 s | **705.6 MB** | 777.7 MB | **30.7 MB** | 15 / 10 / 7 | Release, Server GC, 30 threads. First reading of the 2026-09-02 build. GC committed **486 MB**; **LOH 1.97 MB with 1.38 MB fragmentation** - essentially empty. `processorCount` **4**, `totalAvailableMemory` **39,907 MB**. Workload is NOT comparable to the row above: the database was absent at startup so this is a cold re-index of 25,501 files with metadata and thumbnail generation running flat out, an **empty byte cache and no playback**. Managed heap is under target for the first time; working set still 3.5x over it. |
| **NAS, post-fix, 16 min** | 951 s | **350.6 MB** | 402.4 MB | 75.3 MB | 75 / 15 / 9 | Same instance as the row above, still re-processing. Working set **fell from 705.6 to 350.6 MB** as the GC caught up, so the earlier figure was cold-start churn rather than a steady state. GC committed 121.5 MB. **LOH 7.97 MB of which 6.38 MB is fragmentation** - only ~1.6 MB live, the miniature form of the 5 GB mechanism. Byte cache still empty, no playback. |
| **NAS, 42 min, after streaming** | 2501 s | **2990.9 MB** | 3077.3 MB | **1116.1 MB** | 58 / 13 / 3 | Release, Server GC, 27 threads. Build of 08:46 UTC - **predates the cache rework**. Media was streamed, so this is the workload that produced 5073 MB. **LOH 973.9 MB with only 0.13 MB fragmentation**: the memory is live cache content, not waste - see the analysis below. Reference on the same box at the same moment: 370.4 MB working set, 47.3 MB managed. |
| **NAS, post-review build, cold cache** | 353 s | **380.0 MB** | 426.1 MB | **52.0 MB** | 55 / 50 / 5 | Release, Server GC, 27 threads. First reading of the post-review build (2026-09-03 14:07), taken live while the metadata/thumbnail pass ran over 24,682 files. GC committed **87.3 MB** for a 53 MB heap - the lowest ratio recorded here. Working set **fell from 572 MB at 141 s to 380 MB at 353 s**, so it is settling rather than climbing. **Managed heap is at target for the second time and stayed there under load.** `isConcurrentGc` reads **False** despite the runtimeconfig setting - worth resolving with finding 34. **Still inadmissible by this section's own rule:** `totalAvailableMemory` **9,977 MB**, so `HeapHardLimitPercent` resolved to 25% of a ~40 GB box and never binds, and the byte cache held 32 entries with **no media files** - icons and photos only, no playback. The remaining ~330 MB is native, which is what findings 33 (`MALLOC_ARENA_MAX` is a no-op), 34 (Server GC) and 35 (SQLite page cache per pooled connection) target. |
| **NAS, 12.9 h, byte cache all but empty** | 46530 s | **1171.6 MB** | 1158.7 MB | **892.6 MB** | 1932 / 265 / 42 | Release, **Workstation GC** - `isServerGc: False` read live, so that decision did reach the NAS - 20 threads, the 2026-09-03 23:11 build, read 2026-09-04 after 12.9 h of uptime and a television session. **LOH 899.3 MB with only 28.4 MB fragmented, while `/manage/filecache` reports 0.96 MB held in 59 entries, all icons and SCPD.** That looked like ~871 MB of *live* large objects - **and it was not: a forced collect an hour later freed 883 MB.** The LOH figures in this row are a STALE snapshot (`gcGeneration: 1` - see the sixth reading below). Gen2 42 in 12.9 h is ~**78/day**, worse than the reference's ~60 and 4x the target. Still inadmissible as a target reading: `totalAvailableMemory` 9977 MB and no heap limit. |
| **NAS, same instance, right after `Clear and collect`** | 46856 s | **288.1 MB** | 243.8 MB | **19.4 MB** | 1943 / 273 / 50 | Same process, 326 s after the row above, following one operator press of `/admin/cache` -> **Clear and collect**. Working set **1171.6 -> 288.1 MB**, managed heap **892.6 -> 19.4 MB**, **LOH 899.3 -> 0.80 MB with 0.00 fragmentation**, `gcGeneration` now **2**. The cache held 0.96 MB before the clear, so **the clear freed under 1 MB and the collection freed the other 883**. First NAS reading inside the <400 MB playback target, and **below the reference's 370-403 MB on the same box**. |

| **Dev box, heap-limited — FIRST ADMISSIBLE ROW** | 90 s | **138.7 MB** | 44.3 MB | 10.0 MB | 2 / 1 / 0 | Release, Workstation GC, 18 threads, **heap limit 25%, byte cache pinned to 256 MB** via `tools/measure-memory.ps1 -HeapLimitPercent 25 -PinFileCacheMegabytes 256`. Taken 2026-09-06 with Phases 1-4 in the tree. **The first row in this table that satisfies this section's own admissibility rule** - every earlier one was taken with no heap limit. What it does *not* settle is the NAS: the local library is a 233 KB throwaway database against the NAS's 656 MB, so this measures the server's floor, not its behaviour under a real library. |
| **NAS, live, before the SQLite fix** | 97390 s | **563.4 MB** | 533.5 MB | 13.9 MB | 2316 / 579 / 41 | Read live 2026-09-06 from the 2026-09-05 build after 27 h. **GC committed only 44.0 MB, so ~518 MB is native** - against the reference on the same box at the same moment: 249.0 MB working set, 28.5 MB committed, i.e. **this server at 2.3x the one it replaces**. Cause found and it is not code: the deployed `config.json` carried `mmap 128 MB` and `cache_size 32 MB` **per pooled connection** (the tree ships 0 and 2) The byte cache was also at 5120 MB, which was **deliberate and remains so** - see the note below; only the two SQLite values were stale. `NasBuild.sh:83-98` preserves `config.json` across a redeploy, so **every memory fix shipped as a config change had never once run on the NAS**. Settings corrected live the same day; the process was not restarted, so this row is the *before*. |

| **NAS, deployed and restarted — THE AFTER-ROW** | 255 s | **177.7 MB** | 203.5 MB | 10.7 MB | 57 / 27 / 22 | Release, Workstation GC, 19 threads, read 2026-09-06 minutes after the deploy that finally carried the tuned `config.json`. GC committed **17.95 MB**, **LOH 0.49 MB with no fragmentation**. Against the before-row on the same box: **563.4 -> 177.7 MB**, and against the reference read at the same moment (**248.1 MB** working set, 27.1 MB committed) this server is now **below the one it replaces** rather than 2.3x it. The mechanism is confirmed: nothing changed but `mmap_size` 128 -> 0 and `cache_size` 32 -> 2 per pooled connection. **Caveat, and it matters: 255 s of uptime is a cold reading and the before-row was at 27 h.** A steady-state comparison still needs a day. |
| **NAS, live, mid metadata pass** | 1891 s | **282.9 MB** | 287.5 MB | 25.4 MB | 21219 / 1527 / 125 | Release, Workstation GC, 24 threads, read 2026-09-08 straight off `/manage/memory` without disturbing the process. GC committed **41.0 MB** for a 25.4 MB heap; gen2 at 26.1 MB with 5.0 MB fragmentation. **A loaded reading, not steady state**: a metadata and preview pass was running over 25,532 files throughout, which is ffprobe, SkiaSharp and a write per file - so this is closer to a ceiling under work than to a resting figure. Live configuration at the time: byte cache 5120 / 512 and `mmap 64` / `cache 8`, both by choice (standing decisions 11 and 14), which is *not* the tuning the 177.7 MB row was taken under. Recorded because 31 minutes of uptime answers part of what the after-row's 255 s could not. **Still owed: one reading with the library quiet.** |
| **NAS, after the 2026-09-08 redeploy** | 2982 s | **1205.9 MB** | n/a | 971.6 MB | 22508 / 1650 / 72 | Release, 19 threads, read after the maintainer redeployed at 12:34 local. **The number is the byte cache and nothing else:** `/manage/filecache` reported **950.24 MB held** at the same moment (6,551 entries, 3 of them films, budget 5120 / 512 by choice). 950 of the 971 MB managed heap is cached payload, so this row measures the operator's cache decision, not the server - exactly the confusion the `/manage/filecache` path listing exists to settle, and the reason working set alone is not a verdict. **Not comparable with the rows above**, which were taken with a near-empty cache. |

> **The byte-cache budget on this NAS is 5120 MB / 512 MB BY CHOICE. It is not drift, and it must not
> be "corrected".** The maintainer set it deliberately and restated it on 2026-09-06: they want those numbers in
> `config.json`, and 256 / 32 only as the *code default*. This was misread as stale configuration twice
> in one day and reverted twice; both reverts were wrong and both were undone. The box has ~40 GB and
> reports no cgroup limit, so a multi-gigabyte film cache is a reasonable trade there.
>
> **Consequence for this section's targets:** the <400 MB working-set target describes the server, not
> the cache an operator has authorised on top of it. On this machine a streaming session legitimately
> reaches several GB of working set, almost all of it live byte-cache content. Judge the server by
> `workingSet - byte cache held`, or by a reading taken with the cache empty.
>
> **What WAS genuine stale config, and is fixed:** `mmap_size` 128 -> 0 and `cache_size` 32 -> 2. Those
> are charged *per pooled connection* and were never chosen by anyone.

> **`PrivateMemorySize64` is not physical memory on Linux, and three rows above read it as though it
> were.** .NET populates it from `VmData` in `/proc/pid/status` - the size of private *writable virtual*
> mappings, most of which is never faulted in. Only `WorkingSet64` (`VmRSS`) is resident. Treating the
> 533.5 MB private figure as corroboration of the 563.4 MB working set overstated the gap to explain by
> roughly 200 MB. **Compare working set against working set and nothing else.**

### Measured against the reference, same machine, same moment

Taken 2026-09-02 10:00 local, the reference on 26851 and this server on 26852 on the one NAS:

| Metric | Reference (12 days uptime, idle) | This server (16 min, processing) | Target |
| --- | --- | --- | --- |
| **Working set** | 402.9 MB | **350.6 MB** | < 200 MB |
| Private memory | 518.8 MB | 402.4 MB | — |
| Managed heap | 55.9 MB | 75.3 MB | < 60 MB |
| GC total committed | 83.1 MB | 121.5 MB | — |
| Gen0 / Gen1 / Gen2 | 7163 / 1995 / 727 | 75 / 15 / 9 | < 20 Gen2/day |
| LOH size (fragmented) | 9.03 MB (4.34) | 7.97 MB (6.38) | — |
| Threads | 28 | 28 | — |

**This server is now below the reference on working set and private memory** - while re-indexing 25,501
files and generating thumbnails, against a reference idle for twelve days. That is the first reading where
the rewrite beats the thing it replaces on the metric that matters.

Two cautions against over-reading it. The byte cache is empty and nothing has been streamed, so the
workload that produced 5073 MB has not been repeated. And the comparison is a snapshot: the reference's
727 Gen2 collections over 12 days is roughly **60 per day**, so the reference itself misses the Gen2
target by 3x - this server has to beat that number, not match it.

### First real-library reading, and it is not good

Measured on the NAS at 17 minutes uptime, with VLC browsing and an LG television connected:

| Metric | Reference (11 days, ~20k files) | This server (17 min) | Target |
| --- | --- | --- | --- |
| Working set | 394.9 MB | **391.1 MB** | < 200 MB |
| Managed heap | 42.5 MB | **179.2 MB** | < 60 MB |
| Gen2 collections | 670 in 11 days (~2.5/day) | **9 in 17 minutes** | < 20/day |

**Working set is at parity with the server this rewrite is meant to beat, and the managed heap is four
times the reference's.** Every earlier row in this table was taken against a 4-file library with an empty
cache, which is why they all looked comfortable - they were measuring an idle process, not this one.

The prime suspect is the served-bytes cache: it holds whole files as `byte[]`, its budget is 1024 MB, and
browsing a real library with two renderers is exactly what fills it. That would be the cache doing its job
rather than a leak - but it is a hypothesis, and the two earlier NAS theories that felt this obvious both
turned out wrong. **Do not act on it before measuring.** M9 should:

1. Read `/manage/memory` again with `FileCache.Enabled=false`. If the managed heap drops to reference
   levels, the cache is the whole story and the question becomes what budget the acoustic goal actually
   needs - a decision section 6 says to make deliberately, not by quietly shrinking it.
2. If it does not drop, the cause is something else and a heap dump is the next step, not more guessing.
3. Add a `/manage/filecache` endpoint reporting entry count and total bytes held, so this question is
   answerable from the running server instead of by inference. That belongs in M8's management surface.

Nine Gen2 collections in seventeen minutes is worth its own look. The reference reached that rate through
a forced blocking collect on every cache eviction, which this server does not do - so whatever is driving
it here is different, and probably the same allocation pressure behind the 179 MB.

**Do not read a trend into those two rows** - the libraries and uptimes differ, so the drop is workload,
not improvement. They are the calibration runs that produced the protocol above. **From M6 onward, use a
fixed fixture library and 60 s settle** so each row is comparable to the last.

**Two observations to carry into M9:**
- **Zero garbage collections in either run.** `GC.GetGCMemoryInfo` reports zeroes until a collection has
  happened, which is why the GC heap columns read 0. Allocation pressure is genuinely low - the reference
  logged 670 Gen2 collections in eleven days, driven by its forced blocking collect on every cache
  eviction, and nothing here does that.
- **36-40 threads under Server GC.** Server GC reserves a heap and a thread per core, costing memory for
  throughput this workload does not need. Measuring Workstation GC is on the M9 checklist; this is the
  first evidence it is worth doing rather than assuming.

### Second reading: 5 GB, and the mechanism is no longer a guess

Measured 2026-09-02 on the NAS, 42 minutes uptime, after real playback through the LG television.

| Metric | Reference (11 days) | This server (42 min) | Target |
| --- | --- | --- | --- |
| Working set | 394.9 MB | **5073.0 MB** | < 200 MB |
| Managed heap | 42.5 MB | **1128.2 MB** | < 60 MB |
| GC total committed | 52.6 MB | **4901.8 MB** | — |
| Gen0 / Gen1 / Gen2 | 6511 / 1764 / 670 | **6 / 5 / 4** | < 20 Gen2/day |

**Three facts, and together they are the whole explanation.**

1. **The managed heap is the byte cache.** 1128 MB against a `MaxTotalSizeInMegabytes` of 1240, and the
   startup log confirms the budget resolved to the full `Served-bytes cache holds up to 1024 MB`. The
   cache is doing exactly what it was told to do. `MaxFileSizeInMegabytes` is **512**, and
   `ServedFileCache.LoadAsync` calls `File.ReadAllBytesAsync`, so one episode is one allocation of up to
   512 MB - straight to the large object heap.
2. **The GC has essentially stopped running.** Six gen0 collections in 42 minutes, and **zero** in the
   316 s between the two readings while the heap grew 54 MB. Server GC sizes its gen0 budget per core, so
   on this box nothing reaches the trigger. Note also that `gcHeapSizeMb` and `gcTotalCommittedMb` come
   from `GC.GetGCMemoryInfo()`, which reports *the last collection* - both were byte-identical across the
   two readings because no collection happened in between. They are not live values.
3. **Nothing compacts the large object heap, by deliberate decision.** Committed 4902 MB against a live
   heap of 1163 MB is a 4x ratchet: hundreds of short-lived multi-hundred-megabyte arrays, freed but never
   collected and never compacted, so the runtime keeps committing fresh regions.

**The uncomfortable part.** Section 3 says "Never force a GC", written after seeing the reference fire a
blocking compacting collect on every cache eviction and judging it "pure cost". That judgement was made
against the reference's *managed* numbers, which looked fine - 42.5 MB heap, 52.6 MB committed. It now
looks like that forced LOH compaction was **load-bearing for RSS**, and it is the one thing the reference
does that this server does not. The reference reached 395 MB doing it; this server reaches 5073 MB without
it. That does not make the reference's implementation good - an unbounded `Task.Run` chain firing a
blocking full collect per eviction is still wrong - but the rule as written drew the wrong conclusion from
the evidence available at the time, and it should not be applied without reading this section.

**The fix is upstream of the GC question, though.** The plan's own rule "Stream, do not buffer - never read
a file into memory to serve it" is violated by the media half of this cache by construction. What the log
actually shows being served is overwhelmingly *thumbnails*, which are small; caching those is cheap and
keeps the drives quiet for browsing. Whole-film buffering is what generates the LOH churn, and dropping it
removes cause 1 and 3 together without touching the acoustic goal for the common case. Decide it in M9
against the serving rule in M6 - deliberately, as section 3 insists, not by quietly shrinking a number.

### Third reading: the memory is LIVE cache, not fragmentation

Measured 2026-09-02 at 42 minutes uptime, after media was streamed, against the reference on the same box
at the same moment:

| Metric | Reference (12 days) | This server (42 min, post-streaming) |
| --- | --- | --- |
| Working set | 370.4 MB | **2990.9 MB** |
| Managed heap | 47.3 MB | **1116.1 MB** |
| GC total committed | 76.5 MB | 1112.0 MB |
| **LOH size** | ~9 MB | **973.9 MB** |
| **LOH fragmentation** | 4.34 MB | **0.13 MB** |
| Gen2 collections | 727 | **3** |

**This corrects the earlier diagnosis in this file.** The 16-minute reading showed a large object heap of
7.97 MB with 6.38 MB fragmented, and that was recorded here as the miniature signature of the 5 GB. It was
not. With a full cache the LOH is 973.9 MB with **0.13 MB** of fragmentation - the memory is *live*, held
deliberately, and fragmentation is not the mechanism at all.

The mechanism is three ordinary facts compounding:

1. **The cache holds what it is permitted to hold.** The budget resolved to 1024 MB and filled to 974 MB.
   Nothing is wrong with that except the number.
2. **`MemoryCache` expiry is lazy.** An entry past its 10-minute sliding window is not removed until a
   scan runs, and a scan runs only when the cache is touched.
3. **Large-object memory returns only on a Gen2 collection, and the LOH is never compacted by default.**
   Three Gen2 collections in 42 minutes, because the box reports 40 GB and the GC feels no pressure.

**Consequence for section 3's "Never force a GC" rule: it was written on the wrong evidence.** The
reference's forced compacting collect on every eviction is what buys it 370 MB after twelve days against
this server's 2991 MB after forty-two minutes. That does not make its implementation good - an unbounded
`Task.Run` chain firing a blocking full collect per eviction is what its 727 Gen2 collections are - but the
*intent* is load-bearing and this file was wrong to dismiss it. The compromise now implemented:
`/manage/filecache/clear` forces one compacting collection **when an operator asks**, and nothing forces
one automatically. Whether that is enough, or whether release has to be automatic on eviction, is the
question the next constrained measurement answers.

**Note also what this means for the cache rework**: smaller per-file limits and single-flight do not reduce
the *total* held - only the budget does. At the configured 1024 MB this server will still climb to roughly
a gigabyte of live large-object heap.

### Fourth reading: `/manage/filecache` names what is in the cache, and it is films

Measured 2026-09-02 on the NAS, the reading section 8 item 0a was written to get. Two samples of the same
deployed server, half an hour apart, either side of a redeployment:

| | Warm (30 min uptime) | Cold (2.5 min uptime) |
| --- | --- | --- |
| Working set | 797.2 MB | 392.6 MB |
| Managed heap | 573.0 MB | 182.7 MB |
| **LOH size** | **504.6 MB** | 3.4 MB (stale - see below) |
| LOH fragmentation | 0.74 MB | 0 MB |
| **Cache `heldMb`** | **505** | 109.2 |
| Cache `entryCount` | 133 | 111 |

**The large object heap and the cache are the same 505 MB, to within a megabyte.** That closes item 0a's
question: the memory is live cache content, the instrument agrees with the GC, and the lever is the budget.
No heap dump is needed.

**What the `paths` listing adds is the part no aggregate number could show.** Of the cold sample's 111
entries, **102 are thumbnails, 8 are static resources, and 1 is a video file** - and that single video is
about 100 MB of the 109 MB held. The warm sample's extra ~400 MB over 22 further entries is the same story
at scale: a handful of films.

So the shape the plan already suspected is now measured rather than argued. **The content the cache exists
for - thumbnails, re-served constantly - costs single-digit megabytes. Whole-film buffering is the entire
byte cost and the entire large-object problem.** The option listed first under section 8 item 1, caching
thumbnails and static assets only, would drop the held bytes by roughly 99% while keeping every entry that
is actually being re-served, and every remaining payload would sit under the 85,000-byte LOH threshold, so
the large-object heap stops existing as a concern by construction rather than by tuning.

**One instrument caveat found while doing this.** The dashboard's LOH tile - and `/manage/memory`'s
`generations` block that feeds it - reports the *last collection's* sizes, from `GCMemoryInfo`. On the cold
sample it read 3.4 MB while the cache already held 109 MB, because only two Gen0 collections had ever run.
The figure is trustworthy on a server that has been up and collecting; on a freshly started one it reads
far too low. Compare it against `/manage/filecache` before drawing a conclusion from it.

### Fifth reading: the cache has evicted the very thing it exists for

Measured 2026-09-02 on the NAS, on the redeployed build, and this is the one to quote.

| Metric | Value |
| --- | --- |
| Entries | **4** |
| Held | **906.66 MB** of a 1024 MB budget |
| What they are | **3 films and 1 icon - zero thumbnails** |
| Hits / misses | 12 / 242 |
| Working set | 1137 MB |

An earlier sample the same day held 102 thumbnails. They are gone: **the films have evicted them.** The
cache exists to stop a mechanical disc spinning up a second time for content already served, and the
content actually re-served is thumbnails - so at 89% of its budget spent on three films, the cache is now
defeating its own purpose rather than merely costing memory.

**This settles the choice in section 8 item 1.** "Cache thumbnails and static assets only" is no longer the
cheapest of three options, it is the only one that does what the cache is for. Everything it would keep
sits under the 85,000-byte large-object threshold, so the LOH problem disappears by construction rather
than by tuning. The constrained-limit measurement is still worth doing, but it is now a confirmation, not
a decision.

### Sixth reading, and the correction that followed it: the LOH was a ratchet, not a leak

Measured 2026-09-04 on the NAS at **12.9 hours uptime** - by a wide margin the longest reading here, every
earlier row being under 45 minutes - and then measured again 326 s later, after the maintainer pressed
`/admin/cache` -> **Clear and collect**. The pair is what makes it conclusive; either reading alone is
misleading.

| Metric | At 12.9 h | After the clear + collect | Change |
| --- | --- | --- | --- |
| Working set | 1171.6 MB | **288.1 MB** | **-883.5 MB** |
| Managed heap | 892.6 MB | **19.4 MB** | -873.2 MB |
| LOH size | 899.3 MB | **0.80 MB** | -898.5 MB |
| LOH fragmentation | 28.4 MB | **0.00 MB** | compacted |
| GC total committed | 933.0 MB | **24.7 MB** | -908.3 MB |
| Byte cache held | 0.96 MB (59 entries) | 0 MB (0 entries) | -0.96 MB |
| `gcGeneration` of the snapshot | **1** | **2** | - |

**The 871 MB was collectable garbage, and my first reading of it was wrong.** I recorded it as live and
reachable, on the argument the third reading used - 899 MB of LOH against 28 MB of fragmentation. That
inference was built on a **stale instrument**: `gcGeneration: 1` says the last collection was a gen1, which
does not touch the LOH, so those LOH figures described an older state rather than the present one. This is
exactly the trap section 7 already records - *`GC.GetGCMemoryInfo()` reports the last collection, not the
present* - and it is worth noting that the trap survives being written down, because the numbers look like
a live measurement. **A LOH figure is only evidence when the snapshot's own generation is 2.**

The hypotheses that row carried - `ArrayPool` bucket retention, thumbnail blobs, a film-sized read held
after being declined - are all **disproven by the collect**, since every one of them describes memory
reachable from a live root, which a collection cannot free.

**What it actually is: the ratchet the second reading described, confirmed.** Large short-lived arrays,
freed but never collected, on a box that reports ~10 GB and therefore never applies enough pressure to
trigger a gen2. The clear freed under 1 MB; the collection freed 883. So:

1. **Nothing in normal operation releases it.** The server sat at 1,171 MB for 12.9 hours holding ~880 MB
   of garbage nobody was using, and would have kept sitting there. Only an operator pressing a button
   changed it.
2. **The reference's forced compacting collect is load-bearing, for the third time in this file.** One
   press took this server from 1,171 MB to **288 MB - below the reference's 370-403 MB on the same box at
   the same time**, and inside the <400 MB under-playback target. Section 3's "Never force a GC in normal
   operation" rule has now been contradicted by measurement twice, and this is the cleanest instance.
3. **This is a decision, not a defect to fix.** What is missing is a *trigger* in normal operation. The
   options, and none of them is free: a `PostEvictionCallback` that releases on eviction (the reference's
   intent without its unbounded `Task.Run` chain); a budget-driven periodic collect; or leaving it to the
   runtime and relying on a real 2-4 GB machine applying the pressure this 10 GB box does not. **The third
   is the one this section's own rule implies** - it says repeatedly that the test NAS is the anomaly - and
   it is the only one that costs no code, which is why it should be tested first, under
   `-HeapLimitPercent`, before any collect is scheduled.

**Still inadmissible as a target reading**, both rows: `totalAvailableMemory` 9977 MB and no heap limit.
The *delta* is admissible - it is a controlled before/after on one process 326 s apart - and it is the
mechanism that matters, not the absolute number.

> **The deployed configuration is 20x the repository's.** `/manage/configuration` on the live server reads
> `maxTotalSizeInMegabytes: 5120` and `maxFileSizeInMegabytes: 512` (resolved budget 4988 MB), against the
> 256 / 32 this tree ships and section 4's M9 row describes. `publishNAS/config.json` is dated
> **2026-09-04 09:27**, later than every assembly beside it, so it was written after the deploy - the admin
> Settings page writes that file. **512 MB per file re-admits films to the cache, which is the 5,073 MB
> mechanism M9 warns against.** It has not fired only because nothing has been retained yet (hits 30,
> misses 882). Deciding that number is the maintainer's call; it is recorded here because a reading taken against a
> 5 GB authorisation is not a reading of what this tree would do.

## 3c. Logging - compared against the reference

Checked field by field against `Reference/.../Program.cs`. Differences that mattered are now closed.

| Setting | Reference | Here | Status |
| --- | --- | --- | --- |
| Console + file sinks | `AddSimpleConsole` (MS) + Serilog files | Serilog owns both | Equivalent, one pipeline instead of two |
| App log | `logs/appLog.txt`, daily, 7-day retention | `logs/app.log`, daily, 7-day | Equivalent |
| Error log | `logs/appErrorLog.txt`, Error+, with TraceId/SpanId | Was missing TraceId/SpanId | **Fixed** |
| `ActivityTrackingOptions` | TraceId, SpanId, ParentId | Was absent | **Fixed** |
| Per-category levels into Serilog | Mirrors `Logging:LogLevel` via `MinimumLevel.Override` | Was absent | **Fixed** |
| Debug mode raises level | Yes, to Verbose | Yes | Equivalent |
| EF connection/command events | Demoted to Debug via `ConfigureWarnings` | Was at Information - every migration dumped pages of SQL | **Fixed** |
| Slow-query log | Dedicated file, 50 ms threshold, monthly, 32 MB, 5 files | `SlowQueryThresholdInMilliseconds` existed but **nothing read it** | **Fixed** - `SlowQueryInterceptor` |
| `EnableSensitiveDataLogging` | `true` | Deliberately **not** enabled | Divergence, see below |
| Separate `sqliteLog.txt` | Optional, logs every DB message | Not implemented | Deliberate - the slow-query log covers the useful case |

**The dead-setting bug was mine.** `SlowQueryThresholdInMilliseconds` was declared, documented and read by
nothing - exactly the defect class I flagged in the reference, whose `PRAGMA cache_size` setting was always
overwritten by a hard-coded value. Found by grepping my own configuration properties for readers. Worth
repeating that check whenever a new option is added.

**Sensitive data logging stays off.** The reference enables it, so every logged command carries its
parameter values - which for this server means full filesystem paths written to a log file that rolls for
seven days. The slow-query log records the command text and duration, truncated to one line, with no
parameters. That is enough to identify a slow query without turning the log into a directory listing.

## 7. Conventions and gotchas

Full conventions live in `CLAUDE.md`. These are the ones that have actually cost time.

### Host-level tests - the "no WebApplicationFactory" rule is lifted

Decided 2026-09-23. `CONTRIBUTING.md` and `CLAUDE.md` have both said "real components wired through DI -
no host, no `WebApplicationFactory`", and two independent reviews named the absence of host-level coverage
as the one real testing gap. The rule is lifted for a **small, deliberate** set: ten to twenty tests that
boot the host and prove the assembled application, not a second copy of the suite.

What only a host test can prove is the wiring the unit and integration projects deliberately exclude -
endpoint routing, the port filters, middleware order, model binding, and the status codes and headers a
renderer actually receives. `Microsoft.AspNetCore.Mvc.Testing` is **already** referenced in
`Directory.Packages.props`, so nothing new is taken on.

The boundary that stays: anything provable without a host still goes in `DlnaServer.IntegrationTests`. A
host test that duplicates an existing one earns nothing and costs startup on every run. The two
convention documents are updated when the tests land, not before.

### Assembly version - bump it on EVERY change

Asked for on 2026-09-15 and binding from then on: **every change to this project bumps the assembly
version**, in the form `Major.Minor.MonthDate` - `1.0.0915` is a change made on 15 September.

**Changed 2026-09-23, on the maintainer's decision: each project under `src/` carries its OWN
`<Version>`, and only the ones a change actually touches are bumped.** The single property in
`Directory.Build.props` is gone. The question the number answers is now "which assembly changed", not
"when was the solution last touched" - the old scheme bumped `DlnaServer.Upnp` for a change that only
edited the admin UI, which is what prompted this. `DlnaServer.Host` is the **product version**: the only
executable, what the release tag names, what `release-notes.md` and the `.github/SECURITY.md` support
table track, and what the About page shows. Test projects are not versioned individually - a test
assembly ships to nobody - and take the product version from the `Tests`-conditioned group in
`Directory.Build.props`, which is the one place a version is still set centrally.

The About page reads `AssemblyInformationalVersionAttribute` off the **entry** assembly rather than its
own. That distinction did not matter while every assembly carried the same number and it is load-bearing
now: `typeof(About).Assembly` is `DlnaServer.Admin`, so the page would have reported the admin library's
version as the product's the moment the two diverged. The assembly table further down that page is where
the per-project numbers belong, and it becomes worth reading for the first time.

**Known limit of the scheme, accepted rather than solved:** `MonthDate` has a day's resolution, so two
batches on the same day produce the same number for a project whether it changed in both or one. The
per-project signal is a cross-day signal.

| Part | Who may bump it | How often |
| --- | --- | --- |
| Major | The maintainer only - by hand or on their explicit request | never on my own initiative |
| Minor | me | at most once per two weeks - read the last `MonthDate` to tell when the previous bump was |
| MonthDate | me | daily, and on every change |

Two things to know about the form. The leading zero survives only in the *informational* version:
`1.0.0915` is stored verbatim there and is what the About page shows, while `AssemblyVersion` and
`AssemblyFileVersion` are parsed as numbers and normalise to `1.0.915.0`. And `MonthDate` is `MMDD` with
no year, so it wraps every January - the Minor bump is what keeps the ordering unambiguous across a year
boundary.

**Verify a bump by reading the built assembly, never the property.** An incremental build does not
reliably regenerate `obj/<config>/net8.0/<Project>.AssemblyInfo.cs` when only the version changed: on
2026-09-15 `dotnet msbuild -getProperty:Version` answered `1.0.0915` for every project while eight of
the nine DLLs still carried `1.0.0.0`, at 0 warnings. Delete the generated `*.AssemblyInfo.cs` and
`*.AssemblyInfoInputs.cache` under `src` and `tests`, rebuild, then read `ProductVersion` back off the
DLLs. **Per-project versions make this worse, not better** - nine numbers moving independently means a
stale one no longer stands out by disagreeing with its neighbours, so the read-the-DLL step is the only
check there is.

**`release-notes.md` is updated as part of the change, not afterwards** - the same rule as the
version itself. A change an operator would notice adds its line to the entry for the current
`Major.Minor` before the work is called done; a change they would not notice adds nothing. A version
whose entry still describes the previous one is worse than no notes at all, because it is read as current.

**It is release notes for the operator, not a build log.** One entry per `Major.Minor`, newest at the top,
written in the terms someone using the server thinks in - what they can now do, what changed under them,
whether an upgrade costs them anything. No `MonthDate` headings: a day is not a release. Everything
internal - the reasoning, the measurements, the traps - belongs in this file instead, and that split is
the point of having both.

**`release-notes.md` and `LICENSE` ship with the binaries.** Both stay at the solution root, where a reader
of the repository finds them beside `README.md`, and `DlnaServer.Host.csproj` pulls each in as a `Content`
item with `Link` and `CopyToOutputDirectory`. `Link` is what lands them in the output ROOT rather than
under a `../../` path - the same reason the `Resources` glob names an explicit `TargetPath`. So a
deployment carries what changed and what the terms are without going back to the source, and the rule
above still governs one file in one place.

**That change had a trap that a green build hides**, and it is the reason to check the container path
whenever a file outside the project cone is added to the output: `.dockerignore` excludes `*.md` so a
documentation edit cannot invalidate the restore layer, which put `release-notes.md` outside the build
context entirely. A local build and publish succeed; the container build fails at publish with **MSB3030,
file not found**. `.dockerignore` therefore carries a `!release-notes.md` negation, placed after the `*.md`
line. Verified by building a throwaway context against the real `.dockerignore` and listing what arrived:
`LICENSE` and `release-notes.md` present, `README.md` and `PLAN.md` (as it then was) still excluded - so the restore-layer
protection is intact for everything else. `LICENSE` has no extension and was never matched.

**The licence is MIT**, holder `Kopco`, matching `Copyright.Holder` in `DlnaServer.Admin` - which is
deliberately not `ServerOptions.ManufacturerName`, for the reason recorded on that type. About names the
licence and points at the file rather than reproducing its text.

### Code conventions
- **Block-scoped namespaces** (`.editorconfig`). The global `cs-writeguard` hook asks for
  file-scoped; it carries another project's rules and does not apply here.
- **`is null` is a compile error inside an EF expression tree.** Use `!= null` in projections. The hook's
  `null-pattern` warning is wrong there.
- **A private or internal `async` helper takes a REQUIRED `CancellationToken`, with no `= default`.**
  Roughly forty of them across `LibraryIndexer`, `DatabaseInitializer`, `MediaProcessor` and
  `SsdpNotifierHostedService` are written this way, every one is already called with a real token, and
  the default exists to spare a *caller* who has none - which inside a class that always has one just
  makes it possible to forget. The rule that every async method carries `= default` applies to the
  surface other code calls, not to these. Decided 2026-09-08 during the review fix pass, and recorded
  here so it stops being re-raised on every pass.
- **The admin UI has no JavaScript and no JS interop.** `DlnaServer.Admin` ships exactly one static
  asset, `wwwroot/admin.css`, and not a single `IJSRuntime` call. So a decision that would naturally ask
  the browser about itself - can you play this, how wide is the window - is made **server-side** instead,
  or it introduces the first `.js` file in the project. `BrowserPlayback` is the worked example: it
  answers "can a browser play this" from the container rather than from the browser's own
  `canPlayType`, and the reasoning for the deny-vs-allow direction is on the type. Note the admin CSP
  (`script-src 'self'`) rules out an inline handler or a `blob:` source, so the first JS would have to be
  a file under `wwwroot` referenced from `App.razor` - measured on 2026-09-08, when a `blob:` test came
  back as `Media load rejected by URL safety check`.
- **Entities never leave Persistence.** Enforced by `internal` plus architecture tests.
- **Every entity has `int Id` (internal) and `Guid PublicId` (external).** Repositories take and return
  `PublicId` only.
- **Protocol DTOs document their wire contract on every property**: the wire name in bold, then a plain
  description, beside the attribute carrying element name/namespace/order.
- **`[LoggerMessage]` source-gen logging** in `Foo.Log.cs` siblings, unique EventIds per class.
- **Timestamps in file names: hours and minutes only**, never seconds - so any backup path needs
  `BackupFilePath.CreateUnique`, which appends a suffix when a name is taken.
- **A Razor component endpoint answers POST as well as GET.** A controller route may therefore not share
  a page's path - `/admin/upload` matched both and every post was an `AmbiguousMatchException` and a 500,
  while the get carried on working. See section 6n.
- **MVC reads the whole form before your action runs**, for any request with a form content type, whether
  or not the action binds anything from it. An action that wants to stream a body needs
  `[DisableFormValueModelBinding]` or it gets a consumed, already-buffered request.
- **Removing an endpoint at startup means removing the CONTROLLER from the application model.** Clearing
  its selectors leaves an `[ApiController]` action with no attribute route, which `MapControllers` refuses
  - the server does not start. `DisabledUploadConvention` is the worked example.
- **A tooltip or popover inside a `.panel` must be `position: fixed`**, not absolute. A panel is
  `overflow-x: auto`, which makes its other axis scrollable too, so an absolutely positioned box is
  clipped by its own panel and puts a scrollbar on it.
- **SOAP operation parameters are PascalCase.** SoapCore binds each argument from the body element of the
  same name and renderers send `<ObjectID>`; camelCase names bind nothing and every argument arrives null.
  This is the one place the usual camelCase convention is deliberately broken.

### Virtualization - the constraints are strict and they fail quietly

`<Virtualize>` renders only the rows in the visible region. It is used on the three lists that can grow
without bound: the file cache's cached paths, the folder-search results, and the Library folder list. The
tile grids are deliberately **not** virtualized - see the last bullet.

Everything below is a requirement of the component, not a preference, and breaking one produces a list
that renders wrongly rather than an error:

- **`Items` binds to `ICollection<T>`, which `IReadOnlyList<T>` is not.** Every repository here returns
  `IReadOnlyList<T>`, so the page materialises a `List<T>` (`[.. source]`). Without it the build fails
  with `CS0411` on generated Razor code, which reads as a type-inference error and does not mention the
  interface at all.
- **Every row must be exactly `ItemSize` pixels tall** - that is how a scroll position maps to an index
  without rendering everything first. So a row may not wrap: `.scroller` forces
  `white-space: nowrap` + `text-overflow: ellipsis` on paths and folder links, with the full text in a
  `title`. The constants are **measured from the rendered page**, not derived
  (32.5 paths, 41.5 Library folder, 59.5 folder-search result), and each is commented with the CSS it has
  to stay in step with. A wrong value still works - the component measures and corrects - at the cost of a
  second render and, on a reload, a scroll position showing the wrong rows.
- **The scroll container needs a real height**, which the page's own scroll does not give it: `.scroller`
  is `max-height: 60vh; overflow-y: auto`. `max-height` rather than `height`, so a two-row list does not
  sit in a tall empty box. `tabindex="-1"` is required or keyboard scrolling silently does nothing in
  Chromium.
- **Spacers must be legal children of their parent.** `SpacerElement="tr"` inside `tbody`,
  `SpacerElement="li"` inside `ul`; the default is `div`, which is invalid in both.
- **Content rows must be `display: block` or `table-row`**, so `.folders li` is `display: block` rather
  than the default `list-item`.
- **The tile grids cannot be virtualized as they stand.** `Virtualize` positions spacers and rows as a
  single vertical stack, one item per row; `.tiles` is a CSS grid with several tiles per row, which the
  documentation rules out explicitly. Virtualizing the Library files and the file-search results would mean
  restructuring them into fixed-height rows - a real change to how the page looks, not a drop-in. Left
  alone deliberately; both are already bounded (file search caps at 200), and the images carry
  `loading="lazy"`, which is what keeps a big folder cheap today.
- **`OverscanCount` is 10** on all three, against a default of 3 - rows rendered above and below the visible
  region so a scroll has somewhere to go before it has to re-render. Measured on the folder search: the same
  200 results render 17 rows at the default and 23 at 10.
- **`Placeholder` is declared but cannot render as things stand.** It is only used on the `ItemsProvider`
  path, where a row's data may not have arrived yet; with an in-memory `Items` collection every row is
  already present, so the loading row never appears. It is left in place, commented, because it becomes
  live the moment any of these lists moves to an `ItemsProvider` - which the folder search is the obvious
  candidate for, since that would page the query and retire its 200-row cap.
- **`EmptyContent` covers the file cache and the folder search.** The Library folder list does not use it:
  that page renders its Folders heading only when there are folders, and shows one page-level
  "Nothing indexed here yet." panel for the whole directory, so an `EmptyContent` there would be
  unreachable markup or a second empty message beside the existing one. Changing that page's empty-state
  handling is a decision for the maintainer rather than a side effect of adding virtualization.
- Blazor Server is the render mode throughout, so all of this runs over the circuit - a scroll fetches
  rows over the websocket rather than from a client-side collection.

### DIDL-Lite rules that renderers actually enforce
- **Every object in a `BrowseDirectChildren` listing of container C declares `parentID` = C.** Not the
  folder it physically lives in - the container being browsed. The root listing surfaces recently-added
  files from all over the tree, and those must still say `parentID="0"`. Getting this wrong hid the entire
  library from an LG television while VLC showed it perfectly; the reference gets it right by construction
  (`isRootFolder ? "0" : realParent`). `DidlMapper.MapContainer`/`MapItem` therefore take the parent as an
  argument, and `DidlMapper.ParentIdOf(...)` supplies the real one for `BrowseMetadata`, which is the one
  case where the object describes itself.
- **`BrowseMetadata` describes the object; `BrowseDirectChildren` describes a listing.** The parent differs
  between the two, deliberately.
- **A renderer that shows the server but no content is almost always rejecting the DIDL-Lite**, not failing
  to fetch it. Check `parentID`, `upnp:class` and `res@protocolInfo` before suspecting the network.
- **VLC is not a test.** It tolerates malformed DIDL-Lite that televisions reject. A change to the browse
  output is unverified until a TV has seen it.

### The cs-writeguard hook
Global, and written for another project, so most of what it says does not apply here. Two exceptions:
- **`async-blocking` is real and worth obeying.** It caught genuine sync-over-async in the SOAP service.
- `braces`, `distinct` and `missing-ct` are usually fair.
- `xmldoc-private` and `filescoped-namespace` are noise in this repo - say so and move on rather than
  silently ignoring them.

### Library traps
- **An unhandled exception in a `BackgroundService` stops the whole host, and it looks like a clean exit.**
  `BackgroundServiceExceptionBehavior` defaults to `StopHost` on .NET 6+, and `StopHost` calls
  `StopApplication()` rather than rethrowing - so `Program.Main`'s restart loop sees `IsRestartRequested
  == false`, leaves the loop, and the process ends with code **0**. Nothing in the exit status
  distinguishes a crashed indexing job from a `/manage/stop`. Configure the behaviour explicitly and
  catch **per item**, not around the loop.
- **SoapCore's `CustomMessage` does not emit `soap:encodingStyle`.** It writes the `xsd` and `xsi` xmlns
  declarations and nothing else, so a renderer that requires SOAP 1.1 `encodingStyle` gets a response it
  rejects - which presents as a television that shows the server and an empty library. Verified by
  rendering both `CustomMessage` and `CustomEnvelopeMessage` side by side. Its `Message`,
  `XmlNamespaceLookup` and `AdditionalEnvelopeXmlnsAttributes` setters are all **assembly-internal**, so a
  test has to construct through `CustomMessage(Message)` and seed the lookup by reflection or the write
  throws a `NullReferenceException` from inside `Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix`.
- **`GC.GetGCMemoryInfo()` reports the last collection, not the present.** `HeapSizeBytes` and
  `TotalCommittedBytes` are byte-identical across two readings taken 5 minutes apart when no collection
  happened in between - they are a snapshot, not a live gauge. `GC.GetTotalMemory(false)` does move.
  Anything drawing conclusions from `/manage/memory` needs to know which fields are which.
- **`BoundedChannelFullMode.DropWrite` / `DropOldest` return `true` from `TryWrite` when full.** They
  accept the call and discard an item. Only `FullMode.Wait` makes `TryWrite` return false on a full
  channel. Any code that pairs a channel with a "already queued" set must use `Wait`, or the set leaks
  entries for work that was never delivered.
- **`MemoryCacheOptions.SizeLimit` is fixed for the life of the store.** `FileCache.MaxTotalSizeInMegabytes`
  is therefore the one file-cache setting that needs a restart; the rest are read per request through
  `IOptionsMonitor`.
- **The application-folder fallback is not an operator's choice, and reconciliation must not treat it as one.** On 2026-09-23 a restart on the NAS found no `config.json`, `ConfigurationFileGuard` wrote defaults with no source folders, `DlnaOptionsDefaults` served the application folder, and `ReconcileDirectoriesAsync` removed both indexed source folders as "no longer covered by configuration" - the cascade took all 25,669 file rows, and the restored file rebuilt the library with every `PublicId` regenerated. `DlnaOptionsDefaults.IsSourceFolderFallback` now suspends the coverage rule while the fallback is in force; a folder that is gone from disc is still removed. Why the file was missing is not in the logs.
- **A Xabe `ConversionException` message is ffmpeg's entire standard error.** On a damaged stream that is one line per bad packet - 139,365 lines and 9.9 MB in one warning on the NAS, three of which rolled a 32 MB log file. Every ffmpeg-facing reason in `MediaProcessor` now goes through `FFmpegFailureReason.Summarise`, which keeps the distinct error lines up to a cap; never log `exception.Message` from that path raw.
- **`MaxFailureCount` only means something if the attempts are spread out.** Passes run a second apart while work is queued, so the three attempts used to land within about three seconds - every broken file on the NAS was decoded and logged three times, 1.3-3.7 s apart. `MediaProcessingHostedService` now keeps an in-memory not-before time per failed file (5 minutes, then 30) and passes the waiting files to `GetPendingProcessingAsync`, which leaves them out *in the query* - filtering after the claim would let 25 waiting files fill the batch and starve everything behind them. A row whose counts were reset (recreate, let back in, new content) is offered at once regardless.
- **Xabe.FFmpeg 6.0.2 can throw "Destination array was not long enough" after ffmpeg has succeeded.** Read out of its IL: `FFmpegWrapper.RunProcess` calls `WaitForExit()` - the overload that drains redirected stderr - only when `!HasExited`, so a process that exits first leaves the stderr callback appending to `_outputLog` while `ToArray` copies it. Seen once on the NAS (`VID_20250606_160533.mp4`, fine on the next attempt). `MediaProcessor.StartConversionAsync` treats exactly that exception as success when the frame file exists, since the frame is deleted before every run. The real fix is invoking ffmpeg ourselves with `ProcessStartInfo.ArgumentList`, which would also retire the quote-refusal in `IsUnsafeForFFmpeg` - not done, it rewrites the frame grab and needs verifying on the NAS.
- **`is null` is a compile error inside an EF expression tree** (also in section 7 above) - and the
  `cs-writeguard` `null-pattern` warning fires on every projection that works around it.

### Deployment traps

**`AddJsonFile(Action<JsonConfigurationSource>)` does not resolve a file provider, so an absolute path
loads nothing and the server starts on code defaults.** Added 2026-09-03 by the review fix that made a
malformed `config.json` reload survivable, and it reached the NAS. The convenience overload
`AddJsonFile(path, optional, reloadOnChange)` calls `source.ResolveFileProvider()`; the `Action<>`
overload does not, and that call is what splits a rooted path into a `PhysicalFileProvider` over its
directory plus a bare file name. `PhysicalFileProvider` refuses a rooted path, so the file is "not
found" while `source.Path` reads correctly. A second bug in the same change - an `OnLoadException` that
set `Ignore = true` unconditionally - swallowed the `FileNotFoundException`, so the server came up on
**port 26851**, collided with the reference implementation, served its own publish folder and authorised
a 1024 MB cache against a configured 256. The only clue was the source-folder fallback warning.
Now `Host/Configuration/DlnaConfigurationFile.cs`, which calls `ResolveFileProvider()` and ignores a
failure only **after the first load has succeeded** - `ConfigurationFileGuard` runs immediately before
and guarantees a readable file, so an initial failure is real and must stop the server. Covered by
`DlnaConfigurationFileTest`, verified by removing the call and watching three of its four tests fail.
**A configuration source that loads nothing is indistinguishable from one that loaded successfully with
no values in it - so assert on a value, never on the absence of an error.**


**`dotnet test` on the solution destroys a RID-specific restore, and the build stays green while it
happens.** Added 2026-09-03 by the review fix that put a test gate in `NasBuild.sh`, and it broke the
very next deployment. `dotnet test <solution>` runs its own implicit restore with **no `--runtime`**,
and `DlnaServer.IntegrationTests` references the Host - so it rewrites
`src/DlnaServer.Host/obj/project.assets.json` with a RID-less target, and `dotnet publish --no-restore`
then fails with `NETSDK1047: ... doesn't have a target for 'net8.0/linux-x64'`. Restore, build and the
tests themselves all report success first, so nothing points at the cause. The script's assets guard did
not catch it either, because that guard ran before the test step.
The fix is ordering: the test step runs **before** the restore, so the `--force` RID restore is the last
thing to touch that file. A second guard now asserts `"net8.0/linux-x64"` is in the assets file before
the publish. **Anything inserted between the restore and the publish must not restore the Host without
`--runtime linux-x64`** - that includes a bare `dotnet build` or `dotnet test` on the solution.
- **Never pass `-p:AssemblyName` on the command line in a multi-project build.** It is a *global*
  MSBuild property, so every project in the graph takes that name. `NasBuild.sh` did this, and the first
  real NAS deployment failed with 113 errors, all variations of "the type or namespace name 'Core' does
  not exist in the namespace 'DlnaServer'" - Core, Persistence, Media, Upnp and Admin had all become one
  assembly name, so the compiler kept one reference and every type in the other four vanished. Newer SDKs
  refuse earlier and more clearly, at restore: `error : Ambiguous project name`.
  The fix is a project-scoped property: the script passes `-p:PublishAssemblyName`, and only
  `DlnaServer.Host.csproj` reads it and renames itself.
- **`PublishReadyToRun` must be set at restore time as well as at publish time.** R2R needs the
  linux-x64 runtime pack, and restore only fetches it when it knows R2R is coming. Otherwise publish
  fails with `NETSDK1094: a valid runtime package was not found`.
- **Pass the same properties to `build` and `publish`.** Different global properties mean different
  project instances, so publish recompiles from scratch and can fail on code the build step reported as
  green one second earlier. That asymmetry is what hid the `AssemblyName` defect: `==> Building` said
  "0 Errors" and `==> Publishing` then produced 113.
- **ReadyToRun does not work on this NAS, and the reference only appears to use it.** With
  `PublishReadyToRun` genuinely enabled, crossgen2 on SDK 8.0.414 fails to load its own native JIT
  (`NativeLibrary.Load` -> `CorInfoImpl.Startup`) and publish dies with `NETSDK1096`. The reference's
  script passes **`-p:ReadyToRun=true`**, which is not a property the SDK recognises - the real name is
  `PublishReadyToRun` - so it is silently ignored and crossgen2 never runs. That is the whole reason the
  reference publishes cleanly on the same box. `NasBuild.sh` therefore defaults R2R **off**, matching the
  reference's real behaviour, with `NAS_READY_TO_RUN=1` to retry after an SDK update. The cost is a
  slower JIT warm-up on a process that then runs for weeks.
- **Satellite resource languages were shipping to the NAS and breaking the 0-warning build.** SoapCore's
  WCF dependencies carry translations for 13 locales that .NET does not recognise, so the NAS emitted
  **234 `NETSDK1188` warnings** and deployed 13 locale folders. The server runs with
  `InvariantGlobalization`, so none of them can ever be used:
  `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` in `Directory.Build.props` removes both
  the warnings and the folders (publish 28 MB -> 25 MB, 85 files -> 68).
- **A `.editorconfig` `generated_code = true` section does not survive a symlinked path.** The NAS
  reports source files under `/share/CACHEDEV1_DATA/Public/...` while the project is `/share/Public/...`,
  and a section glob is matched relative to the directory of the `.editorconfig` that declared it - so
  `[**/Migrations/*.cs]` matched nothing there and `CA1861` fired on the generated migration. Proof: EF
  writes `// <auto-generated />` into the Designer and snapshot files but **not** into the migration
  body, and only the unmarked file warned. The marker is now on all three, which is path-independent and
  needs no editorconfig section to work. The editorconfig entry stays - it is still correct off the NAS.
- **The Host does not reference `DlnaServer.Admin` yet.** It is an empty Razor class library - a `.csproj`
  and an `_Imports.razor` - so the reference adds nothing while making one more project that must be
  present and restorable on every deployment machine. M8 adds it back together with the first component
  the Host actually serves.
- **A symlinked path makes NuGet drop every project reference, silently.** `/share/Public` is a symlink
  to `/share/CACHEDEV1_DATA/Public`. `NasBuild.sh` derived its own directory with `pwd`, which keeps the
  symlinked spelling, so the Host was restored as `/share/Public/.../DlnaServer.Host.csproj` while MSBuild
  resolved its relative `ProjectReference`s to `/share/CACHEDEV1_DATA/Public/.../DlnaServer.Core.csproj` -
  the same files under two names. The restore log shows both prefixes side by side. NuGet then produced a
  Host graph with **no project references at all**: 34 entries and none of the packages those projects
  bring. A correct restore of the same tree records 4 project references and 57 entries.
  **The build still succeeded**, because MSBuild resolves `ProjectReference` without NuGet - so the
  project DLLs were compiled and copied while EF Core, Microsoft.Data.Sqlite, SQLitePCLRaw, SkiaSharp,
  Xabe.FFmpeg and CommunityToolkit were absent from `deps.json` and from the output. 42 assemblies instead
  of 54, and the server died at startup on
  `Could not load file or assembly 'Microsoft.EntityFrameworkCore.Relational'`.
  Fixed with `pwd -P` for both the script directory and the publish directory, so every project lives in
  one path universe.
  **Two wrong theories were tested and discarded first**, which is worth recording because both were
  plausible: incremental copying after the clean step (all 54 files come back locally), and a stale `obj/`
  (`--force` restored all five projects and the output was still 42). The restore log's two path prefixes
  were the evidence that settled it. `--force` was kept - it costs seconds and is reasonable hygiene for a
  deployment - but it fixed nothing here.
- **This is the same root cause as the `.editorconfig` failure above.** Source files arriving as
  `CACHEDEV1_DATA` paths while the project is a `/share/Public` path is one systemic path-aliasing problem
  with two unrelated-looking symptoms. **Anything on this NAS that compares or derives paths needs the
  physical path.**
- **`Content Include="Resources\**\*"` flattens on Linux.** A backslash is not a directory separator
  there, so `%(RecursiveDir)` resolves to nothing and every asset is copied to the output root: the NAS
  deployment had `folder.jpg` and `contentDirectory.xml` loose beside the assemblies and no `Resources`
  folder at all, which would have made `/icon/*` and `/SCPD/*` return 404 for every renderer. Fixed with
  forward slashes plus an explicit `TargetPath`, so the layout no longer depends on how the glob is
  interpreted. **Any glob in a csproj that has to work on the NAS needs forward slashes.**
- **A publish that reports success is not evidence of a complete publish.** Both defects above passed
  `dotnet build` and `dotnet publish` with 0 warnings and 0 errors, and only surfaced as a runtime
  `FileNotFoundException` naming a single assembly - which says nothing about how it went missing.
  `NasBuild.sh` now checks its own work twice: after restore it confirms the graph contains
  `Microsoft.EntityFrameworkCore.Relational`, which can only arrive through a project reference and so is
  a direct test that those references resolved; after publish it checks the entry point, its two host
  files, the data stack and both native libraries, prints the assembly count, and fails naming whatever is
  absent. The publish check earned itself on the very next deployment: it listed all 7 missing files and
  refused to start the server, instead of letting it die at startup with one assembly name and no context.
- **Verify a deployment publish locally before running it on the NAS.** The exact command
  (`dotnet publish -c Release --runtime linux-x64 --no-self-contained`) runs fine on Windows and would
  have caught all of the above without a round trip. It did not catch the R2R crash, though - that one is
  specific to the NAS's own crossgen2, and only a real deployment could have found it.

### The admin UI's shared components

`src/DlnaServer.Admin/Shared/` holds what more than one page draws. Reach for one before writing the markup
again; each exists because the same block had already been copied.

| Component | Used by | Why it is shared |
| --- | --- | --- |
| `MediaFileFacts` | preview, library, file search | Type/size/**length**/dates for one file. `Compact` renders the one-line form a 190px tile has room for; the default renders the full panel. Everything comes from `MediaFileDto`, so a listing needs no extra query |
| `MediaTile` | library (twice), file search | The thumbnail card. Carries the fallback *decision* - stored thumbnail, else the image itself for a photo, else a type placeholder - which is the part worth having in one place |
| `StatTile` | dashboard, file cache | One labelled figure. Sixteen copies before |
| `FolderList` | library, folder search | The virtualized folder list, `ShowPath` for the search's second line. **Owns the `ItemSize` constants**, which were duplicated and are measured values |
| `Notice` | five pages | The result message |
| `ProcessingActions` | preview, library | Clear/recreate metadata and thumbnails for a `MediaFileScope`. Owns the wording, which is the part worth having once. **Starts shut** since 2026-09-12 - it is a `CollapsiblePanel` with no parameter, because both call sites want the same thing |
| `FormField` | both searches, settings | One labelled control, in the order heading -> control -> error -> description |
| `FieldPair` | file search | Two filters that belong together - a "from" and its "to" - which must never be split across a row boundary |
| `CollapsiblePanel` | both searches, `ProcessingActions`, Settings | A panel whose body folds away, holding whatever is put in it. `Footer` stays visible while it is shut. `Collapse()` and `Expand()` drive it from outside and render themselves |

**Button variants**: `primary` (blue, the main action), `go` (green, commits) and `stop` (red, backs out)
for a confirm/decline pair, and `danger`. **`danger` has only a `:hover` rule and no base style**, so a
button using it looks ordinary until the pointer is over it - `/admin/cache`'s "Clear and collect" is the
one place that shows. Left as found; a base rule would fix it.

**A row of `<li>` in `.folders` is not all rows.** `SpacerElement="li"` means Virtualize's two spacers are
`<li>` children too, so a rule on `.folders li` paints them: a border there drew an empty line above and
below every folder list. Style the `<a>` inside instead - only a real row has one. The row height must stay
exactly `ItemSize`, so check it after any such change (moving the border kept it at 41.5px).

**And the last row's border is the other half of that.** With the spacers fixed, the list still ended in a
rule sitting 36px above the library root's section divider, and two lines with a gap between them read as
an empty row - which is what it looked like from the page, whatever the DOM said. The last real row is
`li:not(:has(~ li a))`; its border is made **transparent rather than removed**, because taking it away
would leave that one row a pixel shorter than `ItemSize` and Virtualize requires every rendered row to be
the same height.

**Field order is heading, control, error, description - and that order is load-bearing.** With the
description above the control, a field that had one pushed its input below the fields that did not, and
nothing lined up across a row of the search form. `FormField` fixes the order in one place. Date inputs are
styled with the others for the same reason: left native they sat two pixels off their row.

**A related pair of filters must never straddle a row.** The form's columns come from
`repeat(auto-fit, minmax(240px, 1fr))`, so the count changes with the window, and a pair flowing with
everything else broke apart whenever it happened to begin in the last column - "Size from" ending one row
and "Size to" starting the next. `FieldPair` takes `grid-column: 1 / -1` and splits that row itself:
**`1 / -1` rather than `span 2`**, because a span of 2 forces a second column into existence on a form only
wide enough for one, which is exactly the case the rule has to survive. `grid-template-columns: subgrid`
behind an `@supports` then lands the two halves on the form's own tracks so they are as wide as every other
field; the fallback splits the row evenly, which still holds the pair together. Verified at three widths:
side by side at 3 and 2 columns, stacked at 1. One cosmetic consequence, left alone deliberately: a
full-width pair can leave a hole beside the field before it, and filling it would need
`grid-auto-flow: dense`, which reorders unrelated fields.

`MediaFormat` (not a component) holds the formatting: `Megabytes`, `Duration`, `Bitrate`, `Number`. Use it
rather than formatting inline - the raw `TimeSpan.ToString()` renders a half-minute clip as
`00:00:29.9600000`, which is the defect it was written for, and it returns `-` for an absent value so a
table never shows an empty cell.

### Seams that exist only so the admin UI can reach the host

`DlnaServer.Admin` references **only Core and Persistence** - that is the entity/DTO boundary working as
intended, and it is why anything the UI needs from the host arrives through an abstraction in
`DlnaServer.Core`. Reach for the existing one before adding a project reference:

| Need | Seam | Lives in |
| --- | --- | --- |
| Byte cache stats, clear it | `IServedFileCache`, `ServedFileCacheReport` | `Core.Delivery` |
| Block or unblock renderer traffic | `IApiBlocker` | `Core.Diagnostics` |
| Is video processing possible? | `IMediaCapabilities` | `Core.Diagnostics` |
| GENA subscriptions | `ISubscriptionStore`, `EventSubscription` | `Core.Gena` |
| Write `config.json` | `ISettingsWriter` | `Core.Configuration` |
| Restart the host in place | `IRestartSignal` | `Core.Hosting` |
| Rebuild the database on next start | `IDatabaseResetSignal` | `Core.Hosting` |
| Start an indexing pass | `ILibraryScanSignal` | `Core.Hosting` |
| Serialise a circuit's database work | `IAdminOperationGate` | `Core.Hosting` |
| Empty the index | `IIndexMaintenance` | `DlnaServer.Persistence` (public) |

The last row is the exception that proves the rule: emptying the index is persistence's own business and
Admin already references Persistence, so it needs no Core hop.

### A pinned panel costs the results whatever height it has

Which is why the search filters collapse. `CollapsiblePanel` folds its body away and leaves the heading, a
summary of what is still being filtered on, and its `Footer`. Measured on the file search: **583px open,
114px shut** - 469px handed back to the results, on a form that had grown to nine filters plus two pairs.

Three things it gets right, and any replacement has to keep:

- **The filters are hidden, not cleared.** Searching while the panel is shut still applies them - the state
  lives in the page's criteria object, and only the markup is conditional. Verified by searching collapsed
  and reopening to find every value still set.
- **The `Footer` sits outside the fold**, so Search, Reset and the result count stay reachable. Collapsing
  the filters must not also hide the way to run them.
- **The heading is a `<button>` inside the `<h2>`**, so it is reachable by keyboard and carries
  `aria-expanded`. It has to shed the global button styling to look like the heading it replaces.

`StartCollapsed` is read once in `OnInitialized` rather than on every parameter set: re-reading it would
reopen a panel the operator had just shut.

`Expand()` is the mirror of `Collapse()`, added 2026-09-12 so Reset can reopen the filters - see section 6m
for why that reverses the decision recorded in 6k. Both render themselves, because the fold is internal
state and a parent re-render does not reach a child whose parameters have not changed.

### Clearing versus recreating - the distinction is a schema flag, not a delete

Worth reading before touching either. **Discarding a metadata or thumbnail record is what schedules the
background pass to produce it again** - the pending-work query selects rows whose stamp no longer matches
their content stamp, so a null stamp *is* the request. That makes "clear" and "recreate" identical unless
something else separates them, and `MediaFileEntity.IsMetadataSuppressed` / `IsThumbnailSuppressed` is that
something: `GetPendingProcessingAsync` skips a suppressed row.

- **Clear** discards and sets the flag - the file stays without metadata or a thumbnail until asked.
- **Recreate** discards and lifts the flag - the pass produces it again.
- The two dimensions are independent: clearing metadata leaves a file queued for thumbnail work, so a test
  asserting "nothing pending" after clearing one of them is wrong. Assert the flag, or clear both.
- The whole-library actions on the Maintenance page **lift** suppression, deliberately: "recreate
  everything" that quietly skipped the files an operator had cleared would read as a failure.

### Test traps found the hard way

- **`AddRangeAsync` does not order its result.** Neither repository's version has an `OrderBy`, so the rows
  come back in whatever order the projection produced - *not* the order they were passed in. A test doing
  `stored[0]` with more than one row is silently flaky; resolve by path instead
  (`GetByPathAsync`). Cost an afternoon's confusion when a language-filter test failed for the wrong reason.
- **`GetPendingProcessingAsync` is an OR across metadata and thumbnails.** Clearing one of them leaves the
  file queued for the other, so "nothing is pending" is the wrong assertion after clearing metadata alone.
  Assert the suppression flag, or clear both.
- **A path prefix built from `Path.DirectorySeparatorChar` matches nothing on a foreign database.** The
  recursive directory scope uses the separator **read out of the stored path**; the host's separator is not
  necessarily the row's. Caught only because the unit tests run on Windows against `/media/...` fixtures -
  on the NAS both are `/` and it would have looked correct forever. The read-time subtree test
  (`MediaDirectoryRepository.ExcludeWithoutVisibleMedia`) cannot read the stored separator at all, being
  SQL, so it tries **both** - see section 6c.
- **A test for an exemption has to be placed where the general rule could actually reach it.** A test that
  an excluded folder survives a rescan passed against genuinely broken code, because it put the excluded
  folder directly under the source root - which is never pruned - so the exemption was never under
  pressure. Running the server on a deeper tree showed the parent being pruned and cascading the "exempt"
  subtree away with it. After writing such a test, ask: *what in this arrangement makes the rule not
  apply anyway?* If the answer is anything but the exemption itself, the test proves nothing. Section 6c.
- **`ExecuteDelete` does not count rows a cascade removed.** SQLite implements a foreign-key cascade as a
  trigger and trigger-deleted rows never reach `changes()`, so `DELETE FROM Directories` over a parent and
  its child returns 1. Count before deleting when the number is going to be shown to anyone. Section 6e.
- **Writing a fake `-wal`/`-shm` beside a test database needs `SqliteConnection.ClearAllPools()` first.**
  `Pooling=True` means the seed context's connection outlives the context, and on Windows that open handle
  fails the write outright. The corrupt-database test already did this; the reset test had to learn it.
- **Adding a constructor parameter to a service that tests new up directly is a wide diff.** Giving
  `DatabaseInitializer` its reset signal touched ten call sites in one fixture. Not a reason to avoid it -
  just budget for it, and remember `TryAddSingleton` in the module keeps the tests' own containers working
  without letting the module's default shadow the host's instance.

### Local development environment, as left on 2026-09-02

- **ffmpeg and ffprobe are downloaded** into `src/DlnaServer.Host/bin/Debug/net8.0/ffmpeg/` - the
  provisioner fetches them on first use, once. That is what makes local metadata extraction testable.
- **A three-dub test file** can be synthesised with the ffmpeg that is now there, which is how multiple
  audio streams were verified without a real film:
  ```bash
  ffmpeg -y -f lavfi -i "testsrc=size=320x240:rate=10:duration=3"     -f lavfi -i "sine=frequency=440:duration=3" -f lavfi -i "sine=frequency=660:duration=3"     -map 0:v -map 1:a -map 2:a -c:v libx264 -c:a aac     -metadata:s:a:0 language=eng -metadata:s:a:0 title="English original" -disposition:a:0 default     -metadata:s:a:1 language=ces -metadata:s:a:1 title="Czech dub" Dubbed.mkv
  ```
- **A small real video** can be pulled off the running NAS rather than hunted for:
  `curl -o sample.mkv http://192.168.1.100:26852/fileserver/file/<publicId>`.
- Scratch libraries were built under the session scratchpad; nothing in the repository depends on them.

#### Running a throwaway instance that touches nothing in the repository

The command under section 8's "Watch out for" overrides only the source folders and the ports, so it still
writes `src/DlnaServer.Host/dlna.sqlite` and still points the thumbnail cache at the NAS path. Overriding
those two as well gives a run that leaves no trace, which is what makes it safe to test destructive
actions - a reset, a rescan, a wipe - repeatedly:

```bash
cd src/DlnaServer.Host
export ConnectionStrings__DlnaDatabase="Data Source=/tmp/probe.sqlite;Pooling=True;Default Timeout=30;"
export Dlna__Library__SourceFolders__0="/tmp/media"
export Dlna__Library__ExcludeFolders__0=".@__thumb"
export Dlna__Library__ExcludeFolders__1="@Recycle"
export Dlna__Thumbnails__CacheDirectory="/tmp/thumbs"
export Dlna__Server__Port=27851 Dlna__Server__AdminPort=27852
dotnet run --project . --no-build
```

Stop it with `curl -X POST http://localhost:27851/manage/stop` - `/manage` is bound to the **media** port,
so calling stop on the admin port answers 404 and leaves the process holding a lock on the executable,
which then fails the next build with `MSB3027`. Note the environment-variable list binding **also** appends
to a non-empty default, so it is subject to the same trap as `config.json` - see section 6c item 3.

#### A probe tree that exercises every filtering branch at once

Six files, and each one is there for a reason. This is the fixture that found the pruning cascade:

```
media/movies/action/film.mkv     indexed, visible
media/movies/.@__thumb/film.mkv.jpg   skipped by the scanner (excluded segment)
media/@Recycle/gone.mkv          skipped by the scanner (excluded segment)
media/extras/notes.txt           folder NOT indexed - holds files but no media
media/empty/nested/deeper/       folder NOT indexed - nothing anywhere beneath it
media/Films/Private/hidden.mkv   indexed, then hidden by adding an exclusion on a second run
```

A first run must report **2 files in 5 directories**; the old volume-wide walk would have said 9
directories. Then add `Private` to `ExcludeFolders` and restart: the scan must report `-0 file(s) and -0
directory(ies) removed` (hiding is not deleting) while the root's children drop to `['movies']` alone
(`Films` now leads to nothing visible). Use a **segment** name, not `Films/Private` - the read-side
substring test is separator-sensitive and a forward slash matches nothing against Windows-stored paths.

Random bytes are fine for the `.mkv`s - ffprobe fails on them, which conveniently also exercises the
keep-going-through-a-per-file-failure path.

#### Seeing the ffmpeg-unavailable states locally

The dashboard warning needs `IMediaCapabilities.IsVideoProcessingAvailable` to be `false`, and locally the
binaries are present, so:

1. Rename `src/DlnaServer.Host/bin/Debug/net8.0/ffmpeg` aside. **The provisioner recreates the folder
   empty on the next run**, so restoring means deleting that empty one first, then renaming back.
2. Start the server. The dashboard is still silent - correct, and the third state working: nothing has
   needed ffmpeg yet, so the answer is `null`.
3. `curl -X POST http://localhost:27851/manage/recreateAllFilesInfo` to make the processing pass ask.
   `ffmpeg is unavailable` appears in the log and the warning appears on the dashboard.

### Blazor binding traps

- **`@bind` on a `<select>` does not round-trip a `bool?`.** The chosen value is discarded and the
  control snaps back to its empty option, so a tri-state filter silently never applies. Found on the file
  search's *Details read* / *Preview made* filters, and only in a browser: five repository tests covering
  the query all passed while the UI filtered nothing. A nullable **enum** binds correctly - which is why
  the *Kind* and *Exact file type* filters on the same page always worked - so model a tri-state as
  `enum { Yes = 1, No = 2 }` plus null and convert at the edge.
- **`Settings.razor`'s `Clone` is a reflective SHALLOW copy.** Every collection property on the cloned
  options - `SourceFolders`, `ExcludeFolders`, `MediaFileExtensions` - is the *same object* the running
  server holds, so binding an editor straight to one rewrites live settings keystroke by keystroke, which
  is exactly what the "a copy, not the live options" comment above it exists to prevent. Edit a local
  copy and assign a fresh collection on save, the way the folder text areas already do.

### The graphify knowledge graph, installed 2026-09-17

`graphify` parses the tree into a knowledge graph - nodes for files, types and methods, edges for
`calls`, `implements` and `contains`, each carrying a `source_file` and a line - and answers "what
calls this", "what breaks if I change this" and "how does A reach B" without opening a file. It is
**AST-only**: tree-sitter parsing behind a SHA256 cache, no LLM, no token cost. The CLI is the PyPI
package **`graphifyy`** (two y's), installed with `uv tool install graphifyy`; plain `pip` leaves the
executable off PATH on this box.

- `graphify update .` from the repository root refreshes it. A full build is roughly four minutes,
  an incremental one seconds, and deleted files are pruned automatically.
- `graphify query "..."` / `explain "X"` / `path "A" "B"` / `affected "X"` / `god-nodes` read it.
  No `--graph` argument is needed from the root.
- `.claude/skills/graphify/SKILL.md` carries the full procedure, and `/graphify` invokes it.
- Output is `graphify-out/` - `graph.json`, `GRAPH_REPORT.md` for hubs and communities, and an
  interactive `graph.html`. All of it is derived data; deleting it costs one rebuild and nothing else.

**The build runs from the repository root**, and that is not optional. Every default in the tool -
the CLI's `--graph`, and the `PreToolUse` hook-guard, which has no `--graph` flag at all - resolves
`graphify-out/` against the working directory, which is the repository root. Building it anywhere
else leaves the hook silently inert and forces an explicit `--graph` on every query. What limits the
scope is `.graphifyignore` at the root, which excludes build output, generated code and binaries.

Three traps, each hit once already:

- **A non-zero exit code means nothing.** Every graphify invocation returns 255 here, `--help`
  included. Judge success by the output - the `Rebuilt: N nodes, E edges` line, or the answer text.
- **This file is in the graph.** Its headings appear as nodes joined by `INFERRED` `references`
  edges - useful for "which milestone covers X", misleading if read as a code edge. Treat
  `EXTRACTED` as fact, `INFERRED` as a lead, `AMBIGUOUS` as verify-first.
- **Documentation changes are not picked up** by the AST-only build. That needs the semantic
  pipeline, which costs tokens and is deliberately not configured.

The hook-guard lives in `.claude/settings.local.json` - the personal, git-ignored settings file - and
matches `Read|Glob` only. A `Bash` matcher would tax every build and test run for no navigation
value, and Claude Code snapshots hooks at session start, so an edit there takes effect on the next
session rather than the current one.

### Tooling traps, all hit at least once
- **Heredocs above roughly 8 KB get truncated** by the shell tool, producing an unterminated-quote error.
  Write large files with the Write tool.
- **Files can silently revert** if an editor holds a stale buffer. `DlnaOptions.cs` and
  `IMediaFileRepository.cs` both reverted mid-session after building green. Verify content on disk after
  writing, and close the solution in Rider while a session is running.
- **`dotnet run --no-build` uses the Host's existing output.** Rebuilding only a dependency is not enough -
  the Host copy is stale and you will debug behaviour that no longer exists. Build the solution.
- **Stop the server before rebuilding.** A running instance locks `DlnaServer.Host.exe` and the build
  emits `MSB3026` retries instead of failing clearly.
- **`obj` can corrupt** with `CS1504: could not be opened -- The parameter is incorrect`. Delete every
  `bin` and `obj`, restore, rebuild.
- EF migrations are marked `generated_code = true` in `.editorconfig` so analyzers skip them.
- **Restoring a file with `mv` backdates it, and MSBuild then skips the rebuild.** `mv backup source`
  carries the backup's older timestamp, so the project looks up to date and `dotnet test --no-build`
  runs the *previous* assembly - which during a mutation check reads as "the defect is still there" long
  after the source has been restored. It cost a wrong diagnosis on 2026-09-04. Restore by rewriting the
  file (`cat backup > source`) or `touch` it afterwards, and treat a surprising mutation result as a
  build-freshness question first. Sibling of the 2026-09-04 trap 1 in section 7.

### Recurring checks - do these, they have each caught a real defect
1. **Grep new configuration properties for readers.** `SlowQueryThresholdInMilliseconds` was declared,
   documented and read by nothing - the same dead-setting defect flagged in the reference's
   `PRAGMA cache_size`. Run: `grep -rn "<PropertyName>" --include="*.cs" src/ | grep -v Options.cs`
2. **Run it, do not just test it.** Every milestone so far has produced at least one bug that tests
   passed over: directories with `parent=None`, config overriding environment variables, duplicate file
   inserts from overlapping source folders, thumbnails failing on a forced Skia colour type, SOAP
   arguments binding to null, unbracketed IPv6 URLs.
3. **Read the reference before changing its behaviour.** It is in production; its oddities are usually
   load-bearing.
4. **Measure memory after each milestone** - section 3b.
5. **Grep new `[LoggerMessage]` levels against their own XML doc.** Two declarations now say one thing in
   prose and another in the attribute (`FileServerController.LogServingRange`,
   `AvTransportService.LogActionRequested`), and the cost is a 32 MB log in a day. The doc is usually the
   considered decision and the attribute the accident.
6. **Grep new failure flags for a clearing path, not just a setting one.** `IsExcludedFromCache` is set in
   one place and cleared nowhere, which is the write-only-flag defect the reference avoids for the same
   field. Sibling of recurring check 1: a flag with no reader is dead, a flag with no reset is a trap.
7. **A rendered URL is not a working URL.** Checking that markup contains the right `src` proved
   nothing: the thumbnail identifier was wrong and every image 404ed while the HTML looked perfect. Fetch
   the URL and read the status code. Same class as recurring check 2 - "run it, do not just test it" - one
   level further in.
8. **A guard that compiles is not a guard that runs.** An endpoint filter on `MapRazorComponents`, and
   `UseAntiforgery()` before `UseRouting()`, both compiled and both did nothing. Anything whose whole job
   is to refuse a request has to be *observed refusing one* - `curl` the surface it protects on the port
   it should not answer on.
9. **Update this file in the same session as the work.** The 2026-09-01 evening session shipped eleven
   changed files - `CustomEnvelopeMessage` among them, the fix that made the television work - and left no
   trace here, so the next session began by reverse-engineering its own recent history from file
   timestamps. Section 4, section 6 and the header date are part of the change, not paperwork after it.

### Traps carried forward from the 2026-09-04 session

That session's own file was folded into this one on 2026-09-08. These six were separate from the
round-1 trap list, which is in section 7b.

1. **`dotnet test --no-build` silently runs the PREVIOUS assembly when the build failed.** A mutation
   check reported "7 passed" against a defect it should have caught, because the build had 10 errors and
   the old DLL was still on disc. Always check the build result before trusting a `--no-build` run -
   especially a mutation check, where a false pass is the whole risk.
2. **EF Core cannot translate a call to a private helper inside an expression tree.** Extracting the
   wrapped-path expression into a `Wrap(...)` method compiled cleanly and would have thrown, or fallen
   back to client evaluation and pulled every row. The expression has to be written out at each `Where`.
3. **`LibraryOptions.ExcludeFolders` is `IList<string>`, and `IList<T>` does not convert to
   `IReadOnlyList<T>`.** Anything sharing a predicate with `PathExclusion` has to match or materialise.
4. **Git Bash rewrites environment values that look like absolute paths.** Setting
   `Dlna__Library__ExcludeFolders__0="/Private/"` arrived as `C:/Program Files/Git/Private/` and the test
   proved nothing. Export `MSYS_NO_PATHCONV=1` for any run that passes a POSIX-looking value.
5. **A running server locks `DlnaServer.Admin.dll`**, so a rebuild fails with `MSB3027`/`MSB3021` naming
   the Host process. Stop it with `curl -X POST http://localhost:<mediaPort>/manage/stop` first -
   `/manage` is on the **media** port, not the admin one.
6. **The pre-bash guard blocks recursive-force deletes.** Delete the files individually, or remove the
   directory once it is empty.

### Decisions taken 2026-09-04 - do NOT re-ask

| Question | Answer |
|---|---|
| The four technical admin screens (memory panel, file cache page, subscriptions, preview stream tables) | **Keep every figure, relabel only.** Not reduced, not hidden |
| Where to fix the validator's leaked property names | **Rewrite the validator's messages** in operator language, not map them in the page |
| What replaces the word "renderer" | **"television"** |
| The folder names in the live config and the logs | **Delete the logs, leave `publishNAS/config.json` alone** |

Earlier standing decisions are listed in section 7b. **Reporting one of those as a defect is a false
positive.**

### 24 previews that have permanently given up - STILL OPEN

Read from the live NAS database on 2026-09-04, of 25,504 files. Every one has
`thumbnailFailureCount = 3`: the processor backs off after three attempts by design, so **they will
never be retried on their own**.

| What | Count | Note |
|---|---|---|
| Ordinary JPEGs, 1-2 MB (`IMG_0796`-`IMG_0928`, 2012) | 20 | The real cluster. Small files, so not a size limit |
| `*_Pano.jpg`, 6-8 MB | 2, each listed twice | Duplicated across two folders in the library |
| `Astellia_Combine.png` | 1 | **401 MB PNG** - the plausible outlier |
| Two videos (`ovecka.shaun.4.avi` 699 MB, and a 0-byte file) | 2 | Failed metadata as well |

*Recreate preview* on a file's preview page lifts suppression and resets the count, and resize and
encode failures now log separately - so the next attempt names its own reason.

### Browse latency baseline, measured on the NAS 2026-09-04

Server-side **p50 7.6 ms, p90 11.4 ms, p99 31.4 ms**, 1 call over 50 ms in 272 - against the
reference's **p50 16.8 / p90 123.3** measured the same way.

## 7b. Standing decisions, open work and traps

Originally the live remainder of the review documents deleted on 2026-09-08 - the whole of `docs/`, both
`REVIEW-*-FIXES.md` worklists and both session records. Their *history* was the point of deleting them
and is not reproduced; what survived is here, and nothing else points at those files any more.

**It has since become the standing list for the project rather than an archive of those documents.**
Decisions 22-27 and the second traps list come from the 2026-09-08 fix pass (section 6i), not from
anything deleted. Anything decided in future belongs here too - the value of the section is that it is
one place, and that reporting something in it as a defect is a known false positive.

### Standing decisions — reporting one of these as a defect is a FALSE POSITIVE

From the round-2 fact sheet, decided by the maintainer with reasons:

1. **No authentication anywhere.** LAN appliance, by design. Do not propose adding auth.
2. **`appsettings.json` reads `"AllowedHosts": "*"` on purpose.** That value is the SIGNAL that makes
   `AllowedHostsDefaults` generate the real allowlist at startup. It is not an unfixed wildcard.
3. **The media byte-cache machinery is deliberately INERT, not dead.** `MediaCacheBacklog`,
   `MediaCacheFillHostedService` and `CachedContentClass`'s Media arm are kept although the shipped
   32 MB per-file limit admits no film. Chosen over the review's recommendation to delete them.
4. **`Thumbnails.CacheDirectory` and the central-cache branch are unreachable and kept**, marked as such.
5. **`!= null` inside an EF projection or predicate is CORRECT** — `is null` is a compile error there.
6. **Block-scoped namespaces are required**; public protocol contracts carry XML docs on properties.
7. **The first-fill index-date rule is deliberately not gated on `UseFileCreationDateTime`.**
8. **`Max Pool Size` was tried and reverted** — not a valid `Microsoft.Data.Sqlite` keyword (trap 12).
9. **`IsSourceRoot`'s index is kept** — it is used, as the filter in `GetSourceRootsAsync`.
10. **The admin listings page the DATA** rather than using `Virtualize ItemsProvider`.
11. **The byte cache on this NAS stays at 5120 / 512, by choice.** 512 MB per file re-admits films, which
    is the 5,073 MB mechanism — that is understood and intended *there*, while the shipped default stays
    256 / 32. It has been "corrected" twice already, both times wrongly. Do not raise it as drift.

Decided 2026-09-03, and previously in a section of the round-1 record that had already been lost:

12. **Whole-file buffering in the byte cache survives, with shipped limits that admit no film.** The open
    question was whether it should survive at all; the review wanted the machinery deleted. See 3 above.
13. **The code defaults match the shipped `config.json`.** They were 1024 / 512 against a shipped
    256 / 32, so every deployment arriving without a config file silently ran the configuration measured
    at 5,073 MB.

Decided 2026-09-08, while closing the last of those documents:

14. **The live NAS `config.json` runs `Database.MemoryMapLimitInMegabytes: 64` and
    `CacheSizeInMegabytes: 8`, by the operator's choice**, where the tree ships `0` and `2`. Section 4
    credits `mmap 128 → 0` and `cache 32 → 2` with the 563.4 → 177.7 MB drop, so this reads like drift
    and is not. The sibling of decision 11, and the same instruction: do not raise it, and do not
    "correct" the live file.
15. **`transferMode.dlna.org` follows the flags, not the other way round.** `ContentFeaturesFor` sends
    the reference's `Image ? Interactive : Streaming` and **must not be narrowed** -
    `BrowseItemMapper.GetResourceProtocolInfo` asks exactly that one question, and these values are
    matched literally by televisions validated against them. It was `ResolveTransferMode` that moved, so
    `Subtitle` and `Unknown` now echo `Streaming`, which is what their own flags always claimed. Both
    sides read `DlnaProtocolInfo.IsStreamed` now, so they cannot disagree again.
16. **Scanning infers a MIME from the built-in catalog when configuration names no mapping**, and the
    fallback admits **video, audio and images only** (`DlnaMimeCatalog.IsPresentableMedia`). Subtitles are
    deliberately excluded: the catalog knows `.srt`, `.vtt`, `.sub` and `.ttml`, and indexing them would
    put every sidecar beside the films as a `Generic` item on the television. `ConnectionManager`'s
    `Source` list widened to match, deliberately over-approximating - under-claiming a MIME costs
    playback, over-claiming costs nothing. Configuration still wins outright, which is what keeps
    `.mp3 → audio/mp4` working.

    **Know the size of this before the next scan: 137 extensions become media that were not.** Against
    the shipped 14, the catalog adds 137 video/audio/image extensions - and most of them are the point
    (`.wav`, `.flac`, `.ogg`, `.m4a`, `.aac`, `.webm`, `.ts`, `.mka`, `.gif`, `.bmp`, `.tif`, `.webp`,
    `.mid` were all silently not media before, on a real library). A handful are not media in any useful
    sense and will appear on a television if the library holds them: **`.m3u`** (a playlist, classified
    Audio), **`.svg` / `.ico` / `.wmf`** (icons and vector art - `.wmf` is classified *Video*),
    **`.dwg` / `.dxf` / `.svf`** (CAD drawings, classified Image), and the ambiguous **`.pm`**, **`.la`**
    and **`.ts`**, which are also a Perl module, a libtool archive and a TypeScript file. Nothing has
    been narrowed to exclude them, because that list is a judgment call rather than a defect - if any of
    them shows up in a listing, `Library.ExcludeFolders` does not help and the right lever is a deny-list
    beside `IsPresentableMedia`. **The first scan after deploying this will insert a lot of rows.**
17. **`IsUnderFirstFillRoot` comparing `StringComparison.Ordinal` is CORRECT.** The dedup pass reported it
    as a defect; it is not. Source folders are compared ordinally on purpose in
    `NormaliseSourceFolders` and `IsInsideAnySourceFolder`, and `MediaDirectoryEntityConfiguration` makes
    `FullPath` case-sensitive for the same reason - production is Linux, where two paths differing only in
    case are two directories. The case-insensitive rule belongs to *exclusion entries*, which are a
    different thing.
18. **`IMediaFileRepository.AddRangeAsync` is kept although no production path calls it** (W6). It is how
    a test seeds rows and then reads back the identifiers the database assigned, which around thirty
    fixtures rely on; retiring it buys nothing at runtime and costs a rewrite of all of them. Its doc says
    so now. Anything that ships uses `AddRangeReturningCountAsync`.
19. **The SQL exclusion predicate stays as it is** (W10). Its per-row `replace()` and two concats were
    never measured as a cost, and the proposed replacement adds a subquery to the browse path where
    `ExcludeWithoutVisibleMedia` depends on a covering-index range seek. Evidence for leaving it: the NAS
    browse baseline is p50 7.6 ms / p99 31.4 ms, `SlowQueryInterceptor` is registered at a 50 ms
    threshold, and a full day of live logs over 25,532 files holds **zero** "Slow database command"
    entries. Re-open it only with a measurement that contradicts that.
20. **`.avi` maps to `video/x-msvideo` and that is CORRECT - do not "fix" it.** Investigated 2026-09-08
    because an AVI would not play in the admin preview and a wrong MIME was the obvious suspect. It is
    not wrong, and no MIME could be: **Chrome ignores the declared `Content-Type` for media and sniffs
    the bytes.** Proved with a natural experiment already in the library - the LG workaround maps
    `.mp3 → AudioMp4`, so a real MP3 (`ID3` header) is served as `audio/mp4`, and it plays to
    `readyState 4` with a 2356 s duration. Meanwhile every spelling of the AVI type returns `""` from
    `canPlayType`: `video/x-msvideo`, `video/avi`, `video/vnd.avi`, `video/msvideo`. Chrome has no AVI
    demuxer, and the error is `DEMUXER_ERROR_COULD_NOT_OPEN: FFmpegDemuxer: open context failed`.
    Also ruled out by measurement, not reasoning: delivery (206, correct `Content-Range` over
    734,457,166 bytes, intact `RIFF....AVI` payload, and an MP4 loading through the same endpoint on the
    same page seconds later), file size (105 MB to 2.64 GB all fail), non-ASCII filenames, the byte cache
    (warmed a 105 MB AVI into memory and confirmed it in `/manage/filecache` - identical failure), and
    mislabelled containers (44 of 45 sampled `.avi` files are genuine `RIFF....AVI`).
    **The reference ships the same value** (`ServerConfig.cs:100`), which is what televisions were
    validated against; IANA registers `video/vnd.avi` and `video/x-msvideo` is the unregistered
    conventional alias that Apache and nginx ship. Changing it gains nothing and risks the one path that
    works. `DLNA.ORG_PN=MSVIDEO` is likewise not a real DLNA profile - the spec has none for AVI, the
    reference used the string anyway, and sets play AVI on their own codec support. **If a television
    ever refuses an AVI**, blanking `ProfileName` for `.avi` is the lever (6f: a blank and a null profile
    are identical on the wire). Televisions are unaffected by any of this - it is a browser limit.
21. **`ovecka.shaun.3.avi` is corrupt on disc, not mishandled.** 733 MB, and its first bytes are
    `d8ddb2ce72e5718c` - no known container signature. ffprobe reported `608x352 mpeg4` but
    `duration 00:00:00` and `bitrate 0`. Its sibling `ovecka.shaun.4.avi` is already in the
    "24 previews that have permanently given up" table. Nothing to fix in the server.

Decided 2026-09-08, during the fix pass for the third `/review-all --full` (section 6i):

22. **`/manage` living on the MEDIA port is deliberate, and the compose overlay's "never publish the
    media port" advice was describing a control that does not exist.** `docker-compose.yml` mandates
    `network_mode: host` because SSDP needs multicast, and `Program.cs` binds both ports with
    `ListenAnyIP` - so the media port is on every interface and there is nothing to publish or withhold.
    What made that dangerous was `ADMIN_HOSTNAME`: `HostFilteringOptions` is **process-wide**, so
    widening `AllowedHosts` for the admin pages widened it for `/manage` too, and the hostname is in the
    public Certificate Transparency logs. `RejectRemoteManagementEndpointFilter` is the control now.
    Both documents were corrected. **Do not re-raise the absence of authentication** - decision 1 still
    holds; this was about a specific documented setup contradicting itself.
23. **`ServerOptions` has no bind address, and is not getting one.** Considered as the fix for the above
    and rejected: binding one LAN address breaks discovery on a multi-homed box, and it adds a setting
    the validator and the Settings page both have to learn. The endpoint filter costs neither.
24. **A private or internal `async` helper here takes a REQUIRED `CancellationToken`.** See section 7.
    Roughly forty of them; every one is already called with a real token.
25. **The `DLNA.ORG_OP` flag-bit naming is CORRECT - a review reported it as inverted and was wrong.**
    `Format` renders `((short)operation).ToString("B2")` and the wire form is `<byte-seek><time-seek>`,
    so `TimeSeekSupported = 1 << 0` renders `01` (time-seek, correct) and `ByteSeekSupported = 1 << 1`
    renders `10` (byte-seek, correct). **Renaming them would introduce the inversion the finding claimed
    to fix.** Raised by a reviewer running without semantic analysis; verified against the emitted value
    before it was rejected.
26. **A file moved INTO an excluded folder has its row REMOVED, and that is not the same rule as
    decision-by-configuration hiding.** When a folder already indexed becomes excluded by a config
    change its rows survive, so un-hiding is free - `IndexAsync_LeavesAnExcludedFoldersRowsInPlace`
    pins that. When a file *moves* into an already-excluded folder the destination is never enumerated,
    so nothing can re-point the row and reconciliation finds the old path definitely absent. Removing it
    is right: the operator deleted the file. A consequence worth knowing is that a round trip out again
    produces a **new `PublicId`**, unlike a move within the library. Three tests in `LibraryIndexerTest`
    cover the three cases.
27. **`GeneratedThumbnail.Content` stays a `byte[]`, exempted by name in `ContractImmutabilityTest`.**
    The array check was added because init-only stops the reference being replaced and not the contents;
    this one instance is allowed because moving the contract to `ReadOnlyMemory<byte>` would add a full
    copy of every generated thumbnail at the persistence boundary, where EF wants an array - in the one
    project whose hard constraint is memory. A **new** array on a DTO still fails the test.

### Open work from the 2026-09-23 batches (6p to 6s)

**Host-level tests are decided but NOT written.** Section 7 records the rule as lifted; the tests do not
exist. Three concrete obstacles were found while scoping them, and they are the reason this is its own
piece of work rather than an afternoon - start from these rather than re-deriving them:

- **`WebApplicationFactory`'s TestServer will not work as-is.** `RequirePortEndpointFilter` gates every
  endpoint on `HttpContext.Connection.LocalPort`, which TestServer leaves at `0`, so every port-filtered
  endpoint answers 404 and the whole suite would appear to pass nothing. The harness needs **real
  Kestrel on real ports**, not the default in-memory server. This is the finding that matters.
- **`Program.Main` is not reusable from a test.** It calls `Directory.SetCurrentDirectory` - which is
  process-global and hostile to parallel fixtures - and owns the restart loop, and `BuildApplication` is
  `private`. Widening it and taking the content root as a parameter is the likely shape.
- **Startup binds SSDP multicast and starts the scanner, the watcher and the cache fill.** A host test
  needs those either pointed at a temporary tree or suppressed, or the suite touches the real network and
  real folders.

Ten to twenty tests remains the right size. `Microsoft.AspNetCore.Mvc.Testing` is already referenced.

**Two measurements are owed and neither can be taken from a development machine:**

- the steady-state / soak memory reading, which needs the NAS and a real workload over hours. It is born
  with its conditions attached - machine, OS, RAM, .NET version, build configuration, dataset,
  measurement time, workload - because the existing `563.4 MB` to `177.7 MB` figures carry none of them
  and cannot be compared against a later run. The question is not "does memory go up" but "does it
  stabilise".
- the served-bytes cache benchmark, 512 MB contiguous against 64 x 8 MB and 32 x 16 MB under constrained
  RAM. **Measurement only** - the cache is not to be changed before there are numbers.

**Smaller, from 6p to 6s:**

- pull request #11 is still open and still red. The fix is committed here; the PR needs the second
  package line pushed onto its branch or closing in favour of this commit.
- `THIRD-PARTY-NOTICES.md` is not copied into the build output, while `release-notes.md` and `LICENSE`
  are. Arguably it should be if the server is ever redistributed.
- `app.log` retention was left at 14 segments with serving now at Information, so on a busy day the count
  limit can reach back less than the seven-day time limit. Raising the count, lowering the per-file cap,
  or accepting the shorter window is an open choice.

### Open work raised by the second review pass, 2026-09-17 (section 6o)

Everything below was found by a `/review-all --full` re-run and deliberately NOT fixed in that batch.
Each says why, so none of it is re-derived as a fresh discovery.

- **Orphaned previews from before `1.1.0917` are never reclaimed, and the delete path is not atomic.**
  One item, two faces. `RemoveByPublicIdsAsync` can only name a preview that still has a thumbnail row,
  so images orphaned by deletions before this version are invisible to it - nothing enumerates
  `Thumbnails.SubFolderName` anywhere in `src/`. The same blind spot reopens for any file deleted while
  the database is absent (between *Recreate database* deleting it at startup and the rescan), and for a
  crash between `ExecuteDeleteAsync` committing and the in-memory path list being walked.
  A two-phase commit between SQLite and the filesystem is not available, and logging per file on a
  25,000-file pass costs more than it is worth. **The one fix that closes all three is a Maintenance
  sweep**: walk each indexed directory's `SubFolderName` folder and delete any `<name>.<ext>` whose
  `<name>` has no row. It can reuse `GetIndexedPageAsync`'s paging and is a single pass.
  `release-notes.md` currently tells the operator to clear those folders by hand, which is honest but is
  the thing the sweep would replace.
- **No television has seen a retyped file's DIDL-Lite.** Retyping rewrites `upnp:class` and
  `res@protocolInfo` together, which is the exact class of change that once hid a library from an LG.
  "VLC is not a test" applies. This is the largest residual risk on the whole feature.
- **The `(IsExcludedFromCache, FullPath)` index question belongs to W9, and wants a PARTIAL index.**
  The *Kept in memory* search in its headline direction (`= 1`, list the barred files) walks the whole
  `FullPath` index - ~25,000 row operations, 30-150 ms warm, seconds cold. Because the predicate is rare,
  the right shape is `HasFilter("IsExcludedFromCache = 1")` over `FullPath`, not a plain composite: it
  holds a handful of rows and costs nothing in write amplification during a first fill. Decide it with
  the two `EXPLAIN QUERY PLAN` statements W9 already requires rather than adding schema blind. Adding a
  migration is safe - see the next item.
- **A third migration applies cleanly; the docs used to imply otherwise.** `DatabaseInitializer` only
  takes the move-aside-and-rebuild path on `SQLITE_CORRUPT`/`SQLITE_NOTADB` (`IsCorruption`). A
  post-squash NAS database gets an ordinary `MigrateAsync` - no data loss, no `PublicId` regeneration, no
  rescan. **But an ordinary migration error (`SQLITE_ERROR`) is not caught**: it propagates,
  `DatabaseInitializerHostedService` stops the host cleanly, and under `restart: unless-stopped` that is
  a crash loop rather than a self-heal. `CLAUDE.md` now says so. Worth a `DatabaseInitializerTest` case
  pinning that a non-corruption `SqliteException` propagates rather than triggering move-aside.
- **`ServedFileCache.Evict` is `TryGetValue` + `Remove` and the cache has `TrackStatistics = true`**, so
  every eviction counts a cache MISS. The new delete path fires one per deleted file, so a mass deletion
  skews the dashboard hit ratio - the one number an operator uses to size the budget. Runtime cost is
  nil. Know it before reading a post-deletion ratio as a cache fault.
- **`ReadBucketedAsync` reads under `CancellationToken.None`**, so a genuinely *hung* filesystem stalls
  the single cache-fill drain thread and delays shutdown. Pre-existing; the exclusion latch never
  protected it, because a hang does not throw and so was never latched.
- **`AdminPageBase` is one component away from being the right home for the gate wrapper.**
  `FileTypeEditor` and `ProcessingActions` both hand-roll inject-`Files`+`Gate` / `_busy` / `try-finally`
  / `Notice` / `OnCompleted`. At two copies that is acceptable duplication. `AdminPageBase`'s own remarks
  record that this wrapper "had already been extracted twice, privately, under two different names, and
  had drifted" - so a **third** hand-rolled copy makes it a Warning, and the home already exists
  (`AdminPageBase` is public, injects the gate, owns `Message`). It would need a result-returning
  `RunGatedAsync<T>`.
- **`MediaFileRepository.SeparatorOf`** is a single-use private wrapper forwarding verbatim to
  `StoredPath.SeparatorOf`, while `AnyUnderPathAsync` calls `StoredPath.SeparatorOf` directly. Two
  spellings of one call. Pre-existing and left alone under the surgical-changes rule.
- **Four reviewers did not run in the second pass** and would have been near-duplicates of ones that did:
  `commented-code-reviewer`, and the `dotnet-claude-kit` code-reviewer / security-auditor /
  performance-analyst. Their native equivalents covered the same ground and found more.

### Open work inherited from those documents

**Only W9 is left here, and it needs a database rather than a patch.** The dead preview player that
sat in this list was fixed the same day - section 6h.

- **W9. The two `Language` indexes may be dead weight. A MEASUREMENT.** Both exist -
  `IX_AudioStreams_Language` and `IX_SubtitleStreams_Language`, one per stream table. Run these against
  a real database; **if neither plan names an index, drop both in a NEW migration** rather than editing a
  generated one:
  ```sql
  EXPLAIN QUERY PLAN
  SELECT DISTINCT a.Language FROM Files f
  JOIN AudioStreams a ON a.MediaFileId = f.Id WHERE a.Language IS NOT NULL;

  EXPLAIN QUERY PLAN
  SELECT f.Id FROM Files f WHERE EXISTS (
    SELECT 1 FROM AudioStreams a
    WHERE a.MediaFileId = f.Id AND a.Language IS NOT NULL AND a.Language IN ('eng','kor'));
  ```
  The migration, should the plans come back with `SCAN`: `dotnet ef migrations add DropLanguageIndexes
  --project src/DlnaServer.Persistence` (no `--startup-project`), body `DROP INDEX IX_AudioStreams_Language`
  and `DROP INDEX IX_SubtitleStreams_Language`. Nothing is dropped on speculation - the search page's
  language filters read both columns.

  **Still open after the 2026-09-08 squash**, and the squash is why it is worth restating: every other
  unused index was removed by making `EntityBaseConfiguration`'s `CreatedUtc` index opt-in, so these two
  are now the only ones in the schema whose keep is in doubt. They survived deliberately. Static analysis
  during that pass traced every production use and found **no query shape that filters or groups the
  stream tables by `Language` without first correlating on `MediaFileId`** - which supports dropping them
  and is still not a query plan. The migration to write, if the plans agree, is now a second one on top
  of `20260908162737_InitialSchema`.

**Owed from section 6n (uploading), 2026-09-15:**

- **A real browser has never driven the file picker.** The live upload that verified the feature was posted
  by `fetch` with a `FormData` from the page's own origin - same endpoint, same filters, same headers, and
  a real Chrome user agent in the log - but the `<input type="file">` element itself was never clicked.
  Everything else in that batch was driven through the browser.
- **Nothing caps a batch.** `Upload.MaxSizeInMegabytes` bounds one file; ten thousand small ones bound
  nothing but the disc.
- **Uploading is as unauthenticated as the rest of the admin surface.** Standing decision 1 (trusted LAN)
  still holds and is not being re-raised - but this is the first endpoint where that decision lets a device
  on the network WRITE to the filesystem rather than read from it. `Upload.Enabled` shipping off, and
  needing a restart to turn on, is the whole control. Worth revisiting if the admin port is ever exposed
  beyond the LAN, which `docker-compose.admin-remote.yml` exists to do.

**Still owed, and neither is code:**

- **A quiet memory reading.** The live figures on 2026-09-08 were taken *during* a metadata pass, so they
  are a loaded reading (section 3b). No restart is needed for the next one - the live byte-cache and
  database values are by choice, per standing decisions 11 and 14.
- **The four DIDL fields on a real television.** They are on the wire: a SOAP Browse against the running
  server on 2026-09-08 returned `nrAudioChannels="2"`, `sampleFrequency="48000"`,
  `<upnp:videoCodec>h264</upnp:videoCodec>` and `<upnp:audioCodec>aac</upnp:audioCodec>`. What is unproven
  is whether a set honours them.
- **The two Blockers from section 6i, run against the application.** Both are config-shaped, and this
  project's own rule is that a green build and a green suite do not verify one of those. The tests cover
  the provider and the monitor; what they cannot cover is the whole pipeline reacting to a real file
  watcher. So: start the server, overwrite `config.json` with `{ broken`, and confirm from the log and
  `/manage/configuration` that the previous values survived **and the watch did not move to the publish
  folder**; repeat with the file deleted and recreated, which is the silent variant an editor save
  reaches; then try `"Port": 0` and confirm Browse, streaming and the Settings page all keep working on
  the last valid snapshot. Also `curl /manage/database` on both ports, and once with a spoofed `Host`
  from a non-private address, to see the new filter refuse it.
- **The migration squash, against a COPY of the live database first.** Confirm the corruption path moves
  it aside cleanly rather than leaving a half-migrated file. The rescan it forces is expected - see 6i.

### Traps from the round-1 fix pass

Traps 0, 0b, 1, 9, 13 and 14 of that list are already covered in section 7 above. These are the rest.

- **CA1862's advice does not survive EF translation.** `lower()` on both sides produced three CA1862
  warnings, and the analyzer's remedy (`Contains` with `StringComparison`) is not translatable. The right
  answer was `EF.Functions.Like` with `EscapeLikeTerm` — SQLite's `LIKE` folds ASCII natively, and the
  escaping stops a `_` in a folder name acting as a wildcard. **Check whether an analyzer's advice
  survives expression-tree translation.**
- **`DirectoryNotFoundException` derives from `IOException`** and must be caught **first** in
  `IsDefinitelyAbsent`, returning `true` — a missing parent means the file really is gone.
- **`List<T>.GetRange` rejects `index > Count` even when `count == 0`.** Clamping only the count is not
  enough.
- **Adding one `<param>` tag obliges all of them (CS1573).** Put the explanation in `<remarks>`.
- **A `required` member on a DTO breaks every construction site.** Prefer a non-required optional
  property when extending a `required` record.
- **An idempotency guard on a scripted patch can silently skip work.** Check the build, not the script's
  output.
- **`dotnet ef migrations add` needs `--project src/DlnaServer.Persistence` and NO `--startup-project`.**
  The Host does not reference `Microsoft.EntityFrameworkCore.Design`; Persistence does, and carries
  `DesignTimeDlnaDbContextFactory`. **And never hand-delete the generated `*.Designer.cs`** — it carries
  the `[Migration]` attribute, without which the migration is silently never applied.
- **`Serilog.Log.Logger` was never assigned** before that work; anything logging through the static `Log`
  went nowhere. Keep it assigned if you add another out-of-DI handler.
- **An MSBuild property in `Directory.Build.props` under a `Condition` on `$(OutputType)` NEVER APPLIES**,
  and looks fine for as long as its value matches the SDK default. `Microsoft.Common.props` imports that
  file *before* the Web SDK defines `OutputType`, so the condition tests an empty string. A
  `runtimeconfig.template.json` entry does not rescue it — where a knob has both, the SDK writes the
  property over the template. **The `.csproj` property body is the only place that reliably wins.**
  Verify by artifact, never by reading source:
  ```powershell
  dotnet msbuild src\DlnaServer.Host\DlnaServer.Host.csproj -getProperty:ServerGarbageCollection
  Get-Content src\DlnaServer.Host\bin\Debug\net8.0\DlnaServer.Host.runtimeconfig.json
  ```
  Delete the generated `runtimeconfig.json` before rebuilding — an incremental build does not always
  regenerate it, which looks exactly like the fix having no effect. This class has bitten the repo three
  times.
- **`Microsoft.Data.Sqlite` has no `Max Pool Size` keyword.** `SqliteConnectionStringBuilder` throws
  inside `DatabaseInitializer.GetDatabasePath()`, so **the server refuses to start** while the build and
  every test stay green. That provider offers `Pooling` on/off and no size control;
  `Database.CacheSizeInMegabytes` is the only lever over the total page cache. **A connection-string
  keyword is a runtime contract, not a compile-time one.**

### Traps from the 2026-09-08 fix pass (section 6i)

- **`context.Ignore = true` on a configuration source does NOT keep the previous values.** It suppresses
  the rethrow. `FileConfigurationProvider.Load(reload: true)` has already replaced `Data` with an empty
  dictionary by the time the handler runs, and raises the change token afterwards either way. **And a
  MISSING file on reload raises no exception at all** - `Load(reload: true)` treats it as optional - so
  the handler never runs and nothing is reported. Note the asymmetry that makes this easy to mis-test:
  `IConfigurationRoot.Reload()` calls `Load(reload: **false**)`, which *does* report a missing
  non-optional file, while the file watcher calls `Load(reload: true)`, which does not.
- **`Lazy<T>` in `ExecutionAndPublication` caches the EXCEPTION, and `OptionsCache` uses that mode.** So
  one options-validation failure is rethrown to every later reader until the change token fires again.
  Anything that throws from inside options validation - not just a validation failure, but an
  `ArgumentException` out of `Path.GetFullPath` - poisons the whole application, because
  `IOptionsMonitor.CurrentValue` is read on every request path there is.
- **A review that is told not to build cannot report that the build is clean.** Twenty reviewers read
  `ServedFileCache.cs` on a day when it did not compile. Run `dotnet build` before trusting any claim
  about warnings - and **delete `obj\Debug` first**, because an incremental build silently skips a
  project and hid a real `CS1570` in this very pass. Same class as the `runtimeconfig.json` trap above.
- **An agent finding can be confidently wrong about the thing it is naming.** The `DLNA.ORG_OP` bits were
  reported as inverted against the spec; they are correct, and renaming them would have introduced the
  inversion. Verify a finding against the emitted value before acting on it, especially when the reviewer
  that raised it had no semantic analysis available. See standing decision 25.
- **A stale number in a doc comment is not harmless.** `FileCacheOptions`' remark still described the
  pre-fix 512/1024 defaults, and a reviewer read it during the audit and reported the cache budget
  reverting to 1 GB - a claim that then had to be chased down and disproved against source. The remark
  now records that it misled someone, which is the cheapest way to stop it happening twice.

---

## 8. How to resume from this file

This file is written to be the only context needed. Work through it in order.

1. **Read this file top to bottom.** Sections 2 (decisions), 3 (memory budget) and 7 (gotchas) are the
   ones that change how you work; the rest is reference.
2. **Read `CLAUDE.md`** for repo conventions and a summary of the reference architecture.
3. **Confirm the baseline is green** before changing anything:
   ```bash
   dotnet build DlnaServer.sln     # expect 0 warnings, 0 errors
   dotnet test  DlnaServer.sln     # note the number; the count keeps moving, do not trust a docmd
   ```
   **Build CLEAN if the warning count matters** - `Remove-Item -Recurse -Force src\*\obj\Debug` first.
   An incremental build skipped a project and hid a real `CS1570` during the section 6i pass, and a
   whole review ran against a tree that did not compile because nobody built it at all.
   If the build fails with `CS1504` or file locks, see section 7 "Tooling traps" first.
4. **Read section 7b, then "Where things stand" at the end of this section.** The milestones are no
   longer the whole picture: the server is deployed and running, and as of 2026-09-08 the review
   backlog is closed apart from W9 and the four items section 6i records as deliberately not applied.
   **Section 6s is the newest work and none of 6n onwards is deployed.** What is left needs a television, a quiet
   server, a database or the application actually running - so
   check first whether the tree has been deployed since 2026-09-08 (**it had not been** when that work
   finished; a metadata pass was running).
5. **Read the named reference sources before changing behaviour.** The reference server, in
   `T:\repos\DLNAServer_Legacy`, is READ-ONLY and is the behavioural specification.
6. Do the work. Keep the build at 0 warnings. Add tests. Then **run the server and exercise the change** -
   see section 7, recurring check 2.
7. **Measure memory** (section 3b) and append the row.
8. **Update `history.md`** - its section 4 status table, a new section 6 entry for the batch, and the
   "Last updated" line. Anything durable the batch decided or any trap it cost goes in THIS file
   instead, in section 7 or 7b.

### Where things stand

**The server is deployed on the QNAP NAS and running, and the LG television now lists and plays content.**
`NasBuild.sh` builds, publishes and starts it cleanly. Milestones 1-8 are complete, the solution builds at
0 warnings, and the work has shifted from writing milestones to fixing what real devices
and real memory readings reveal.

**The NAS is one batch behind the tree.** Checked 2026-09-15 against
`http://192.168.1.100:26853/admin/about`, which is served from the `publishNAS` folder: the page answers,
and it reports **version `1.0.0`**. Both halves of that are evidence. The page existing means the deployed
build carries **section 6m** - About arrived with it on 2026-09-12 - and the version reading `1.0.0`, the
implicit default, means it predates the `<Version>` property added on 2026-09-15. So everything through 6m
is deployed and **section 6n and `1.1.0915` are not**. That one request is the cheapest way to ask the
question again later.

**Deploying is not a routine redeploy, for two separate reasons.** The migration squash (6i) means a
database older than `20260908162737_InitialSchema` fails to migrate, is moved aside as
`dlna.sqlite.corrupt-<date>_<time>` and rebuilt empty - a full rescan and a new `PublicId` for every file,
with thumbnails adopted rather than rebuilt. The **second** migration, `20260915173846_AddUploadDevices`,
is additive and costs nothing on top of that. And the config-shaped fixes still want the live checks at
the end of section 7b's open work, because this project's own rule is that a green suite does not verify
them. After deploying, About reports the version - that is the quickest confirmation the redeploy took.

**What actually fixed the LG television was the SOAP envelope, not the `parentID` defect.**
`DlnaServer.Upnp.Soap.CustomEnvelopeMessage` overrides SoapCore's `CustomMessage` to put
`soap:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/"` back on the envelope, alongside the
`xsd`/`xsi` declarations - copied from the reference, which has always had it. SoapCore's own
`CustomMessage` emits the two xmlns declarations but **not** `encodingStyle`, verified by rendering both.
The `parentID` fix was real and is still correct; it simply was not what the television was rejecting.
Covered by `CustomEnvelopeMessageTest`, which seeds `XmlNamespaceLookup` by reflection because SoapCore's
setter is assembly-internal - that is the only in-process seam short of booting the host.

**Recorded late: the rest of that session's work**, reconstructed from file timestamps because the session
ended without updating this file (see recurring check 7). Alongside `CustomEnvelopeMessage`:
`Library.FileSettleSeconds` (default 30) now defers indexing until a file stops changing, each further
write restarting the window - the reference's fixed per-event delay could not do that;
`ServedFileCache.IsEnabled` clears the payloads when the cache is switched off, which is what makes the
`FileCache.Enabled=false` measurement in section 3b possible without a restart; plus changes to
`FileServerController`, `MediaProcessor`, `ImageThumbnailGenerator` and `Program.cs`. None of it carried a
test - the suite stood at 240 before and after - and `config.json` moved to `/share/Media` with the
`FriendlyName` line removed, so that earlier to-do is done.

Six separate deployment defects were found and fixed by actually deploying - all recorded under
"Deployment traps" in section 7. The lesson worth carrying: **`dotnet build` and `dotnet publish` both
reporting success proves very little.** Two of those defects passed a clean build and only appeared at
runtime, and one of them produced a publish folder missing a third of its assemblies.

#### Deployment state at the end of 2026-09-02 - SUPERSEDED, see the top of this file

**Superseded by "Deployment state at the end of 2026-09-03" at the top of this file.** A deploy on
2026-09-03 replaced everything described below, so do not read this for what the NAS is running. It is
kept for the lesson in its last paragraph, which is still the most useful thing in it: when a UI report
does not match what you measured locally, find out which build the browser is talking to before doubting
either.

The NAS was redeployed **twice** during the session, so what is running there is neither the start nor the
end of it. Everything from the collapsible filter panel onwards is **local only**.

**Deployed and confirmed live on the NAS:** folder search, the favicon, the preview image fix, the pinned
panels, the source-folder Check button, restart/stop on Maintenance, virtualization, the source-folder
re-rooting fix, clear/recreate, multiple audio streams and the language filters.

**Built and verified locally, NOT deployed:** `CollapsiblePanel` on both searches, `FieldPair` and the
`FormField` order change, the folder-list last-row border fix, the green/red confirm pair, and the
source-folders restart note.

How to tell without guessing - fetch the stylesheet and look for a rule only the newer build has:

```bash
curl -s http://192.168.1.100:26853/_content/DlnaServer.Admin/admin.css | grep -c 'collapse-toggle'
```

That is how the "I still see an empty line" report was resolved: the NAS was serving the pre-fix CSS, so
the complaint was true and the fix was real at the same time. **When a UI report does not match what you
measured locally, check which build the browser is actually talking to before doubting either.**

#### Two migrations are pending on the NAS, and one needs a backfill

`ProcessingSuppressionFlags` and `MultipleAudioStreams`. Both are additive with defaults, so the deployed
database upgrades without loss - but **existing files keep their single audio track until their metadata is
read again**, because the extra rows only appear when ffprobe runs. The Library page's *Recreate metadata*,
with "include every subfolder" ticked on a source root, is the way to backfill it.

#### Do these next, in this order

**Read the deployment-state note at the top of this file first** - the NAS is running the middle of
2026-09-03, not the end of it. Items 0 and 0a are historical and complete.

**A. Decide about ffmpeg on the NAS (section 6d).** Nothing else on this list is worth as much. Until the
   binaries are there, a 25,504-file library has no video metadata and never will. Two routes, both
   the maintainer's call: drop `ffmpeg` and `ffprobe` into `publishNAS/ffmpeg/` (and check `NasBuild.sh` does not
   wipe that folder on redeploy), or turn `Thumbnails.DownloadFFmpeg` on once, knowingly, and off again.
   Then *Recreate metadata* on Maintenance, or `POST /manage/recreateAllFilesInfo`, and expect it to take
   a long while. Expect the language lists to stay short even afterwards - a stream contributes a language
   only if it carries the tag, and downloaded `.mp4`s frequently do not.

**B. Deploy the rest of 2026-09-03** - the ffmpeg dashboard warning, the language-filter empty state, and
   the two Maintenance rebuilds. All were driven in a browser against a running local server; none has
   been on the NAS. Confirm which build is answering before drawing any conclusion from what a page looks
   like: two of the previous session's defect reports were raised *because* a deployed build lagged the
   tree.

**C. Then the pre-existing list below**, which is still accurate.

0a. ~~**Deploy the 2026-09-03 fixes and check the four things below.**~~ **DONE 2026-09-03, all four.**
   `-7001 directory(ies) removed` with `-0 file(s)` and 25504 files still indexed; `excludeFolders` down
   to four entries with no repeats; `.@upload_cache` and `.streams` gone from the root's children; and
   the subtree predicate measured on the real library rather than inferred from a plan - see below. The
   deploy also turned up section 6d, which is the more important thing on this list.

   The original checklist, kept because the reasoning is what matters if this is ever re-run:

   1. **The scan's own log line.** The first pass will report a very large `removedDirectories` - the live
      index holds 8,056 directories against 25,504 files, and every folder that leads to no media is now
      removed. **That is the intended cleanup.** `totalFiles` is what proves it went right: it must stay at
      about 25,504. A large drop in *files* means the directory pruning cascaded into content, which is a
      defect, not a cleanup - the excluded-folder exemption is what stands between those two outcomes.
   2. **`/manage/configuration`.** `excludeFolders` must read
      `[".@__thumb", "@Recycle", "Personal", "Films/Private"]` - four entries, no repeats. Then save from the
      settings page and read it again: still four. That is the whole of item 3.
   3. **`/manage/directory/{root}`.** `.@upload_cache` and `.streams` must both be gone from the children.
   4. **`logs/slowQuery.log`.** The subtree predicate's plan was measured on a small test database, and a
      covering-index seek per candidate folder is still a per-folder cost. Browse a few folders from the
      television and check nothing from `MediaDirectoryRepository` shows up there.

0b. **Redeploy, then look at the four things built after the last deployment** - the collapsible filter
   panels, the paired from/to filters, the folder-list last-row fix and the green/red confirm pair. All
   four were verified locally in a browser; none has been seen on the NAS. Two of the session's defects
   were reported *because* a deployed build lagged the tree, so confirm which build is answering before
   drawing any conclusion from what the page looks like.

   Also still not done from item 0: **the thumbnail count**. `/manage/thumbnail` against the real library
   is the cheapest signal that adoption is working - a count well short of the file count means an existing
   `.@__thumb` image is being taken rather than regenerated. Adoption logs at Debug, so the count is the
   only signal at the default level.

0. ~~**RE-CHECK THE DEPLOYED BUILD.**~~ **DONE 2026-09-02.** All three were looked at, and the memory
   instrument answered its question. What was found:

   a. **`/manage/filecache` against the real library - answered, and it settles item 1's diagnosis.**
      The large object heap and the cache's `heldMb` are the same 505 MB, so the memory is live cache
      content and no heap dump is needed. The `paths` listing then showed what no aggregate could: of 111
      entries, 102 were thumbnails, 8 static resources, and **one video accounted for ~100 of the 109 MB**.
      Section 3b's fourth reading carries both samples and the instrument caveat found alongside them - the
      LOH tile reports the *last collection's* size, so on a freshly started server it reads far too low.
      The clear-and-compare step was not needed once the two numbers matched to within a megabyte.

   b. **The admin UI in a real browser - the sidebar is fine, the rest was not.** The pinned sidebar works
      (`position: sticky` holds it at viewport top 0 after scrolling 952 px) and the responsive breakpoint
      and filter grid render correctly. Two real defects turned up that markup inspection could not have
      caught, both now fixed: **the preview image was upscaled**, a 675x897 PNG rendered at 994x1321
      because `.player img` set `width: 100%`; and **nothing served a favicon at all**.

   c. **A photo and a video in the preview page - both correct, with one caveat.** Aspect ratio was
      genuinely preserved (0.7525 rendered against 0.7525 natural), and the video element letterboxes and
      reads its metadata. But with no `max-height` a portrait photo pushed the Next/Previous controls
      ~1300 px down the page. `.player img` now takes only maxima (`max-width` / `max-height: 62vh`, both
      dimensions `auto`), which preserves the ratio the old comment was protecting while capping the height
      the way the video already was and never upscaling past natural size. Verified on a 48x48 icon (stays
      48x48), a 512x512 icon (stays 512x512) and the original portrait PNG (page height 1852 -> 1090).

   The **thumbnail count** check was not done; `/manage/thumbnail` against the real library is still the
   cheapest signal that adoption is working, and it is a one-request answer whenever someone is next on
   the NAS.

1. **The cache rework - decided, not open.** Section 3b's **fifth** reading is the evidence that matters:
   **906 MB in four entries, three films and one icon, every thumbnail evicted.** Restrict the cache to
   thumbnails and static assets. They all sit under the 85,000-byte large-object threshold, so the LOH
   problem ends by construction, and they are what actually gets re-served. The measurement below is worth
   running afterwards as confirmation. The older readings, kept for the reasoning: Section 3 carries the
   2-4 GB production constraint and the maintainer's decision to keep 1024 MB total / 512 MB per file anyway.
   What has to be decided, with a measurement rather than an argument:

   - Does the working set on a **constrained** run stay inside what a 2-4 GB machine can give? Run under
     `DOTNET_GCHeapHardLimit=0x30000000` (768 MB) or `DOTNET_GCHeapHardLimitPercent=20`, **stream a film**,
     and read `/manage/memory`. A reading from the 40 GB test NAS without that limit is not evidence -
     section 3b says so and means it.
   - If it does not hold, the options in order of preference are: cache thumbnails and static assets only
     (all naturally under the 85,000-byte LOH threshold, so the problem disappears by construction);
     lower `MaxFileSizeInMegabytes` so payloads are smaller; or release on eviction the way the reference
     does. **The reference's forced compacting collect turns out to be load-bearing** - 370 MB after
     twelve days against this server's 2991 MB after forty-two minutes - so section 3's "never force a
     GC" rule was written on the wrong evidence and must not be quoted against that option without
     reading the third-reading analysis first.

2. **Backlog items 8 and 12**, both still blocked on the maintainer rather than on work:
   - **8, resource translations**: needs the first admin string worth translating, plus a decision on
     `InvariantGlobalization=true` and `SatelliteResourceLanguages=en` in `Directory.Build.props` - the
     second silently strips any non-English satellite assembly at publish, so translations would work
     locally and never ship.
   - **12, embedded cover art and lyrics**: needs one music video with embedded art and lyrics placed
     somewhere readable and named here. Two unknowns cannot be settled by reading: whether Xabe's
     `IMediaInfo` surfaces a stream's `attached_pic` disposition, and whether the lyrics are a text
     subtitle stream (already mapped) or an ID3 `USLT` tag (nothing reads it).

3. **Two dead references and one loose end**, all small:
   - `AudioStreamInfoEntity` was renamed to `AudioStreamEntity`; nothing else is outstanding from the
     naming pass.
   - The XML documentation files (`DlnaServer.Core.xml` and four siblings) ship to the NAS for nothing.
     `-p:GenerateDocumentationFile=false` at publish removes them; harmless either way.
   - `/manage` has no equivalent of the admin UI's search. Adding one is a thin wrapper over
     `IMediaFileRepository.SearchAsync`, which already exists and is tested.

4. **Then M9** proper - the memory tuning pass measured against section 3's targets - and M10's remaining
   verification against a real television.

#### What the 2026-09-02 admin-UI session added, after closing item 0

Everything below was exercised against a running server before being called done - the two defects item 0
found were both invisible to markup inspection, which is the whole argument for doing it that way.

- **Folder search.** `IMediaDirectoryRepository.SearchAsync` plus `MediaDirectorySearchRequest`
  (`NameContains` / `PathContains`, both case-insensitive, capped by `Take`), and a `Search folders` page.
  The existing search page became **`SearchFiles.razor`** at `/admin/library/search/files`, keeping
  `/admin/library/search` as a second route so old links still resolve; the menu now reads
  *Search folders* then *Search files*. Five repository tests, including the LIKE-wildcard escaping case
  that the file search already had - folder names are as full of underscores as file names are.
- **`.player img` takes only maxima.** See item 0c. This is the fix for the upscaling and the runaway page
  height, and it keeps the ratio the original comment was written to protect.
- **A favicon exists.** `StaticResourceController.GetFavicon` serves the existing 48x48 `small.png` device
  icon at `/favicon.ico` **and** `/admin/favicon.ico`. Two routes because the two ports are two sites to a
  browser and `AdminOrMediaPortEndpointFilter` routes by the `/admin` prefix - the second route is what
  keeps the promise that no admin page names the media port. Verified 200 with `image/png` on both ports,
  and 404 for each path on the other port.
- **Pinned controls, scrolling lists.** `.panel.pinned` (sticky, desktop only) on the file-cache stats and
  both search forms, so a long list scrolls under the controls instead of taking them off screen.
- **Source folders can be checked before they are saved.** `ISourceFolderChecker` (Core) /
  `SourceFolderChecker` (Host) reports **every** path rather than throwing on the first bad one, and
  distinguishes the four failures that look alike: blank, malformed, not absolute, missing, a file rather
  than a folder, and - the one that matters on a NAS - *present but unreadable*, which it finds by reading
  a single directory entry. `Directory.Exists` returns false for a permissions failure, so without that
  step an unreadable share is reported as a missing one. Six tests.
- **Stop and restart on the Maintenance page**, each behind a two-click confirm. They use the same
  `IRestartSignal` + `IHostApplicationLifetime` pair as `/manage/restart` and `/manage/stop`, including
  resetting the signal before a stop. Both verified end to end: restart rebuilt the host **in the same
  process in 0.75 s** (pid and uptime unchanged, "shutting down" and "Application started" 0.75 s apart in
  the log), and stop exited cleanly with both ports released.
- **`<Virtualize>` on the three long lists** - cached paths, folder-search results, and the Library folder
  list, each with `OverscanCount="10"`, a `Placeholder` and (where it can render) `EmptyContent`. Measured
  on a 300-folder library: **302 folders render 22 rows**, and a 200-result search renders 23. See
  "Virtualization" under section 7 for the constraints, which are strict and fail quietly - including why
  `Placeholder` never shows while `Items` is used.

#### Second batch, same session - deployed build checked, then two real bugs and a feature

The rewrite was redeployed mid-session and re-checked against the real 25,501-file library: favicon 200 on
both ports with the isolation intact, folder search returning 200 results as 31 DOM rows with uniform 59.5px
heights despite the real paths, and the Library and file-cache pages behaving. Then:

- **The file cache reading got much sharper, and it settles the memory question.** On the redeployed
  server: **4 entries holding 906.66 MB against a 1024 MB budget - three films and one icon, and
  *zero* thumbnails.** Hits 12, misses 242. The earlier sample had 102 thumbnails cached; they have been
  evicted by films. **The cache is now actively defeating its own purpose** - it exists to stop a disc
  spinning up for a thumbnail that is served constantly, and the films have pushed every one of them out.
  Section 8 item 1's first option (thumbnails and static assets only) is no longer the cheapest option, it
  is the only one that does what the cache is for.
- **Bug: a narrowed source folder never took effect.** Changing `SourceFolders` from `/share/Media` to two
  of its subfolders left the library showing the old folder, and a restart did not help. Two causes, both
  fixed: reconciliation only asked whether a path still existed, never whether configuration still covered
  it; and `IsSourceRoot` was decided once at insert, so a folder already indexed as a child could never
  become a root. `LibraryIndexer.RerootKnownDirectoriesAsync` now re-reads both facts from configuration
  and runs **before** the removal pass, which is what stops the old parent's cascade deleting the new roots
  and their metadata with it. Verified end to end: the old root and an unconfigured sibling were removed,
  the two new roots appeared at the top level, and `+0 added` on that scan proves the surviving files kept
  their rows rather than being deleted and re-indexed. Two tests, including the deep-nesting case.
  **This was also a leak**: files under a folder the operator had stopped sharing carried on being served.
- **Bug: no "back to folder" from a recently-added item.** The root's recently-added tiles link without a
  directory, and the crumbs read only the route parameter - while the rest of the page already fell back to
  the file's own directory. One-line fix, same fallback.
- **Clear and recreate, per file and per folder.** A *This folder* panel on the Library page with an
  "include every subfolder" switch, and the same four actions for a single file on its **preview page** -
  not on the library's tiles, where four buttons per tile buried the pictures. **Clearing and recreating
  are genuinely different**, which needed a schema change: both discard the record, and only
  `IsMetadataSuppressed` / `IsThumbnailSuppressed` (migration `ProcessingSuppressionFlags`, additive with
  defaults) stop the background pass producing it again. `MediaFileScope` carries the selection - one file,
  or one directory with or without its tree - and the four repository operations take one. Recursion is a
  path-prefix match, with the separator read **from the stored path** rather than from
  `Path.DirectorySeparatorChar`; a test on Windows against `/media` paths is what caught that, and on a
  mixed host the host's separator matches nothing, silently.
- **The Maintenance page's vocabulary was contradicting the new one.** Its "Clear all metadata" always
  meant *recreate* all - clearing a stamp is what schedules the pass. Renamed to "Recreate all metadata" /
  "Recreate all thumbnails", and both now lift suppression, so the whole-library action really does mean
  produce everything again.

#### Third batch: several audio tracks per file, language filters, and the admin components

- **A video keeps every audio track.** `MediaFileEntity.Audio` was one-to-one with a **unique index on the
  file key**, so a second dub was not merely dropped by the mapper - the database would have rejected it.
  Now `AudioStreams`, mirroring subtitles: `StreamIndex`, `Title` and `IsDefault` alongside what was there,
  a unique index on `(MediaFileId, StreamIndex)`, and extraction reading `info.AudioStreams` in full.
  Migration `MultipleAudioStreams` drops the old index and adds the columns.
  Verified end to end against real ffprobe on a synthesised three-dub file: eng/ces/deu all extracted with
  their titles and the default flag on track 0, and shown on the preview page as an Audio table.
- **Search by audio and subtitle language**, as multiple-selection lists offered from
  `IMediaFileRepository.GetLanguagesAsync` - the languages the index actually holds, so a selection can
  always match something. Several selected widen the search (any of them); the two filters still combine
  with AND, like every other filter. The codec filter now matches *any* audio track rather than whichever
  happened to be first.
- **`MediaFileDto` carries `Duration`**, projected from the video track or the default audio track, so a
  listing can show a running time without a second read per file. That is what lets the tiles show it.
- **Admin components.** A pass over every Razor page for markup worth sharing produced five, all in
  `Shared/`: `MediaFileFacts` (the file's facts, as a panel or as a tile's one-line form - used by the
  preview page, the library and the file search), `MediaTile` (the thumbnail card, three copies before),
  `StatTile` (**sixteen** copies before, across the dashboard and the file cache), `FolderList` (the
  virtualized folder list, which also **ends the duplicated `ItemSize` constants** - a measured value in
  two places is one that goes stale), and `Notice` (five copies). `MediaFormat` holds the shared
  formatting.
- **Source folders carry a restart note**, and it says *why*: the library is indexed at startup, so a
  folder added there is not scanned - and one removed is still served - until the server restarts. That is
  exactly the trap that produced the re-rooting bug, so the note is the cheap half of the fix.
- **The restart/stop confirm pair is green and red** (`--go` / `--stop`, filled). Read positionally from
  the request: **confirm is green, Cancel is red**. The opposite convention - red for the destructive
  action - is equally defensible and was not chosen; flipping it is a two-word change if it reads wrongly.
- **Durations and bitrates are formatted.** `00:00:29.9600000` becomes `0:30`, a bare `500734` becomes
  `500 kbps`, and an absent value renders as `-` rather than an empty cell. Two of the "small display
  defects" recorded earlier are therefore closed; the `.@__thumb` duplicate and the h1 focus ring are not.

#### What M8 delivered, for orientation after a context reset

All 24 `/manage` endpoints (the reference's whole surface) plus a six-page Blazor admin UI on the admin
port. `README.md` is the reference for both: "Management endpoints" and "Admin UI". The traps found while
building it are in section 6's M8 entries and are worth reading before touching that code, in particular:

- an **endpoint filter does not apply to Razor component endpoints** - it compiles, reads as though it
  works, and was measured doing nothing; `AdminSurfaceMiddleware` is what confines the UI to its port
- **`UseAntiforgery()` must sit between `UseRouting()` and the endpoints**, or every admin page 500s at
  request time while startup reports nothing wrong
- **thumbnails are addressed by the thumbnail's identifier, not the file's** - and a wrong one 404s, which
  looks exactly like a thumbnail that has not been generated yet
- **`MediaContentResolver` is the single delivery decision** for both `/fileserver` and `/admin/media`, so
  a film behaves identically whichever port it is watched through. Do not reintroduce a difference.

Abstractions now live in Core so the Razor class library can use them: `IServedFileCache`,
`CachedContentClass`, `ServedFileCacheReport` (`Core.Delivery`), `IApiBlocker` (`Core.Diagnostics`),
`ISubscriptionStore` + `EventSubscription` (`Core.Gena`), `ISettingsWriter` (`Core.Configuration`).

#### Watch out for

- **The repository's `config.json` holds NAS paths** (`/share/Media`), because this tree is what deploys
  to the NAS. Running the server on a development machine fails options validation. Override with
  environment variables rather than editing the file - they are applied last and win. The whole local
  run, including a second instance beside the deployed one, is:
  ```bash
  Dlna__Library__SourceFolders__0=<some folder> \
  Dlna__Server__Port=27851 Dlna__Server__AdminPort=27852 \
  dotnet run --project src/DlnaServer.Host --no-build
  ```
  Then the admin UI is at `http://localhost:27852/admin` and `/manage` is on 27851. **Stop it through
  `curl http://localhost:27851/manage/stop`** - `/manage` is bound to the *media* port, so calling stop on
  the admin port answers 404 and leaves the process holding a lock on `DlnaServer.Host.exe`, which then
  fails the next build with `MSB3027`.
- **The deployed `publishNAS/config.json` survives a redeploy**, so a default changed in the repository's
  `config.json` does not reach the NAS on its own. Both were set to 1024/512 for the file cache on
  2026-09-02; check the deployed one before concluding a setting had no effect.
- **A local `dotnet run` writes into the same `bin/` the NAS build uses.** Harmless, but a deployment
  immediately afterwards recompiles from scratch.
- **The NAS repo and the development tree are the same files.** `T:\repos\DLNAServer` is the NAS
  share `\\NAS\Public` (`/share/Public`), so the deployment folder and the NAS's own `obj/` are
  directly readable - which is how several of this session's diagnoses were made. It also means a local
  build overwrites the NAS's `obj/` state. The folder was `DLNAServer_Claude` until 2026-09-21.
- **The deployment folder moved on 2026-09-21**, out of the repository and up to `T:\apps\Dlna-server`
  (`/share/Public/apps/Dlna-server`), so that the repository holds source only. Every `publishNAS/...`
  path below means a path in that folder. `run.sh` `cd`s to its own directory, so it runs unchanged -
  but whatever starts the server on the NAS has to name the new path, and the deployed `config.json`
  still carries the old one in `Thumbnails.CacheDirectory` (an unreachable setting - thumbnails are
  written beside the media).
- **`/share/Public` on the NAS is a symlink to `/share/CACHEDEV1_DATA/Public`**, and that aliasing has
  already caused two unrelated-looking failures. Anything comparing or deriving paths there needs the
  physical path.

### Small display defects seen in the browser 2026-09-02, not fixed

Noticed while working through item 0. The duration and bitrate formatting are now closed by `MediaFormat`; these two are not:

- ~~**`ExcludeFolders` shows `.@__thumb` twice** on the settings page.~~ **Fixed 2026-09-03, and the
  diagnosis here was wrong** - the cause was `ConfigurationBinder` appending to the property's non-empty
  initializer, not `DlnaOptionsDefaults`. It was also not harmless: the list grew by two entries on every
  save. Section 6c, item 3.
- **The `<h1>` draws a focus ring on every navigation**, because Blazor's `FocusOnNavigate` focuses it. It
  is correct for a screen reader and ugly with a mouse; `:focus-visible` is the usual answer.

### Open questions carried forward

- ~~**Does the LG television list files now?**~~ **Resolved 2026-09-02** - it does, once
  `CustomEnvelopeMessage` restored `soap:encodingStyle`. See "Where things stand" above.
- **Memory: working set 5073 MB against a 200 MB target, managed heap 1128 MB against 60 MB.** Section 3b
  carries the numbers and the mechanism. The open *decision* is whether whole-file media buffering
  survives, and what replaces the LOH compaction the reference performs and this server deliberately
  dropped.
- **Is `Server.DebugMode` the right gate for the per-request connection log?** Unrelated to the above, but
  `app20260901.log` reached **32 MB in one day** with `DebugMode` off, because `LogServingRange`
  (EventId 2, one line per byte range) is declared at Information while its own XML doc says those lines
  "are Debug". `AvTransportService.LogActionRequested` has the same shape: renderers poll
  `GetTransportInfo` and `GetPositionInfo` about once a second, so a 90-minute film emits on the order of
  10,000 Information lines from actions whose reply is a hard-coded idle state.
- **Metadata extraction logs nothing on success, at any level.** The only evidence ffprobe is working is
  the absence of a warning. The reference logs `Set metadata for file: '{file}'` at Information for audio
  and video. Thumbnails do log, but `LogThumbnailCreated` fires for an *adopted* thumbnail too, because
  `MediaProcessor` short-circuits on `Describe` and the caller cannot tell adoption from generation - so
  a first pass over an already-previewed library claims to have created every thumbnail in it.
- **The slow-query log has no dedicated file.** `SlowQueryInterceptor` logs at Warning through the normal
  `ILogger`, and the Serilog configuration has exactly two file sinks and no filter, so slow queries land
  interleaved in `logs/app.log`. This contradicts the interceptor's own XML doc, which explains why the
  reference's dedicated monthly file "proved its worth". The reference builds a standalone
  `Serilog.Core.Logger` and injects it into the interceptor; a `WriteTo.Logger` sub-logger filtered on
  `SourceContext` is the cleaner equivalent here - note `SlowQueryInterceptor` is `internal`, so
  `Matching.FromSource<T>()` needs the type name as a string.
- ~~**No test covers any hosted service.**~~ **Partly closed 2026-09-02** -
  `MediaProcessingHostedServiceTest` is the first, with `RecordingMediaFileRepository` and
  `ThrowingMediaProcessor` as reusable fakes, so the next hosted-service test is cheap. The other six
  services are still uncovered; `MediaCacheFillHostedService` is the one worth doing next.
- **`MediaSlidingExpirationInMinutes` (10) and the between-episodes gap** - section 6, M6.
- ~~**`FileCacheOptions.MediaAbsoluteExpiration` is 1 hour**~~ **Answered 2026-09-02 by reading
  `FileMemoryCacheManager`: the reference uses `AbsoluteExpirationRelativeToNow = 12 hours`.** An absolute
  cap outranks the sliding refresh, so at 1 hour a film longer than that is dropped and re-read from the
  platter *while it is being watched* - the exact noise the cache exists to prevent. Take the 12 hours.
  One-line edit in `FileCacheOptions`; fold it into the cache rework.
- **The byte cache's contribution to working set is unmeasured.** The M6 row was taken with an empty
  cache, so the trend so far says nothing. **The budget is no longer 1024 MB** - the code defaults were
  brought down to match the shipped `config.json` (256 MB total, 32 MB per file) on 2026-09-03, because
  a deployment arriving without a configuration file was silently running the configuration that
  measured 5,073 MB. A warm-cache reading still belongs in M9, and it will now be a cache holding
  thumbnails and static assets only, since nothing film-sized fits.
- **`res@class` is emitted by the reference and not here.** It is not a DIDL-Lite attribute and `upnp:class`
  already carries the item kind, so it was left out - see `src/DlnaServer.Upnp/Didl/DidlMetadata.md`
  section 2. If a TV misbehaves in M10, that row is the first thing to reverse.
- **Server GC versus Workstation GC.** 36-40 threads observed; Server GC reserves a heap per core. M9.
- **Every `/manage` endpoint is unauthenticated**, as in the reference - now a stated decision rather than
  an inherited one, and load-bearing since `/manage/stop` landed. The remaining destructive endpoints
  (`restart`, rescan, clear thumbnails) still need the same call made explicitly in M8.
- **GENA event push is not implemented.** SUBSCRIBE and UNSUBSCRIBE are handled properly as of M7 -
  identifiers are recognised, renewals work, UNSUBSCRIBE removes - but no NOTIFY is ever sent, so
  `SubscriptionStore` is written to and never read. Deliberate, recorded in section 2's divergence list.
  The store already holds the parsed callback URLs, so adding push is a matter of a sender, not a redesign.
- **The file watcher triggers a full rescan** rather than a targeted single-path update. Correct and cheap
  at the current library size; revisit if a scan becomes slow on the real 20,000-file library.
