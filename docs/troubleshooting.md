# Troubleshooting

Symptoms an operator actually hits, and where each one has come from before. Every entry here happened;
this is not a list of what could theoretically go wrong.

## The television cannot see the server at all

**Check the network mode first if this is a container.** SSDP is UDP multicast and a bridge network does
not forward it, so on a bridge the admin pages work perfectly and no television ever sees the server -
which reads like a DLNA fault and is not one. Host networking is not optional. See `Docker.usage.md`.

Otherwise, in order: the media port is the one the television talks to, not the admin port; a firewall
between the two devices will drop the multicast before it drops anything else; and the server has to have
started cleanly, because a configuration it refuses to boot on never advertises.

## The television sees the server but cannot browse it

The device is talking to the right port and being refused or misunderstood. Look at `logs/app.log` for the
Browse request arriving. If nothing arrives, the renderer resolved an address the server is not listening
on. If it arrives and the response is rejected, that is a wire-format problem and belongs with
[DLNA compatibility](dlna-compatibility.md) - VLC tolerates malformed DIDL-Lite that televisions reject,
so a library that plays in VLC proves nothing here.

## Video plays but seeking does not work

Seeking is byte ranges. A renderer seeks by asking for a range, and anything that breaks the
`Content-Range` contract breaks seeking specifically while leaving straight playback fine. Note that no
response compression is registered for this application deliberately: compressing a `206 Partial Content`
changes the byte count the renderer was promised.

`Dlna.Compatibility.SendDlnaResponseHeaders` is the escape hatch if a television regresses here.

## There are no previews

Images use SkiaSharp and are unaffected by anything external. **Video previews need ffmpeg, and its
absence is deliberately quiet** - indexing, browsing and streaming all work without it, so this can go
unnoticed for a long time. The dashboard warns when the answer is a definite no.

Put the binaries in an `ffmpeg` folder beside the server and use *Rebuild all file details* on
Maintenance. `Thumbnails.DownloadFFmpeg` ships off because that path executes an unverified third-party
binary.

## There are no durations, resolutions, codecs or language filters

The same cause as the previous entry, and worth its own line because the symptom looks like a metadata
bug rather than a missing program. Video and audio metadata come from ffprobe. Pictures use
MetadataExtractor and are unaffected.

## Memory is higher than expected

*Total in use* on the Dashboard is the working set. The usual cause is the served-bytes cache doing its
job: a cached film is one contiguous allocation on the large object heap, and cache churn is what takes
the figure up. [Performance](performance.md) explains the trade and what the limits do.

*Empty the memory* on the File cache page releases it and forces a compacting collection. That is an
operator action on purpose and is not something automatic eviction does.

## The drives keep waking up

That is the problem the byte cache exists to solve, so it means the cache is not holding what is being
played - the file is larger than `MaxFileSizeInMegabytes`, the total budget is too small for the library
being watched, or the file has been barred from the cache after a read failure. The File cache page
reports the resolved budget, what is held and the hit rate, and *Search files* can list files barred from
memory.

## A setting was changed and nothing happened

Most settings apply without a restart. The ones that do not say so on the Settings page - `Upload.Enabled`
is the clearest, because the endpoint is created at startup or not at all.

If **no** setting is taking effect, read `logs/app.log` from the top. The server reports what it did with
`config.json` on every start: read and well-formed, written from defaults, replaced after being unusable,
or parsed but carrying no `Dlna` section - that last one is what a file in the reference server's flat
layout produces, and it means every setting in the file is being ignored.

## The watcher keeps faulting on a large library

Linux caps how many directories one user can watch with inotify at once (`fs.inotify.max_user_watches`,
often 8192 by default), and `FileSystemWatcher` needs one watch per directory it is told to cover. A
library with more directories than that budget makes the watcher fault repeatedly instead of quietly
missing events - look in `logs/app.log` for it naming the limit before assuming the library itself is the
problem. Raise the limit on the host (not inside the container, unless the container has its own PID/user
namespace for this):

```bash
sudo sysctl fs.inotify.max_user_watches=524288
# and to survive a reboot:
echo 'fs.inotify.max_user_watches=524288' | sudo tee /etc/sysctl.d/99-dlna-inotify.conf
```

If raising the limit is not an option, `Library.UsePeriodicRescan` is the fallback - see the Docker
notes on why a bind mount already needs it for a different reason.

## The index is empty or missing files

A folder is hidden by `Library.ExcludeFolders` matching on whole path segments, or the scan has not run
yet. `Library.ExcludeFolders` hides a folder from every listing without discarding its rows, so a hidden
folder looks identical to a missing one from the admin pages.

*Rebuild index* on Maintenance deletes every row and rescans without a restart. *Recreate database*
deletes the file and restarts, which costs a full rescan and regenerates every `PublicId`.
