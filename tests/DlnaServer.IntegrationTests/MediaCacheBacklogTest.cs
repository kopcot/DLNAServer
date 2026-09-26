using DlnaServer.Core.Delivery;
using DlnaServer.Host.Delivery;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Host.Delivery.Prefetch;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the backlog that hands files to the background cache filler. Both of its properties exist
    /// because of a reference defect: the channel is bounded, and a path already waiting is not added
    /// twice - the reference used an unbounded channel and queued one file once per range request.
    /// </summary>
    [TestFixture]
    internal sealed class MediaCacheBacklogTest
    {
        /// <summary>
        /// Matches the capacity in <see cref="MediaCacheBacklog"/>, which is deliberately not public.
        /// </summary>
        private const int Capacity = 32;

        [Test]
        public async Task TryEnqueue_MakesTheRequestReadable()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();
            var request = new MediaCacheRequest(Guid.NewGuid(), "/media/film.mkv");

            // Act
            var isAdded = backlog.TryEnqueue(request);
            var read = await backlog.Reader.ReadAsync(CancellationToken.None);

            // Assert
            isAdded.Should().BeTrue("because an empty backlog accepts work");
            read.Should().Be(request, "because the filler must receive both the path and the identifier");
        }

        /// <summary>
        /// A renderer issues one request per byte range, so a single playback would otherwise queue - and
        /// read - the same file dozens of times.
        /// </summary>
        [Test]
        public void TryEnqueue_ForAPathAlreadyWaiting_IsRejected()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();
            var publicId = Guid.NewGuid();

            // Act
            var first = backlog.TryEnqueue(new MediaCacheRequest(publicId, "/media/film.mkv"));
            var second = backlog.TryEnqueue(new MediaCacheRequest(publicId, "/media/film.mkv"));

            // Assert
            first.Should().BeTrue("because the path was not waiting yet");
            second.Should().BeFalse("because one pending read per file is all that is ever useful");
        }

        [Test]
        public void TryEnqueue_AfterRelease_AcceptsThePathAgain()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();
            var publicId = Guid.NewGuid();
            _ = backlog.TryEnqueue(new MediaCacheRequest(publicId, "/media/film.mkv"));

            // Act
            backlog.Release("/media/film.mkv");
            var isAdded = backlog.TryEnqueue(new MediaCacheRequest(publicId, "/media/film.mkv"));

            // Assert
            isAdded.Should().BeTrue(
                "because a file that has been dealt with may be re-read after its entry expires");
        }

        /// <summary>
        /// Overflow is a normal outcome, not an error: caching is an optimisation, so a dropped request
        /// only means the next play of that file reads the disc again. Unbounded growth on a NAS is not
        /// recoverable, and that is what the reference risked.
        /// </summary>
        [Test]
        public void TryEnqueue_BeyondCapacity_IsRejectedRatherThanGrowing()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();

            for (var index = 0; index < Capacity; index++)
            {
                backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), $"/media/film-{index}.mkv"))
                    .Should().BeTrue($"because request {index} is within the capacity of {Capacity}");
            }

            // Act
            var overflow = backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), "/media/one-too-many.mkv"));

            // Assert
            overflow.Should().BeFalse(
                "because the backlog is bounded - a burst of requests must not queue unbounded memory");
        }

        /// <summary>
        /// A rejected overflow must not leave the path marked as waiting, or that file could never be
        /// cached again for the lifetime of the process.
        /// </summary>
        [Test]
        public async Task TryEnqueue_AfterAnOverflowIsDrained_AcceptsTheRejectedPath()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();

            for (var index = 0; index < Capacity; index++)
            {
                _ = backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), $"/media/film-{index}.mkv"));
            }

            var rejected = new MediaCacheRequest(Guid.NewGuid(), "/media/rejected.mkv");
            backlog.TryEnqueue(rejected).Should().BeFalse("because the backlog is full");

            // Act
            _ = await backlog.Reader.ReadAsync(CancellationToken.None);
            var isAdded = backlog.TryEnqueue(rejected);

            // Assert
            isAdded.Should().BeTrue(
                "because a rejection must release the path, not blacklist the file permanently");
        }

        /// <summary>
        /// Browse warms a whole page of previews at once, and a page used to fill the backlog so the film
        /// asked for next was refused - the one read the cache exists for.
        /// </summary>
        [Test]
        public void TryEnqueue_AfterAPageOfPreviewsWasWarmed_StillAcceptsAFilm()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();

            for (var index = 0; index < Capacity; index++)
            {
                _ = backlog.TryEnqueue(new MediaCacheRequest(
                    Guid.NewGuid(),
                    $"/media/.@__thumb/film-{index}.mkv.jpg",
                    CachedContentClass.Thumbnail));
            }

            // Act
            var isAdded = backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), "/media/film.mkv"));

            // Assert
            isAdded.Should().BeTrue("because previews may never take the slots a film needs");
        }

        [Test]
        public void TryEnqueue_ForAPreviewOnceTheirShareIsFull_AcceptsOneAgainAfterARelease()
        {
            // Arrange
            var backlog = new MediaCacheBacklog();
            var accepted = new List<string>();

            for (var index = 0; index < Capacity; index++)
            {
                var path = $"/media/.@__thumb/film-{index}.mkv.jpg";

                if (backlog.TryEnqueue(new MediaCacheRequest(Guid.NewGuid(), path, CachedContentClass.Thumbnail)))
                {
                    accepted.Add(path);
                }
            }

            var refused = new MediaCacheRequest(Guid.NewGuid(), "/media/.@__thumb/late.mkv.jpg", CachedContentClass.Thumbnail);
            backlog.TryEnqueue(refused).Should().BeFalse("because the previews' share of the backlog is taken");

            // Act
            backlog.Release(accepted[0]);
            var isAdded = backlog.TryEnqueue(refused);

            // Assert
            accepted.Count.Should().BeLessThan(Capacity, "because previews are capped below the whole backlog");
            isAdded.Should().BeTrue("because a preview that has been dealt with gives its slot back");
        }
    }
}
