using DlnaServer.Admin.Playback;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers which containers the admin preview offers a player for.
    /// </summary>
    /// <remarks>
    /// The expectations here are measurements, not opinions: every case marked as playing or failing was
    /// loaded in Chrome 152 from the live server on 2026-09-08, and <c>docs/decisions.md</c> standing decision 20
    /// records the run. A future browser could change any of them, which is why the video rule is a
    /// deny-list - see the test at the end.
    /// </remarks>
    [TestFixture]
    internal sealed class BrowserPlaybackTest
    {
        [TestCase(".avi", TestName = "avi - measured, DEMUXER_ERROR_COULD_NOT_OPEN")]
        [TestCase(".AVI", TestName = "avi upper case - the extension is stored lower-cased, but not by this type")]
        [TestCase(".wmv", TestName = "wmv - measured")]
        [TestCase(".flv", TestName = "flv - measured")]
        [TestCase(".mpg", TestName = "mpg - measured")]
        [TestCase(".mpeg")]
        [TestCase(".vob")]
        [TestCase(".asf")]
        [TestCase(".wma")]
        [TestCase(".rm")]
        public void CanPlay_ForAContainerNoBrowserOpens_IsFalse(string extension)
        {
            // Assert
            BrowserPlayback.CanPlay(extension).Should().BeFalse(
                $"because '{extension}' gives the operator a player that does nothing, which is the "
                + "defect - the page shows the format and the Download button instead");
        }

        [TestCase(".mp4", TestName = "mp4 - measured, 854x480")]
        [TestCase(".mkv", TestName = "mkv - measured, 1920x1080")]
        [TestCase(".mov", TestName = "mov - measured, 1920x1080")]
        [TestCase(".m4v", TestName = "m4v - measured")]
        [TestCase(".3gp", TestName = "3gp - measured, and it does play")]
        [TestCase(".webm")]
        [TestCase(".mp3")]
        [TestCase(".flac", TestName = "flac - measured")]
        [TestCase(".m4a")]
        [TestCase(".wav")]
        public void CanPlay_ForAContainerBrowsersOpen_IsTrue(string extension)
        {
            // Assert
            BrowserPlayback.CanPlay(extension).Should().BeTrue(
                $"because '{extension}' plays, and hiding a working player would break something that "
                + "works today");
        }

        [TestCase(".jpg")]
        [TestCase(".jpeg")]
        [TestCase(".png")]
        [TestCase(".gif")]
        [TestCase(".webp")]
        [TestCase(".bmp")]
        [TestCase(".ico")]
        [TestCase(".svg")]
        public void CanDisplayImage_ForAWebImageFormat_IsTrue(string extension)
        {
            // Assert
            BrowserPlayback.CanDisplayImage(extension).Should().BeTrue(
                $"because '{extension}' is one of the formats a browser renders in an img element");
        }

        /// <summary>
        /// The image formats the MIME catalogue knows and a browser cannot render.
        /// </summary>
        /// <remarks>
        /// These matter more than they used to. Scanning now infers a MIME from the extension when
        /// configuration names none, so every one of these is indexable without an operator asking for
        /// it - and each would have rendered as a broken image on the preview page.
        /// </remarks>
        [TestCase(".tif")]
        [TestCase(".tiff")]
        [TestCase(".pcx")]
        [TestCase(".pict")]
        [TestCase(".ras")]
        [TestCase(".pnm")]
        [TestCase(".dwg")]
        [TestCase(".dxf")]
        [TestCase(".xbm")]
        public void CanDisplayImage_ForAFormatOnlyTheCatalogueKnows_IsFalse(string extension)
        {
            // Assert
            BrowserPlayback.CanDisplayImage(extension).Should().BeFalse(
                $"because '{extension}' renders as a broken image, and the extension fallback in "
                + "scanning makes it indexable without anyone asking");
        }

        /// <summary>
        /// An unknown video container still gets a player.
        /// </summary>
        /// <remarks>
        /// The direction of the video rule, and the reason it is a deny-list. Wrongly hiding a player
        /// breaks a file that worked; wrongly showing one is only the behaviour that already shipped. So
        /// a container nobody has measured is given the benefit of the doubt.
        /// </remarks>
        [Test]
        public void CanPlay_ForAContainerNobodyHasMeasured_GivesItTheBenefitOfTheDoubt()
        {
            // Assert
            BrowserPlayback.CanPlay(".somethingnewin2030").Should().BeTrue(
                "because failing safe here means showing a player, not hiding one");
        }

        [TestCase(".avi", "AVI")]
        [TestCase(".mpeg", "MPEG")]
        [TestCase("avi", "AVI", TestName = "ContainerName tolerates a missing dot")]
        public void ContainerName_IsTheFormatAnOperatorWouldRecognise(string extension, string expected)
        {
            // Assert
            BrowserPlayback.ContainerName(extension).Should().Be(expected,
                "because the message names the format, and '.avi' is not how anyone says it");
        }
    }
}
