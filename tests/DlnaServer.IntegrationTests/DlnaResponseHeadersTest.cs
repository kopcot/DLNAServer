using DlnaServer.Core.Dlna;
using DlnaServer.Host.Delivery;
using DlnaServer.Upnp.Constants;
using Microsoft.AspNetCore.Http;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Pins the DLNA response headers. The reference sends none of them, so there is nothing to compare
    /// against on the wire - what these tests protect is agreement with the <c>res@protocolInfo</c> the
    /// same resource advertised while browsing, which is what a renderer checks.
    /// </summary>
    [TestFixture]
    internal sealed class DlnaResponseHeadersTest
    {
        [Test]
        public void Apply_ForVideo_DeclaresStreamingTransferMode()
        {
            // Arrange
            var context = new DefaultHttpContext();

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Video,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.VideoMp4, "AVC_MP4_BL_CIF15_AAC_520", ".mp4"));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.TransferMode].ToString().Should().Be("Streaming",
                "because video is consumed continuously while it plays");
        }

        [Test]
        public void Apply_ForAnImage_DeclaresInteractiveTransferMode()
        {
            // Arrange
            var context = new DefaultHttpContext();

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Image,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.ImageJpeg, "JPEG", ".jpg"));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.TransferMode].ToString().Should().Be("Interactive",
                "because an image is fetched whole for display rather than streamed");
        }

        /// <summary>
        /// DLNA has the server confirm the transfer mode by echoing what the renderer asked for.
        /// </summary>
        [TestCase(DlnaResponseHeaders.StreamingMode)]
        [TestCase(DlnaResponseHeaders.InteractiveMode)]
        [TestCase(DlnaResponseHeaders.BackgroundMode)]
        public void Apply_WhenTheRendererRequestsATransferMode_EchoesIt(string requested)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers[DlnaResponseHeaders.TransferMode] = requested;

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Video,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.VideoMp4, "MP4", ".mp4"));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.TransferMode].ToString().Should().Be(requested,
                "because the server confirms the mode the renderer asked for");
        }

        [Test]
        public void Apply_WhenTheRendererRequestsAnUnknownTransferMode_FallsBackToTheContentKind()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers[DlnaResponseHeaders.TransferMode] = "Teleportation";

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Audio,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.AudioMp4, "MP4", ".mp3"));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.TransferMode].ToString().Should().Be("Streaming",
                "because an unrecognised request must not be echoed back as though it were honoured");
        }

        /// <summary>
        /// The header and the browse result describe one resource. A renderer that finds them disagreeing
        /// refuses to play, with no diagnostic.
        /// </summary>
        [Test]
        public void Apply_SendsTheSameFeatureListTheBrowseResultAdvertised()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var protocolInfo = DlnaProtocolInfo.ForResource(DlnaMime.VideoXMatroska, null, ".mkv");

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Video,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.VideoXMatroska, null, ".mkv"));

            // Assert
            var contentFeatures = context.Response.Headers[DlnaResponseHeaders.ContentFeatures].ToString();

            protocolInfo.Should().EndWith(contentFeatures,
                "because the header is the tail of the protocolInfo the item was browsed with");
            contentFeatures.Should().Be(
                "DLNA.ORG_PN=MATROSKA;DLNA.ORG_OP=01;DLNA.ORG_CI=0;"
                + "DLNA.ORG_FLAGS=21F00000000000000000000000000000",
                "because these are the exact fields a renderer parses out of the header");
        }

        [Test]
        public void Apply_ForAThumbnail_MarksItAsAThumbnailResource()
        {
            // Arrange
            var context = new DefaultHttpContext();

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Image,
                DlnaProtocolInfo.ContentFeaturesForThumbnail(
                    DlnaMime.ImageJpeg,
                    DlnaProtocolInfo.ThumbnailProfileName));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.ContentFeatures].ToString().Should().Be(
                "DLNA.ORG_PN=JPEG_TN;DLNA.ORG_OP=00;DLNA.ORG_CI=1;"
                + "DLNA.ORG_FLAGS=20F00000000000000000000000000000",
                "because a thumbnail declares no seek support and marks itself as an index-1 resource");
        }

        /// <summary>
        /// The transfer mode agrees with the flags the same response carries, for every kind.
        /// </summary>
        /// <remarks>
        /// This is the invariant the fixture already stated but only ever exercised for video, which is
        /// how the two disagreed for <see cref="DlnaMedia.Subtitle"/> and <see cref="DlnaMedia.Unknown"/>
        /// unnoticed: the flags claimed the streaming bit while the header answered <c>Interactive</c>.
        /// Derived from the emitted feature list rather than restating the rule, so a future change to
        /// either side has to keep them in step.
        /// </remarks>
        [TestCase(DlnaMime.VideoMp4)]
        [TestCase(DlnaMime.AudioMp4)]
        [TestCase(DlnaMime.ImageJpeg)]
        [TestCase(DlnaMime.SubtitleSubrip)]
        [TestCase(DlnaMime.Undefined)]
        public void Apply_ForEveryKind_DeclaresTheTransferModeItsOwnFlagsClaim(DlnaMime mime)
        {
            // Arrange
            var context = new DefaultHttpContext();
            var contentFeatures = DlnaProtocolInfo.ContentFeaturesFor(
                mime,
                dlnaProfileName: null,
                fileExtension: ".bin");

            // Act
            DlnaResponseHeaders.Apply(context.Request, context.Response, mime.ToMedia(), contentFeatures);

            // Assert
            var claimsStreaming = contentFeatures.Contains(
                DlnaProtocolInfo.FlagsStreaming,
                StringComparison.Ordinal);
            var expected = claimsStreaming
                ? DlnaResponseHeaders.StreamingMode
                : DlnaResponseHeaders.InteractiveMode;

            context.Response.Headers[DlnaResponseHeaders.TransferMode].ToString().Should().Be(expected,
                $"because {mime.ToMedia()} advertises "
                + (claimsStreaming ? "the streaming flag set" : "the interactive flag set")
                + ", and a renderer that finds the header contradicting it refuses to play with no "
                + "diagnostic");
        }

        [Test]
        public void Apply_AlwaysDeclaresNoRealTimeTag()
        {
            // Arrange
            var context = new DefaultHttpContext();

            // Act
            DlnaResponseHeaders.Apply(
                context.Request,
                context.Response,
                DlnaMedia.Video,
                DlnaProtocolInfo.ContentFeaturesFor(DlnaMime.VideoMp4, "MP4", ".mp4"));

            // Assert
            context.Response.Headers[DlnaResponseHeaders.RealTimeInfo].ToString().Should().Be("DLNA.ORG_TLAG=*",
                "because this server serves stored files, never a live stream with a time-based tag");
        }
    }
}
