using DlnaServer.Core.Contracts;
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
            var repository = new UploadDeviceRepository(context);

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
            var repository = new UploadDeviceRepository(context);

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
            var repository = new UploadDeviceRepository(context);

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
