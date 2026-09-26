using System.Data.Common;
using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Entities;
using DlnaServer.Persistence.Repositories;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// Covers the row that answers when the browser's own memory of the destination is gone.
    /// </summary>
    [TestFixture]
    internal sealed class UploadDeviceRepositoryTest
    {
        private const string Fingerprint = "8E9B2C1D4A5F6071";

        private SqliteTestDatabase _database = null!;

        [SetUp]
        public void SetUp()
        {
            _database = new SqliteTestDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            _database.Dispose();
        }

        [Test]
        public async Task GetLastDestinationAsync_ForADeviceThatHasNeverUploaded_ReturnsNull()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = new UploadDeviceRepository(context, TimeProvider.System);

            // Act
            var destination = await repository.GetLastDestinationAsync(Fingerprint, CancellationToken.None);

            // Assert
            destination.Should().BeNull(
                "because a first visit has nothing to prefill, and the page must offer the plain default "
                + "rather than fail");
        }

        [Test]
        public async Task RecordAsync_ForANewDevice_StoresTheDestination()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = new UploadDeviceRepository(context, TimeProvider.System);

            // Act
            await repository.RecordAsync(Device("/share/Media/Films"), CancellationToken.None);

            // Assert
            var destination = await repository.GetLastDestinationAsync(Fingerprint, CancellationToken.None);

            destination.Should().Be("/share/Media/Films",
                "because the whole point of the row is that the same device is offered the same folder "
                + "again after it has cleared its browser");
        }

        [Test]
        public async Task RecordAsync_ForADeviceThatHasUploadedBefore_ReplacesTheDestinationAndAddsToTheCount()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = new UploadDeviceRepository(context, TimeProvider.System);

            await repository.RecordAsync(Device("/share/Media/Films"), CancellationToken.None);

            // Act
            await repository.RecordAsync(Device("/share/Media/Music"), CancellationToken.None);

            // Assert
            var destination = await repository.GetLastDestinationAsync(Fingerprint, CancellationToken.None);

            destination.Should().Be("/share/Media/Music",
                "because the folder offered is the one last used, not the one first used");

            await using var reading = _database.CreateContext();

            reading.UploadDevices.Should().ContainSingle(
                "because a device has exactly one row - the unique index on the fingerprint is what stops "
                + "a second upload from a known device inserting a duplicate")
                .Which.UploadCount.Should().Be(4,
                    "because the file count is a running total across every upload the device has made");
        }

        [Test]
        public async Task RecordAsync_StampsTheUploadWithTheInjectedClock()
        {
            // Arrange
            var now = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
            await using var context = _database.CreateContext();
            var repository = new UploadDeviceRepository(context, new MutableTimeProvider(now));

            // Act
            await repository.RecordAsync(Device("/share/Media/Films"), CancellationToken.None);

            // Assert
            await using var reading = _database.CreateContext();

            reading.UploadDevices.Should().ContainSingle(
                    "because one device uploaded once")
                .Which.LastUploadUtc.Should().Be(now.UtcDateTime,
                    "because the time comes from the TimeProvider the rest of the persistence layer uses, "
                    + "so a test or a replaced clock sees the same instant everywhere");
        }

        /// <summary>
        /// The upload controller can catch only <see cref="DbException"/>, so a failed save must arrive as one.
        /// </summary>
        [Test]
        public async Task RecordAsync_WhenTheSaveFails_ThrowsTheProviderException()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = new UploadDeviceRepository(context, TimeProvider.System);

            // Pending in the tracker but not in the table, so the repository's lookup misses it and the
            // save inserts the fingerprint twice against its unique index.
            _ = context.UploadDevices.Add(new UploadDeviceEntity
            {
                Fingerprint = Fingerprint,
                RemoteAddress = "192.168.1.51",
                UserAgent = "Mozilla/5.0 (Android)",
                LastDestination = "/share/Media/Other",
            });

            // Act
            var act = () => repository.RecordAsync(Device("/share/Media/Films"), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<DbException>(
                "because EF's DbUpdateException does not derive from DbException, and the caller outside "
                + "Persistence cannot name the EF type to catch it");
        }

        private static UploadDeviceUpdateDto Device(string destination)
        {
            return new UploadDeviceUpdateDto
            {
                Fingerprint = Fingerprint,
                RemoteAddress = "192.168.1.50",
                UserAgent = "Mozilla/5.0 (Android)",
                AcceptLanguage = "en-GB,en;q=0.9",
                Destination = destination,
                FileCount = 2,
            };
        }
    }
}
