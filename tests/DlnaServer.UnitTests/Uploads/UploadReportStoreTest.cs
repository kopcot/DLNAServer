using DlnaServer.Core.Uploads;

namespace DlnaServer.UnitTests.Uploads
{
    /// <summary>
    /// Covers the report surviving exactly the one redirect it has to, and no longer.
    /// </summary>
    [TestFixture]
    internal sealed class UploadReportStoreTest
    {
        [Test]
        public void Find_RightAfterAnUpload_ReturnsTheReport()
        {
            // Arrange
            var store = new UploadReportStore(new MutableTimeProvider(DateTimeOffset.UnixEpoch));
            var report = Report();

            store.Add(report);

            // Act
            var found = store.Find(report.Id);

            // Assert
            found.Should().BeSameAs(report,
                "because the post answers with a redirect and the page that follows it is the only reader");
        }

        [Test]
        public void Find_WithAnIdentifierNothingStored_ReturnsNull()
        {
            // Arrange
            var store = new UploadReportStore(new MutableTimeProvider(DateTimeOffset.UnixEpoch));

            // Act
            var found = store.Find(Guid.NewGuid());

            // Assert
            found.Should().BeNull(
                "because a report identifier arrives in the query string, where anyone can type one - and "
                + "an unknown one is an empty page rather than an error");
        }

        [Test]
        public void Find_LongAfterTheUpload_ReturnsNull()
        {
            // Arrange
            var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            var store = new UploadReportStore(clock);
            var report = Report();

            store.Add(report);

            // Act
            clock.Advance(TimeSpan.FromHours(1));

            // Assert
            store.Find(report.Id).Should().BeNull(
                "because a report names files and folders, and it is held only for as long as the page "
                + "that shows it might still be reloaded - the log file is the lasting record");
        }

        /// <summary>
        /// The sweep runs on write, so a lapsed report has to be gone even if nobody asks for it.
        /// </summary>
        [Test]
        public void Add_AfterAnEarlierReportLapsed_DropsTheOldOne()
        {
            // Arrange
            var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
            var store = new UploadReportStore(clock);
            var first = Report();

            store.Add(first);

            // Act
            clock.Advance(TimeSpan.FromHours(1));
            store.Add(Report());

            // Assert
            store.Find(first.Id).Should().BeNull(
                "because nothing schedules a sweep - an upload is the only thing that grows this, so it "
                + "is also the only thing that tidies it");
        }

        private static UploadReport Report()
        {
            return new UploadReport
            {
                Id = Guid.NewGuid(),
                CompletedUtc = DateTimeOffset.UnixEpoch,
                Destination = Path.Combine(Path.GetTempPath(), "dlna-media"),
                Files = [],
            };
        }
    }
}
