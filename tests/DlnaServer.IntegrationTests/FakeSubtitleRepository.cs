using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Repositories;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// An <see cref="ISubtitleRepository"/> answering reads from a list a test fills in, for tests about
    /// what the file server and the DIDL mapping do with the links rather than how they are stored.
    /// </summary>
    internal sealed class FakeSubtitleRepository : ISubtitleRepository
    {
        public List<SubtitleFileDto> Links { get; } = [];

        public Task<(int Added, int Dropped)> SyncAutomaticAsync(
            IReadOnlyDictionary<string, HashSet<string>> fileNamesByDirectory,
            IReadOnlyList<string> excludedFolders,
            Func<string, bool> isDefinitelyAbsent,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Syncing is covered against the real repository.");
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<SubtitleFileDto>>> GetForFilesAsync(
            IReadOnlyCollection<Guid> mediaFilePublicIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, IReadOnlyList<SubtitleFileDto>> found = Links
                .Where(l => mediaFilePublicIds.Contains(l.MediaFilePublicId))
                .GroupBy(static l => l.MediaFilePublicId)
                .ToDictionary(static g => g.Key, static g => (IReadOnlyList<SubtitleFileDto>)[.. g]);

            return Task.FromResult(found);
        }

        public Task<SubtitleFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Links.FirstOrDefault(l => l.PublicId == publicId));
        }

        public Task<bool> AddManualAsync(
            Guid mediaFilePublicId,
            string relativePath,
            string? language,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Editing is covered against the real repository.");
        }

        public Task RemoveAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Editing is covered against the real repository.");
        }

        public Task SetLanguageAsync(Guid publicId, string? language, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Editing is covered against the real repository.");
        }
    }
}
