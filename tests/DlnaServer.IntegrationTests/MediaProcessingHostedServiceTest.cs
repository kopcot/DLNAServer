using DlnaServer.Core.Hosting;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Indexing;
using DlnaServer.Media.Processing;
using DlnaServer.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Proves that one unprocessable file cannot stop media processing, and that a file which throws is
    /// counted as a failed attempt rather than retried forever.
    /// </summary>
    /// <remarks>
    /// Both properties were absent before 2026-09-02. A throw from the metadata or thumbnail call passed
    /// the loop's cancellation-only filter and left <c>ExecuteAsync</c>, and because
    /// <see cref="BackgroundServiceExceptionBehavior"/> defaults to <c>StopHost</c> - which calls
    /// <c>StopApplication</c> instead of rethrowing - the whole server exited with code 0, looking for
    /// all the world like a clean shutdown.
    /// <para>
    /// The assertion that carries the most weight is
    /// <see cref="RecordingMediaFileRepository.PendingRequestedTwice"/>: a service that died never asks
    /// for a second batch, so a regression here times out rather than reporting a wrong value.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class MediaProcessingHostedServiceTest
    {
        private static readonly TimeSpan _signalTimeout = TimeSpan.FromSeconds(30);

        private static readonly string _failingPath = Path.Combine(Path.GetTempPath(), "Corrupt.mkv");
        private static readonly string _healthyPath = Path.Combine(Path.GetTempPath(), "Healthy.mkv");

        [Test]
        public async Task ExecuteAsync_WhenTheProcessorThrows_KeepsProcessingAndRecordsTheFailure()
        {
            // Arrange
            var file = CreateFile(_failingPath);
            var repository = new RecordingMediaFileRepository(file);
            var processor = new ThrowingMediaProcessor(_failingPath);

            using var service = CreateService(repository, processor);

            // Act
            await service.StartAsync(CancellationToken.None);
            await repository.PendingRequestedTwice.WaitAsync(_signalTimeout);
            await service.StopAsync(CancellationToken.None);

            // Assert
            repository.RecordedFailures.Should().ContainSingle(
                "because a throw must be recorded as one failed attempt, so MaxFailureCount can retire "
                + "the file instead of the pass re-probing it forever");
            repository.RecordedFailures[0].PublicId.Should().Be(file.PublicId,
                "because the failure belongs to the file that threw");
            repository.RecordedFailures[0].MetadataFailed.Should().BeTrue(
                "because the metadata call is the one that threw");
            repository.SavedMetadata.Should().BeEmpty(
                "because nothing could be read from a file that cannot be probed");
        }

        [Test]
        public async Task ExecuteAsync_WhenOneFileThrows_StillProcessesTheRestOfTheBatch()
        {
            // Arrange
            var failing = CreateFile(_failingPath);
            var healthy = CreateFile(_healthyPath);
            var repository = new RecordingMediaFileRepository(failing, healthy);
            var processor = new ThrowingMediaProcessor(_failingPath);

            using var service = CreateService(repository, processor);

            // Act
            await service.StartAsync(CancellationToken.None);
            await repository.PendingRequestedTwice.WaitAsync(_signalTimeout);
            await service.StopAsync(CancellationToken.None);

            // Assert
            repository.SavedMetadata.Should().Equal([healthy.PublicId],
                "because the guard is scoped to one file - the bad file must not abandon the files "
                + "queued behind it, which a catch around the whole loop would do");
            repository.SavedThumbnails.Should().Equal([healthy.PublicId],
                "because the healthy file's thumbnail is generated in the same pass");
            repository.RecordedFailures.Should().ContainSingle(
                "because only the corrupt file failed");
        }

        /// <summary>
        /// A file that just failed is not offered again on the next pass, a second later.
        /// </summary>
        /// <remarks>
        /// All three attempts used to land within about three seconds on the NAS, because passes run a
        /// second apart while work is queued: three decodes and three identical warnings per broken file,
        /// and nothing transient given time to clear.
        /// </remarks>
        [Test]
        public async Task ExecuteAsync_AfterAFailure_LeavesTheFileOutOfTheNextClaim()
        {
            // Arrange
            var file = CreateFile(_failingPath);
            var repository = new RecordingMediaFileRepository(file);
            var processor = new ThrowingMediaProcessor(_failingPath);

            using var service = CreateService(repository, processor);

            // Act
            await service.StartAsync(CancellationToken.None);
            await repository.PendingRequestedTwice.WaitAsync(_signalTimeout);
            await service.StopAsync(CancellationToken.None);

            // Assert
            repository.ExcludedPerRequest[0].Should().BeEmpty(
                "because nothing had failed before the first claim");

            repository.ExcludedPerRequest[1].Should().Equal([file.PublicId],
                "because the file that just failed must wait out its retry delay rather than be tried again");
        }

        /// <summary>
        /// A half that <c>MaxFailureCount</c> has retired stays retired when the other half is claimed.
        /// </summary>
        /// <remarks>
        /// The claim is an OR across the two halves, so a file still owed its metadata came back with a
        /// thumbnail that had already failed three times - and the thumbnail was attempted again.
        /// </remarks>
        [Test]
        public async Task ExecuteAsync_ForARetiredThumbnail_DoesNotAttemptItAgain()
        {
            // Arrange
            var file = CreateFile(_failingPath) with { ThumbnailFailureCount = 3 };
            var repository = new RecordingMediaFileRepository(file);
            var processor = new ThrowingMediaProcessor(_failingPath);

            using var service = CreateService(repository, processor);

            // Act
            await service.StartAsync(CancellationToken.None);
            await repository.PendingRequestedTwice.WaitAsync(_signalTimeout);
            await service.StopAsync(CancellationToken.None);

            // Assert
            repository.RecordedFailures.Should().ContainSingle(
                "because the metadata half was still owed and was attempted");
            repository.RecordedFailures[0].MetadataFailed.Should().BeTrue(
                "because the metadata call is the one that was made, and it threw");
            repository.RecordedFailures[0].ThumbnailFailed.Should().BeFalse(
                "because a thumbnail retired by MaxFailureCount must not be attempted again");
            repository.SavedThumbnails.Should().BeEmpty(
                "because nothing was generated for the retired thumbnail");
        }

        [TestCase(1, 5)]
        [TestCase(2, 30)]
        [TestCase(3, 30)]
        public void ResolveRetryDelay_WaitsLongerAfterEachFailure(int failuresSoFar, int expectedMinutes)
        {
            // Act
            var delay = MediaProcessingHostedService.ResolveRetryDelay(failuresSoFar);

            // Assert
            delay.Should().Be(TimeSpan.FromMinutes(expectedMinutes),
                $"because a file that has failed {failuresSoFar} time(s) waits {expectedMinutes} minutes before the next attempt");
        }

        private static MediaProcessingHostedService CreateService(
            IMediaFileRepository repository,
            IMediaProcessor processor)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(repository);
            _ = services.AddSingleton(processor);

            var provider = services.BuildServiceProvider();

            // Already ready: the service now waits for a schema before its first batch, and this fixture
            // supplies the repository directly rather than a database.
            var readySignal = new DatabaseReadySignal();
            readySignal.MarkReady();

            return new MediaProcessingHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions()),
                new MutableTimeProvider(DateTimeOffset.UnixEpoch),
                new NoOpServedFileCache(),
                readySignal,
                NullLogger<MediaProcessingHostedService>.Instance);
        }

        /// <summary>
        /// The idle wait holds at 15 s while work might still arrive, then doubles to a 4-minute cap.
        /// </summary>
        /// <remarks>
        /// The fixed 15 s wait ran the pending-work query 5,760 times a day over a settled library, and
        /// its predicate cannot use an index - so each pass walked all 25,504 rows and kept the whole
        /// Files table hot, defeating the point of letting the disc spin down. The cap is a deliberate
        /// trade: a new file can wait up to four minutes for its thumbnail on an idle server.
        /// </remarks>
        [TestCase(0, 15)]
        [TestCase(4, 15)]
        [TestCase(5, 30)]
        [TestCase(6, 60)]
        [TestCase(7, 120)]
        [TestCase(8, 240)]
        [TestCase(9, 240)]
        [TestCase(5_000, 240)]
        public void ResolveIdleDelay_HoldsThenDoublesToTheCap(int emptyPasses, int expectedSeconds)
        {
            // Arrange
            var expected = TimeSpan.FromSeconds(expectedSeconds);

            // Act
            var delay = MediaProcessingHostedService.ResolveIdleDelay(emptyPasses);

            // Assert
            delay.Should().Be(expected,
                $"because {emptyPasses} consecutive empty pass(es) must wait {expectedSeconds}s before the next one");
        }

        /// <summary>
        /// A video file whose stamps do not match, so both metadata and a thumbnail are wanted.
        /// </summary>
        private static MediaFileDto CreateFile(string fullPath)
        {
            return new MediaFileDto
            {
                PublicId = Guid.NewGuid(),
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                Title = Path.GetFileNameWithoutExtension(fullPath),
                Extension = Path.GetExtension(fullPath),
                DirectoryPublicId = Guid.NewGuid(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsExcludedFromCache = false,
                HasSubtitleTracks = false,
                HasSubtitleFiles = false,
                ContentStamp = "1024:638000000000000000",
            };
        }
    }
}
