using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Persistence.Repositories
{
    /// <summary>
    /// Reads and writes indexed media files. The only way in or out of the file tables.
    /// </summary>
    /// <remarks>
    /// Everything crossing this boundary is a DTO. Entities are internal to this assembly, so no other
    /// project can hold one, and reads project straight into the DTO - the emitted SQL selects only the
    /// columns the DTO declares and no entity is materialised.
    /// <para>
    /// Identifiers are always the external <c>PublicId</c>; the integer keys the database joins on never
    /// leave this assembly.
    /// </para>
    /// <para>
    /// Every <b>listing</b> method - by directory, recently added, search - hides files under
    /// <c>Library.ExcludeFolders</c>, and there is no flag to ask for more: the admin UI and a television
    /// are served by these same methods, so the two cannot disagree about what the library holds. Lookup
    /// by identifier or by path, and the indexer's own reads, are deliberately unfiltered - see
    /// <c>MediaFileRepository.ExcludeHidden</c> for why filtering them breaks scanning and deletion.
    /// </para>
    /// <para>
    /// Reads are uncached. The reference layered a per-query memory cache inside its repositories, keyed
    /// by method and arguments, which made staleness impossible to reason about and counted an entire
    /// entity graph as one unit against a byte-denominated size limit.
    /// </para>
    /// </remarks>
    public interface IMediaFileRepository
    {
        Task<MediaFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a file together with the metadata, subtitles and thumbnail a DIDL-Lite item needs.
        /// </summary>
        Task<MediaFileDetailsDto?> GetWithDetailsAsync(Guid publicId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Every tag one file carries about itself, in the order the container lists them.
        /// </summary>
        /// <remarks>
        /// Its own read rather than part of <see cref="GetWithDetailsAsync"/>, which every DIDL-Lite item
        /// in a Browse response goes through: tags are wanted by one admin page and by nothing on the
        /// renderer path, so folding them in would put a second query behind every row of every browse.
        /// </remarks>
        Task<IReadOnlyList<MediaFileTagDto>> GetTagsAsync(
            Guid publicId,
            CancellationToken cancellationToken = default);

        Task<MediaFileDto?> GetByPathAsync(string fullPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Files directly inside a directory, ordered by title.
        /// </summary>
        Task<int> CountByDirectoryAsync(
            Guid directoryPublicId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MediaFileDto>> GetByDirectoryPageAsync(
            Guid directoryPublicId,
            int skip,
            int take,
            bool sortByDate,
            bool descending,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MediaFileDto>> GetByDirectoryAsync(
            Guid directoryPublicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Where a file sits among the playable files of its folder, and the identifiers either side.
        /// </summary>
        /// <remarks>
        /// The preview page's Previous and Next buttons. It read the whole folder through
        /// <see cref="GetByDirectoryAsync"/> and kept two entries, which on a folder of 1,564 photographs
        /// materialised 1,564 full records to render two buttons; this projects three columns instead.
        /// <para>
        /// Ordering and the definition of "playable" are deliberately identical to what the page did for
        /// itself, so the buttons step through exactly the same sequence as before.
        /// </para>
        /// </remarks>
        Task<MediaFileNeighboursDto> GetPlayableNeighboursAsync(
            Guid directoryPublicId,
            Guid filePublicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Most recently indexed files, newest first. Surfaced as a synthetic listing in the DLNA root.
        /// </summary>
        Task<IReadOnlyList<MediaFileDto>> GetRecentlyAddedAsync(
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Files whose content changed since metadata or a thumbnail was produced, plus those never
        /// processed, excluding any that have failed too often to be worth retrying.
        /// </summary>
        Task<IReadOnlyList<MediaFileDto>> GetPendingProcessingAsync(
            int maxCount,
            int maxFailureCount,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Paths already indexed, out of the supplied candidates. Used by scanning to add only what is new.
        /// </summary>
        Task<IReadOnlySet<string>> GetExistingPathsAsync(
            IReadOnlyCollection<string> fullPaths,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// A page of indexed files ordered by path, for reconciliation against the filesystem.
        /// </summary>
        /// <remarks>
        /// Keyset paging on the path rather than skip/take: rows are deleted as the pass runs, and an
        /// offset would silently skip records once earlier ones disappear.
        /// </remarks>
        Task<IReadOnlyList<IndexedFileDto>> GetIndexedPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Indexed files whose content stamp is one of these, so a row that has vanished from one path
        /// can be recognised at another.
        /// </summary>
        /// <remarks>
        /// The stamp is size and modification time, not a hash, so a match is a strong hint rather than
        /// proof of identity - two copies of one file share a stamp. The caller is expected to act only
        /// on an unambiguous match.
        /// </remarks>
        Task<IReadOnlyList<IndexedFileDto>> FindByContentStampsAsync(
            IReadOnlyCollection<string> contentStamps,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Carries an indexed file onto the path a duplicate row was inserted at, and deletes the
        /// duplicate - a move when the folder changed, a rename when only the name did. Reports whether
        /// it happened, and names the preview image left behind for the caller to delete.
        /// </summary>
        /// <remarks>
        /// What a moved file keeps: its <c>PublicId</c> - and therefore its DLNA ObjectID, its media URL
        /// and any renderer bookmark - along with the metadata already extracted from it, the operator's
        /// suppression flags, and the date it entered the library, which is what stops a move looking
        /// like a new arrival at the top of <i>Recently added</i>. Without this a move is a delete and an
        /// insert, and all of that is lost.
        /// <para>
        /// The preview is deliberately NOT kept: it lives beside the media, so the move invalidates its
        /// path and it is cleared for regeneration in the new folder.
        /// </para>
        /// </remarks>
        Task<(bool Moved, string? AbandonedThumbnailPath)> MoveOrRenameAsync(
            Guid publicId,
            Guid supersededPublicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies new size, timestamp and content stamp, and clears the metadata and thumbnail stamps
        /// so the file is reprocessed.
        /// </summary>
        Task<int> UpdateContentAsync(
            IReadOnlyCollection<MediaFileContentUpdateDto> updates,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Rewrites what one file claims to be on the wire - its MIME type and DLNA profile - and
        /// returns whether a row was found to rewrite.
        /// </summary>
        /// <remarks>
        /// The operator's correction for a file the extension map typed wrongly, which is otherwise
        /// unreachable: these three columns are written once when the file is first indexed and nothing
        /// re-derives them, so a rescan leaves an edit standing.
        /// <para>
        /// The UPnP class is recomputed from the new MIME rather than taken as an argument, because it
        /// is not an independent choice - <c>DlnaMimeCatalog.ToDefaultItemClass</c> is the same rule the
        /// indexer applies at insert, and a picture retyped as video that kept <c>imageItem</c> would
        /// disappear from a television's video listing while still claiming to be one.
        /// </para>
        /// <para>
        /// A blank or whitespace profile is stored as <see langword="null"/>, which is what "the
        /// standard profile for this type" means in <c>res@protocolInfo</c>.
        /// </para>
        /// </remarks>
        Task<bool> UpdateDlnaMappingAsync(
            Guid publicId,
            DlnaMime mime,
            string? dlnaProfileName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores extracted metadata and marks the file as processed at its current content stamp.
        /// </summary>
        Task SaveMetadataAsync(
            Guid publicId,
            MediaMetadataResult metadata,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// One generated thumbnail by its external identifier, without its image bytes.
        /// </summary>
        /// <remarks>
        /// This is the read behind <c>/fileserver/thumbnail/{id}</c>, whose identifier comes from the
        /// <c>albumArtURI</c> a renderer was handed while browsing.
        /// </remarks>
        Task<ThumbnailDto?> GetThumbnailByPublicIdAsync(
            Guid thumbnailPublicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The image bytes of a thumbnail stored in the database, or null when only the file copy exists.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="GetThumbnailByPublicIdAsync"/> so resolving a thumbnail never drags
        /// its blob along; the blob lives in its own table for the same reason.
        /// </remarks>
        Task<byte[]?> GetThumbnailContentAsync(
            Guid thumbnailPublicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a generated thumbnail and marks the file as thumbnailed at its current content stamp.
        /// </summary>
        Task SaveThumbnailAsync(
            Guid publicId,
            GeneratedThumbnail thumbnail,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a file as needing no thumbnail, so it stops appearing as pending work.
        /// </summary>
        /// <remarks>
        /// Audio files have no thumbnail of their own. Without this they match the pending query on
        /// every pass forever - which is what the reference did, re-examining them endlessly.
        /// </remarks>
        Task MarkThumbnailNotApplicableAsync(Guid publicId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts a failed attempt so a file that cannot be processed stops being retried on every pass.
        /// </summary>
        Task RecordProcessingFailureAsync(
            Guid publicId,
            bool metadataFailed,
            bool thumbnailFailed,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records whether reading this file into the served-bytes cache is worth attempting again.
        /// </summary>
        /// <remarks>
        /// Set once reading the file has actually failed - an IO error or a permission denial - so it is
        /// not re-attempted on every request. Deliberately not set for a file that is merely larger than
        /// the per-file limit: that limit is configuration, and raising it must be enough to make the
        /// file cacheable again, so the size is compared live at request time instead of recorded here.
        /// Streaming from disc is unaffected either way; only caching is skipped.
        /// <para>
        /// One direction only. Clearing the flag belongs to <see cref="ResetCacheExclusionAsync"/>, which
        /// the operator drives, and to <see cref="UpdateContentAsync"/>, where new bytes say nothing
        /// about the old failure - so this never had a caller passing the other value.
        /// </para>
        /// </remarks>
        Task MarkExcludedFromCacheAsync(
            Guid publicId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lets the files in <paramref name="scope"/> be read into the byte cache again, and returns how
        /// many were still barred.
        /// </summary>
        /// <remarks>
        /// The operator's way out of a latch. <see cref="MarkExcludedFromCacheAsync"/> sets the flag when
        /// reading a file has actually failed and nothing clears it afterwards, so a file that was
        /// briefly unreadable - an unmounted share, a permission since corrected - stays barred from the
        /// cache for the life of its row however healthy it has become. Only rows that are barred are
        /// counted, so the number reported is what changed rather than what was looked at.
        /// </remarks>
        Task<int> ResetCacheExclusionAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the tags stored for a scope and leaves it at that. Nothing is re-read.
        /// </summary>
        Task<int> ClearTagsAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the tags stored for a scope and re-queues those files so they are read again.
        /// </summary>
        /// <remarks>
        /// Rebuilds the typed details along with them, because both come from the same probe. Returns the
        /// number of files re-queued, not the number of tags removed.
        /// </remarks>
        Task<int> RecreateTagsAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every stored tag and leaves it at that. Nothing is re-read.
        /// </summary>
        /// <remarks>
        /// The counterpart to turning <c>Library.ReadContainerTags</c> off: that stops tags being
        /// collected but leaves what is already there, and this is what removes it. A file whose
        /// metadata is read again later will collect its tags again, if the option is back on.
        /// </remarks>
        Task<int> PurgeAllTagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every stored tag and re-queues every file so they are read again.
        /// </summary>
        /// <remarks>
        /// Tags can only be obtained by probing the file, and the probe that obtains them is the metadata
        /// pass - so this necessarily rebuilds the typed metadata too, and costs the same as
        /// <see cref="ClearAllMetadataAsync"/> over the whole library. Returns the number of files
        /// re-queued, not the number of tags removed.
        /// </remarks>
        Task<int> RecreateAllTagsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgets every file's extracted metadata, so the processing pass reads it again.
        /// </summary>
        /// <remarks>
        /// Clears the stamp rather than the values: the pending-work query selects rows whose metadata
        /// stamp no longer matches their content stamp, so a null stamp is what schedules the work. The
        /// failure count is reset too, otherwise a file that had already failed three times would stay
        /// retired and never be re-read.
        /// </remarks>
        Task<int> ClearAllMetadataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes every thumbnail row and forgets each file's thumbnail stamp.
        /// </summary>
        /// <remarks>
        /// Deletes the rows rather than only clearing stamps, so the stored copies go with them - a
        /// thumbnail row kept alongside a null stamp would be served while a replacement was generated.
        /// The image files beside the media are left alone: they are the reference's layout and a rescan
        /// adopts them, which is the behaviour that makes a redeploy cheap.
        /// </remarks>
        Task<int> ClearAllThumbnailsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgets one file's metadata and thumbnail, so both are produced again.
        /// </summary>
        Task<bool> ResetProcessingAsync(Guid publicId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards the metadata of the files in <paramref name="scope"/> and stops it being produced again.
        /// </summary>
        /// <remarks>
        /// The difference from <see cref="RecreateMetadataAsync"/> is the suppression flag, not the delete:
        /// clearing the stamp alone is exactly what schedules the background pass, so without suppression
        /// the two operations would be indistinguishable. Returns the number of files affected.
        /// </remarks>
        Task<int> ClearMetadataAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards the metadata of the files in <paramref name="scope"/> and queues it to be read again.
        /// </summary>
        /// <remarks>
        /// Lifts any suppression left by <see cref="ClearMetadataAsync"/> and resets the failure count,
        /// so a file that had already failed its retries is reconsidered rather than staying retired.
        /// </remarks>
        Task<int> RecreateMetadataAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the thumbnails of the files in <paramref name="scope"/> and stops them being made again.
        /// </summary>
        /// <remarks>
        /// As with <see cref="ClearAllThumbnailsAsync"/>, the images beside the media are left alone - a
        /// rescan adopts them, which is what makes a redeploy cheap.
        /// </remarks>
        Task<int> ClearThumbnailsAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the thumbnails of the files in <paramref name="scope"/> and queues them to be made again.
        /// </summary>
        Task<int> RecreateThumbnailsAsync(MediaFileScope scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// A page of thumbnails, ordered by the media path they belong to.
        /// </summary>
        Task<IReadOnlyList<ThumbnailDto>> GetThumbnailPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds indexed files matching every filter that is set.
        /// </summary>
        /// <remarks>
        /// The name match is case-insensitive and the metadata filters read the extracted stream records,
        /// so a file whose metadata has not been produced yet cannot satisfy one. Ordered by path, and
        /// bounded by <see cref="MediaFileSearchRequest.Take"/>.
        /// </remarks>
        Task<IReadOnlyList<MediaFileDto>> SearchAsync(
            MediaFileSearchRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The distinct audio and subtitle language tags present in the <i>visible</i> index, each sorted.
        /// </summary>
        /// <remarks>
        /// Feeds the search form's language filters, so it only ever offers a tick that can match
        /// something. Files whose metadata has not been read yet contribute nothing.
        /// <para>
        /// Hides <c>Library.ExcludeFolders</c> like every other listing, and that is load-bearing rather
        /// than tidiness: this used to read the stream tables directly, so a hidden folder's languages
        /// populated the dropdowns. That both told the operator what was in there and offered a filter
        /// that then matched nothing.
        /// </para>
        /// </remarks>
        Task<MediaLanguagesDto> GetLanguagesAsync(CancellationToken cancellationToken = default);

        Task<int> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// How many files a listing would show, split by kind.
        /// </summary>
        /// <remarks>
        /// Hides <c>Library.ExcludeFolders</c> like every other listing, which is why it does not simply
        /// break <see cref="CountAsync"/> down: that one counts every row on purpose, because the indexer
        /// reconciles against it, and the two answers are allowed to differ.
        /// <para>
        /// The kind is derived from the MIME rather than stored, and no column indexes either, so the
        /// grouping is done in SQL on <c>Mime</c> and folded to the kind here - the same shape the media
        /// filter in <c>SearchAsync</c> uses, and the reason both are one query rather than one per kind.
        /// </para>
        /// </remarks>
        Task<LibraryCountsDto> CountByKindAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether any file is indexed at or under <paramref name="folderPath"/>.
        /// </summary>
        /// <remarks>
        /// What decides a <b>first fill</b>, and it has to be the files rather than the folder row. The
        /// indexer used to ask whether a directory row existed for the source folder, but
        /// <c>LibraryScanner.EnumerateDirectories</c> yields a readable root unconditionally and the
        /// directory pass runs before the unusable-folder gate - so a pass over a present, readable,
        /// <b>empty</b> mountpoint created the row. A NAS that starts the server before the volume mounts
        /// produces exactly that, and the next pass then saw a root that already had a row, decided it
        /// was not a first fill, and stamped all 25,504 files with one identical date. That is the
        /// destruction of <i>Recently added</i> the first-fill rule exists to prevent, arriving through a
        /// path nothing covered. A folder with no files in it has not been filled, whatever its own row
        /// says.
        /// <para>
        /// Deliberately <b>not</b> filtered by <c>Library.ExcludeFolders</c>, like the indexer's other
        /// reads: whether a fill happened is a fact about the index, not about what a renderer is shown.
        /// </para>
        /// </remarks>
        Task<bool> AnyUnderPathAsync(string folderPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds files and returns how many were stored, without reading them back.
        /// </summary>
        /// <remarks>
        /// What a scan wants. <see cref="AddRangeAsync"/> re-reads every inserted row through the full
        /// DTO projection - three LEFT JOINs and an ordered correlated subquery each - and the indexer
        /// used it only for <c>Count</c>, so a cold index over 25,504 files paid ~15 MB of allocation and
        /// 51 extra queries for a number the insert already knew.
        /// </remarks>
        Task<int> AddRangeReturningCountAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts files and saves. Returns the stored rows, including the identifiers assigned to them.
        /// </summary>
        /// <remarks>
        /// <b>No production path calls this</b> - the indexer moved to
        /// <see cref="AddRangeReturningCountAsync"/> and nothing else inserts files. It is kept
        /// deliberately rather than retired: it is how a test seeds rows and then gets hold of the
        /// identifiers the database assigned them, which around thirty fixtures rely on, and the
        /// alternative is an insert followed by a read at every one of those sites for no runtime gain.
        /// Reach for the count overload from anything that ships.
        /// </remarks>
        Task<IReadOnlyList<MediaFileDto>> AddRangeAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes files and their metadata, subtitles and thumbnails. Reports how many were removed,
        /// and names the preview images left behind for the caller to delete.
        /// </summary>
        /// <remarks>
        /// The paths are the caller's to act on, because persistence never touches the disc - the same
        /// split <see cref="MoveOrRenameAsync"/> makes. Nothing else ever removes one of these images:
        /// previews sit beside the media in a folder scanning is configured to skip, so a preview whose
        /// media file is gone is unreachable and permanent.
        /// </remarks>
        Task<(int Removed, IReadOnlyList<string> AbandonedThumbnailPaths)> RemoveByPublicIdsAsync(
            IReadOnlyCollection<Guid> publicIds,
            CancellationToken cancellationToken = default);
    }
}
