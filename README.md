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
> **177.7 MB** under the memory pass, and a television plays from it. 798 tests at 0 warnings, no
> vulnerable packages, no open Majors. What is still owed is a *steady-state* memory reading — the
> 177.7 MB was taken 255 s after a restart — and confirmation on a real television of the four DIDL
> fields added on 2026-09-06.
>
> **Version `1.1.0924`.** It is `Major.Minor.MonthDate`, and each project carries its own, so a number
> says which assembly changed. The one on the About page is the host's, which is the product version;
> the assembly table on that page lists the rest. `release-notes.md` says what changed for whoever runs the server,
> one entry per `Major.Minor`; `docs/` holds everything internal. `release-notes.md` and `LICENSE` are
> both copied into the build and publish output, so a deployment carries them beside the binaries.
>
> **Licence: [MIT](LICENSE).** Free to use, change and pass on, with no warranty.
>
> **`docs/decisions.md` carries the conventions, the standing decisions and the traps**, and its section 7b
> is the open-work list. `docs/history.md` is the batch-by-batch record of what changed and when.

## What it does

- Serves a media library over UPnP/DLNA to televisions and other renderers on the local network.
- Indexes a folder tree, reads metadata with ffprobe, and makes previews with SkiaSharp.
- Keeps recently served files in memory so the NAS drives are not woken for a re-watch.
- Ships a Blazor admin UI on a second port: browse, search, per-file details, diagnostics, maintenance and an optional upload page.
- Reproduces the reference server's wire behaviour, with every deliberate difference written down and tested.

## Projects

| Project | Role |
| --- | --- |
| `src/DlnaServer.Core` | Domain model, DLNA types, configuration options, abstractions. No dependencies. |
| `src/DlnaServer.Persistence` | EF Core over SQLite: DbContext, entities, migrations, repositories. |
| `src/DlnaServer.Media` | Library scanning, ffprobe metadata, SkiaSharp thumbnails. |
| `src/DlnaServer.Upnp` | SSDP, `description.xml`, SCPD, SOAP services, DIDL-Lite. No EF, no HTTP. |
| `src/DlnaServer.Admin` | Blazor Server admin UI, as a Razor Class Library. |
| `src/DlnaServer.Host` | The ASP.NET Core host. The only executable; nothing references it. |
| `tests/` | Unit, architecture and integration projects - see [Architecture](docs/architecture.md). |

The dependency directions and the entity/DTO boundary are enforced by `tests/DlnaServer.ArchitectureTests`,
so a violation fails the build rather than a review.

## Requirements

.NET 8 (`global.json` pins the 8.0.4xx SDK band). Linux or Windows; the target deployment is a QNAP NAS.
ffmpeg is optional and only affects video and audio metadata and video previews.

## Quick start

```bash
dotnet build DlnaServer.sln     # must stay at zero warnings
dotnet test  DlnaServer.sln
dotnet run --project src/DlnaServer.Host
```

Set `Dlna.Library.SourceFolders` before the first run. With no source folder configured the server falls
back to its own folder and warns rather than refusing to start - so a fresh deployment comes up serving
the wrong thing rather than not coming up.

Two ports, one process: one serves UPnP/DLNA and media, the other the admin UI. They must differ or
startup fails. Deployment to the NAS is `./NasBuild.sh` (`NasBuild.usage.txt`); the container path is
`Docker.usage.md`, and **host networking is not optional** there, because SSDP is UDP multicast and a
bridge does not forward it.

## Documentation

| Where | What |
| --- | --- |
| [docs/](docs/README.md) | How it works, in detail |
| [docs/architecture.md](docs/architecture.md) | Layout, dependency rules, naming |
| [docs/dlna-compatibility.md](docs/dlna-compatibility.md) | What goes on the wire, and every deliberate difference |
| [docs/configuration.md](docs/configuration.md) | Every setting, and which need a restart |
| [docs/operations.md](docs/operations.md) | Running it, uploading, rebuilding, the admin UI, the management endpoints |
| [docs/performance.md](docs/performance.md) | The served-bytes cache and what it costs |
| [docs/troubleshooting.md](docs/troubleshooting.md) | It is not working - start here |
| [docs/decisions.md](docs/decisions.md) | Why it is built this way: decisions, conventions, traps, open work |
| [docs/history.md](docs/history.md) | Status, milestones, and the batch-by-batch record |
| `release-notes.md` | What changed, for whoever runs the server |
| [docs/development.md](docs/development.md) | Build, the two hard rules, versioning, releases |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Who contributes, and where to start |

## Licence

[MIT](LICENSE). Free to use, change and pass on, with no warranty.
