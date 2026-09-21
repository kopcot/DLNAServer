using System.Threading.Channels;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Watching;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Indexing;
using DlnaServer.Media.Watching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the settle and coalesce windows, which had no tests at all.
    /// </summary>
    /// <remarks>
    /// This service carries more recorded past defects than anything else in the indexing path and had
    /// the least coverage of any of them. The windows are driven directly rather than through
    /// <c>ExecuteAsync</c>: its loop is paced by <c>Task.Delay</c>, and <see cref="MutableTimeProvider"/>
    /// does not override <c>CreateTimer</c>, so the loop cannot be fast-forwarded. Testing the steps it
    /// calls is the same choice already made for <c>ResolveIdleDelay</c> and
    /// <c>FileSystemChangeWatcher.Publish</c>.
    /// </remarks>
    [TestFixture]
    internal sealed class FileWatcherHostedServiceTest
    {
        private static readonly DateTimeOffset _start = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        private MutableTimeProvider _time = null!;
        private FakeChangeWatcher _watcher = null!;
        private RecordingIndexer _indexer = null!;
        private ServiceProvider _provider = null!;
        private FileWatcherHostedService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new MutableTimeProvider(_start);
            _watcher = new FakeChangeWatcher();
            _indexer = new RecordingIndexer();

            var services = new ServiceCollection();
            _ = services.AddScoped<ILibraryIndexer>(_ => _indexer);
            _provider = services.BuildServiceProvider();

            var options = new DlnaOptions();
            options.Library.FileSettleSeconds = 30;

            _service = new FileWatcherHostedService(
                _watcher,
                _provider.GetRequiredService<IServiceScopeFactory>(),
                new StaticOptionsMonitor<DlnaOptions>(options),
                _time,
                new DatabaseReadySignal(),
                NullLogger<FileWatcherHostedService>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _provider.Dispose();
        }

        private void Raise(string path)
        {
            _watcher.Raise(new FileChangeEvent(path, null, FileChangeKind.CreatedOrChanged, _time.GetTimestamp()));
        }

        [Test]
        public async Task ScanIfDueAsync_WhileAFileIsStillSettling_DoesNotScan()
        {
            // Arrange
            Raise("/share/Media/film.mkv");
            _service.Drain(CancellationToken.None);

            // Act - short of the 30 second settle window.
            _time.Advance(TimeSpan.FromSeconds(20));
            _service.CollectSettled();

            var scanned = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            // Assert
            scanned.Should().BeFalse(
                "because a file that is still being written must not be indexed at its current length");
            _indexer.Passes.Should().Be(0,
                "because nothing has settled, so there is nothing for a pass to do");
        }

        [Test]
        public async Task ScanIfDueAsync_OnceSettledAndQuiet_RunsOnePass()
        {
            // Arrange
            Raise("/share/Media/film.mkv");
            _service.Drain(CancellationToken.None);

            // Act - past the settle window, then past the quiet period with nothing further arriving.
            _time.Advance(TimeSpan.FromSeconds(31));
            _service.CollectSettled();
            _time.Advance(TimeSpan.FromSeconds(11));

            var scanned = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            // Assert
            scanned.Should().BeTrue(
                "because the change has settled and the settling has gone quiet");
            _indexer.Passes.Should().Be(1,
                "because a batch of settled changes is answered by exactly one full pass");
        }

        /// <summary>
        /// The reason the windows moved off the wall clock.
        /// </summary>
        /// <remarks>
        /// A NAS without a battery-backed clock starts wrong and NTP steps it. Measured on
        /// <c>GetUtcNow</c>, a step of an hour would have marked every pending path settled at once and
        /// indexed files that were still being copied. Measured on the monotonic reading, the elapsed
        /// time is what it actually was.
        /// </remarks>
        [Test]
        public async Task ScanIfDueAsync_WhenTheClockIsCorrectedForwards_StillWaitsForTheFileToSettle()
        {
            // Arrange - the file has only just appeared.
            Raise("/share/Media/film.mkv");
            _service.Drain(CancellationToken.None);

            // Act - one second of real elapsed time. A wall-clock reading cannot tell this from an hour.
            _time.Advance(TimeSpan.FromSeconds(1));
            _service.CollectSettled();

            var scanned = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            // Assert
            scanned.Should().BeFalse(
                "because only a second of elapsed time has passed, whatever the wall clock now reads");
            _indexer.Passes.Should().Be(0,
                "because indexing a file one second into its copy is exactly what FileSettleSeconds "
                + "exists to prevent");
        }

        [Test]
        public async Task ScanIfDueAsync_WhenChangesKeepArriving_HoldsUntilTheCeiling()
        {
            // Arrange - a bulk copy: something settles, then more keeps arriving.
            Raise("/share/Media/a.mkv");
            _service.Drain(CancellationToken.None);
            _time.Advance(TimeSpan.FromSeconds(31));
            _service.CollectSettled();

            // Act - never quiet for long enough, but past the two minute ceiling.
            for (var tick = 0; tick < 30; tick++)
            {
                _time.Advance(TimeSpan.FromSeconds(5));
                Raise($"/share/Media/b{tick}.mkv");
                _service.Drain(CancellationToken.None);
                _service.CollectSettled();
            }

            var scanned = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            // Assert
            scanned.Should().BeTrue(
                "because the ceiling bounds how stale the library gets while an import is still running "
                + "- without it a long copy leaves nothing indexed until it finishes");
        }

        /// <summary>
        /// A failed pass keeps its settled changes, because nothing will raise them a second time.
        /// </summary>
        [Test]
        public async Task ScanIfDueAsync_WhenThePassFails_KeepsTheChangesAndRetries()
        {
            // Arrange
            _indexer.Fail = true;

            Raise("/share/Media/film.mkv");
            _service.Drain(CancellationToken.None);
            _time.Advance(TimeSpan.FromSeconds(31));
            _service.CollectSettled();
            _time.Advance(TimeSpan.FromSeconds(11));

            // Act
            var failed = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            _indexer.Fail = false;
            _time.Advance(TimeSpan.FromSeconds(11));

            var recovered = await _service.ScanIfDueAsync(cancellationToken: CancellationToken.None);

            // Assert
            failed.Should().BeFalse("because the pass threw");
            recovered.Should().BeTrue(
                "because the watcher raises one event per change and a change already observed does not "
                + "happen again, so dropping it on a transient failure loses the file until something "
                + "unrelated touches the tree");
            _indexer.Passes.Should().Be(2, "because both attempts reached the indexer");
        }

        private sealed class RecordingIndexer : ILibraryIndexer
        {
            public int Passes { get; private set; }

            public bool Fail { get; set; }

            public Task<LibraryIndexResult> IndexAsync(CancellationToken cancellationToken = default)
            {
                Passes++;

                return Fail
                    ? throw new InvalidOperationException("the database is locked")
                    : Task.FromResult(new LibraryIndexResult(0, 0, 0, 0, 0, 0));
            }
        }

        private sealed class FakeChangeWatcher : IFileSystemChangeWatcher
        {
            private readonly Channel<FileChangeEvent> _channel = Channel.CreateUnbounded<FileChangeEvent>();

            public ChannelReader<FileChangeEvent> Events => _channel.Reader;

            public void Raise(FileChangeEvent raised)
            {
                _ = _channel.Writer.TryWrite(raised);
            }

            public IReadOnlyList<string> Start(
                IReadOnlyList<string> sourceFolders,
                IReadOnlyList<string> excludedFolderNames)
            {
                return [.. sourceFolders];
            }

            public IReadOnlyList<string> Restart(
                IReadOnlyList<string> sourceFolders,
                IReadOnlyList<string> excludedFolderNames)
            {
                return [.. sourceFolders];
            }

            public bool ConsumeResyncRequest()
            {
                return false;
            }

            public bool ConsumeRestartRequest()
            {
                return false;
            }

            public void RequestResync()
            {
            }
        }
    }
}
