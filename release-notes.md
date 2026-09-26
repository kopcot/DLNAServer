# What's new

What changed in each version, newest first, in plain terms.

A version is written `Major.Minor` here - `1.1`. The full number a build reports, on the About page,
carries the day it was built as well, but a day is not a release and gets no entry of its own: anything
worth telling you about is added to the version it belongs to.

---

## 1.1

**Subtitles reach the television.** A subtitle file named like a video - `film.srt`, `film.en.srt`, `film.1.en.srt` next to `film.mkv`, or in a folder just below it such as `Subs` - is now offered with that video, and lyrics (`.lrc`) with a song, with nothing to set up. Copy one in a week after the video and it is picked up the next time the server looks over your folders. Whether your television shows it depends on the television, so if one starts misbehaving, *Offer subtitles to televisions* on Settings turns it all off.

**Choose them yourself on a file's page.** The file's page lists what is linked to it, with the language each one appears to be. You can add one by hand - it must be in the file's folder or one folder below, and not reached through a shortcut (a link) - which then replaces the automatic ones, remove one (it stays removed, whether it was found by name or added by you), or correct its language. A subtitle in a folder you later add to *Excluded folders* is let go of the next time the server looks over your folders, and so is one you added whose file has gone. Until then the page says why it cannot be used. Subtitles you add by hand are forgotten if you use *Rebuild index*; the ones found by name come back by themselves.

**Find them.** *Search files* has a *Subtitles* filter, its subtitle-language filter now includes the languages of linked files, and the Library tiles carry a small badge under the video or music one when a file has subtitles - a track inside it or a linked file. The filter can ask for either kind or for one of them: *yes (embedded in file)* lists files with a subtitle track inside them, *yes (linked file)* files with a subtitle file linked to them, and a film carrying both appears under either.

**Download them.** A file's page has a *Download subtitle* button beside *Download* when one subtitle file is linked to it, and *Download subtitles (zip)* when several are - all of them in one zip, each under its own name.

**Choose which files count as subtitles.** *Settings* has a *Subtitle types* list beside *File types*: which extensions are linked to videos as subtitles and which to music as lyrics. *Restore the default subtitle types* puts the usual list back.

**The Dashboard counts them.** The Library panel shows *Video*, *Music*, *Photos* and *Subtitles* in one row, and *Other* below them when your library holds files of no media kind. Subtitles are the files linked to your media, so they are not part of the *Files* total; Video, Music, Photos and Other still add up to it.

**The admin pages only answer your own network.** On a NAS with an internet-reachable IPv6 address, the admin port - settings, *Recreate database*, uploads - could be reached from outside if the router let it through. It now answers only devices on the local network, a Tailscale address, or the optional admin proxy. The server also answers discovery requests only from the local network now, and one device can no longer take every event subscription.

**A mistyped setting no longer stops the server.** A value of the wrong kind in the settings file - `"yes"` where a true/false belongs - made every page fail until the file was fixed. The server now keeps running on the last settings that worked, as it already did for a value that was merely out of range, and says so.

**A share that drops out mid-scan no longer empties the library.** The server checked once, at the start of a scan, that your media folders were really there. If a share disappeared partway through, the rest of the scan took every file under it for deleted. It now checks again as it goes and stops removing anything the moment a folder goes missing.

**A slow file no longer loses its details.** When reading a file's details timed out - a disc spinning up is enough - the server stored the empty result as though it were the answer, wiping the duration, resolution and codecs it already had, and never tried again. A timeout is now a failed attempt that is retried like any other.

**Uploads are sturdier.** Two uploads of the same name at once no longer spoil each other, *keep existing files* is honoured even when a file of that name arrives mid-upload, an upload cut off halfway leaves nothing behind, and one oversized request can no longer run the server out of memory. *Settings* now refuses an upload folder inside one of your *Excluded folders*, instead of accepting it and refusing every upload later.

**Stop waits for Recreate database.** Pressing *Stop server* while *Recreate database* was restarting the server quietly cancelled the recreate. Stop is now refused until the restart has happened.

**Behind the admin proxy, televisions keep working.** The optional admin proxy's settings restricted which addresses the whole server answered to, so a television reaching it by its local address could be turned away. `LAN_HOSTS` in `.env` now lists the local names and addresses to keep answering; see `Docker.usage.md`.

**A very large library no longer keeps the discs spinning when the system runs out of file watches.** Watching folders for changes failed over and over on such a library, and every failure started a full scan. The server now waits longer after each failure and writes one log line saying what to raise (`fs.inotify.max_user_watches`).

**A server that cannot open its database now says so to whatever runs it.** It exits with an error code instead of the code a deliberate stop uses, so a supervisor can tell the two apart.

**Memory is handed back when nobody is watching.** Films and previews kept in memory are meant to be let go after a while, but the server only noticed they had expired the next time something asked it for a file - so a server left alone overnight kept everything it had served the evening before, over a gigabyte on one real library. It now checks every minute. A film let go of is also really freed now, rather than lingering until you pressed *Empty the memory*.

**The log says which version started.** Every start writes the server's version to the log, so after an upgrade you can tell which lines came from which build.

**A menu button on a phone.** On a narrow screen the pages were a sideways-scrolling strip across the top, and most of it was out of sight. There is now a menu button beside the server's name that opens the full list, and it closes again once you pick a page.

**Figures line up.** The figures on the Dashboard and on *Recently served* sit in four columns (two on a narrower screen), so each one sits under the one above it in every group, rather than each group spreading its own few across the page. *Tidy-ups so far* is now *Memory tidy-ups so far*, and *Memory / disc / database* is now *Times served from memory / disc / database*, with a line underneath explaining it.

**A file's page is named after the file**, and so is the photograph page a phone opens, in the browser tab and in your history, instead of every page being called *ZEN DLNA Server*.

**A missing settings file no longer empties your library.** If the server starts and cannot find its settings file, it writes a fresh one and shares its own folder until you set your media folders again. That used to be taken as you deciding to stop sharing your media, so the next scan forgot the whole library - and restoring the file meant indexing everything again from scratch, with every preview remade. Your library is now kept while the server runs on that fallback, and picks up where it left off once your folders are set again. The fresh file also lists the usual file types now; it used to list none, so nothing at all was found until you added them back on Settings.

**One damaged video can no longer fill the log.** When a preview could not be made from a broken video, the server wrote everything the video tool printed into the log - on one real library that was over 139,000 lines for a single file, and three of those filled a whole day's log file. It now writes the few lines that say what went wrong, and how much it left out.

**A file that cannot be read is no longer tried three times in a row.** The server gives each file three chances before leaving it alone, but all three used to happen within a few seconds - too quickly for anything to have changed, and three times the work and the log lines for a file that will never work. It now waits five minutes before the second try and half an hour before the third. Asking for a file to be redone from its page, or letting it back in, still happens straight away.

**Behind the optional admin proxy, the server now sees who is really calling.** If you put the admin
pages behind the TLS proxy, every request used to arrive looking as though it came from the proxy itself
- so the upload security log and the remembered-devices list recorded the same local address for
everyone, which made them useless for the one question they exist to answer. They now record the
device's own address, and cookies the admin pages set are marked as secure over that connection. Nothing
changes for a server reached directly on the local network.

**These notes and the licence now ship with the server.** This file and a `LICENSE` file are copied in
beside the program files, so whoever is running a deployment can read what changed and what the terms are
without going back to the source. The server is under the **MIT licence** - free to use, change and pass
on, with no warranty - and About says so.

**Put the file types back the way they shipped.** Settings, under *Advanced → File types*, has a
*Restore the default file types* button that refills the list with the one the server comes with. Nothing
changes until you press *Save*, so *Discard changes* still takes you back to what you had.

**Correct what one file says it is.** A file's own page can now change its *file type* and its
*compatibility profile*, in the *File* group beside the facts it corrects. It is the fix for a file a
television refuses while it plays perfectly on these pages, and it affects that one file - the list that
types files by their extension is still on Settings, and still applies to everything else. You are only
offered types this server actually advertises, and changing a file from a video to a picture (or the
other way) has its details and preview read again, so what it says about itself keeps matching what it is.

**Let a file back into memory.** When reading a file fails once - an unplugged disc, a folder the server
was not allowed to open - the server stops keeping that file in memory and the page says *Kept in memory:
no*. *Allow in memory again*, on the file or on a whole folder, is how you undo that once the cause is
fixed. *Search files* has a matching filter, so you can list the files it happened to.

**Deleting media now deletes its preview.** Previews sit beside your media in a hidden folder, and the
image used to stay behind forever when the file it belonged to was deleted. From this version on, a
deleted file takes its preview with it the next time the server looks over your folders. Images left
behind by deletions *before* this version are not swept up - the server no longer knows they exist - so
if you want that space back, delete the hidden preview folders yourself and let them be made again.

**Add files from a browser.** A new *Add files* page copies media from whatever you are reading this on -
a phone, a laptop - straight into one of your library folders. It is **off until you turn it on**: tick
*Accept uploads* on Settings and restart the server. You can restrict it to a single folder, cap how large
one file may be, choose whether a file already of that name is left alone or replaced, and you get a table
afterwards saying what happened to every file. Only the file types your library already holds are
accepted. The folder you last used is offered again next time, even after clearing your browser.

**Every upload is logged**, to a file of its own, with the address and browser it came from - so you can
see later how a file arrived.

**Settings can check a folder for you.** *Upload folder* has a *Check* button beside it, the same one
*Source folders* has: it says whether the folder is there and whether the server can read it, before you
save rather than after.

**Folders and files fold away** on the Library page. The heading keeps the count while a section is shut,
and it stays shut as you move from folder to folder.

**Hover a figure to see where it comes from.** *Total in use* says *Working Set (MB)*; a contact address
says which setting it came from. It is off by default and switched on for a while from *Settings →
Temporary*, because it underlines a large part of every page.

**About lists every program library this build uses**, with versions, in a group that starts shut.

**Help covers all of the above** - including what uploading does, how to turn it on, and where the upload
log is kept.

**The log says which file is being played.** Each media file the server sends now appears in `app.log` at
the normal level, with whether it came from memory or from the disc. Previews stay out of the way at the
debug level. Note that this fills the log faster - on a busy day it may not reach back the usual week.

**The log also says what happened to your settings file.** A clean read is now reported rather than
passed over in silence, and a file that parses but carries none of this server's settings - the older
flat layout, for instance - is called out and left untouched instead of being quietly ignored.

**The Dashboard says what your library is made of**, not just how many files it holds - video, music and
photos each get their own figure, on a row below the file and folder totals. They are counted the way the Library page lists, so
folders you have hidden are left out and the three add up to the total.

**The Dashboard keeps itself up to date.** Memory and the recently-served-files figures refresh every few
seconds, the library counts within seconds of a scan changing them, and *Recently added* on the Library page refreshes on its
own too - so a scan you started is visible without reloading the page. A Dashboard left open no longer reads the whole library every half minute while nothing is changing, so it no longer keeps the disks awake.

**Uploads no longer follow a shortcut out of your library.** If a folder inside your upload destination is
a link to somewhere else on the machine, *Add files* now refuses it and says so instead of writing there.
Nothing put behind such a link had ever shown up in the library anyway - the scanner skips them - so this
only closes the gap between what was refused and what was reachable.

Upgrading from 1.0 keeps your index. Nothing is rebuilt and no rescan is needed.

## 1.0

The first numbered version, and the point these notes start. It is the server as it had been running:
televisions find it by themselves and play from it, the admin pages browse and search the library, make
previews, read file details, and report what the server is doing with the machine it is on.
