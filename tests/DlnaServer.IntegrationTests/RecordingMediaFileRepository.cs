using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Persistence.Repositories;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="IMediaFileRepository"/> that serves one batch of pending files and records what was
    /// done to them, so a background service can be driven without a database.
    /// </summary>
    /// <remarks>
    /// Only the members the media processing pass uses are implemented; the rest throw, so a test that
    /// starts depending on one fails loudly rather than silently seeing a default.
    /// <para>
    /// <see cref="PendingRequestedTwice"/> is the signal that the first batch finished <b>and the loop
    /// came back for more</b> - which is the property under test. A service killed by an unhandled
    /// exception never asks a second time, so a regression shows up as a timeout rather than as a
    /// wrong value.
    /// </para>
    /// </remarks>
    internal sealed class RecordingMediaFileRepository : IMediaFileRepository
    {
        private readonly List<MediaFileDto> _pending;
        private readonly TaskCompletionSource _pendingRequestedTwice =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _pendingRequests;

        public RecordingMediaFileRepository(params MediaFileDto[] pending)
        {
            _pending = [.. pending];
        }

        public Task PendingRequestedTwice => _pendingRequestedTwice.Task;

        public List<Guid> SavedMetadata { get; } = [];

        public List<Guid> SavedThumbnails { get; } = [];

        public List<(Guid PublicId, bool MetadataFailed, bool ThumbnailFailed)> RecordedFailures { get; } = [];

        /// <summary>
        /// What each claim asked to leave out, in order.
        /// </summary>
        public List<Guid[]> ExcludedPerRequest { get; } = [];

        public Task<IReadOnlyList<MediaFileDto>> GetPendingProcessingAsync(
            int maxCount,
            int maxFailureCount,
            IReadOnlyCollection<Guid>? excludedPublicIds = null,
            CancellationToken cancellationToken = default)
        {
            ExcludedPerRequest.Add(excludedPublicIds is null ? [] : [.. excludedPublicIds]);
            _pendingRequests++;

            if (_pendingRequests == 1)
            {
                return Task.FromResult<IReadOnlyList<MediaFileDto>>(_pending);
            }

            _ = _pendingRequestedTwice.TrySetResult();

            return Task.FromResult<IReadOnlyList<MediaFileDto>>([]);
        }

        public Task SaveMetadataAsync(
            Guid publicId,
            MediaMetadataResult metadata,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default)
        {
            SavedMetadata.Add(publicId);

            return Task.CompletedTask;
        }

        public Task SaveThumbnailAsync(
            Guid publicId,
            GeneratedThumbnail thumbnail,
            string extractedFromContentStamp,
            CancellationToken cancellationToken = default)
        {
            SavedThumbnails.Add(publicId);

            return Task.CompletedTask;
        }

        public Task RecordProcessingFailureAsync(
            Guid publicId,
            bool metadataFailed,
            bool thumbnailFailed,
            CancellationToken cancellationToken = default)
        {
            RecordedFailures.Add((publicId, metadataFailed, thumbnailFailed));

            return Task.CompletedTask;
        }

        public Task MarkThumbnailNotApplicableAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Looked up by identifier, for the delivery fixtures. Null unless a test supplies one.
        /// </summary>
        /// <remarks>
        /// These three lookups answer from properties rather than throwing, so FileServerControllerTest
        /// can reuse this double instead of restating forty members it never touches. Everything else
        /// still throws, which is what keeps a fixture that starts depending on one honest.
        /// </remarks>
        public MediaFileDto? File { get; init; }

        public ThumbnailDto? Thumbnail { get; init; }

        public byte[]? ThumbnailContent { get; init; }

        public Task<MediaFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(File);
        }

        public Task<MediaFileDetailsDto?> GetWithDetailsAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MediaFileDto?> GetByPathAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountByDirectoryAsync(
            Guid directoryPublicId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MediaFileDto>> GetByDirectoryPageAsync(
            Guid directoryPublicId,
            int skip,
            int take,
            bool sortByDate,
            bool descending,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MediaFileNeighboursDto> GetPlayableNeighboursAsync(
            Guid directoryPublicId,
            Guid filePublicId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MediaFileDto>> GetRecentlyAddedAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlySet<string>> GetExistingPathsAsync(
            IReadOnlyCollection<string> fullPaths,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IndexedFileDto>> GetIndexedPageAsync(
            string? afterPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ClearTagsAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RecreateTagsAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> PurgeAllTagsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RecreateAllTagsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MediaFileTagDto>> GetTagsAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<IndexedFileDto>> FindByContentStampsAsync(
            IReadOnlyCollection<string> contentStamps,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<(bool Moved, string? AbandonedThumbnailPath)> MoveOrRenameAsync(
            Guid publicId,
            Guid supersededPublicId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> UpdateContentAsync(
            IReadOnlyCollection<MediaFileContentUpdateDto> updates,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> UpdateDlnaMappingAsync(
            Guid publicId,
            DlnaMime mime,
            string? dlnaProfileName,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ThumbnailDto?> GetThumbnailByPublicIdAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Thumbnail);
        }

        public Task<byte[]?> GetThumbnailContentAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ThumbnailContent);
        }

        public Task MarkExcludedFromCacheAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ResetCacheExclusionAsync(
            MediaFileScope scope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LibraryCountsDto> CountByKindAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> AnyUnderPathAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MediaFileDto>> AddRangeAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> AddRangeReturningCountAsync(
            IReadOnlyCollection<MediaFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<(int Removed, IReadOnlyList<string> AbandonedThumbnailPaths)> RemoveByPublicIdsAsync(
            IReadOnlyCollection<Guid> publicIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ClearAllMetadataAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ClearAllThumbnailsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ResetProcessingAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MediaLanguagesDto> GetLanguagesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ClearMetadataAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RecreateMetadataAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> ClearThumbnailsAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> RecreateThumbnailsAsync(MediaFileScope scope, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ThumbnailDto>> GetThumbnailPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MediaFileDto>> SearchAsync(
            MediaFileSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
