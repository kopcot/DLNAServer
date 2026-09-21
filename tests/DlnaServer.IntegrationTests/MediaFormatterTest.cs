using System.Globalization;
using DlnaServer.Admin.Formatting;

namespace DlnaServer.IntegrationTests
{
    [TestFixture]
    internal sealed class MediaFormatterTest
    {
        private MediaFormatter _formatter = null!;

        [SetUp]
        public void SetUp()
        {
            _formatter = new MediaFormatter();
        }

        /// <summary>
        /// The defect this exists for: the preview page showed a half-minute clip as
        /// <c>00:00:29.9600000</c>, which is what <see cref="TimeSpan.ToString()"/> produces.
        /// </summary>
        [Test]
        public void Duration_ForLessThanAnHour_IsMinutesAndSeconds()
        {
            // Arrange
            var duration = TimeSpan.FromSeconds(29.96);

            // Act
            var formatted = _formatter.Duration(duration);

            // Assert
            formatted.Should().Be("0:30",
                "because a running time is read in minutes and seconds, rounded rather than truncated");
        }

        [Test]
        public void Duration_ForMoreThanAnHour_IncludesTheHours()
        {
            // Arrange
            var duration = new TimeSpan(1, 24, 5);

            // Act
            var formatted = _formatter.Duration(duration);

            // Assert
            formatted.Should().Be("1:24:05",
                "because a film's length has to show its hours, with minutes and seconds padded");
        }

        [TestCase(null)]
        [TestCase(0)]
        public void Duration_WithNothingToShow_IsThePlaceholder(int? seconds)
        {
            // Arrange
            var duration = seconds is { } value ? TimeSpan.FromSeconds(value) : (TimeSpan?)null;

            // Act
            var formatted = _formatter.Duration(duration);

            // Assert
            formatted.Should().Be("-",
                "because a file whose metadata has not been read must not render an empty cell");
        }

        /// <summary>
        /// The dashboard's <em>Running for</em>. Two units at every scale, the smaller one dropped when
        /// it is zero, and never the raw TimeSpan - a server up three days would otherwise read
        /// <c>76:41:03</c>, which is arithmetic nobody should do to learn it restarted yesterday.
        /// </summary>
        [TestCase(0, 0, 30, "less than a minute")]
        [TestCase(0, 0, 59, "less than a minute")]
        [TestCase(0, 0, 60, "1 minute")]
        [TestCase(0, 45, 0, "45 minutes")]
        [TestCase(0, 60, 0, "1 hour")]
        [TestCase(0, 61, 0, "1 hour, 1 minute")]
        [TestCase(0, 300, 0, "5 hours")]
        [TestCase(1, 0, 0, "1 day")]
        [TestCase(3, 240, 0, "3 days, 4 hours")]
        [TestCase(2, 0, 0, "2 days")]
        public void Elapsed_ReadsAsWordsAndNeverAsAClock(int days, int minutes, int seconds, string expected)
        {
            // Arrange
            var elapsed = new TimeSpan(days, 0, minutes, seconds);

            // Act
            var actual = _formatter.Elapsed(elapsed);

            // Assert
            actual.Should().Be(expected,
                "because the dashboard is read by someone who wants to know whether the server restarted, "
                + "not to do arithmetic");
        }

        [Test]
        public void Elapsed_WithANegativeSpan_IsThePlaceholder()
        {
            // Arrange
            var elapsed = TimeSpan.FromMinutes(-5);

            // Act
            var actual = _formatter.Elapsed(elapsed);

            // Assert
            actual.Should().Be(_formatter.Unknown,
                "because a clock that moved backwards must not render as a negative uptime");
        }

        [TestCase(500_734L, "500 kbps")]
        [TestCase(1_500_000L, "1.5 Mbps")]
        [TestCase(999_999L, "999 kbps")]
        public void Bitrate_UsesTheUnitThatKeepsItShort(long bitsPerSecond, string expected)
        {
            // Arrange
            // Act
            var formatted = _formatter.Bitrate(bitsPerSecond);

            // Assert
            formatted.Should().Be(expected,
                "because a bare five- or seven-digit number is unreadable next to the other figures");
        }

        [Test]
        public void Bitrate_WithNothingToShow_IsThePlaceholder()
        {
            // Arrange
            // Act
            var formatted = _formatter.Bitrate(null);

            // Assert
            formatted.Should().Be("-",
                "because an absent bitrate is not a zero bitrate");
        }

        [Test]
        public void Megabytes_RoundsToOneDecimal()
        {
            // Arrange
            // Act
            var formatted = _formatter.Megabytes(1_875_249);

            // Assert
            formatted.Should().Be("1.8 MB",
                "because file sizes are compared at a glance, not audited to the byte");
        }

        /// <summary>
        /// The defect this exists for: the dashboard and the file-cache page each had their own copy of
        /// this formatter, one of them dividing two <see cref="long"/> operands, so the two pages reported
        /// different numbers for the same byte count.
        /// </summary>
        [Test]
        public void Megabytes_ForAValueUnderOneMegabyte_KeepsItsDecimal()
        {
            // Arrange
            // Act
            var formatted = _formatter.Megabytes(524_288);

            // Assert
            formatted.Should().Be("0.5 MB",
                "because integer division would report half a megabyte as 0 MB");
        }

        [TestCase(null, "-")]
        [TestCase("", "-")]
        [TestCase("   ", "-")]
        [TestCase("eng", "eng")]
        public void Text_FallsBackToThePlaceholder(string? value, string expected)
        {
            // Arrange
            // Act
            var formatted = _formatter.Text(value);

            // Assert
            formatted.Should().Be(expected,
                "because a stream that carries no language must not leave the cell blank");
        }

        [TestCase(null, "-")]
        [TestCase(6, "6")]
        public void Number_FallsBackToThePlaceholder(int? value, string expected)
        {
            // Arrange
            // Act
            var formatted = _formatter.Number(value);

            // Assert
            formatted.Should().Be(expected,
                "because an unread channel count is not a zero channel count");
        }

        /// <remarks>
        /// Empty rather than the placeholder, which is what the two call sites rendered before this method
        /// existed: one of them null-propagates inside a branch where the value cannot be null, and a dash
        /// there would put a stray character into a sentence.
        /// </remarks>
        [Test]
        public void Clock_WithNoInstant_IsEmpty()
        {
            // Arrange
            // Act
            var formatted = _formatter.Clock(null);

            // Assert
            formatted.Should().BeEmpty(
                "because an absent block expiry renders nothing, not a placeholder");
        }

        [Test]
        public void Clock_RendersTheLocalTimeOfDay()
        {
            // Arrange
            var instant = new DateTimeOffset(2026, 9, 3, 14, 30, 0, TimeSpan.Zero);

            // Act
            var formatted = _formatter.Clock(instant);

            // Assert
            formatted.Should().Be(instant.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                "because the operator reads a wall-clock time in their own zone");
        }

        [Test]
        public void Day_RendersTheLocalCalendarDate()
        {
            // Arrange
            var instant = new DateTime(2026, 9, 3, 14, 30, 0, DateTimeKind.Utc);

            // Act
            var formatted = _formatter.Day(instant);

            // Assert
            formatted.Should().Be(instant.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "because a tile shows the date a file was last written, not its time");
        }

        [Test]
        public void Unknown_IsTheDashEveryPlaceholderUses()
        {
            // Arrange
            // Act
            var placeholder = _formatter.Unknown;

            // Assert
            placeholder.Should().Be("-",
                "because six call sites once spelled this literal out beside the constant");
        }
    }
}
