using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class VideoCaptureTimeTest
    {
        /// <summary>
        /// Tiers carried over verbatim from the reference. Grabbing frame zero of a film usually yields
        /// a black rectangle, so the offset scales with duration.
        /// </summary>
        [TestCase(90, 30, TestName = "Feature length captures at 30 minutes")]
        [TestCase(50, 10, TestName = "Long episode captures at 10 minutes")]
        [TestCase(25, 5, TestName = "Episode captures at 5 minutes")]
        [TestCase(10, 1, TestName = "Short captures at 1 minute")]
        public void For_LongerVideos_CapturesProportionallyLaterInMinutes(int durationMinutes, int expectedMinutes)
        {
            // Arrange
            // Act
            var captureAt = VideoCaptureTime.For(TimeSpan.FromMinutes(durationMinutes));

            // Assert
            captureAt.Should().Be(TimeSpan.FromMinutes(expectedMinutes),
                "because the capture point must sit inside the video and past its opening titles");
        }

        [TestCase(150, 30, TestName = "Two-and-a-half minutes captures at 30 seconds")]
        [TestCase(60, 10, TestName = "One minute captures at 10 seconds")]
        [TestCase(10, 2, TestName = "Ten seconds captures at 2 seconds")]
        public void For_ShorterVideos_CapturesProportionallyLaterInSeconds(int durationSeconds, int expectedSeconds)
        {
            // Arrange
            // Act
            var captureAt = VideoCaptureTime.For(TimeSpan.FromSeconds(durationSeconds));

            // Assert
            captureAt.Should().Be(TimeSpan.FromSeconds(expectedSeconds),
                "because a short clip still needs a capture point inside its own duration");
        }

        [Test]
        public void For_AVeryShortClip_CapturesTheFirstFrame()
        {
            // Arrange
            // Act
            var captureAt = VideoCaptureTime.For(TimeSpan.FromSeconds(3));

            // Assert
            captureAt.Should().Be(TimeSpan.Zero,
                "because there is nowhere else to capture from in a three-second clip");
        }

        private static readonly int[] _boundaryDurationsInSeconds =
            [1, 6, 31, 121, 301, 1201, 2401, 3601, 7200];

        [Test]
        public void For_EveryDuration_ProducesACapturePointInsideTheVideo()
        {
            // Arrange
            var durations = _boundaryDurationsInSeconds
                .Select(static seconds => TimeSpan.FromSeconds(seconds));

            // Act & Assert
            foreach (var duration in durations)
            {
                VideoCaptureTime.For(duration).Should().BeLessThan(duration + TimeSpan.FromSeconds(1),
                    "because seeking past the end of a {0} video yields no frame", duration);
            }
        }
    }
}
