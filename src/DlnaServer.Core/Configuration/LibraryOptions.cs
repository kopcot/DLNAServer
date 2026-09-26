using System.ComponentModel.DataAnnotations;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Which folders are indexed, and which file extensions are treated as media.
    /// </summary>
    public sealed class LibraryOptions
    {
        /// <summary>
        /// Root folders scanned for media. Each becomes a top-level container in the DLNA root listing.
        /// </summary>
        [MinLength(1)]
        public IList<string> SourceFolders { get; set; } = [];

        /// <summary>
        /// Folder names or partial paths skipped during scanning, and hidden from renderers even once
        /// already indexed.
        /// </summary>
        /// <remarks>
        /// Two effects, deliberately, so a folder can be retired from the library without deleting its
        /// rows and without a rescan: new files under these names are never imported, and files and
        /// folders already in the database stop being browsed and stop being processed.
        /// <para>
        /// <b>An entry is a folder name or a partial path</b> - <c>@Recycle</c>, or <c>Films/Private</c>
        /// to hide one folder without hiding every other <c>Private</c> in the library. Either path
        /// separator may be used and the two are equivalent, so one <c>config.json</c> works on the NAS
        /// and on a Windows development machine.
        /// </para>
        /// <para>
        /// <b>Matching is on whole path-segment boundaries.</b> <c>path1</c> hides <c>path1</c> and
        /// everything under it, and does <b>not</b> hide <c>path1L</c> or <c>path10</c>. Both halves of
        /// the setting use that one rule - <see cref="Files.PathExclusion.IsExcluded"/> in memory and an
        /// equivalent LIKE in the repositories - so what is skipped and what is hidden agree.
        /// </para>
        /// <para>
        /// <b>Reported by a customer.</b> Hiding used to be a raw substring test over the whole path,
        /// matching what the reference does, so <c>path1</c> also hid <c>path1L</c>; scanning meanwhile
        /// compared single segments, so it kept importing that folder, and a partial path was refused by
        /// validation outright. The operator was given no sign that more was hidden than they had named.
        /// </para>
        /// <para>
        /// Hiding applies to every listing, the admin UI included - the admin pages and a renderer are
        /// answered by the same repository methods, so the two cannot disagree about what the library
        /// holds. Delivery by identifier is exempt, so a renderer already streaming a file is not cut
        /// off mid-playback when this changes, and so an operator can still open a hidden file's page
        /// from a link they already have.
        /// </para>
        /// <para>
        /// Empty by default rather than carrying the two names a NAS always wants:
        /// <c>ConfigurationBinder</c> <b>adds to</b> a non-empty list instead of replacing it, so an
        /// initializer here appended itself to whatever <c>config.json</c> named, and the admin UI then
        /// saved the longer list back - two more entries on every save. The defaults are seeded by
        /// <c>DlnaOptionsDefaults</c> instead, which runs after binding and de-duplicates.
        /// </para>
        /// </remarks>
        public IList<string> ExcludeFolders { get; set; } = [];

        /// <summary>
        /// Folder names or partial paths hidden from browsing and from delivery, but still indexed and
        /// kept up to date.
        /// </summary>
        /// <remarks>
        /// The other half of <see cref="ExcludeFolders"/>, for content that should stay current rather
        /// than be retired. An entry here is scanned, watched for created, changed and removed files, and
        /// processed for metadata and thumbnails exactly as any other folder - so it is ready to play the
        /// moment it is revealed. Only the answers change: it is absent from every listing, and a request
        /// for one of its files by identifier is answered as though the file were not there.
        /// <para>
        /// <b>Entries take the same form and the same matching rule as <see cref="ExcludeFolders"/></b> -
        /// a folder name or a partial path, either separator, matched on whole path-segment boundaries.
        /// One rule, one implementation, so the two settings cannot disagree about what an entry means.
        /// </para>
        /// <para>
        /// <b>Hiding is undone by a reveal window</b>, which is deliberately not a setting in this file:
        /// see <c>ITemporaryFolderVisibility</c>. It changes far too often to be worth a write and a
        /// backup of <c>config.json</c> each time, and it is meant to lapse rather than to be remembered
        /// across a restart.
        /// </para>
        /// <para>
        /// <b>Blocking delivery is where this parts company with <see cref="ExcludeFolders"/></b>, which
        /// exempts lookup by identifier so a renderer mid-stream is not cut off. Here the exemption would
        /// defeat the point, so a television holding a link from an earlier listing is refused - and a
        /// stream in progress stops when the window lapses. It remains a listing and delivery control, not
        /// an access control: anything with filesystem access to the machine still sees the files.
        /// </para>
        /// <para>
        /// Empty by default, for the same reason <see cref="ExcludeFolders"/> is - see its remark on
        /// <c>ConfigurationBinder</c> adding to a non-empty list.
        /// </para>
        /// </remarks>
        public IList<string> TemporarilyHiddenFolders { get; set; } = [];

        /// <summary>
        /// Maps a file extension (leading dot, lower case) to the DLNA MIME the server advertises for it.
        /// Overriding an extension here is how device-specific quirks are expressed.
        /// </summary>
        public IDictionary<string, MediaExtensionOptions> MediaFileExtensions { get; set; } =
            new Dictionary<string, MediaExtensionOptions>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maps a subtitle or lyrics file extension (leading dot, lower case) to the kind of media it is
        /// linked to: <see cref="DlnaMedia.Video"/> for a subtitle, <see cref="DlnaMedia.Audio"/> for lyrics.
        /// </summary>
        /// <remarks>
        /// A file whose extension is listed here is linked, by name, to the media file of that kind it
        /// belongs to - <c>film.en.srt</c> to <c>film.mkv</c>, <c>song.lrc</c> to <c>song.mp3</c> - and
        /// served beside it. A file of any other type is neither found by name nor accepted by hand.
        /// <para>
        /// Empty by default, for the reason <see cref="ExcludeFolders"/> is: <c>ConfigurationBinder</c> adds
        /// to a non-empty collection. <c>DlnaOptionsDefaults</c> seeds
        /// <see cref="SubtitleFileExtensionDefaults"/> after binding when configuration names none, and
        /// normalises every key to the stored form either way.
        /// </para>
        /// </remarks>
        public IDictionary<string, DlnaMedia> SubtitleFileExtensions { get; set; } =
            new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// How many recently added files the root container surfaces alongside the source folders.
        /// </summary>
        [Range(0, 1000)]
        public int RecentlyAddedCount { get; set; } = 30;


        /// <summary>
        /// How long a file must stop changing before the watcher indexes it.
        /// </summary>
        /// <remarks>
        /// This exists because a file arriving over a slow network share is visible long before it is
        /// complete. Indexing it while it is still being written records a partial size, a stamp that is
        /// immediately stale, and metadata read from a truncated file. Thirty seconds is the value the
        /// reference server has been running with in production.
        /// <para>
        /// Each further write to the same path restarts the window, so a long upload defers indexing
        /// until it genuinely stops - which the reference's fixed per-event delay could not do.
        /// </para>
        /// </remarks>
        [Range(0, 3600)]
        public int FileSettleSeconds { get; set; } = 30;

        /// <summary>
        /// Look through the source folders on a timer as well as reacting to filesystem events.
        /// </summary>
        /// <remarks>
        /// <b>Off by default, and it should stay off wherever the watcher works.</b> The server normally
        /// learns about a new, changed or deleted file from the operating system the moment it happens,
        /// which costs nothing until something actually changes.
        /// <para>
        /// Turn this on where those notifications do not arrive. A container is the case that prompted
        /// it: a bind mount or an overlay filesystem often delivers no inotify events for writes made
        /// outside the container, so the watcher stays silent and the library never changes. A network
        /// share mounted into the host has the same problem.
        /// </para>
        /// <para>
        /// The cost is real and is why this is not simply always on. Each pass walks every source folder
        /// twice - once to enumerate and once to reconcile - which on a 25,000-file library is around
        /// 51,000 filesystem calls, and on a small box that evicts the directory entries and page cache
        /// the rest of the server depends on. Pick the longest interval that is acceptable rather than
        /// the shortest one that is tolerable.
        /// </para>
        /// </remarks>
        public bool UsePeriodicRescan { get; set; }

        /// <summary>
        /// How long to wait between those timed passes, in minutes. Ignored unless
        /// <see cref="UsePeriodicRescan"/> is on.
        /// </summary>
        /// <remarks>
        /// The timer and a scan asked for from the admin pages share one wait, so a manual scan resets
        /// the clock rather than queueing behind it.
        /// </remarks>
        [Range(1, 1440)]
        public int RescanIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Use the file's own creation timestamp as the date it sorts by, rather than the time it was
        /// indexed.
        /// </summary>
        /// <remarks>
        /// This governs <c>FileCreatedUtc</c>, which is the date <b>Browse's date sort</b> uses. It does
        /// <b>not</b> govern <c>CreatedUtc</c>, the row's own indexing time, which is what
        /// <b>Recently added</b> orders by - two different columns, and the distinction matters.
        /// <para>
        /// <b>Recently added has one rule this setting cannot switch off</b>, added after a customer
        /// report. A <b>first fill</b> - an empty index, or a source folder that has never been indexed -
        /// takes each row's indexing date from the filesystem, whatever this setting says. A bulk import
        /// otherwise gives every row one identical timestamp, which is no ordering at all: recreating the
        /// database wiped the operator's Recently added list, and adding a source folder dumped its whole
        /// contents in at one instant and pushed everything real off the list. On a first fill the file's
        /// own date is the only information that exists, so refusing to use it buys nothing.
        /// </para>
        /// <para>
        /// A file arriving into a folder already indexed is genuinely new and keeps the current time,
        /// which is what makes Recently added mean anything afterwards. See
        /// <c>LibraryIndexer.ResolveFirstFillRootsAsync</c>.
        /// </para>
        /// </remarks>
        public bool UseFileCreationDateTime { get; set; }

        /// <summary>
        /// Read every tag a media file carries about itself - artist, album, lyrics, and whatever else
        /// the container holds - and show them on the file's page.
        /// </summary>
        /// <remarks>
        /// Off means the tags are neither read nor stored; rows already collected stay until the file is
        /// reprocessed. It costs a second ffprobe call per file on top of the one metadata extraction
        /// already makes, so a first pass over a large library is meaningfully slower with it on.
        /// </remarks>
        public bool ReadContainerTags { get; set; } = true;
    }
}
