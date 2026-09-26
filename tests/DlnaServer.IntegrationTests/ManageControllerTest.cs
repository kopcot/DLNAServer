using DlnaServer.Host.Diagnostics;
using DlnaServer.Host.Gena;
using DlnaServer.Host.Delivery.Caching;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using DlnaServer.Core.Delivery;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the management endpoints that change process state. <c>/manage/memory</c> is a pure read and
    /// is exercised by the memory protocol in <c>docs/decisions.md</c> section 3b instead.
    /// </summary>
    [TestFixture]
    internal sealed class ManageControllerTest
    {
        private RecordingApplicationLifetime _lifetime = null!;
        private RestartSignal _restartSignal = null!;
        private DatabaseResetSignal _databaseResetSignal = null!;
        private StaticOptionsMonitor<DlnaOptions> _options = null!;
        private ServedFileCache _fileCache = null!;
        private ApiBlocker _blocker = null!;
        private ManageController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new RecordingApplicationLifetime();
            _restartSignal = new RestartSignal();
            _databaseResetSignal = new DatabaseResetSignal();

            _options = new StaticOptionsMonitor<DlnaOptions>(new DlnaOptions());
            _fileCache = new ServedFileCache(_options, TimeProvider.System, NullLogger<ServedFileCache>.Instance);
            _blocker = new ApiBlocker(TimeProvider.System);

            _controller = new ManageController(
                _lifetime,
                _restartSignal,
                _databaseResetSignal,
                _fileCache,
                _blocker,
                new SubscriptionStore(TimeProvider.System),
                _options,
                NullLogger<ManageController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            _fileCache.Dispose();
            _lifetime.Dispose();
        }

        /// <summary>
        /// The listing of cached paths is the reason this endpoint exists.
        /// </summary>
        /// <remarks>
        /// A byte total alone cannot distinguish a cache doing its job from a leak, which is exactly the
        /// question a 974 MB large object heap raised on 2026-09-02. The paths answer it.
        /// </remarks>
        [Test]
        public void GetFileCache_ReportsTheHeldPathsAndTheResolvedBudget()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "reported.jpg");
            _fileCache.Store(path, CachedContentClass.Thumbnail, new byte[] { 1, 2, 3, 4 });

            // Act
            var report = _fileCache.Describe();
            var paths = _fileCache.ListPaths();

            // Assert
            paths.Should().Contain(path,
                "because the endpoint must name what is held, not only how much");
            report.EntryCount.Should().Be(1, "because exactly one payload was stored");
            report.BudgetInBytes.Should().BeGreaterThan(0,
                "because the resolved budget is what the store is actually enforcing");
            report.BytesHeld.Should().Be(4,
                "because size accounting is by real payload length, which is what makes the budget mean "
                + "megabytes rather than a count of entries");
        }

        [Test]
        public void GetFileCache_TracksHitsAndMisses()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "counted.jpg");
            _fileCache.Store(path, CachedContentClass.Thumbnail, new byte[] { 7 });

            // Act
            _ = _fileCache.TryGet(path, out _);
            _ = _fileCache.TryGet(Path.Combine(Path.GetTempPath(), "absent.jpg"), out _);

            var report = _fileCache.Describe();

            // Assert
            report.Hits.Should().BeGreaterThan(0,
                "because statistics tracking must be enabled on the store - without it the hit rate is "
                + "null and nothing says whether the memory is buying anything");
            report.Misses.Should().BeGreaterThan(0, "because the second path was never stored");
        }

        [Test]
        public void ClearFileCache_DropsEveryEntry()
        {
            // Arrange
            _fileCache.Store(
                Path.Combine(Path.GetTempPath(), "one.jpg"),
                CachedContentClass.Thumbnail,
                new byte[] { 1 });
            _fileCache.Store(
                Path.Combine(Path.GetTempPath(), "two.jpg"),
                CachedContentClass.Thumbnail,
                new byte[] { 2 });

            // Act
            var cleared = _fileCache.Clear();

            // Assert
            cleared.Should().Be(2, "because the caller is told how many payloads were dropped");
            _fileCache.Describe().EntryCount.Should().Be(0, "because the store is emptied");
        }

        /// <summary>
        /// The reference has this endpoint but answers with an empty <c>Ok()</c>.
        /// </summary>
        [Test]
        public void GetSubscriptions_ListsWhatTheStoreHolds()
        {
            // Arrange
            var store = new SubscriptionStore(TimeProvider.System);
            var added = store.Add(
                serviceId: "urn:upnp-org:serviceId:ContentDirectory",
                callbackUrls: ["http://192.168.1.50:8080/notify"],
                granted: TimeSpan.FromMinutes(30),
                subscriber: System.Net.IPAddress.Parse("192.168.1.50"));

            // Act
            var listed = store.List();

            // Assert
            added.Should().NotBeNull("because the store was empty and had room");
            listed.Should().ContainSingle("because one subscription is live")
                .Which.Sid.Should().Be(added!.Sid,
                    "because the listing reports the identifier the renderer was given");
        }

        [Test]
        public void Stop_AsksTheHostToShutDown()
        {
            // Arrange
            // Act
            var result = _controller.Stop();

            // Assert
            _lifetime.WasStopRequested.Should().BeTrue(
                "because the endpoint exists to shut the server down");
            result.Should().BeOfType<OkObjectResult>(
                "because the reply is sent during the graceful shutdown, before the process exits");
        }

        /// <summary>
        /// Program.Main rebuilds the host after it stops whenever a restart was requested, so a stop that
        /// left the signal set would silently restart instead of stopping.
        /// </summary>
        [Test]
        public void Stop_AfterARestartWasRequested_StillStops()
        {
            // Arrange
            _restartSignal.RequestRestart();

            // Act
            _ = _controller.Stop();

            // Assert
            _restartSignal.IsRestartRequested.Should().BeFalse(
                "because stop must mean stop, whatever was pending before it");
            _lifetime.WasStopRequested.Should().BeTrue("because the host is still asked to shut down");
        }

        [Test]
        public void Stop_WithNoRestartPending_LeavesTheSignalClear()
        {
            // Arrange
            // Act
            _ = _controller.Stop();

            // Assert
            _restartSignal.IsRestartRequested.Should().BeFalse(
                "because nothing asked for a restart and stopping must not introduce one");
        }

        /// <summary>
        /// A pending <i>Recreate database</i> lives only in this process and is carried out by the restart
        /// it requested, so a stop that cleared the restart signal dropped it without a word.
        /// </summary>
        [Test]
        public void Stop_WhileADatabaseRecreateIsPending_IsRefusedAndKeepsTheRestart()
        {
            // Arrange
            _databaseResetSignal.RequestReset();
            _restartSignal.RequestRestart();

            // Act
            var result = _controller.Stop();

            // Assert
            result.Should().BeOfType<ConflictObjectResult>(
                "because the caller has to be told the stop did not happen, and why");
            _restartSignal.IsRestartRequested.Should().BeTrue(
                "because clearing it would let the host exit and the recreate would never run");
            _databaseResetSignal.IsResetRequested.Should().BeTrue(
                "because the recreate the operator confirmed must still happen on the next start");
            _lifetime.WasStopRequested.Should().BeFalse(
                "because the host is already stopping to restart, and nothing here may change that into an exit");
        }

        [Test]
        public void ListPaths_ReturnsEveryHeldPathInOrdinalOrder()
        {
            // Arrange
            var later = Path.Combine(Path.GetTempPath(), "b.jpg");
            var earlier = Path.Combine(Path.GetTempPath(), "a.jpg");
            _fileCache.Store(later, CachedContentClass.Thumbnail, new byte[] { 1 });
            _fileCache.Store(earlier, CachedContentClass.Thumbnail, new byte[] { 2 });

            // Act
            var paths = _fileCache.ListPaths();

            // Assert
            paths.Should().Equal([earlier, later],
                "because the listing is ordered, so two reads of it are comparable rather than in hash order");
        }
    }
}
