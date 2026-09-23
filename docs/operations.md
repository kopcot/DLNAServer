# Operations

Running the server, keeping it fed, and finding out what it is doing.

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
