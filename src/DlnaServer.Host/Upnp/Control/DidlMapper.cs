using System.Globalization;
using System.Text;
using System.Xml;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Subtitles;
using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Didl;
using DlnaServer.Upnp.Ssdp;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// Turns indexed files and directories into DIDL-Lite objects.
    /// </summary>
    /// <remarks>
    /// Resource URLs are built against the local endpoint the request arrived on, so a multi-homed
    /// server hands each renderer a URL reachable from the interface it is actually using.
    /// </remarks>
    internal static class DidlMapper
    {
        /// <summary>
        /// Identifier the UPnP root container is addressed by.
        /// </summary>
        public const string RootObjectId = "0";

        // A folder of forty language variants must not make every Browse of it forty resources longer.
        private const int MaxSubtitleResources = 8;

        /// <summary>
        /// Maps a directory, declaring <paramref name="parentId"/> as its parent.
        /// </summary>
        /// <remarks>
        /// The parent is supplied rather than derived because it depends on why the object is being
        /// returned. In a <c>BrowseDirectChildren</c> listing of container C every object must declare
        /// <c>parentID</c> = C, whatever its position in the tree; in a <c>BrowseMetadata</c> reply the
        /// object describes itself and declares its real parent. See <see cref="ParentIdOf(MediaFileDto)"/>.
        /// </remarks>
        public static DidlContainer MapContainer(MediaDirectoryDto directory, string endpoint, string parentId)
        {
            ArgumentNullException.ThrowIfNull(directory);

            return new DidlContainer
            {
                ObjectId = directory.PublicId.ToString(),
                ParentId = parentId,
                Class = DlnaItemClass.Container.ToUpnpClass(),
                Title = ToXmlText(directory.Name),
                Searchable = "1",
                AlbumArtUri = $"http://{endpoint}/icon/folder.jpg",
                Icon = $"http://{endpoint}/icon/folder.jpg",
            };
        }

        /// <summary>
        /// Maps a file, declaring <paramref name="parentId"/> as its parent.
        /// </summary>
        /// <remarks>
        /// Getting this wrong is what hid the library from a television. The root listing surfaces the
        /// most recently indexed files alongside the source folders, and those files were declaring the
        /// directory they physically live in - so a renderer browsing container <c>0</c> received objects
        /// claiming to belong somewhere else. VLC ignores that; an LG television showed the server and no
        /// content at all. The reference gets it right by construction: it stamps the root's own
        /// identifier on every object it returns from a root listing.
        /// </remarks>
        public static DidlItem MapItem(
            MediaFileDto file,
            string endpoint,
            string parentId,
            IReadOnlyList<SubtitleFileDto> subtitles)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(subtitles);

            var thumbnail = ResolveThumbnail(file, endpoint);

            return new DidlItem
            {
                ObjectId = file.PublicId.ToString(),
                ParentId = parentId,
                Class = file.UpnpClass.ToUpnpClass(),
                Title = ToXmlText(file.Title),
                Date = file.FileCreatedUtc.ToString("O", CultureInfo.InvariantCulture),
                Resources =
                [
                    new DidlResource
                    {
                        ProtocolInfo = DlnaProtocolInfo.ForResource(file.Mime, file.DlnaProfileName, file.Extension),
                        Size = file.SizeInBytes.ToString(CultureInfo.InvariantCulture),

                        // A renderer reads these three to show a running time and to drive its seek bar.
                        // Without duration most televisions show no length and either disable scrubbing
                        // or guess at it, and the reference emits all three - so their absence was a
                        // wire divergence, not a simplification.
                        Duration = file.Duration is { } duration
                            ? BrowseRequest.FormatDuration(duration)
                            : null,
                        Resolution = FormatResolution(file.Width, file.Height),
                        Bitrate = FormatBitrate(file.Bitrate),

                        // Both are named in the Browse filter an LG sends, alongside res@resolution and
                        // res@bitrate, and the reference emits both - so their absence was a wire
                        // divergence rather than a simplification. They were declared on DidlResource
                        // from the start and never assigned by anything.
                        AudioChannels = FormatCount(file.AudioChannels),
                        SampleFrequency = FormatCount(file.AudioSampleRate),
                        Url = $"http://{endpoint}/fileserver/file/{file.PublicId}",
                    },
                    .. MapSubtitleResources(subtitles, endpoint),
                ],
                VideoCodec = ToXmlTextOrNull(file.VideoCodec),
                AudioCodec = ToXmlTextOrNull(file.AudioCodec),
                ThumbnailResource = thumbnail is null
                    ? null
                    : new DidlResource
                    {
                        ProtocolInfo = DlnaProtocolInfo.ForThumbnail(
                            thumbnail.Mime,
                            thumbnail.DlnaProfileName),
                        Url = thumbnail.Url,
                    },
                AlbumArtUri = thumbnail?.Url,
                Icon = thumbnail?.Url,
                CaptionInfo = MapCaptionInfo(subtitles, endpoint),
            };
        }

        /// <summary>
        /// The endpoint that resource URLs are built against, for a request that arrived on
        /// <paramref name="connection"/>.
        /// </summary>
        /// <remarks>
        /// Resolved through the device registry rather than taken straight from the connection, for two
        /// reasons. It guarantees the URL uses an address the server actually advertised over SSDP, so a
        /// renderer is never handed one it cannot reach. And it avoids emitting a raw IPv6 address, which
        /// would need bracketing to be a valid URL - a request arriving on the IPv6 loopback otherwise
        /// produces <c>http://::1:26852/...</c>, which no client can parse. Never the <c>Host</c> header:
        /// that is whatever the caller chose to send.
        /// </remarks>
        public static string ResolveEndpoint(
            IUpnpDeviceRegistry devices,
            ConnectionInfo? connection,
            int fallbackPort)
        {
            ArgumentNullException.ThrowIfNull(devices);

            var port = connection?.LocalPort ?? fallbackPort;

            try
            {
                var identity = devices.Resolve(connection?.LocalIpAddress);

                return $"{identity.Address}:{port}";
            }
            catch (InvalidOperationException)
            {
                // No advertised interface - only reachable over loopback, so say so plainly.
                return $"127.0.0.1:{port}";
            }
        }

        /// <summary>
        /// Where a renderer fetches a linked subtitle.
        /// </summary>
        /// <remarks>
        /// Ends in the subtitle's own extension: some televisions decide whether a URL is a subtitle they
        /// can read by how it ends, and the file server ignores that part of the path.
        /// </remarks>
        public static string SubtitleUrl(string endpoint, SubtitleFileDto subtitle)
        {
            ArgumentNullException.ThrowIfNull(subtitle);

            var extension = Path.GetExtension(subtitle.RelativePath).TrimStart('.').ToLowerInvariant();

            return $"http://{endpoint}/fileserver/subtitle/{subtitle.PublicId}.{extension}";
        }

        /// <summary>
        /// The links whose type is still a configured subtitle type - the ones <c>GetSubtitle</c> will serve.
        /// </summary>
        /// <remarks>
        /// A type removed on Settings drops its links only at the next scan, while the file server refuses
        /// them at once. Filtering here keeps a television from being offered a subtitle that then 404s in
        /// between. Returns the list it was given when nothing is left out, which is the usual case.
        /// </remarks>
        public static IReadOnlyList<SubtitleFileDto> Offerable(
            IReadOnlyList<SubtitleFileDto> subtitles,
            IReadOnlyDictionary<string, DlnaMedia> subtitleTypes)
        {
            ArgumentNullException.ThrowIfNull(subtitles);
            ArgumentNullException.ThrowIfNull(subtitleTypes);

            for (var i = 0; i < subtitles.Count; i++)
            {
                if (!SubtitleMatcher.IsLinkablePath(subtitles[i].RelativePath, subtitleTypes))
                {
                    return subtitles.Where(s => SubtitleMatcher.IsLinkablePath(s.RelativePath, subtitleTypes)).ToArray();
                }
            }

            return subtitles;
        }

        /// <summary>
        /// The one subtitle a Samsung television is told about, which reads a single subtitle from
        /// <c>sec:CaptionInfoEx</c> or the <c>CaptionInfo.sec</c> header: the first SRT, the format it is
        /// surest to show, or else the first subtitle.
        /// </summary>
        public static SubtitleFileDto? PreferredCaption(IReadOnlyList<SubtitleFileDto> subtitles)
        {
            ArgumentNullException.ThrowIfNull(subtitles);

            SubtitleFileDto? chosen = null;

            foreach (var subtitle in subtitles)
            {
                if (subtitle.RelativePath.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                {
                    return subtitle;
                }

                chosen ??= subtitle;
            }

            return chosen;
        }

        /// <summary>
        /// The container an object actually belongs to, for a reply that describes the object itself.
        /// </summary>
        public static string ParentIdOf(MediaFileDto file)
        {
            ArgumentNullException.ThrowIfNull(file);

            return file.DirectoryPublicId?.ToString() ?? RootObjectId;
        }

        /// <inheritdoc cref="ParentIdOf(MediaFileDto)"/>
        public static string ParentIdOf(MediaDirectoryDto directory)
        {
            ArgumentNullException.ThrowIfNull(directory);

            return directory.ParentDirectoryPublicId?.ToString() ?? RootObjectId;
        }

        /// <summary>
        /// Preview image for an item, falling back to a generic icon when none has been generated.
        /// </summary>
        /// <remarks>
        /// An image with no thumbnail yet points at the image itself, which is what the reference does -
        /// a renderer showing a full-size photo as its own preview is better than showing nothing.
        /// </remarks>
        private static ThumbnailReference? ResolveThumbnail(MediaFileDto file, string endpoint)
        {
            // Generated previews are written as .jpg, so this arm and the two icon arms are genuinely
            // JPEG and carry the thumbnail profile.
            if (file.ThumbnailPublicId is Guid thumbnailId)
            {
                return new ThumbnailReference(
                    $"http://{endpoint}/fileserver/thumbnail/{thumbnailId}",
                    DlnaMime.ImageJpeg,
                    DlnaProtocolInfo.ThumbnailProfileName);
            }

            return file.Mime.ToMedia() switch
            {
                // The image itself, which is whatever it is - a PNG, a GIF, a BMP. This used to be
                // advertised as image/jpeg with DLNA.ORG_PN=JPEG_TN regardless, so a renderer was told
                // one type and handed another. A null profile lets ForThumbnail resolve the right one
                // from the MIME rather than asserting a JPEG profile over it.
                DlnaMedia.Image => new ThumbnailReference(
                    $"http://{endpoint}/fileserver/file/{file.PublicId}",
                    file.Mime,
                    DlnaProfileName: null),
                DlnaMedia.Video => new ThumbnailReference(
                    $"http://{endpoint}/icon/fileMovie.jpg",
                    DlnaMime.ImageJpeg,
                    DlnaProtocolInfo.ThumbnailProfileName),
                DlnaMedia.Audio => new ThumbnailReference(
                    $"http://{endpoint}/icon/fileAudio.jpg",
                    DlnaMime.ImageJpeg,
                    DlnaProtocolInfo.ThumbnailProfileName),
                _ => null,
            };
        }

        /// <summary>
        /// A preview URL together with what actually answers at it.
        /// </summary>
        private sealed record ThumbnailReference(string Url, DlnaMime Mime, string? DlnaProfileName);

        // Both dimensions or neither: a renderer handed "1920x" reads it as malformed and some drop the
        // whole res element rather than just the attribute.
        private static string? FormatResolution(int? width, int? height)
        {
            if (width is not > 0 || height is not > 0)
            {
                return null;
            }

            return string.Create(CultureInfo.InvariantCulture, $"{width.Value}x{height.Value}");
        }

        // DIDL-Lite defines res@bitrate in BYTES per second while containers report bits, so this is the
        // one place the eight matters. Getting it wrong by that factor is what makes a renderer decide the
        // network cannot keep up and refuse to start a stream.
        private static string? FormatBitrate(long? bitsPerSecond)
        {
            if (bitsPerSecond is not > 0)
            {
                return null;
            }

            return (bitsPerSecond.Value / 8).ToString(CultureInfo.InvariantCulture);
        }

        /// <remarks>
        /// Zero is treated as absent, not as a value. ffprobe reports 0 channels or a 0 sample rate for a
        /// track it could not read properly, and <c>nrAudioChannels="0"</c> tells a renderer the file has
        /// no audio - which is worse than telling it nothing, because omitting the attribute leaves it
        /// free to find out for itself when it opens the stream.
        /// </remarks>
        private static string? FormatCount(int? value)
        {
            if (value is not > 0)
            {
                return null;
            }

            return value.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Text safe to put in a DIDL-Lite element, with characters XML cannot represent removed.
        /// </summary>
        /// <remarks>
        /// Titles come off a volume anyone can write to, and a byte such as <c>0x01</c> is legal in a
        /// POSIX filename, legal UTF-8, and illegal in XML 1.0. <see cref="XmlWriter"/> throws on one -
        /// and because the SOAP <c>Result</c> is a property getter invoked while the response body is
        /// already being written, the status and headers are on the wire before it does, so no exception
        /// handler can turn it into a fault: the renderer gets a torn connection. One such file made
        /// every Browse of its folder fail, and since the root listing carries recently-added files it
        /// took the whole library with it.
        /// <para>
        /// Escaping is not an option - these characters have no XML representation - and nor is turning
        /// <see cref="XmlWriterSettings.CheckCharacters"/> off, which would emit bytes a renderer's parser
        /// rejects instead. Dropping them is the only thing that leaves a document on the wire.
        /// </para>
        /// </remarks>
        private static string ToXmlText(string value)
        {
            if (IsXmlSafe(value))
            {
                return value;
            }

            var kept = new StringBuilder(value.Length);

            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]) && char.IsSurrogatePair(value, index))
                {
                    _ = kept.Append(value[index]).Append(value[index + 1]);
                    index++;
                    continue;
                }

                if (XmlConvert.IsXmlChar(value[index]))
                {
                    _ = kept.Append(value[index]);
                }
            }

            return kept.ToString();
        }

        /// <remarks>
        /// A codec name reaches us from ffprobe rather than from a filename, so it is far less likely to
        /// carry a character XML cannot hold - but it is still external input written into the same
        /// document, and the failure mode is the whole Browse response tearing mid-write.
        /// </remarks>
        private static string? ToXmlTextOrNull(string? value)
        {
            return string.IsNullOrEmpty(value)
                ? null
                : ToXmlText(value);
        }

        /// <remarks>
        /// Separate so the overwhelmingly common case - a title that is already valid - costs one scan
        /// and no allocation.
        /// </remarks>
        private static bool IsXmlSafe(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]) && char.IsSurrogatePair(value, index))
                {
                    index++;
                    continue;
                }

                if (!XmlConvert.IsXmlChar(value[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<DidlResource> MapSubtitleResources(
            IReadOnlyList<SubtitleFileDto> subtitles,
            string endpoint)
        {
            var mapped = 0;

            foreach (var subtitle in subtitles)
            {
                if (mapped == MaxSubtitleResources)
                {
                    yield break;
                }

                if (DlnaMimeCatalog.TryGetByFileExtension(Path.GetExtension(subtitle.RelativePath), out var mime))
                {
                    mapped++;

                    yield return new DidlResource
                    {
                        ProtocolInfo = DlnaProtocolInfo.ForSubtitle(mime),
                        Url = SubtitleUrl(endpoint, subtitle),
                    };
                }
            }
        }

        private static DidlCaptionInfo? MapCaptionInfo(IReadOnlyList<SubtitleFileDto> subtitles, string endpoint)
        {
            var chosen = PreferredCaption(subtitles);

            return chosen is null
                ? null
                : new DidlCaptionInfo
                {
                    Type = Path.GetExtension(chosen.RelativePath).TrimStart('.').ToLowerInvariant(),
                    Url = SubtitleUrl(endpoint, chosen),
                };
        }
    }
}
