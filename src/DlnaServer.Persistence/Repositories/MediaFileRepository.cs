using System.Linq.Expressions;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DlnaServer.Persistence.Repositories
{
    /// <inheritdoc cref="IMediaFileRepository"/>
    internal sealed class MediaFileRepository : IMediaFileRepository
    {
        /// <summary>
        /// Ceiling on a search, however much the caller asks for.
        /// </summary>
        private const int MaxSearchResults = 1_000;

        // Matches MediaFileEntityConfiguration's HasMaxLength(128). SQLite ignores a declared text
        // length, so that declaration bounds nothing at runtime and this constant is what does.
        private const int MaxProfileNameLength = 128;

        // Aliased rather than redeclared: HiddenPathQuery owns the value, and the patterns built
        // there and the ones built here must escape identically.
        private const string LikeEscape = HiddenPathQuery.LikeEscape;

        private readonly DlnaDbContext _dbContext;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ITemporaryFolderVisibility _visibility;

        public MediaFileRepository(
            DlnaDbContext dbContext,
            IOptionsMonitor<DlnaOptions> options,
            ITemporaryFolderVisibility visibility)
        {
            _dbContext = dbContext;
            _options = options;
            _visibility = visibility;
        }

        /// <remarks>
        /// Filtered by <see cref="ITemporaryFolderVisibility.HiddenFromDelivery"/>, and by that alone.
        /// <c>ExcludeFolders</c> stays exempt here, so a renderer already streaming a file is not cut off
        /// when a folder is retired; a temporarily hidden folder is the opposite case, where a link kept
        /// from an earlier listing has to stop working or the hiding means nothing.
        /// </remarks>
        public Task<MediaFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return ProjectFiles(
                    f => f.PublicId == publicId,
                    hiddenFolders: _visibility.HiddenFromDelivery)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <remarks>
        /// Unfiltered, unlike the identifier lookup above: this is the indexer's own read, and hiding a
        /// row from it would have the scan re-insert the file against the unique index on the path.
        /// </remarks>
        public Task<MediaFileDto?> GetByPathAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

            return ProjectFiles(f => f.FullPath == fullPath).FirstOrDefaultAsync(cancellationToken);
        }

        public Task<MediaFileDetailsDto?> GetWithDetailsAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            var files = HiddenPathQuery.ExcludeHiddenFiles(
                _dbContext.Files.AsNoTracking(),
                _visibility.HiddenFromDelivery);

            return files

                // Split: AudioStreams and Subtitles are both projected as collections, so a single query
                // multiplies their rows against each other - 5 audio tracks and 4 subtitles build 20 rows
                // instead of 9, each repeating a FullPath declared at 4096 chars. EF warns about it at
                // runtime, which is how this was found in the live log rather than by a test.
                .AsSplitQuery()
                .Where(f => f.PublicId == publicId)
                .Select(static f => new MediaFileDetailsDto
                {
                    File = new MediaFileDto
                    {
                        PublicId = f.PublicId,
                        FullPath = f.FullPath,
                        FileName = f.FileName,
                        Title = f.Title,
                        Extension = f.Extension,
                        DirectoryPublicId = f.Directory != null ? f.Directory.PublicId : null,
                        Mime = f.Mime,
                        DlnaProfileName = f.DlnaProfileName,
                        UpnpClass = f.UpnpClass,
                        SizeInBytes = f.SizeInBytes,
                        Duration = f.Video != null && f.Video.Duration != null
                            ? f.Video.Duration
                            : f.AudioStreams
                                .OrderByDescending(a => a.IsDefault)
                                .ThenBy(a => a.StreamIndex)
                                .Select(a => a.Duration)
                                .FirstOrDefault(),
                        Width = f.Video != null ? f.Video.Width : null,
                        Height = f.Video != null ? f.Video.Height : null,
                        Bitrate = f.Video != null ? f.Video.Bitrate : null,
                        VideoCodec = f.Video != null ? f.Video.Codec : null,

                        // The same track Duration falls back to - flagged default first, then container order -
                        // so every audio attribute in one DIDL response describes one track rather than three.
                        AudioChannels = f.AudioStreams
                            .OrderByDescending(a => a.IsDefault)
                            .ThenBy(a => a.StreamIndex)
                            .Select(a => a.Channels)
                            .FirstOrDefault(),
                        AudioSampleRate = f.AudioStreams
                            .OrderByDescending(a => a.IsDefault)
                            .ThenBy(a => a.StreamIndex)
                            .Select(a => a.SampleRate)
                            .FirstOrDefault(),
                        AudioCodec = f.AudioStreams
                            .OrderByDescending(a => a.IsDefault)
                            .ThenBy(a => a.StreamIndex)
                            .Select(a => a.Codec)
                            .FirstOrDefault(),
                        FileCreatedUtc = f.FileCreatedUtc,
                        FileModifiedUtc = f.FileModifiedUtc,
                        CreatedUtc = f.CreatedUtc,
                        IsExcludedFromCache = f.IsExcludedFromCache,
                        ContentStamp = f.ContentStamp,
                        MetadataStamp = f.MetadataStamp,
                        ThumbnailStamp = f.ThumbnailStamp,
                        IsMetadataSuppressed = f.IsMetadataSuppressed,
                        IsThumbnailSuppressed = f.IsThumbnailSuppressed,
                        IsThumbnailRebuildForced = f.IsThumbnailRebuildForced,
                        MetadataFailureCount = f.MetadataFailureCount,
                        ThumbnailFailureCount = f.ThumbnailFailureCount,
                        ThumbnailPublicId = f.Thumbnail != null ? f.Thumbnail.PublicId : null,
                    },
                    DirectoryFullPath = f.Directory != null ? f.Directory.FullPath : null,
                    AudioStreams = f.AudioStreams
                        .OrderBy(a => a.StreamIndex)
                        .Select(a => new AudioStreamDto
                        {
                            StreamIndex = a.StreamIndex,
                            Title = a.Title,
                            IsDefault = a.IsDefault,
                            Duration = a.Duration,
                            Codec = a.Codec,
                            Bitrate = a.Bitrate,
                            SampleRate = a.SampleRate,
                            Channels = a.Channels,
                            Language = a.Language,
                        })
                        .ToList(),
                    Video = f.Video == null
                        ? null
                        : new VideoStreamDto
                        {
                            Duration = f.Video.Duration,
                            Width = f.Video.Width,
                            Height = f.Video.Height,
                            FrameRate = f.Video.FrameRate,
                            AspectRatio = f.Video.AspectRatio,
                            Bitrate = f.Video.Bitrate,
                            PixelFormat = f.Video.PixelFormat,
                            Rotation = f.Video.Rotation,
                            Codec = f.Video.Codec,
                        },
                    Subtitles = f.Subtitles
                        .OrderBy(s => s.StreamIndex)
                        .Select(s => new SubtitleStreamDto
                        {
                            StreamIndex = s.StreamIndex,
                            Language = s.Language,
                            Codec = s.Codec,
                            ExternalFilePath = s.ExternalFilePath,
                        })
                        .ToList(),
                    Thumbnail = f.Thumbnail == null
                        ? null
                        : new ThumbnailDto
                        {
                            PublicId = f.Thumbnail.PublicId,
                            MediaFilePublicId = f.PublicId,
                            FilePath = f.Thumbnail.FilePath,
                            MediaFileFullPath = f.FullPath,
                            Mime = f.Thumbnail.Mime,
                            Width = f.Thumbnail.Width,
                            Height = f.Thumbnail.Height,
                            SizeInBytes = f.Thumbnail.SizeInBytes,
                            HasStoredContent = f.Thumbnail.Content != null,
                        },
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<int> CountByDirectoryAsync(
            Guid directoryPublicId,
            CancellationToken cancellationToken = default)
        {
            return ProjectFiles(
                    f => f.Directory != null && f.Directory.PublicId == directoryPublicId,
                    hiddenFolders: _visibility.HiddenFromListings)
                .CountAsync(cancellationToken);
        }

        /// <summary>
        /// One page of a directory's files, ordered and sliced by the database.
        /// </summary>
        /// <remarks>
        /// Browse used to read every row in the folder, sort the whole list in memory and then hand back
        /// at most a hundred: a 5,000-file folder cost 5,000 DTOs per request, and the backing array
        /// crossed the large object heap past about ten thousand entries.
        /// <para>
        /// Both sort keys are now covered by a composite index leading on <c>DirectoryId</c>
        /// - <c>(DirectoryId, Title)</c> and <c>(DirectoryId, FileCreatedUtc)</c> - so SQLite satisfies
        /// the filter and the order from one index. This comment used to claim they were indexed when
        /// only <c>Title</c> was, and only on its own: with single-column indexes SQLite can serve the
        /// filter or the order but not both, so it sorted the matched rows in a temp B-tree, and
        /// <c>UseMemoryTempStore</c> is <c>false</c>, which put that B-tree on the platter.
        /// <c>FileCreatedUtc</c> had no index at all - distinct from the indexed <c>CreatedUtc</c>.
        /// </para>
        /// </remarks>
        public async Task<IReadOnlyList<MediaFileDto>> GetByDirectoryPageAsync(
            Guid directoryPublicId,
            int skip,
            int take,
            bool sortByDate,
            bool descending,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(skip);

            if (take <= 0)
            {
                return [];
            }

            var query = ProjectFiles(
                f => f.Directory != null && f.Directory.PublicId == directoryPublicId,
                hiddenFolders: _visibility.HiddenFromListings);

            query = (sortByDate, descending) switch
            {
                (true, false) => query.OrderBy(static f => f.FileCreatedUtc),
                (true, true) => query.OrderByDescending(static f => f.FileCreatedUtc),
                (false, false) => query.OrderBy(static f => f.Title),
                (false, true) => query.OrderByDescending(static f => f.Title),
            };

            return await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaFileDto>> GetByDirectoryAsync(
            Guid directoryPublicId,
            CancellationToken cancellationToken = default)
        {
            return await ProjectFiles(
                    f => f.Directory != null && f.Directory.PublicId == directoryPublicId,
                    hiddenFolders: _visibility.HiddenFromListings)
                .OrderBy(static f => f.Title)
                .ToListAsync(cancellationToken);
        }

        public async Task<MediaFileNeighboursDto> GetPlayableNeighboursAsync(
            Guid directoryPublicId,
            Guid filePublicId,
            CancellationToken cancellationToken = default)
        {
            // Two columns, not a MediaFileDto. Deliberately NOT composed over ProjectFiles: that
            // projection carries correlated subqueries for duration and the audio stream, and narrowing
            // it afterwards is not guaranteed to remove them from the emitted SQL. Same predicate, same
            // hidden-path filter and same ordering, so the sequence the buttons walk is unchanged.
            //
            // `!= null` rather than `is null`: the latter is a compile error inside an expression tree.
            IQueryable<MediaFileEntity> query = _dbContext.Files
                .AsNoTracking()
                .Where(f => f.Directory != null && f.Directory.PublicId == directoryPublicId);

            var siblings = await ExcludeHidden(query)
                .OrderBy(static f => f.Title)
                .Select(static f => new { f.PublicId, f.Mime })
                .ToListAsync(cancellationToken);

            // Playable filtered here rather than in SQL: DlnaMime maps to a media kind through
            // DlnaMimeCatalog, a dictionary lookup no expression tree can translate. Emitting the
            // equivalent IN list would pin a hundred enum members into the query and drift the moment
            // the catalog gains a format.
            var playable = siblings
                .Where(static s => s.Mime.ToMedia() is DlnaMedia.Video or DlnaMedia.Audio or DlnaMedia.Image)
                .ToList();

            var index = playable.FindIndex(s => s.PublicId == filePublicId);

            return new MediaFileNeighboursDto
            {
                Index = index,
                Count = playable.Count,
                PreviousPublicId = index > 0
                    ? playable[index - 1].PublicId
                    : null,
                NextPublicId = index >= 0 && index < playable.Count - 1
                    ? playable[index + 1].PublicId
                    : null,
            };
        }

        public async Task<IReadOnlyList<MediaFileDto>> GetRecentlyAddedAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            return await ProjectFiles(predicate: null, hiddenFolders: _visibility.HiddenFromListings)
                .OrderByDescending(static f => f.CreatedUtc)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaFileDto>> GetPendingProcessingAsync(
            int maxCount,
            int maxFailureCount,
            IReadOnlyCollection<Guid>? excludedPublicIds = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

            // The suppression flags are what make "clear" different from "recreate": a cleared file has
            // nothing to serve and must stay that way until asked for, so it is excluded here rather than
            // picked up on the next pass.
            // Hidden files are excluded unconditionally: retiring a folder through ExcludeFolders is meant
            // to stop work on it, so there is no caller that wants ffprobe and thumbnails run over content
            // no renderer will ever be shown.
            // ExcludeFolders alone, deliberately, and the one place the two kinds of hiding differ in this
            // direction: a temporarily hidden folder stays current, so it must still get its metadata and
            // its thumbnails - otherwise revealing it would show a wall of blank tiles until a later pass.
            var pending = ProjectFiles(
                f =>
                    (!f.IsMetadataSuppressed && f.MetadataStamp != f.ContentStamp && f.MetadataFailureCount < maxFailureCount)
                    || (!f.IsThumbnailSuppressed && f.ThumbnailStamp != f.ContentStamp && f.ThumbnailFailureCount < maxFailureCount),
                hiddenFolders: _options.CurrentValue.Library.ExcludeFolders);

            // Only while the row still carries a failure. Recreating, letting a file back in and a change
            // of content all reset the counts, and none of those should wait out a retry delay.
            if (excludedPublicIds is { Count: > 0 })
            {
                pending = pending.Where(f =>
                    !excludedPublicIds.Contains(f.PublicId)
                    || (f.MetadataFailureCount == 0 && f.ThumbnailFailureCount == 0));
            }

            return await pending
                .OrderBy(static f => f.CreatedUtc)
                .Take(maxCount)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlySet<string>> GetExistingPathsAsync(
            IReadOnlyCollection<string> fullPaths,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fullPaths);

            if (fullPaths.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var found = await _dbContext.Files
                .AsNoTracking()
                .Where(f => fullPaths.Contains(f.FullPath))
                .Select(static f => f.FullPath)
                .ToListAsync(cancellationToken);

            return new HashSet<string>(found, StringComparer.Ordinal);
        }

        public async Task<IReadOnlyList<IndexedFileDto>> GetIndexedPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            var query = _dbContext.Files.AsNoTracking();

            if (afterFullPath is not null)
            {
                query = query.Where(f => string.Compare(f.FullPath, afterFullPath) > 0);
            }

            return await query
                .OrderBy(static f => f.FullPath)
                .Take(take)
                .Select(static f => new IndexedFileDto
                {
                    PublicId = f.PublicId,
                    FullPath = f.FullPath,
                    ContentStamp = f.ContentStamp,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<IndexedFileDto>> FindByContentStampsAsync(
            IReadOnlyCollection<string> contentStamps,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(contentStamps);

            if (contentStamps.Count == 0)
            {
                return [];
            }

            return await _dbContext.Files
                .AsNoTracking()
                .Where(f => contentStamps.Contains(f.ContentStamp))
                .Select(static f => new IndexedFileDto
                {
                    PublicId = f.PublicId,
                    FullPath = f.FullPath,
                    ContentStamp = f.ContentStamp,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<(bool Moved, string? AbandonedThumbnailPath)> MoveOrRenameAsync(
            Guid publicId,
            Guid supersededPublicId,
            CancellationToken cancellationToken = default)
        {
            var kept = await _dbContext.Files
                .Include(static f => f.Thumbnail)
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            var superseded = await _dbContext.Files
                .FirstOrDefaultAsync(f => f.PublicId == supersededPublicId, cancellationToken);

            if (kept is null || superseded is null || kept.Id == superseded.Id)
            {
                return (false, null);
            }

            // Every location field is taken from the row the scan just inserted rather than recomputed
            // here: that row was built by the scanner from the file as it now is, so the directory link
            // and the name are already resolved and cannot disagree with what the rest of the pass saw.
            var fullPath = superseded.FullPath;
            var fileName = superseded.FileName;
            var title = superseded.Title;
            var extension = superseded.Extension;
            var directoryId = superseded.DirectoryId;
            var fileCreatedUtc = superseded.FileCreatedUtc;
            var fileModifiedUtc = superseded.FileModifiedUtc;
            var sizeInBytes = superseded.SizeInBytes;
            var contentStamp = superseded.ContentStamp;

            // Saved in two steps on purpose. FullPath is uniquely indexed, so moving the kept row onto a
            // path the superseded row still occupies depends on EF choosing to emit the delete first -
            // which it does not guarantee. Freeing the path in its own round trip makes the order ours.
            // Deliberately untransacted: a failure between the two leaves the file simply un-indexed, and
            // the next pass inserts it and pairs it again, so the window costs a scan rather than a row.
            _ = _dbContext.Files.Remove(superseded);

            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            kept.FullPath = fullPath;
            kept.FileName = fileName;
            kept.Title = title;
            kept.Extension = extension;
            kept.DirectoryId = directoryId;
            kept.FileCreatedUtc = fileCreatedUtc;
            kept.FileModifiedUtc = fileModifiedUtc;
            kept.SizeInBytes = sizeInBytes;
            kept.ContentStamp = contentStamp;

            // The preview lives beside the media, so its path is derived from the old folder and is wrong
            // the moment the file moves. Dropped and re-made rather than moved on disc, which is what the
            // reference does: one regenerated preview is cheaper than a file operation that can half-fail
            // and leave the row pointing at neither copy.
            var abandonedThumbnailPath = kept.Thumbnail?.FilePath;

            if (kept.Thumbnail is not null)
            {
                _ = _dbContext.Thumbnails.Remove(kept.Thumbnail);

                kept.Thumbnail = null;
                kept.ThumbnailStamp = null;
                kept.ThumbnailFailureCount = 0;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            return (true, abandonedThumbnailPath);
        }

        public async Task<int> UpdateContentAsync(
            IReadOnlyCollection<MediaFileContentUpdateDto> updates,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updates);

            if (updates.Count == 0)
            {
                return 0;
            }

            var byPublicId = updates.ToDictionary(static u => u.PublicId);
            var publicIds = byPublicId.Keys.ToArray();

            var entities = await _dbContext.Files
                .Where(f => publicIds.Contains(f.PublicId))
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                var update = byPublicId[entity.PublicId];

                entity.SizeInBytes = update.SizeInBytes;
                entity.FileModifiedUtc = update.FileModifiedUtc;
                entity.ContentStamp = update.ContentStamp;

                // Clearing the stamps is what puts the file back in the processing queue.
                entity.MetadataStamp = null;
                entity.ThumbnailStamp = null;
                entity.MetadataFailureCount = 0;
                entity.ThumbnailFailureCount = 0;

                // The content changed, so a previous failure to cache it says nothing about the new
                // bytes - a file that was too large may now be small enough. Left set, this flag was
                // permanent: it was written in one place and cleared nowhere, so one failed read
                // condemned the path forever. The reference clears its own equivalent both ways.
                entity.IsExcludedFromCache = false;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            return entities.Count;
        }

        public async Task<bool> UpdateDlnaMappingAsync(
            Guid publicId,
            DlnaMime mime,
            string? dlnaProfileName,
            CancellationToken cancellationToken = default)
        {
            // Gated here rather than only in the editor, because the editor's dropdown is client-side.
            // `Enum.TryParse` accepts a numeric string, so a bound enum can arrive undefined - the same
            // hole LibraryIndexer.BuildScanOptions already guards, and its comment there names the admin
            // editor as the validation that was assumed and was not enough.
            if (!Enum.IsDefined(mime) || !mime.IsServable() || !IsValidProfileName(dlnaProfileName))
            {
                return false;
            }

            // AsNoTracking, and this is load-bearing rather than tidiness. A Blazor circuit holds ONE DI
            // scope for its whole life, so this context's identity map accumulates across every click -
            // and a tracked read for an already-tracked row returns the cached copy, not the row. The
            // comparison below reads a VALUE, and the ExecuteUpdate siblings (ResetCacheExclusionAsync,
            // RecordProcessingFailureAsync) bypass the tracker entirely, so a cached copy can disagree
            // with the database with nothing to signal it.
            var current = await _dbContext.Files
                .AsNoTracking()
                .Where(f => f.PublicId == publicId)
                .Select(static f => new { f.Mime })
                .FirstOrDefaultAsync(cancellationToken);

            if (current is null)
            {
                return false;
            }

            // Blank resolves to the catalogue's own profile for the type, which is what the insert path
            // stores. Storing null instead would put two files identical on the wire into two different
            // states, and the admin facts row renders the column directly.
            var profileName = string.IsNullOrWhiteSpace(dlnaProfileName)
                ? mime.ToMainProfileName()
                : dlnaProfileName.Trim();

            // Derived, not chosen - the same rule LibraryIndexer applies at insert. A picture retyped as
            // video that kept imageItem would claim one thing in its class and another in its type.
            var upnpClass = mime.ToDefaultItemClass();

            _ = await _dbContext.Files
                .Where(f => f.PublicId == publicId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(f => f.Mime, _ => mime)
                        .SetProperty(f => f.DlnaProfileName, _ => profileName)
                        .SetProperty(f => f.UpnpClass, _ => upnpClass),
                    cancellationToken);

            if (current.Mime.ToMedia() != mime.ToMedia())
            {
                // Its own statement rather than more setters above, because it must not run when only the
                // profile or the container changed. Both the metadata probe and the thumbnail generator
                // dispatch on the media KIND, so on a kind change everything already stored was produced
                // by the pipeline for the old one - a video retyped to a picture otherwise keeps
                // advertising a duration and a video codec on an imageItem. Clearing the stamps is what
                // re-queues it; SaveMetadataAsync replaces the stream rows wholesale, so nothing is
                // deleted here.
                _ = await _dbContext.Files
                    .Where(f => f.PublicId == publicId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(static f => f.MetadataStamp, static _ => null)
                            .SetProperty(static f => f.ThumbnailStamp, static _ => null)
                            .SetProperty(static f => f.MetadataFailureCount, static _ => 0)
                            .SetProperty(static f => f.ThumbnailFailureCount, static _ => 0)
                            .SetProperty(static f => f.IsMetadataSuppressed, static _ => false)
                            .SetProperty(static f => f.IsThumbnailSuppressed, static _ => false)
                            .SetProperty(static f => f.IsThumbnailRebuildForced, static _ => true),
                        cancellationToken);
            }

            return true;
        }

        /// <remarks>
        /// A DLNA.ORG_PN token, which is letters, digits and underscores. The column declares
        /// <c>HasMaxLength(128)</c> but SQLite ignores a declared text length and EF Core validates none,
        /// so this is the only thing bounding what reaches
        /// <c>DLNA.ORG_PN={profile};DLNA.ORG_OP=...</c> - where a <c>;</c> would append fields of its own.
        /// </remarks>
        private static bool IsValidProfileName(string? dlnaProfileName)
        {
            if (string.IsNullOrWhiteSpace(dlnaProfileName))
            {
                return true;
            }

            var trimmed = dlnaProfileName.AsSpan().Trim();

            if (trimmed.Length > MaxProfileNameLength)
            {
                return false;
            }

            foreach (var character in trimmed)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        public async Task SaveMetadataAsync(
            Guid publicId,
            MediaMetadataResult metadata,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(extractedFromContentStamp);

            // Split: AudioStreams and Subtitles are both collections, so a single query multiplies their
            // rows against each other and repeats a FullPath declared at 4096 chars in every one.
            var entity = await _dbContext.Files
                .AsSplitQuery()
                .Include(static f => f.AudioStreams)
                .Include(static f => f.Video)
                .Include(static f => f.Subtitles)
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            ApplyAudioStreams(entity, metadata.AudioStreams);
            ApplyVideo(entity, metadata.Video);
            ApplySubtitles(entity, metadata.Subtitles);

            await ApplyTagsAsync(entity.Id, metadata.Tags, cancellationToken);

            // Stamping with the current content stamp is what removes the file from the pending query -
            // so it is only correct when the content has not moved on since the extraction ran. If the
            // file changed mid-probe, ReconcileFilesAsync has already advanced ContentStamp, and stamping
            // it here would mark the row current while holding metadata read from the previous bytes.
            // The pending predicate would then never fire again and the stale metadata would be permanent.
            if (string.Equals(entity.ContentStamp, extractedFromContentStamp, StringComparison.Ordinal))
            {
                entity.MetadataStamp = entity.ContentStamp;
                entity.MetadataFailureCount = 0;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <remarks>
        /// Replaced wholesale rather than merged. A tag that has been removed from the file has to
        /// disappear from the row too, and there is no stable identity to match a tag on - the name is
        /// not unique once a container repeats one per track. The set is small enough that rewriting it
        /// is cheaper than working out what changed.
        /// <para>
        /// Loaded and written through the tag table directly, because the relationship is declared from
        /// that side alone and <c>MediaFileEntity</c> deliberately carries no navigation to it.
        /// </para>
        /// </remarks>
        private async Task ApplyTagsAsync(
            int mediaFileId,
            IReadOnlyList<MediaFileTagDto> tags,
            CancellationToken cancellationToken)
        {
            var existing = await _dbContext.MediaFileTags
                .Where(t => t.MediaFileId == mediaFileId)
                .ToListAsync(cancellationToken);

            if (existing.Count == 0 && tags.Count == 0)
            {
                return;
            }

            _dbContext.MediaFileTags.RemoveRange(existing);

            for (var index = 0; index < tags.Count; index++)
            {
                var tag = tags[index];

                _ = _dbContext.MediaFileTags.Add(new MediaFileTagEntity
                {
                    MediaFileId = mediaFileId,
                    StreamIndex = tag.StreamIndex,
                    Name = tag.Name,
                    Value = tag.Value,
                });
            }
        }

        public async Task<IReadOnlyList<MediaFileTagDto>> GetTagsAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            // Joined explicitly because the tag table carries no navigation back to the file - the
            // relationship is declared from the tag side alone so that adding it altered nothing that
            // already existed. Insertion order is the container's own order, which is the order worth
            // showing.
            var query =
                from tag in _dbContext.MediaFileTags.AsNoTracking()
                join file in _dbContext.Files on tag.MediaFileId equals file.Id
                where file.PublicId == publicId
                orderby tag.Id
                select new MediaFileTagDto
                {
                    StreamIndex = tag.StreamIndex,
                    Name = tag.Name,
                    Value = tag.Value,
                };

            return await query.ToListAsync(cancellationToken);
        }

        public Task<ThumbnailDto?> GetThumbnailByPublicIdAsync(
            Guid thumbnailPublicId,
            CancellationToken cancellationToken = default)
        {
            return HiddenPathQuery
                .ExcludeHiddenThumbnails(_dbContext.Thumbnails.AsNoTracking(), _visibility.HiddenFromDelivery)
                .Where(t => t.PublicId == thumbnailPublicId)
                .Select(static t => new ThumbnailDto
                {
                    PublicId = t.PublicId,
                    MediaFilePublicId = t.MediaFile != null ? t.MediaFile.PublicId : Guid.Empty,
                    FilePath = t.FilePath,
                    MediaFileFullPath = t.MediaFile != null ? t.MediaFile.FullPath : null,
                    Mime = t.Mime,
                    Width = t.Width,
                    Height = t.Height,
                    SizeInBytes = t.SizeInBytes,
                    HasStoredContent = t.Content != null,
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<byte[]?> GetThumbnailContentAsync(
            Guid thumbnailPublicId,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Thumbnails
                .AsNoTracking()
                .Where(t => t.PublicId == thumbnailPublicId && t.Content != null)
                .Select(static t => t.Content!.Data)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task SaveThumbnailAsync(
            Guid publicId,
            GeneratedThumbnail thumbnail,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(thumbnail);
            ArgumentException.ThrowIfNullOrWhiteSpace(extractedFromContentStamp);

            // The blob is deliberately NOT included. Removing the thumbnail cascades into its content
            // row in the database, so loading roughly 40 KB per save to delete a row we already know the
            // key of was pure waste - about 800 MB of reads across a 20,000-file re-thumbnail.
            var entity = await _dbContext.Files
                .Include(static f => f.Thumbnail)
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            if (entity.Thumbnail is not null)
            {
                _dbContext.Thumbnails.Remove(entity.Thumbnail);
            }

            entity.Thumbnail = new ThumbnailEntity
            {
                FilePath = thumbnail.FilePath,
                Mime = thumbnail.Mime,
                Width = thumbnail.Width,
                Height = thumbnail.Height,
                SizeInBytes = thumbnail.SizeInBytes,
                Content = thumbnail.Content is null
                    ? null
                    : new ThumbnailContentEntity { Data = thumbnail.Content },
            };

            // Conditional for the same reason as SaveMetadataAsync: a file that changed while its
            // thumbnail was being generated must stay pending rather than be marked current for content
            // the image does not depict.
            if (string.Equals(entity.ContentStamp, extractedFromContentStamp, StringComparison.Ordinal))
            {
                entity.ThumbnailStamp = entity.ContentStamp;
                entity.ThumbnailFailureCount = 0;

                // The rebuild asked for has happened, so adoption is allowed again. Cleared alongside
                // the stamp rather than unconditionally: a file that changed mid-generation stays
                // pending, and it must stay forced too or the next pass would adopt the image that was
                // just superseded.
                entity.IsThumbnailRebuildForced = false;
            }

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkThumbnailNotApplicableAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Files
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            if (entity is null)
            {
                return;
            }

            entity.ThumbnailStamp = entity.ContentStamp;

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task RecordProcessingFailureAsync(
            Guid publicId,
            bool metadataFailed,
            bool thumbnailFailed,
            CancellationToken cancellationToken = default)
        {
            // Incremented server-side rather than read-modify-written. There is no concurrency token on
            // MediaFileEntity, and an operator clicking "Recreate" between the worker claiming a file and
            // this write used to be silently reverted: the reset set the count to 0, this wrote the stale
            // base + 1, and the file crossed MaxFailureCount and was never returned again while the UI
            // said it had been queued.
            var files = _dbContext.Files.Where(f => f.PublicId == publicId);

            if (metadataFailed)
            {
                _ = await files.ExecuteUpdateAsync(
                    setters => setters.SetProperty(f => f.MetadataFailureCount, f => f.MetadataFailureCount + 1),
                    cancellationToken);
            }

            if (thumbnailFailed)
            {
                _ = await files.ExecuteUpdateAsync(
                    setters => setters.SetProperty(f => f.ThumbnailFailureCount, f => f.ThumbnailFailureCount + 1),
                    cancellationToken);
            }
        }

        public async Task MarkExcludedFromCacheAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Files
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            if (entity is null || entity.IsExcludedFromCache)
            {
                return;
            }

            entity.IsExcludedFromCache = true;

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> ResetCacheExclusionAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            var files = await ResolveScopeAsync(scope, cancellationToken);

            if (files is null)
            {
                return 0;
            }

            // Narrowed to the barred rows, so the count the operator is shown is what was let back in
            // rather than how many files the scope happened to cover.
            return await files
                .Where(static f => f.IsExcludedFromCache)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(static f => f.IsExcludedFromCache, static _ => false),
                    cancellationToken);
        }

        /// <remarks>
        /// Replaced wholesale rather than merged, the way <see cref="ApplySubtitles"/> is: a re-probe can
        /// return a different number of tracks, and matching them up by index would keep a stale row when
        /// the count shrinks.
        /// </remarks>
        private static void ApplyAudioStreams(MediaFileEntity entity, IReadOnlyList<AudioStreamDto> audioStreams)
        {
            entity.AudioStreams.Clear();

            foreach (var audio in audioStreams)
            {
                entity.AudioStreams.Add(new AudioStreamEntity
                {
                    StreamIndex = audio.StreamIndex,
                    Title = audio.Title,
                    IsDefault = audio.IsDefault,
                    Duration = audio.Duration,
                    Codec = audio.Codec,
                    Bitrate = audio.Bitrate,
                    SampleRate = audio.SampleRate,
                    Channels = audio.Channels,
                    Language = audio.Language,
                });
            }
        }

        private static void ApplyVideo(MediaFileEntity entity, VideoStreamDto? video)
        {
            if (video is null)
            {
                entity.Video = null;
                return;
            }

            entity.Video ??= new VideoStreamEntity();
            entity.Video.Duration = video.Duration;
            entity.Video.Width = video.Width;
            entity.Video.Height = video.Height;
            entity.Video.FrameRate = video.FrameRate;
            entity.Video.AspectRatio = video.AspectRatio;
            entity.Video.Bitrate = video.Bitrate;
            entity.Video.PixelFormat = video.PixelFormat;
            entity.Video.Rotation = video.Rotation;
            entity.Video.Codec = video.Codec;
        }

        /// <summary>
        /// Replaces the tracks wholesale: a re-probe is the authority on what the container holds.
        /// </summary>
        private static void ApplySubtitles(MediaFileEntity entity, IReadOnlyList<SubtitleStreamDto> subtitles)
        {
            entity.Subtitles.Clear();

            foreach (var subtitle in subtitles)
            {
                entity.Subtitles.Add(new SubtitleStreamEntity
                {
                    StreamIndex = subtitle.StreamIndex,
                    Language = subtitle.Language,
                    Codec = subtitle.Codec,
                    ExternalFilePath = subtitle.ExternalFilePath,
                });
            }
        }

        public async Task<MediaLanguagesDto> GetLanguagesAsync(CancellationToken cancellationToken = default)
        {
            // Reached through Files rather than off the stream tables directly, so ExcludeHidden applies.
            // Querying AudioStreams/SubtitleStreams straight was a fourth, undocumented exception to the
            // one-library rule: a hidden folder's languages still populated the search page's dropdowns,
            // which both tells the operator what is in there and offers a filter that then matches
            // nothing visible.
            var visible = ExcludeHidden(_dbContext.Files.AsNoTracking());

            var audio = await visible
                .SelectMany(static f => f.AudioStreams)
                .Where(static a => a.Language != null)
                .Select(static a => a.Language!)
                .Distinct()
                .OrderBy(static language => language)
                .ToListAsync(cancellationToken);

            var subtitle = await visible
                .SelectMany(static f => f.Subtitles)
                .Where(static s => s.Language != null)
                .Select(static s => s.Language!)
                .Distinct()
                .OrderBy(static language => language)
                .ToListAsync(cancellationToken);

            return new MediaLanguagesDto
            {
                Audio = audio,
                Subtitle = subtitle,
            };
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return _dbContext.Files.CountAsync(cancellationToken);
        }

        public async Task<LibraryCountsDto> CountByKindAsync(CancellationToken cancellationToken = default)
        {
            IQueryable<MediaFileEntity> query = _dbContext.Files.AsNoTracking();
            var hiddenFolders = _visibility.HiddenFromListings;

            if (hiddenFolders is { Count: > 0 })
            {
                query = HiddenPathQuery.ExcludeHiddenFiles(query, hiddenFolders);
            }

            // Grouped on the stored MIME, which is what SQL can see; the kind each one maps to is a
            // dictionary lookup in DlnaMimeCatalog and cannot be translated.
            var byMime = await query
                .GroupBy(static f => f.Mime)
                .Select(static g => new { Mime = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var video = 0;
            var audio = 0;
            var image = 0;
            var other = 0;

            foreach (var row in byMime)
            {
                switch (row.Mime.ToMedia())
                {
                    case DlnaMedia.Video:
                        video += row.Count;
                        break;
                    case DlnaMedia.Audio:
                        audio += row.Count;
                        break;
                    case DlnaMedia.Image:
                        image += row.Count;
                        break;
                    default:
                        other += row.Count;
                        break;
                }
            }

            return new LibraryCountsDto
            {
                Total = video + audio + image + other,
                Video = video,
                Audio = audio,
                Image = image,
                Other = other,
            };
        }

        /// <remarks>
        /// A half-open range rather than a <c>LIKE 'path%'</c> prefix, so SQLite can seek the unique
        /// index on <c>FullPath</c> instead of scanning it: the successor character is the separator plus
        /// one, which bounds the subtree exactly and leaves a sibling such as <c>Media2</c> outside it.
        /// The separator is read from the stored path rather than from this host, because the rows carry
        /// the separator of whichever machine indexed them.
        /// </remarks>
        public Task<bool> AnyUnderPathAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

            var separator = StoredPath.SeparatorOf(folderPath);
            var trimmed = folderPath.TrimEnd(separator);
            var lower = trimmed + separator;
            var upper = trimmed + (char)(separator + 1);

            return _dbContext.Files
                .AsNoTracking()
                .AnyAsync(
                    f => string.Compare(f.FullPath, lower) > 0 && string.Compare(f.FullPath, upper) < 0,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<MediaFileDto>> AddRangeAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(files);

            if (files.Count == 0)
            {
                return [];
            }

            var storedIds = await InsertAsync(files, cancellationToken);

            return await ProjectFiles(f => storedIds.Contains(f.PublicId)).ToListAsync(cancellationToken);
        }

        public async Task<int> AddRangeReturningCountAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(files);

            if (files.Count == 0)
            {
                return 0;
            }

            var storedIds = await InsertAsync(files, cancellationToken);

            return storedIds.Length;
        }

        /// <remarks>
        /// The insert both public entry points share. The split exists because the indexer - the only
        /// production caller - used the DTO-returning overload and then read nothing but
        /// <c>Count</c> off it, so a cold index issued 51 extra queries returning 25,504 fully-projected
        /// DTOs, each with three LEFT JOINs and an ordered correlated subquery, purely to be counted:
        /// ~15 MB of managed allocation against a 60 MB target.
        /// </remarks>
        private async Task<Guid[]> InsertAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken)
        {
            var directoryKeys = await ResolveDirectoryKeysAsync(files, cancellationToken);
            var entities = new List<MediaFileEntity>(files.Count);

            foreach (var file in files)
            {
                int? directoryId = null;

                if (file.DirectoryPublicId is Guid directoryPublicId)
                {
                    if (!directoryKeys.TryGetValue(directoryPublicId, out var resolvedId))
                    {
                        throw new InvalidOperationException(
                            $"Parent directory '{directoryPublicId}' is not indexed, so '{file.FullPath}' cannot be added.");
                    }

                    directoryId = resolvedId;
                }

                entities.Add(new MediaFileEntity
                {
                    FullPath = file.FullPath,
                    FileName = file.FileName,
                    Title = file.Title,
                    Extension = file.Extension,
                    DirectoryId = directoryId,
                    Mime = file.Mime,
                    DlnaProfileName = file.DlnaProfileName,
                    UpnpClass = file.UpnpClass,
                    SizeInBytes = file.SizeInBytes,
                    FileCreatedUtc = file.FileCreatedUtc,
                    FileModifiedUtc = file.FileModifiedUtc,

                    // Left at default unless the caller supplied one, because DlnaDbContext.StampTimestamps
                    // fills a default CreatedUtc with the current time - that "only if unset" is the seam
                    // this rides on, so do not make it unconditional there.
                    CreatedUtc = file.IndexedUtc ?? default,
                    ContentStamp = file.ContentStamp,
                });
            }

            await _dbContext.Files.AddRangeAsync(entities, cancellationToken);

            try
            {
                _ = await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The scope lives for the whole pass, so a failed save whose entities stay tracked is
                // re-submitted by every later batch and fails again: one duplicate path ended the pass
                // rather than the batch. Clearing here keeps the failure local, and the throw still
                // reaches the caller.
                _dbContext.ForgetTrackedEntities();
                throw;
            }

            var storedIds = entities.Select(static e => e.PublicId).ToArray();

            // A scan runs every batch through one scope, so without this the tracker keeps every entity
            // inserted so far and DlnaDbContext.StampTimestamps walks all of them on the next save:
            // batch k pays O(500k), which is quadratic over a library and holds the whole of it resident
            // against a 60 MB managed-heap target. Safe here because any re-read by a caller is
            // AsNoTracking and every other read projects into a DTO.
            _dbContext.ForgetTrackedEntities();

            return storedIds;
        }

        public async Task<(int Removed, IReadOnlyList<string> AbandonedThumbnailPaths)> RemoveByPublicIdsAsync(
            IReadOnlyCollection<Guid> publicIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publicIds);

            if (publicIds.Count == 0)
            {
                return (0, []);
            }

            // Read before the delete, because the cascade takes the thumbnail rows with the files and
            // the image paths are only recoverable while they are still there. One projection of one
            // column over the same set the delete matches, not a second pass over the library.
            var abandoned = await _dbContext.Thumbnails
                .AsNoTracking()
                .Where(t => t.MediaFile != null && publicIds.Contains(t.MediaFile.PublicId))
                .Select(static t => t.FilePath)
                .ToListAsync(cancellationToken);

            // Metadata, subtitles and the thumbnail follow by cascade, configured on the relationships.
            var removed = await _dbContext.Files
                .Where(f => publicIds.Contains(f.PublicId))
                .ExecuteDeleteAsync(cancellationToken);

            return (removed, abandoned);
        }

        private async Task<Dictionary<Guid, int>> ResolveDirectoryKeysAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken)
        {
            var wanted = files
                .Select(static f => f.DirectoryPublicId)
                .OfType<Guid>()
                .ToHashSet();

            if (wanted.Count == 0)
            {
                return [];
            }

            // One lookup for the whole batch rather than a query per file.
            return await _dbContext.Directories
                .AsNoTracking()
                .Where(d => wanted.Contains(d.PublicId))
                .ToDictionaryAsync(static d => d.PublicId, static d => d.Id, cancellationToken);
        }

        /// <summary>
        /// Drops rows whose path lies under a configured excluded folder, in SQL.
        /// </summary>
        /// <remarks>
        /// <c>ExcludeFolders</c> hides content that is already indexed as well as skipping new imports, so
        /// an operator can retire a folder from the library without deleting its rows and without a
        /// rescan. The rows stay in the database - this only decides what a renderer is shown.
        /// <para>
        /// <b>Applied to every listing, the admin UI included</b>, so an operator and a television are
        /// never shown different libraries. It must still never reach the indexer's own reads:
        /// <see cref="GetExistingPathsAsync"/> and <see cref="GetIndexedPageAsync"/> decide what to
        /// insert and what to delete, so hiding an already-indexed row from them would make the next scan
        /// re-insert the same path and violate the unique index on <c>FullPath</c> - and would strand a
        /// hidden file's row after its file was deleted from disc. Lookup by identifier or by path is
        /// unfiltered too, so a renderer mid-stream is not cut off and a hidden file's page still opens
        /// from a link.
        /// </para>
        /// <para>
        /// <b>Matched on whole path-segment boundaries</b>, and an entry may be a folder name or a
        /// partial path - <c>@Recycle</c> or <c>Films/Private</c>. This is the same rule as
        /// <see cref="Core.Files.PathExclusion.IsExcluded"/>, which the scanner uses, so the two halves
        /// of the setting now agree; keep them in step.
        /// </para>
        /// <para>
        /// It used to be a raw substring of the whole path, which is what the reference does. <b>A
        /// customer reported the consequence:</b> an entry of <c>path1</c> also hid <c>path1L</c> and
        /// <c>path10</c>, and the operator had no way to see that far more was hidden than they had
        /// named. It was also asymmetric with scanning, so such a folder was imported and then hidden.
        /// Segment alignment is expressed here by wrapping both sides in a separator - <c>/a/b/</c>
        /// contains <c>/b/</c> but not <c>/bL/</c> - and the path's separators are normalised so one
        /// <c>config.json</c> works on the NAS and on a Windows development machine.
        /// </para>
        /// </remarks>
        private IQueryable<MediaFileEntity> ExcludeHidden(IQueryable<MediaFileEntity> query)
        {
            return HiddenPathQuery.ExcludeHiddenFiles(query, _visibility.HiddenFromListings);
        }

        /// <summary>
        /// The read projection for the listing DTO, and the one every listing query goes through.
        /// <para>
        /// It is <b>not</b> the only copy. Two more exist: the nested one inside
        /// <see cref="GetWithDetailsAsync"/> and the one in <see cref="SearchAsync"/> - so a new column
        /// on <see cref="MediaFileDto"/> has to be added in three places, and the compiler says so,
        /// because the DTO's properties are <c>required</c> or checked by an architecture test.
        /// </para>
        /// <para>
        /// <b>Only one of the two genuinely cannot be shared, and this doc used to claim both could not.</b>
        /// An expression expander is needed to <i>nest</i> one lambda inside another, which is true of
        /// <see cref="GetWithDetailsAsync"/>'s inner projection alone. <see cref="SearchAsync"/>'s copy is
        /// an ordinary top-level <c>Select</c> and could take an overload of this method - it orders and
        /// takes before projecting, so composing afterwards preserves the operator order and the emitted
        /// SQL. Not collapsed here because it touches committed code for no behaviour change; recorded so
        /// the next reader does not re-derive a constraint that does not exist.
        /// </para>
        /// </summary>
        private IQueryable<MediaFileDto> ProjectFiles(
            Expression<Func<MediaFileEntity, bool>>? predicate = null,
            IList<string>? hiddenFolders = null)
        {
            IQueryable<MediaFileEntity> query = _dbContext.Files.AsNoTracking();

            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            if (hiddenFolders is { Count: > 0 })
            {
                query = HiddenPathQuery.ExcludeHiddenFiles(query, hiddenFolders);
            }

            return query.Select(static f => new MediaFileDto
            {
                PublicId = f.PublicId,
                FullPath = f.FullPath,
                FileName = f.FileName,
                Title = f.Title,
                Extension = f.Extension,
                DirectoryPublicId = f.Directory != null ? f.Directory.PublicId : null,
                Mime = f.Mime,
                DlnaProfileName = f.DlnaProfileName,
                UpnpClass = f.UpnpClass,
                SizeInBytes = f.SizeInBytes,
                Duration = f.Video != null && f.Video.Duration != null
                    ? f.Video.Duration
                    : f.AudioStreams
                        .OrderByDescending(a => a.IsDefault)
                        .ThenBy(a => a.StreamIndex)
                        .Select(a => a.Duration)
                        .FirstOrDefault(),
                Width = f.Video != null ? f.Video.Width : null,
                Height = f.Video != null ? f.Video.Height : null,
                Bitrate = f.Video != null ? f.Video.Bitrate : null,
                VideoCodec = f.Video != null ? f.Video.Codec : null,

                // The same track Duration falls back to - flagged default first, then container order -
                // so every audio attribute in one DIDL response describes one track rather than three.
                AudioChannels = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.Channels)
                    .FirstOrDefault(),
                AudioSampleRate = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.SampleRate)
                    .FirstOrDefault(),
                AudioCodec = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.Codec)
                    .FirstOrDefault(),
                FileCreatedUtc = f.FileCreatedUtc,
                FileModifiedUtc = f.FileModifiedUtc,
                CreatedUtc = f.CreatedUtc,
                IsExcludedFromCache = f.IsExcludedFromCache,
                ContentStamp = f.ContentStamp,
                MetadataStamp = f.MetadataStamp,
                ThumbnailStamp = f.ThumbnailStamp,
                IsMetadataSuppressed = f.IsMetadataSuppressed,
                IsThumbnailSuppressed = f.IsThumbnailSuppressed,
                IsThumbnailRebuildForced = f.IsThumbnailRebuildForced,
                MetadataFailureCount = f.MetadataFailureCount,
                ThumbnailFailureCount = f.ThumbnailFailureCount,
                ThumbnailPublicId = f.Thumbnail != null ? f.Thumbnail.PublicId : null,
            });
        }

        public Task<int> ClearAllMetadataAsync(CancellationToken cancellationToken = default)
        {
            // ExecuteUpdateAsync, not a load-and-save loop: this touches every row in the library and
            // the change tracker has nothing useful to add. Tracked entities in this context are stale
            // afterwards, which is safe because the caller is a management endpoint that holds none.
            // Suppression is lifted too: this is the "produce everything again" action, so a file an
            // operator had cleared individually would otherwise stay absent and look like a failure.
            return _dbContext.Files
                .Where(static f => f.MetadataStamp != null || f.MetadataFailureCount != 0 || f.IsMetadataSuppressed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(static f => f.MetadataStamp, static _ => null)
                        .SetProperty(static f => f.MetadataFailureCount, static _ => 0)
                        .SetProperty(static f => f.IsMetadataSuppressed, static _ => false),
                    cancellationToken);
        }

        public Task<int> PurgeAllTagsAsync(CancellationToken cancellationToken = default)
        {
            // Only the tag rows. Nothing on the file is touched, so no file is re-probed: this is the
            // action for reclaiming the space after the option is turned off, not for rebuilding.
            return _dbContext.MediaFileTags.ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<int> RecreateAllTagsAsync(CancellationToken cancellationToken = default)
        {
            // Rows first, for the same reason as thumbnails: a page load between the two statements
            // would otherwise show the old tags while the file was already marked for re-reading.
            _ = await _dbContext.MediaFileTags.ExecuteDeleteAsync(cancellationToken);

            // Tags can only be read by probing the file, and the probe that reads them is the metadata
            // pass - so recreating tags IS recreating metadata. Clearing the stamp is what re-queues
            // every file for it; suppression is lifted for the same reason ClearAllMetadataAsync lifts
            // it, since a file an operator had cleared individually would otherwise stay tagless and
            // look like a failure.
            return await _dbContext.Files
                .Where(static f => f.MetadataStamp != null || f.MetadataFailureCount != 0 || f.IsMetadataSuppressed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(static f => f.MetadataStamp, static _ => null)
                        .SetProperty(static f => f.MetadataFailureCount, static _ => 0)
                        .SetProperty(static f => f.IsMetadataSuppressed, static _ => false),
                    cancellationToken);
        }

        public async Task<int> ClearAllThumbnailsAsync(CancellationToken cancellationToken = default)
        {
            // Rows first: the file's stamp must not be cleared while a thumbnail row still exists, or a
            // browse between the two statements would serve the old image as though it were current.
            _ = await _dbContext.Thumbnails.ExecuteDeleteAsync(cancellationToken);

            return await _dbContext.Files
                .Where(static f => f.ThumbnailStamp != null || f.ThumbnailFailureCount != 0 || f.IsThumbnailSuppressed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(static f => f.ThumbnailStamp, static _ => null)
                        .SetProperty(static f => f.ThumbnailFailureCount, static _ => 0)
                        .SetProperty(static f => f.IsThumbnailSuppressed, static _ => false)

                        // Without this the image already beside the media is adopted straight back, so
                        // this endpoint reported success and produced nothing.
                        .SetProperty(static f => f.IsThumbnailRebuildForced, static _ => true),
                    cancellationToken);
        }

        /// <remarks>
        /// Both metadata entry points funnel through one private, mirroring how the thumbnail pair
        /// already delegates - the two used to be identical bar the suppression flag.
        /// </remarks>
        private async Task<int> SetMetadataStateAsync(
            MediaFileScope scope,
            bool isSuppressed,
            CancellationToken cancellationToken)
        {
            var files = await ResolveScopeAsync(scope, cancellationToken);

            if (files is null)
            {
                return 0;
            }

            return await files.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(static f => f.MetadataStamp, static _ => null)
                    .SetProperty(static f => f.MetadataFailureCount, static _ => 0)
                    .SetProperty(f => f.IsMetadataSuppressed, _ => isSuppressed),
                cancellationToken);
        }

        public Task<int> ClearMetadataAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            return SetMetadataStateAsync(scope, isSuppressed: true, cancellationToken);
        }

        public Task<int> RecreateMetadataAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            return SetMetadataStateAsync(scope, isSuppressed: false, cancellationToken);
        }

        public async Task<int> ClearThumbnailsAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            return await ClearThumbnailsAsync(scope, isSuppressed: true, cancellationToken);
        }

        public async Task<int> RecreateThumbnailsAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            return await ClearThumbnailsAsync(scope, isSuppressed: false, cancellationToken);
        }

        public async Task<bool> ResetProcessingAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Files
                .Include(static f => f.Thumbnail)
                .FirstOrDefaultAsync(f => f.PublicId == publicId, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            if (entity.Thumbnail is not null)
            {
                _ = _dbContext.Thumbnails.Remove(entity.Thumbnail);
            }

            entity.MetadataStamp = null;
            entity.ThumbnailStamp = null;
            entity.MetadataFailureCount = 0;
            entity.ThumbnailFailureCount = 0;

            // Lifted, as the three sibling recreate methods do. Without this, /manage/recreateFilesInfo
            // on a cleared file answered Reset = true while the pending query kept excluding the row:
            // success reported, nothing ever produced.
            entity.IsMetadataSuppressed = false;
            entity.IsThumbnailSuppressed = false;

            // A full reset means produce everything again, which for a thumbnail means generate rather
            // than adopt what is already on disc.
            entity.IsThumbnailRebuildForced = true;

            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<IReadOnlyList<ThumbnailDto>> GetThumbnailPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            var query = _dbContext.Files.AsNoTracking().Where(static f => f.Thumbnail != null);

            // Written the same way as the two sibling page reads, which used string.Compare where this
            // used CompareTo and is not null where this used IsNullOrEmpty. Same answer either way - an
            // empty cursor sorts below every path - but three keyset readers spelled three ways is how
            // one of them ends up drifting.
            if (afterFullPath is not null)
            {
                query = query.Where(f => string.Compare(f.FullPath, afterFullPath) > 0);
            }

            return await query
                .OrderBy(static f => f.FullPath)
                .Take(take)
                .Select(static f => new ThumbnailDto
                {
                    PublicId = f.Thumbnail!.PublicId,
                    MediaFilePublicId = f.PublicId,
                    FilePath = f.Thumbnail.FilePath,
                    MediaFileFullPath = f.FullPath,
                    Mime = f.Thumbnail.Mime,
                    Width = f.Thumbnail.Width,
                    Height = f.Thumbnail.Height,
                    SizeInBytes = f.Thumbnail.SizeInBytes,
                    HasStoredContent = f.Thumbnail.Content != null,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaFileDto>> SearchAsync(
            MediaFileSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = ExcludeHidden(_dbContext.Files.AsNoTracking());

            if (!string.IsNullOrWhiteSpace(request.NameContains))
            {
                // LIKE rather than Contains: SQLite folds case for LIKE, which is what makes the search
                // capital-irrelevant. The term is escaped first so a name containing % or _ is searched
                // for literally instead of behaving as a wildcard.
                var pattern = $"%{HiddenPathQuery.EscapeLikeTerm(request.NameContains.Trim())}%";

                query = query.Where(f => EF.Functions.Like(f.FileName, pattern, LikeEscape));
            }

            if (request.Mime is { } mime)
            {
                query = query.Where(f => f.Mime == mime);
            }
            else if (request.Media is { } media)
            {
                // Media kind is derived from the MIME rather than stored, so it cannot be compared in
                // SQL - the matching MIME values are resolved first and the query filters on those.
                var mimes = DlnaMimeCatalog.All
                    .Where(info => info.Mime.ToMedia() == media)
                    .Select(static info => info.Mime)
                    .ToArray();

                query = query.Where(f => mimes.Contains(f.Mime));
            }

            if (request.ModifiedFromUtc is { } from)
            {
                query = query.Where(f => f.FileModifiedUtc >= from);
            }

            if (request.ModifiedToUtc is { } to)
            {
                query = query.Where(f => f.FileModifiedUtc <= to);
            }

            if (request.MinSizeInBytes is { } minSize)
            {
                query = query.Where(f => f.SizeInBytes >= minSize);
            }

            if (request.MaxSizeInBytes is { } maxSize)
            {
                query = query.Where(f => f.SizeInBytes <= maxSize);
            }

            if (request.MinWidth is { } minWidth)
            {
                // != null rather than "is not null": a null check written as a pattern is a compile error
                // inside an expression tree.
                query = query.Where(f => f.Video != null && f.Video.Width >= minWidth);
            }

            if (request.MinHeight is { } minHeight)
            {
                query = query.Where(f => f.Video != null && f.Video.Height >= minHeight);
            }

            if (!string.IsNullOrWhiteSpace(request.CodecContains))
            {
                var codec = $"%{HiddenPathQuery.EscapeLikeTerm(request.CodecContains.Trim())}%";

                query = query.Where(f =>
                    (f.Video != null && f.Video.Codec != null && EF.Functions.Like(f.Video.Codec, codec, LikeEscape))
                    || f.AudioStreams.Any(a => a.Codec != null && EF.Functions.Like(a.Codec, codec, LikeEscape)));
            }

            if (request.AudioLanguages.Count > 0)
            {
                // Any of the selected languages, not all of them: a film is wanted if it carries one of
                // the chosen dubs.
                var languages = request.AudioLanguages;

                query = query.Where(f => f.AudioStreams.Any(a => a.Language != null && languages.Contains(a.Language)));
            }

            if (request.SubtitleLanguages.Count > 0)
            {
                var languages = request.SubtitleLanguages;

                query = query.Where(f => f.Subtitles.Any(s => s.Language != null && languages.Contains(s.Language)));
            }

            if (request.HasMetadata is { } hasMetadata)
            {
                // The stamp, not the extracted rows: a file whose every attempt failed carries no stamp
                // and is still genuinely missing its details.
                query = hasMetadata
                    ? query.Where(static f => f.MetadataStamp != null)
                    : query.Where(static f => f.MetadataStamp == null);
            }

            if (request.HasThumbnail is { } hasThumbnail)
            {
                // MarkThumbnailNotApplicableAsync stamps without producing an image, so audio counts as
                // done here rather than as missing a preview.
                query = hasThumbnail
                    ? query.Where(static f => f.ThumbnailStamp != null)
                    : query.Where(static f => f.ThumbnailStamp == null);
            }

            if (request.IsKeptInMemory is { } isKeptInMemory)
            {
                // The one place the filter's direction is inverted against the column - see the remarks
                // on MediaFileSearchRequest.IsKeptInMemory.
                query = isKeptInMemory
                    ? query.Where(static f => !f.IsExcludedFromCache)
                    : query.Where(static f => f.IsExcludedFromCache);
            }

            return await query
                .OrderBy(static f => f.FullPath)
                .Take(Math.Clamp(request.Take, 1, MaxSearchResults))
                .Select(static f => new MediaFileDto
                {
                    PublicId = f.PublicId,
                    FullPath = f.FullPath,
                    FileName = f.FileName,
                    Title = f.Title,
                    Extension = f.Extension,
                    DirectoryPublicId = f.Directory != null ? f.Directory.PublicId : null,
                    Mime = f.Mime,
                    DlnaProfileName = f.DlnaProfileName,
                    UpnpClass = f.UpnpClass,
                    SizeInBytes = f.SizeInBytes,
                    Duration = f.Video != null && f.Video.Duration != null
                        ? f.Video.Duration
                        : f.AudioStreams
                            .OrderByDescending(a => a.IsDefault)
                            .ThenBy(a => a.StreamIndex)
                            .Select(a => a.Duration)
                            .FirstOrDefault(),
                    Width = f.Video != null ? f.Video.Width : null,
                    Height = f.Video != null ? f.Video.Height : null,
                    Bitrate = f.Video != null ? f.Video.Bitrate : null,
                    VideoCodec = f.Video != null ? f.Video.Codec : null,

                    // The same track Duration falls back to - flagged default first, then container order -
                    // so every audio attribute in one DIDL response describes one track rather than three.
                    AudioChannels = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.Channels)
                    .FirstOrDefault(),
                    AudioSampleRate = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.SampleRate)
                    .FirstOrDefault(),
                    AudioCodec = f.AudioStreams
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.StreamIndex)
                    .Select(a => a.Codec)
                    .FirstOrDefault(),
                    FileCreatedUtc = f.FileCreatedUtc,
                    FileModifiedUtc = f.FileModifiedUtc,
                    CreatedUtc = f.CreatedUtc,
                    IsExcludedFromCache = f.IsExcludedFromCache,
                    ContentStamp = f.ContentStamp,
                    MetadataStamp = f.MetadataStamp,
                    ThumbnailStamp = f.ThumbnailStamp,
                    IsMetadataSuppressed = f.IsMetadataSuppressed,
                    IsThumbnailSuppressed = f.IsThumbnailSuppressed,
                    IsThumbnailRebuildForced = f.IsThumbnailRebuildForced,
                    MetadataFailureCount = f.MetadataFailureCount,
                    ThumbnailFailureCount = f.ThumbnailFailureCount,
                    ThumbnailPublicId = f.Thumbnail != null ? f.Thumbnail.PublicId : null,
                })
                .ToListAsync(cancellationToken);
        }

        /// <remarks>
        /// Both thumbnail operations differ only in the flag they leave behind, and the row delete has to
        /// happen before the stamp is cleared either way - a thumbnail row surviving a null stamp would be
        /// served as current while a replacement was generated.
        /// </remarks>
        private async Task<int> ClearThumbnailsAsync(
            MediaFileScope scope,
            bool isSuppressed,
            CancellationToken cancellationToken)
        {
            var files = await ResolveScopeAsync(scope, cancellationToken);

            if (files is null)
            {
                return 0;
            }

            // Server-side: materialising every file key and sending it back as a parameter turned a
            // delete SQLite can express on its own into 20,000 integers on the wire. files is already an
            // IQueryable over the scope, so it composes directly.
            _ = await _dbContext.Thumbnails
                .Where(t => files.Select(static f => f.Id).Contains(t.MediaFileId))
                .ExecuteDeleteAsync(cancellationToken);

            return await files.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(static f => f.ThumbnailStamp, static _ => null)
                    .SetProperty(static f => f.ThumbnailFailureCount, static _ => 0)
                    .SetProperty(f => f.IsThumbnailSuppressed, _ => isSuppressed)

                    // Only a recreate forces generation. Suppressing wants no thumbnail at all, so
                    // marking it would leave the flag standing for whenever it is un-suppressed.
                    .SetProperty(f => f.IsThumbnailRebuildForced, _ => !isSuppressed),
                cancellationToken);
        }

        public async Task<int> ClearTagsAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            var files = await ResolveScopeAsync(scope, cancellationToken);

            if (files is null)
            {
                return 0;
            }

            // Composed against the scope query rather than materialising its keys, for the reason
            // ClearThumbnailsAsync gives. Nothing on the file is touched, so nothing is re-read: these
            // stay gone until the details are rebuilt, which is what Clear means everywhere else here.
            return await _dbContext.MediaFileTags
                .Where(t => files.Select(static f => f.Id).Contains(t.MediaFileId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<int> RecreateTagsAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            var files = await ResolveScopeAsync(scope, cancellationToken);

            if (files is null)
            {
                return 0;
            }

            // Rows first, so a page load between the two statements cannot show the old tags against a
            // file already marked for re-reading.
            _ = await _dbContext.MediaFileTags
                .Where(t => files.Select(static f => f.Id).Contains(t.MediaFileId))
                .ExecuteDeleteAsync(cancellationToken);

            // Tags come only from probing the file, and the probe that reads them is the metadata pass -
            // so this necessarily rebuilds the typed details too, exactly as RecreateMetadataAsync does.
            return await files.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(static f => f.MetadataStamp, static _ => null)
                    .SetProperty(static f => f.MetadataFailureCount, static _ => 0)
                    .SetProperty(static f => f.IsMetadataSuppressed, static _ => false),
                cancellationToken);
        }

        /// <summary>
        /// The files a scope selects, or null when it names something that is not indexed.
        /// </summary>
        /// <remarks>
        /// A recursive directory scope is matched by path prefix rather than by walking parents, because
        /// SQLite has no recursive query available through LINQ and a walk would cost one round trip per
        /// level. The separator is this machine's: the stored paths were produced by this process's own
        /// filesystem walk.
        /// </remarks>
        private async Task<IQueryable<MediaFileEntity>?> ResolveScopeAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scope);

            if (scope.FilePublicId is { } filePublicId)
            {
                return _dbContext.Files.Where(f => f.PublicId == filePublicId);
            }

            if (scope.DirectoryPublicId is not { } directoryPublicId)
            {
                return null;
            }

            if (!scope.IncludeSubdirectories)
            {
                return _dbContext.Files
                    .Where(f => f.Directory != null && f.Directory.PublicId == directoryPublicId);
            }

            var directoryPath = await _dbContext.Directories
                .AsNoTracking()
                .Where(d => d.PublicId == directoryPublicId)
                .Select(static d => d.FullPath)
                .FirstOrDefaultAsync(cancellationToken);

            if (directoryPath is null)
            {
                return null;
            }

            // A half-open range rather than a LIKE 'path%' prefix, which is the same fix AnyUnderPathAsync
            // and MediaDirectoryRepository.ExcludeWithoutVisibleMedia already carry, and this was the last
            // site still doing it the old way. SQLite rewrites a prefix LIKE into an index seek only when
            // the column's collation agrees with LIKE's case folding - Directories.FullPath is BINARY by
            // design and no case_sensitive_like pragma is set, so it could not, and every recursive
            // "include every subfolder" action from the Library page scanned the table instead of seeking
            // the unique index on it.
            var separator = SeparatorOf(directoryPath);
            var subtreeStart = directoryPath + separator;
            var subtreeEnd = directoryPath + (char)(separator + 1);

            return _dbContext.Files.Where(f =>
                f.Directory != null
                && (f.Directory.PublicId == directoryPublicId
                    || (string.Compare(f.Directory.FullPath, subtreeStart) >= 0
                        && string.Compare(f.Directory.FullPath, subtreeEnd) < 0)));
        }

        /// <summary>
        /// The separator a stored path is actually built from.
        /// </summary>
        /// <remarks>
        /// Read from the path rather than taken from <see cref="Path.DirectorySeparatorChar"/>, which is
        /// the host's and not necessarily the one in the row: a database indexed on the NAS and opened on
        /// a Windows development machine holds forward slashes either way, and a prefix built with a
        /// backslash then matches nothing at all - silently, since a descendant search that finds nothing
        /// looks exactly like a directory with no subdirectories.
        /// </remarks>
        private static char SeparatorOf(string fullPath)
        {
            return StoredPath.SeparatorOf(fullPath);
        }

    }
}
