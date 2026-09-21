using DlnaServer.Core.Dlna;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Pins <c>res@protocolInfo</c>. A renderer matches this string literally before it will play
    /// anything, so a wrong field is the difference between a file playing and a silent refusal.
    /// </summary>
    [TestFixture]
    internal sealed class DlnaProtocolInfoTest
    {
        [Test]
        public void ForResource_ForVideo_MatchesTheReferenceExactly()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(
                DlnaMime.VideoMp4,
                dlnaProfileName: "AVC_MP4_BL_CIF15_AAC_520",
                fileExtension: ".mp4");

            // Assert
            protocolInfo.Should().Be(
                "http-get:*:video/mp4:DLNA.ORG_PN=AVC_MP4_BL_CIF15_AAC_520;DLNA.ORG_OP=01;DLNA.ORG_CI=0;"
                + "DLNA.ORG_FLAGS=21F00000000000000000000000000000",
                "because this is byte-for-byte what the reference advertises for an MP4");
        }

        /// <summary>
        /// Images use the interactive flag set, which is the streaming set minus the streaming-transfer bit.
        /// </summary>
        [Test]
        public void ForResource_ForImage_UsesTheInteractiveFlags()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(
                DlnaMime.ImageJpeg,
                dlnaProfileName: "JPEG",
                fileExtension: ".jpg");

            // Assert
            protocolInfo.Should().Be(
                "http-get:*:image/jpeg:DLNA.ORG_PN=JPEG;DLNA.ORG_OP=01;DLNA.ORG_CI=0;"
                + "DLNA.ORG_FLAGS=20F00000000000000000000000000000",
                "because an image is fetched interactively rather than streamed");
        }

        /// <summary>
        /// The reference advertises time-seek only, even though its media endpoint serves byte ranges.
        /// Preserved deliberately - the devices in use were validated against this value.
        /// </summary>
        [Test]
        public void ForResource_AlwaysAdvertisesTimeSeekOnly()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(DlnaMime.VideoXMatroska, "MATROSKA", ".mkv");

            // Assert
            protocolInfo.Should().Contain("DLNA.ORG_OP=01;",
                "because 01 is time-seek only; advertising byte-seek changes how a renderer scrubs");
        }

        [Test]
        public void ForResource_WithoutAConfiguredProfile_FallsBackToTheCatalogProfile()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(
                DlnaMime.VideoXMatroska,
                dlnaProfileName: null,
                fileExtension: ".mkv");

            // Assert
            protocolInfo.Should().Contain("DLNA.ORG_PN=MATROSKA;",
                "because the catalog supplies the profile when configuration does not");
        }

        [Test]
        public void ForResource_ForAMimeWithNoProfile_FallsBackToTheUpperCasedExtension()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(
                DlnaMime.VideoOgg,
                dlnaProfileName: null,
                fileExtension: ".ogv");

            // Assert
            protocolInfo.Should().Contain("DLNA.ORG_PN=OGV;",
                "because the reference falls back to the extension without its dot, upper-cased");
        }

        /// <summary>
        /// The LG workaround: .mp3 is configured as audio/mp4, and that must survive into protocolInfo.
        /// </summary>
        [Test]
        public void ForResource_ForTheLgMp3Workaround_AdvertisesAudioMp4()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(DlnaMime.AudioMp4, "MP4", ".mp3");

            // Assert
            protocolInfo.Should().StartWith("http-get:*:audio/mp4:DLNA.ORG_PN=MP4;",
                "because LG TVs accept audio/mp4 for MP3 files and reject audio/mpeg3 - "
                + "this mapping is a deliberate device workaround carried over from the reference");
        }

        [Test]
        public void ForThumbnail_AdvertisesNoSeekAndTheThumbnailContentIndex()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForThumbnail(DlnaMime.ImageJpeg, "JPEG_TN");

            // Assert
            protocolInfo.Should().Be(
                "http-get:*:image/jpeg:DLNA.ORG_PN=JPEG_TN;DLNA.ORG_OP=00;DLNA.ORG_CI=1;"
                + "DLNA.ORG_FLAGS=20F00000000000000000000000000000",
                "because a thumbnail declares no seek support and marks itself as an index-1 resource");
        }

        /// <summary>
        /// The <c>contentFeatures.dlna.org</c> header and the resource's own <c>protocolInfo</c> must
        /// agree: a renderer compares the two, and a mismatch is a refusal to play with no diagnostic.
        /// </summary>
        [Test]
        public void ContentFeaturesFor_IsTheProtocolInfoWithoutItsTransportAndMimeFields()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForResource(
                DlnaMime.VideoMp4,
                dlnaProfileName: "AVC_MP4_BL_CIF15_AAC_520",
                fileExtension: ".mp4");

            var contentFeatures = DlnaProtocolInfo.ContentFeaturesFor(
                DlnaMime.VideoMp4,
                dlnaProfileName: "AVC_MP4_BL_CIF15_AAC_520",
                fileExtension: ".mp4");

            // Assert
            protocolInfo.Should().Be($"http-get:*:video/mp4:{contentFeatures}",
                "because the header form is the same feature list the browse result advertised");
        }

        [Test]
        public void ContentFeaturesFor_ForVideo_MatchesTheAdvertisedFeatureList()
        {
            // Arrange
            // Act
            var contentFeatures = DlnaProtocolInfo.ContentFeaturesFor(
                DlnaMime.VideoMp4,
                dlnaProfileName: "AVC_MP4_BL_CIF15_AAC_520",
                fileExtension: ".mp4");

            // Assert
            contentFeatures.Should().Be(
                "DLNA.ORG_PN=AVC_MP4_BL_CIF15_AAC_520;DLNA.ORG_OP=01;DLNA.ORG_CI=0;"
                + "DLNA.ORG_FLAGS=21F00000000000000000000000000000",
                "because this is the exact header value a renderer reads off a video response");
        }

        [Test]
        public void ContentFeaturesForThumbnail_IsTheThumbnailProtocolInfoWithoutItsTransportAndMimeFields()
        {
            // Arrange
            // Act
            var protocolInfo = DlnaProtocolInfo.ForThumbnail(
                DlnaMime.ImageJpeg,
                DlnaProtocolInfo.ThumbnailProfileName);

            var contentFeatures = DlnaProtocolInfo.ContentFeaturesForThumbnail(
                DlnaMime.ImageJpeg,
                DlnaProtocolInfo.ThumbnailProfileName);

            // Assert
            protocolInfo.Should().Be($"http-get:*:image/jpeg:{contentFeatures}",
                "because the thumbnail endpoint and the DIDL-Lite thumbnail resource describe one thing");
        }

        [Test]
        public void ThumbnailProfileName_IsTheProfileTheDidlThumbnailResourceAdvertises()
        {
            // Assert
            DlnaProtocolInfo.ThumbnailProfileName.Should().Be("JPEG_TN",
                "because renderers match this profile literally when deciding a preview is a thumbnail");
        }

        /// <summary>
        /// ConnectionManager's <c>Source</c> list. The reference returns it empty, which reads as
        /// "serves nothing" to a renderer that pre-filters on it.
        /// </summary>
        [Test]
        public void BuildSourceList_ListsOneWildcardEntryPerMimeType()
        {
            // Arrange
            // Act
            var source = DlnaProtocolInfo.BuildSourceList(
                [DlnaMime.VideoMp4, DlnaMime.ImageJpeg]);

            // Assert
            source.Should().Be("http-get:*:image/jpeg:*,http-get:*:video/mp4:*",
                "because each entry claims any profile of that MIME type over HTTP");
        }

        /// <summary>
        /// Several configured extensions map to one MIME type - .jpg and .jpeg both being image/jpeg -
        /// and a repeated entry would claim the same capability twice.
        /// </summary>
        [Test]
        public void BuildSourceList_DeduplicatesRepeatedMimeTypes()
        {
            // Arrange
            // Act
            var source = DlnaProtocolInfo.BuildSourceList(
                [DlnaMime.ImageJpeg, DlnaMime.ImageJpeg, DlnaMime.VideoMp4]);

            // Assert
            source.Should().Be("http-get:*:image/jpeg:*,http-get:*:video/mp4:*",
                "because .jpg and .jpeg are two extensions but one capability");
        }

        [Test]
        public void BuildSourceList_ForNoConfiguredTypes_IsEmpty()
        {
            // Arrange
            // Act
            var source = DlnaProtocolInfo.BuildSourceList([]);

            // Assert
            source.Should().BeEmpty(
                "because a server with nothing configured has nothing to claim, and an empty list is the "
                + "honest answer rather than a wildcard that promises everything");
        }

        /// <summary>
        /// Everything but an image is streamed, which is the reference's own test.
        /// </summary>
        /// <remarks>
        /// <c>BrowseItemMapper.GetResourceProtocolInfo</c> asks exactly this single question, so subtitles
        /// and unmapped files take the streaming flag set on the reference wire too. Pinned because the
        /// obvious-looking repair for the transfer-mode disagreement was to narrow this instead - and
        /// these values are matched literally by televisions that were validated against them.
        /// </remarks>
        [TestCase(DlnaMedia.Video, true)]
        [TestCase(DlnaMedia.Audio, true)]
        [TestCase(DlnaMedia.Subtitle, true)]
        [TestCase(DlnaMedia.Unknown, true)]
        [TestCase(DlnaMedia.Container, true)]
        [TestCase(DlnaMedia.Image, false)]
        public void IsStreamed_MatchesTheReferencesImageOnlyTest(DlnaMedia media, bool expected)
        {
            // Assert
            DlnaProtocolInfo.IsStreamed(media).Should().Be(expected,
                $"because the reference sends the interactive flags for images and the streaming flags "
                + $"for everything else, and {media} is no exception to that");
        }

        /// <summary>
        /// A subtitle's feature list carries the reference's streaming flags, unchanged.
        /// </summary>
        [Test]
        public void ContentFeaturesFor_ForASubtitle_KeepsTheReferenceFlags()
        {
            // Arrange
            // Act
            var contentFeatures = DlnaProtocolInfo.ContentFeaturesFor(
                DlnaMime.SubtitleSubrip,
                dlnaProfileName: null,
                fileExtension: ".srt");

            // Assert
            contentFeatures.Should().EndWith("DLNA.ORG_FLAGS=21F00000000000000000000000000000",
                "because the reference tests the item class for Image alone, so a subtitle lands on the "
                + "streaming flag set - the header is what had to move to agree with it, not this");
        }

        [Test]
        public void Flags_AreThirtyTwoCharactersLong()
        {
            // Assert
            DlnaProtocolInfo.FlagsStreaming.Should().HaveLength(32,
                "because DLNA.ORG_FLAGS is 8 hex digits followed by 24 zeros");
            DlnaProtocolInfo.FlagsInteractive.Should().HaveLength(32,
                "because DLNA.ORG_FLAGS is 8 hex digits followed by 24 zeros");
        }
    }
}
