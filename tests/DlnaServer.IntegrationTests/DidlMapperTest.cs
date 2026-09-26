using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Host.Upnp.Control;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the <c>parentID</c> an object declares, which is the field that decides whether a renderer
    /// will show a listing at all.
    /// </summary>
    [TestFixture]
    internal sealed class DidlMapperTest
    {
        private const string Endpoint = "192.168.1.50:26851";

        private static readonly Guid _fileId = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid _directoryId = new("22222222-2222-2222-2222-222222222222");

        /// <summary>
        /// The defect that hid the whole library from an LG television. The root listing surfaces the most
        /// recently indexed files alongside the source folders, and those files were declaring the folder
        /// they physically live in - so a renderer browsing container 0 was handed objects claiming to
        /// belong somewhere else. VLC ignored it; the television showed the server and nothing in it.
        /// </summary>
        [Test]
        public void MapItem_InARootListing_DeclaresTheRootAsItsParent()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, DidlMapper.RootObjectId, subtitles: []);

            // Assert
            item.ParentId.Should().Be("0",
                "because BrowseDirectChildren on container 0 must return objects whose parentID is 0, "
                + "whatever their position in the tree");
        }

        [Test]
        public void MapItem_WithLinkedSubtitles_OffersEachAsAResourceAndOneToSamsung()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);
            var vtt = CreateSubtitle(file, "film.en.vtt");
            var srt = CreateSubtitle(file, "Subs/film.cz.srt");

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: [vtt, srt]);

            // Assert
            item.Resources.Should().HaveCount(3, "because each linked subtitle is a resource of its own after the media");
            item.Resources[0].Url.Should().EndWith($"/fileserver/file/{file.PublicId}",
                "because a renderer that reads the first res must still get the film");
            item.Resources[1].ProtocolInfo.Should().Be("http-get:*:text/vtt:*", "because a subtitle is matched on its MIME");
            item.Resources[2].Url.Should().Be($"http://{Endpoint}/fileserver/subtitle/{srt.PublicId}.srt",
                "because the URL ends in the subtitle's own extension");
            item.CaptionInfo.Should().NotBeNull("because Samsung televisions read a subtitle from sec:CaptionInfoEx");
            item.CaptionInfo!.Type.Should().Be("srt", "because an SRT is preferred over the other formats");
        }

        [Test]
        public void Offerable_LeavesOutALinkWhoseTypeIsNoLongerListed()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);
            var srt = CreateSubtitle(file, "film.srt");
            var ass = CreateSubtitle(file, "film.ass");
            var types = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".srt"] = DlnaMedia.Video };

            // Act
            var offered = DidlMapper.Offerable(subtitles: [srt, ass], subtitleTypes: types);

            // Assert
            offered.Should().Equal([srt],
                "because a type taken off Settings is refused by the file server at once, so it must not be offered");
        }

        [Test]
        public void Offerable_WhenEveryTypeIsListed_ReturnsTheSameList()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);
            IReadOnlyList<SubtitleFileDto> subtitles = [CreateSubtitle(file, "film.SRT")];
            var types = new Dictionary<string, DlnaMedia>(StringComparer.OrdinalIgnoreCase) { [".srt"] = DlnaMedia.Video };

            // Act
            var offered = DidlMapper.Offerable(subtitles: subtitles, subtitleTypes: types);

            // Assert
            offered.Should().BeSameAs(subtitles,
                "because the usual case allocates nothing on the Browse path, and extensions match whatever their case");
        }

        [Test]
        public void MapItem_WithManyLinkedSubtitles_OffersOnlyTheFirstFew()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);
            var subtitles = Enumerable.Range(0, 20)
                .Select(i => CreateSubtitle(file, $"film.{i}.srt"))
                .ToArray();

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: subtitles);

            // Assert
            item.Resources.Should().HaveCount(9,
                "because a folder of many language variants must not make every Browse of it that much longer");
        }

        [Test]
        public void MapItem_WithoutSubtitles_AddsNothing()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);

            // Assert
            item.Resources.Should().ContainSingle("because an item with no subtitles is exactly what it was before");
            item.CaptionInfo.Should().BeNull("because there is nothing to point a Samsung television at");
        }

        [Test]
        public void MapItem_InAFolderListing_DeclaresThatFolderAsItsParent()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);

            // Assert
            item.ParentId.Should().Be(_directoryId.ToString(),
                "because the browsed container is the parent every child declares");
        }

        /// <summary>
        /// BrowseMetadata describes the object itself, so there the real parent is the correct answer.
        /// </summary>
        [Test]
        public void ParentIdOf_AFile_IsTheDirectoryItLivesIn()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var parentId = DidlMapper.ParentIdOf(file);

            // Assert
            parentId.Should().Be(_directoryId.ToString(),
                "because a metadata reply describes where the object actually sits");
        }

        [Test]
        public void ParentIdOf_AFileWithNoDirectory_IsTheRoot()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: null);

            // Act
            var parentId = DidlMapper.ParentIdOf(file);

            // Assert
            parentId.Should().Be("0", "because a file outside any indexed folder hangs off the root");
        }

        [Test]
        public void MapContainer_InARootListing_DeclaresTheRootAsItsParent()
        {
            // Arrange
            var directory = new MediaDirectoryDto
            {
                PublicId = _directoryId,
                ParentDirectoryPublicId = null,
                Name = "Movies",
                FullPath = "/share/Media/Movies",
                Depth = 0,
                IsSourceRoot = true,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };

            // Act
            var container = DidlMapper.MapContainer(directory, Endpoint, DidlMapper.RootObjectId);

            // Assert
            container.ParentId.Should().Be("0", "because a source folder is listed as a child of the root");
            container.ObjectId.Should().Be(_directoryId.ToString(),
                "because the renderer browses back using this identifier");
        }

        /// <summary>
        /// A renderer reads <c>res@duration</c> to show a running time and to drive its seek bar.
        /// </summary>
        /// <remarks>
        /// These three attributes were declared on <c>DidlResource</c> and never assigned, so the wire
        /// carried only protocolInfo, size and url - a divergence from the reference that no test could
        /// catch, because nothing serialises DIDL. <c>res@bitrate</c> is defined in <b>bytes</b> per
        /// second while the container reports bits, so the factor of eight is asserted explicitly.
        /// </remarks>
        [Test]
        public void MapItem_WithVideoMetadata_WritesDurationResolutionAndBitrate()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId) with
            {
                Duration = new TimeSpan(0, 1, 30, 15, 250),
                Width = 1920,
                Height = 1080,
                Bitrate = 8_000_000,
            };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);
            var resource = item.Resources[0];

            // Assert
            resource.Duration.Should().Be("1:30:15.250",
                "because DIDL-Lite wants H:mm:ss.fff with hours unbounded rather than wrapping at 24");
            resource.Resolution.Should().Be("1920x1080",
                "because a renderer uses the pixel dimensions to pick a display mode");
            resource.Bitrate.Should().Be("1000000",
                "because res@bitrate is bytes per second, so 8 Mbit/s must be written as 1 MB/s");
        }

        /// <summary>
        /// Televisions name <c>res@nrAudioChannels</c> and <c>res@sampleFrequency</c> in the Browse
        /// filter they send, and the reference server emits both plus the two codec elements. All four
        /// were declared on the DIDL types and assigned by nothing, so none of them ever reached a wire.
        /// </summary>
        [Test]
        public void MapItem_WithAudioMetadata_WritesTheChannelsSampleRateAndCodecs()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId) with
            {
                AudioChannels = 2,
                AudioSampleRate = 48_000,
                AudioCodec = "aac",
                VideoCodec = "h264",
            };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);
            var resource = item.Resources[0];

            // Assert
            resource.AudioChannels.Should().Be("2",
                "because a television asks for res@nrAudioChannels by name and decides its downmix on it");
            resource.SampleFrequency.Should().Be("48000",
                "because res@sampleFrequency is in hertz and is requested in the same filter");
            item.AudioCodec.Should().Be("aac",
                "because upnp:audioCodec is what the reference emits and this server emitted nothing");
            item.VideoCodec.Should().Be("h264",
                "because upnp:videoCodec is what the reference emits and this server emitted nothing");
        }

        /// <summary>
        /// ffprobe reports zero channels or a zero sample rate for a track it could not read. Writing
        /// <c>nrAudioChannels="0"</c> asserts the file has no audio, which is worse than saying nothing.
        /// </summary>
        [Test]
        public void MapItem_WithZeroedAudioMetadata_WritesNeitherAttribute()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId) with
            {
                AudioChannels = 0,
                AudioSampleRate = 0,
                AudioCodec = string.Empty,
            };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);
            var resource = item.Resources[0];

            // Assert
            resource.AudioChannels.Should().BeNull(
                "because an unreadable track must leave the renderer free to find out for itself");
            resource.SampleFrequency.Should().BeNull(
                "because a zero sample rate is a failed probe, not a property of the file");
            item.AudioCodec.Should().BeNull(
                "because an empty codec name would serialise as an empty upnp:audioCodec element");
        }

        [Test]
        public void MapItem_WithNoVideoMetadata_LeavesTheOptionalAttributesUnset()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);
            var resource = item.Resources[0];

            // Assert
            resource.Duration.Should().BeNull(
                "because an absent attribute is correct here - a renderer handed an empty duration can "
                + "drop the whole res element");
            resource.Resolution.Should().BeNull("because there are no dimensions to report");
            resource.Bitrate.Should().BeNull("because there is no bitrate to report");
        }

        /// <summary>
        /// Half a resolution is worse than none, so a partial pair must not be written.
        /// </summary>
        [Test]
        public void MapItem_WithOnlyOneDimension_WritesNoResolution()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId) with { Width = 1920 };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, _directoryId.ToString(), subtitles: []);

            // Assert
            item.Resources[0].Resolution.Should().BeNull(
                "because '1920x' is malformed and some renderers reject the whole resource for it");
        }

        /// <summary>
        /// A byte such as 0x01 is legal in a POSIX filename, legal UTF-8, and illegal in XML 1.0. Left in
        /// a title it threw out of XmlWriter while the SOAP body was already being written, so no
        /// exception handler could turn it into a fault - and because the root listing carries
        /// recently-added files, one such file made the whole library unbrowsable.
        /// </summary>
        [Test]
        public void MapItem_WithAControlCharacterInTheTitle_DropsIt()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId) with { Title = "Ep01" };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, DidlMapper.RootObjectId, subtitles: []);

            // Assert
            item.Title.Should().Be("Ep01",
                "because a character XML cannot represent has to be dropped rather than escaped - it has "
                + "no XML representation at all");
        }

        [Test]
        public void MapContainer_WithAControlCharacterInTheName_DropsIt()
        {
            // Arrange
            var directory = CreateDirectory() with { Name = "Films" };

            // Act
            var container = DidlMapper.MapContainer(directory, Endpoint, DidlMapper.RootObjectId);

            // Assert
            container.Title.Should().Be("Films",
                "because a folder name comes off the same volume as a filename and is just as free to "
                + "hold a byte XML rejects");
        }

        [Test]
        public void MapItem_WithAnAstralCharacterInTheTitle_KeepsIt()
        {
            // Arrange - a surrogate pair is two chars, neither valid alone, and the pair is valid XML.
            var file = CreateFile(directoryPublicId: _directoryId) with { Title = "Party \U0001F389" };

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, DidlMapper.RootObjectId, subtitles: []);

            // Assert
            item.Title.Should().Be("Party \U0001F389",
                "because filtering must not mistake a valid surrogate pair for two invalid characters");
        }

        [Test]
        public void MapItem_WithAnOrdinaryTitle_ReturnsTheSameInstance()
        {
            // Arrange
            var file = CreateFile(directoryPublicId: _directoryId);

            // Act
            var item = DidlMapper.MapItem(file, Endpoint, DidlMapper.RootObjectId, subtitles: []);

            // Assert
            item.Title.Should().BeSameAs(file.Title,
                "because the common case must cost one scan and no allocation");
        }

        private static SubtitleFileDto CreateSubtitle(MediaFileDto file, string relativePath)
        {
            return new SubtitleFileDto
            {
                PublicId = Guid.NewGuid(),
                MediaFilePublicId = file.PublicId,
                MediaFileFullPath = file.FullPath,
                RelativePath = relativePath,
                Source = SubtitleSource.Automatic,
            };
        }

        private static MediaFileDto CreateFile(Guid? directoryPublicId)
        {
            return new MediaFileDto
            {
                PublicId = _fileId,
                FullPath = "/share/Media/Movies/Film.mkv",
                FileName = "Film.mkv",
                Title = "Film",
                Extension = ".mkv",
                DirectoryPublicId = directoryPublicId,
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsExcludedFromCache = false,
                HasSubtitleTracks = false,
                HasSubtitleFiles = false,
                ContentStamp = "1024:638000000000000000",
            };
        }

        private static MediaDirectoryDto CreateDirectory()
        {
            return new MediaDirectoryDto
            {
                PublicId = _directoryId,
                FullPath = "/share/Media/Movies",
                Name = "Movies",
                ParentDirectoryPublicId = null,
                Depth = 1,
                IsSourceRoot = true,
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };
        }
    }
}
