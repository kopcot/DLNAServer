using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Files
{
    /// <summary>
    /// Covers the plausibility rule the first fill dates both files and folders through.
    /// </summary>
    /// <remarks>
    /// It was private to the scanner while only files needed it. The folder half of the first-fill rule
    /// lives in the host, which cannot reach a private method in another project, and two copies of a
    /// plausibility rule is how the two halves of a rule stop agreeing.
    /// </remarks>
    [TestFixture]
    internal sealed class FileSystemDateTest
    {
        [Test]
        public void Resolve_WithAPlausibleCreationTime_PrefersIt()
        {
            // Arrange
            var created = new DateTime(2014, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            var modified = new DateTime(2021, 8, 9, 10, 11, 12, DateTimeKind.Utc);

            // Act
            var resolved = FileSystemDate.Resolve(created, modified);

            // Assert
            resolved.Should().Be(created,
                "because when a filesystem does record a birth time, that is the entry's own date");
        }

        /// <summary>
        /// A filesystem reporting no birth time falls back to the write time.
        /// </summary>
        /// <remarks>
        /// Common on Linux, and the value comes back as the Unix epoch or as
        /// <see cref="DateTime.MinValue"/> depending on the platform and the mount. Using it unchecked
        /// would date every entry on such a volume to 1970 - the exact no-ordering the rule exists to
        /// avoid, while looking like a working feature.
        /// </remarks>
        [Test]
        public void Resolve_WithNoBirthTime_FallsBackToTheWriteTime()
        {
            // Arrange
            var modified = new DateTime(2021, 8, 9, 10, 11, 12, DateTimeKind.Utc);

            // Act
            var fromEpoch = FileSystemDate.Resolve(DateTime.UnixEpoch, modified);
            var fromMinValue = FileSystemDate.Resolve(DateTime.MinValue, modified);

            // Assert
            fromEpoch.Should().Be(modified, "because 1970 is a filesystem saying it does not know");
            fromMinValue.Should().Be(modified,
                "because DateTime.MinValue is the other way it says so - and because the write time is "
                + "always populated, which is what keeps the result off default(DateTime): a default and "
                + "a null collapse into the same thing at DlnaDbContext.StampTimestamps, so a row "
                + "carrying one would silently take the clock");
        }

        /// <summary>
        /// A creation time in the future is treated as unknown too.
        /// </summary>
        /// <remarks>
        /// A clock that ran ahead, or an archive unpacked with bad metadata, otherwise pins entries to the
        /// top of Recently added permanently.
        /// </remarks>
        [Test]
        public void Resolve_WithACreationTimeInTheFuture_FallsBackToTheWriteTime()
        {
            // Arrange
            var modified = DateTime.UtcNow.AddDays(-30);

            // Act
            var resolved = FileSystemDate.Resolve(DateTime.UtcNow.AddYears(5), modified);

            // Assert
            resolved.Should().Be(modified,
                "because nothing on disc was created five years from now, so the date is not to be trusted");
        }
    }
}
