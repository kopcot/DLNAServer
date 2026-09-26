using DlnaServer.Admin.Components;
using DlnaServer.Core.Contracts;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// The Dashboard re-reads the whole Files table only when this says the stored counts are no longer
    /// current, so each way they can go stale has to be seen here.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryCountsCacheTest
    {
        private static readonly DateTimeOffset _takenAt = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

        private static readonly LibraryCountsDto _counts =
            new() { Total = 10, Video = 4, Audio = 3, Image = 2, Other = 1, Subtitles = 0 };

        [Test]
        public void Find_ForTheSameGenerationAndHiddenFolders_ReturnsTheStoredCounts()
        {
            // Arrange
            var cache = CreateCacheHolding(generation: 7, hiddenFolders: "@Recycle");

            // Act
            var found = cache.Find(generation: 7, hiddenFolders: "@Recycle", now: _takenAt.AddMinutes(9));

            // Assert
            found.Should().NotBeNull("because nothing that decides the counts has changed since they were taken");
            found!.Counts.Should().Be(_counts, "because the stored counts are the ones handed back");
        }

        [Test]
        public void Find_AfterTheLibraryChanged_ReturnsNothing()
        {
            // Arrange
            var cache = CreateCacheHolding(generation: 7, hiddenFolders: "@Recycle");

            // Act
            var found = cache.Find(generation: 8, hiddenFolders: "@Recycle", now: _takenAt.AddSeconds(5));

            // Assert
            found.Should().BeNull("because a scan or an edit marked the library as changed since the count");
        }

        [Test]
        public void Find_AfterTheHiddenFoldersChanged_ReturnsNothing()
        {
            // Arrange
            var cache = CreateCacheHolding(generation: 7, hiddenFolders: "@Recycle");

            // Act
            var found = cache.Find(generation: 7, hiddenFolders: "@Recycle\nPrivate", now: _takenAt.AddSeconds(5));

            // Assert
            found.Should().BeNull(
                "because hiding a folder changes the counts without writing anything to the library");
        }

        [Test]
        public void Find_OnceTheSnapshotIsTooOld_ReturnsNothing()
        {
            // Arrange
            var cache = CreateCacheHolding(generation: 7, hiddenFolders: "@Recycle");

            // Act
            var found = cache.Find(
                generation: 7,
                hiddenFolders: "@Recycle",
                now: _takenAt + LibraryCountsCache.MaxAge);

            // Assert
            found.Should().BeNull("because the age limit is the safety net for a writer that forgot to mark");
        }

        [Test]
        public void Find_BeforeAnythingWasStored_ReturnsNothing()
        {
            // Arrange
            var cache = new LibraryCountsCache();

            // Act
            var found = cache.Find(generation: 0, hiddenFolders: string.Empty, now: _takenAt);

            // Assert
            found.Should().BeNull("because the first Dashboard to open has to count");
        }

        private static LibraryCountsCache CreateCacheHolding(long generation, string hiddenFolders)
        {
            var cache = new LibraryCountsCache();
            cache.Store(new LibraryCountsCache.Snapshot(generation, hiddenFolders, _takenAt, _counts, DirectoryCount: 3));

            return cache;
        }
    }
}
