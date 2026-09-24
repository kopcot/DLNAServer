using DlnaServer.Media.Processing;

namespace DlnaServer.UnitTests.Media
{
    [TestFixture]
    internal sealed class MediaProcessorTest
    {
        private const string XabeStackTrace =
            "   at System.Collections.Generic.List`1.ToArray()\n"
            + "   at Xabe.FFmpeg.FFmpegWrapper.<>c__DisplayClass14_0.<RunProcess>b__0()";

        /// <summary>
        /// The shape logged on the NAS for VID_20250606_160533.mp4, whose next attempt three seconds later
        /// produced its preview without complaint.
        /// </summary>
        [Test]
        public void IsOutputLogRace_ForTheCopyFailingInsideXabe_IsTrue()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("destinationArray", XabeStackTrace);

            // Assert
            isRace.Should().BeTrue(
                "because that is Xabe copying its output log while ffmpeg's last lines are still arriving");
        }

        [Test]
        public void IsOutputLogRace_ForTheSameParameterOutsideXabe_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace(
                "destinationArray",
                "   at System.Array.Copy(Array sourceArray, Array destinationArray, Int32 length)\n"
                    + "   at DlnaServer.Media.Processing.Thumbnails.ImageThumbnailGenerator.Generate()");

            // Assert
            isRace.Should().BeFalse(
                "because a copy failing in our own code is a defect to report, not a race to ride out");
        }

        [Test]
        public void IsOutputLogRace_ForAnotherArgumentInsideXabe_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("path", XabeStackTrace);

            // Assert
            isRace.Should().BeFalse(
                "because only the output-log copy is known to fail after ffmpeg has finished its work");
        }

        [Test]
        public void IsOutputLogRace_WithoutAStackTrace_IsFalse()
        {
            // Act
            var isRace = MediaProcessor.IsOutputLogRace("destinationArray", stackTrace: null);

            // Assert
            isRace.Should().BeFalse("because an exception that was never thrown cannot have come from Xabe");
        }
    }
}
