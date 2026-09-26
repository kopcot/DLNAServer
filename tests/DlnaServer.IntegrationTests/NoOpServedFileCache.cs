using DlnaServer.Core.Delivery;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// A cache that holds nothing, for fixtures that exercise a collaborator which happens to evict.
    /// </summary>
    /// <remarks>
    /// Media processing tells the cache to drop a thumbnail it has just regenerated, because the cache is
    /// keyed by path and would otherwise keep serving the previous image. That is a one-line side effect
    /// of the work under test rather than part of it, so a double that records nothing keeps the fixture
    /// about the processing.
    /// </remarks>
    internal sealed class NoOpServedFileCache : IServedFileCache
    {
        public bool IsEnabled => false;

        public long BudgetInBytes => 0;

        public long MaxFileSizeInBytes => 0;

        public bool TryGet(string filePath, out ReadOnlyMemory<byte> content)
        {
            content = ReadOnlyMemory<byte>.Empty;

            return false;
        }

        public Task<ReadOnlyMemory<byte>> LoadAsync(
            string filePath,
            CachedContentClass contentClass,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ServedFileCacheReport Describe()
        {
            return new ServedFileCacheReport
            {
                IsEnabled = false,
                EntryCount = 0,
                BytesHeld = 0,
                BudgetInBytes = 0,
                MaxFileSizeInBytes = 0,
                Hits = 0,
                Misses = 0,
                DatabaseHits = 0,
                ReadsInFlight = 0,
            };
        }

        public IReadOnlyList<string> ListPaths()
        {
            return [];
        }

        public int Clear()
        {
            return 0;
        }

        public bool Evict(string filePath)
        {
            return false;
        }

        public void Store(string filePath, CachedContentClass contentClass, ReadOnlyMemory<byte> content)
        {
        }
    }
}
