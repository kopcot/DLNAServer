# What's new

What changed in each version, newest first, in plain terms.

A version is written `Major.Minor` here - `1.1`. The full number a build reports, on the About page,
carries the day it was built as well, but a day is not a release and gets no entry of its own: anything
worth telling you about is added to the version it belongs to.

---

## 1.1

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
seconds, the library counts every half minute, and *Recently added* on the Library page refreshes on its
own too - so a scan you started is visible without reloading the page.

**Uploads no longer follow a shortcut out of your library.** If a folder inside your upload destination is
a link to somewhere else on the machine, *Add files* now refuses it and says so instead of writing there.
Nothing put behind such a link had ever shown up in the library anyway - the scanner skips them - so this
only closes the gap between what was refused and what was reachable.

Upgrading from 1.0 keeps your index. Nothing is rebuilt and no rescan is needed.

## 1.0

The first numbered version, and the point these notes start. It is the server as it had been running:
televisions find it by themselves and play from it, the admin pages browse and search the library, make
previews, read file details, and report what the server is doing with the machine it is on.
