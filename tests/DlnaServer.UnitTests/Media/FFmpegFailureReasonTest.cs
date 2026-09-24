using System.Text;
using DlnaServer.Media.Processing;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class FFmpegFailureReasonTest
    {
        [Test]
        public void Summarise_ForAShortReason_ReturnsItUnchanged()
        {
            // Arrange
            const string reason = "Invalid file. Cannot load file /share/Media/film.avi";

            // Act
            var summary = FFmpegFailureReason.Summarise(reason);

            // Assert
            summary.Should().Be(reason, "because a reason that already fits one log line has nothing to lose");
        }

        /// <summary>
        /// The shape seen on the NAS: a banner, then the same decoder error once per bad packet, then
        /// progress lines - 139,365 lines in one warning.
        /// </summary>
        [Test]
        public void Summarise_ForAFloodOfRepeatedErrors_KeepsEachDistinctErrorOnce()
        {
            // Arrange
            var output = new StringBuilder();
            output.AppendLine("ffmpeg version 6.1-static https://johnvansickle.com/ffmpeg/");
            output.AppendLine("  built with gcc 8 (Debian 8.3.0-6)");

            for (var index = 0; index < 50_000; index++)
            {
                output.AppendLine("[vist#0:0/h264 @ 0x7060340] Decoding error: Invalid data found when processing input");
            }

            output.AppendLine("frame=    1 fps=0.0 q=-0.0 Lsize=N/A time=00:00:00.00 bitrate=N/A");

            // Act
            var summary = FFmpegFailureReason.Summarise(output.ToString());

            // Assert
            summary.Length.Should().BeLessThan(1_000,
                "because the reason has to fit one log line, not fill the log file");

            summary.Should().Contain("Decoding error: Invalid data found when processing input",
                "because the error line is the part that says why the preview failed");

            summary.Should().Contain("50000 error line(s)",
                "because the operator should still see how much output was condensed");
        }

        [Test]
        public void Summarise_ForManyDistinctErrors_KeepsOnlyTheFirstFew()
        {
            // Arrange
            var output = new StringBuilder();

            for (var index = 0; index < 100; index++)
            {
                output.AppendLine($"[h264 @ 0x7071b80] Invalid NAL unit size ({index} > 6195).");
            }

            // Act
            var summary = FFmpegFailureReason.Summarise(output.ToString());

            // Assert
            summary.Should().Contain("(0 > 6195)",
                "because the first error is usually the cause and the rest follow from it");

            summary.Should().NotContain("(99 > 6195)",
                "because the kept lines are capped, whatever ffmpeg printed");
        }

        [Test]
        public void Summarise_WhenNoLineLooksLikeAnError_KeepsTheLastLine()
        {
            // Arrange
            var output = new StringBuilder();

            for (var index = 0; index < 20; index++)
            {
                output.AppendLine($"  libavcodec     60. 31.{index}");
            }

            output.AppendLine("Conversion stopped at frame 0");

            // Act
            var summary = FFmpegFailureReason.Summarise(output.ToString());

            // Assert
            summary.Should().Contain("Conversion stopped at frame 0",
                "because the last line is where ffmpeg usually says why it stopped");

            summary.Should().NotContain("libavcodec",
                "because the version banner says nothing about the failure");
        }
    }
}
