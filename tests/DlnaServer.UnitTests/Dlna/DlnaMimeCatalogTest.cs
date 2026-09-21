using DlnaServer.Core.Dlna;

namespace DlnaServer.UnitTests.Dlna
{
    [TestFixture]
    internal sealed class DlnaMimeCatalogTest
    {
        /// <summary>
        /// The reference kept MIME strings, profiles, media kinds and extensions in five separate
        /// <c>switch</c> statements, and a member missing from one of them threw mid-response.
        /// One table cannot drift, but it can still have a gap - this is the test for that.
        /// </summary>
        [Test]
        public void All_EveryDeclaredMimeExceptUndefined_HasCatalogEntry()
        {
            // Arrange
            var declared = Enum.GetValues<DlnaMime>()
                .Where(static m => m != DlnaMime.Undefined)
                .ToArray();

            // Act
            var missing = declared
                .Where(static m => !DlnaMimeCatalog.TryGetInfo(m, out _))
                .ToArray();

            // Assert
            missing.Should().BeEmpty(
                "because a MIME with no catalog entry would be served with no content type and no DLNA profile");
        }

        [Test]
        public void All_EveryEntry_HasNonEmptyMimeString()
        {
            // Arrange & Act
            var blank = DlnaMimeCatalog.All
                .Where(static i => string.IsNullOrWhiteSpace(i.MimeString))
                .ToArray();

            // Assert
            blank.Should().BeEmpty("because the MIME string is sent as the Content-Type header");
        }

        [Test]
        public void All_EveryEntry_HasMediaKindAssigned()
        {
            // Arrange & Act
            var unknown = DlnaMimeCatalog.All
                .Where(static i => i.Media == DlnaMedia.Unknown)
                .ToArray();

            // Assert
            unknown.Should().BeEmpty(
                "because the media kind selects the processor and the default upnp:class");
        }

        /// <summary>
        /// Values carried over from the reference. A change here changes what reaches a TV.
        /// </summary>
        [TestCase(DlnaMime.VideoMp4, "video/mp4", "AVC_MP4_BL_CIF15_AAC_520")]
        [TestCase(DlnaMime.VideoXMatroska, "video/x-matroska", "MATROSKA")]
        [TestCase(DlnaMime.VideoXMsvideo, "video/x-msvideo", "MSVIDEO")]
        [TestCase(DlnaMime.VideoQuicktime, "video/quicktime", "QT")]
        [TestCase(DlnaMime.VideoXFlv, "video/x-flv", "FLV")]
        [TestCase(DlnaMime.Video3gpp, "video/3gpp", "AVC_MP4_BL_CIF15_AAC_520")]
        [TestCase(DlnaMime.AudioMp4, "audio/mp4", "MP4")]
        [TestCase(DlnaMime.AudioMpeg3, "audio/mpeg3", "MP3")]
        [TestCase(DlnaMime.ImageJpeg, "image/jpeg", "JPEG")]
        [TestCase(DlnaMime.ImagePng, "image/png", "PNG")]
        public void ToMimeString_ForKnownFormat_MatchesReferenceImplementation(
            DlnaMime mime,
            string expectedMimeString,
            string expectedProfile)
        {
            // Act
            var actualMime = mime.ToMimeString();
            var actualProfile = mime.ToMainProfileName();

            // Assert
            actualMime.Should().Be(expectedMimeString,
                "because the Content-Type and res@protocolInfo must stay byte-identical to the reference");
            actualProfile.Should().Be(expectedProfile,
                "because DLNA.ORG_PN is what a renderer matches against to decide it can play the file");
        }

        [Test]
        public void ToMimeString_ForUndefined_FallsBackToOctetStream()
        {
            // Arrange
            // Act
            var actual = DlnaMime.Undefined.ToMimeString();

            // Assert
            actual.Should().Be("application/octet-stream",
                "because an unrecognised file should still be served, just without a specific type - "
                + "the reference threw NotImplementedException here");
        }

        [TestCase(DlnaMime.VideoMp4, DlnaItemClass.VideoItem)]
        [TestCase(DlnaMime.AudioMp4, DlnaItemClass.AudioItem)]
        [TestCase(DlnaMime.ImageJpeg, DlnaItemClass.ImageItem)]
        [TestCase(DlnaMime.SubtitleSubrip, DlnaItemClass.Generic)]
        [TestCase(DlnaMime.Undefined, DlnaItemClass.Generic)]
        public void ToDefaultItemClass_ForMime_ReturnsExpectedClass(DlnaMime mime, DlnaItemClass expected)
        {
            // Arrange
            // Act
            var actual = mime.ToDefaultItemClass();

            // Assert
            actual.Should().Be(expected,
                "because upnp:class decides which section of a renderer's UI the item appears in");
        }

        [TestCase(".mkv", DlnaMime.VideoXMatroska)]
        [TestCase(".MKV", DlnaMime.VideoXMatroska)]
        [TestCase(".jpg", DlnaMime.ImageJpeg)]
        [TestCase(".mp3", DlnaMime.AudioMpeg3)]
        public void TryGetByFileExtension_ForKnownExtension_ResolvesCaseInsensitively(
            string extension,
            DlnaMime expected)
        {
            // Act
            var found = DlnaMimeCatalog.TryGetByFileExtension(extension, out var actual);

            // Assert
            found.Should().BeTrue("because '{0}' is a supported media extension", extension);
            actual.Should().Be(expected, "because extension resolution must ignore case");
        }

        [Test]
        public void TryGetByFileExtension_ForUnknownExtension_ReturnsFalse()
        {
            // Arrange
            // Act
            var found = DlnaMimeCatalog.TryGetByFileExtension(".not-a-media-file", out _);

            // Assert
            found.Should().BeFalse("because an unmapped extension must not resolve to an arbitrary MIME");
        }
    }
}
