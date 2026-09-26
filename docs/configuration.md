# Configuration

Every setting the server reads, where it is read from, and which ones need a
restart.

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

> The schema is **not** compatible with the reference's flat `config.json`. Settings are grouped into
> `Server`, `Library`, `Thumbnails`, `FileCache` and `Compatibility`. Port values across by hand.

`config.json` sits beside the binaries, binds to `DlnaOptions` (section `Dlna`) through the normal
`IConfiguration` pipeline, and is consumed via `IOptionsMonitor` so most edits apply without a restart.

With **no source folder configured the application's own folder is served**, so a fresh deployment starts
rather than refusing to boot. The thumbnail cache and `Resources` are excluded from that fallback library,
because otherwise the generated thumbnails and the device icons would be indexed as media. A warning names
the folder it fell back to.

`DlnaOptionsValidator` then refuses to start the server on: a source folder that does not exist, equal media
and admin ports, a per-file cache limit above the total cache budget, a blank `Thumbnails.SubFolderName`, a
thumbnail cache directory inside a source folder that no `ExcludeFolders` entry covers, an
`Upload.DestinationFolder` that is not inside one of the source folders, or a `Library.SubtitleFileExtensions`
entry that is not a usable extension, goes with neither `Video` nor `Audio`, or is also a media type.

`Library.ExcludeFolders` carries no default on the property itself. `ConfigurationBinder` **adds to** a
non-empty `IList<string>` rather than replacing it, so a default declared there appended itself to whatever
`config.json` named - and because the admin UI writes the bound list back, the file grew by two entries on
every save. `DlnaOptionsDefaults` seeds `.@__thumb` and `@Recycle` after binding, only when configuration
names none of its own, and de-duplicates case-insensitively either way, so an already-doubled file heals on
its next load.

`Library.SubtitleFileExtensions` decides which files are linked to their media by name, and what each goes
with: `".srt": "Video"` for a subtitle, `".lrc": "Audio"` for lyrics, which only ever match music. It carries
no default on the property either, for the same reason: `DlnaOptionsDefaults` seeds the shipped seven (`.srt`,
`.vtt`, `.ass`, `.ssa`, `.sub`, `.smi` for video, `.lrc` for music) only when configuration names none, and
normalises every key to lower case with a leading dot. An empty list therefore means "not configured", not
"link nothing" - `Compatibility.SendSubtitles` is the switch for that. An extension that is also a media type -
in `MediaFileExtensions` or known to the catalog as video, audio or an image - is refused, because the scanner
would index those files as items of their own. A change applies to the next scan. A type taken off the list
stops being served and offered for download at once, though a television may still be told about it until
that scan drops its links - **including ones added by hand**. The one exception is a link the operator
removed, whose marker stays so the file is not re-linked if the type returns.

Thumbnails are written beside the media they describe, into `Thumbnails.SubFolderName` (`.@__thumb`) - so
`Films/Film.mkv` is previewed by `Films/.@__thumb/Film.mkv.jpg`, which is where the reference puts them and
where an existing library already has them. One that is already there is adopted rather than regenerated.
The name is **required**, and it is added to `ExcludeFolders` after binding whether or not an operator
listed it: a scan that does not skip that folder re-ingests every preview as media and then previews those.

## What each setting costs to change

`config.json` is re-read while the server runs, so most edits apply without a restart. The exceptions are
not obvious from the schema, which is why they are listed rather than left to be discovered.

| Group | Persisted | Applies live | Needs a restart | Refused by validation |
| --- | --- | --- | --- | --- |
| `Server.Port` / `Server.AdminPort` | yes | no | yes - the listeners are bound at startup | equal ports, and a missing one |
| `Server` identity strings | yes | yes | no | - |
| `Library.SourceFolders` | yes | yes | no | empty after defaults, and a relative path |
| `Library.ExcludeFolders` | yes | yes | no | - |
| `Library.UseFileCreationDateTime` | yes | yes | no | - |
| `Library.SubtitleFileExtensions` | yes | yes - from the next scan | no | not an extension, neither `Video` nor `Audio`, or also a media type |
| `Thumbnails.SubFolderName` | yes | yes | no | blank, and a cache inside a source folder no exclusion covers |
| `Thumbnails.DownloadFFmpeg` | yes | yes | no | - |
| `FileCache.*` | yes | yes | no | a per-file limit above the total |
| `Compatibility.*` | yes | yes | no | - |
| `Database.*` | yes | no | yes | - |
| `Upload.Enabled` | yes | no | **yes** - the endpoint is created at startup or not at all | - |
| `Upload.DestinationFolder` | yes | yes | no | a folder outside every source folder |

A value the validator refuses does **not** take the server down once it is running: the last settings
that validated stay in use and every read logs why. It does refuse to boot on one, which is what
`ValidateOnStart` is for.

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
