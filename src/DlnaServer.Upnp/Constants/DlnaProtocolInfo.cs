using System.Globalization;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// Builds the <c>res@protocolInfo</c> string a renderer uses to decide whether it can play a resource.
    /// </summary>
    /// <remarks>
    /// Every value and format here is reproduced from the reference exactly. This string is matched
    /// literally by renderers, and getting a field wrong is the difference between a file playing and a
    /// TV refusing it with no explanation.
    /// </remarks>
    public static class DlnaProtocolInfo
    {
        /// <summary>
        /// <b>DLNA.ORG_PN</b><br />
        /// Profile every generated thumbnail is advertised under, in DIDL-Lite and in the
        /// <c>contentFeatures.dlna.org</c> header alike.
        /// </summary>
        public const string ThumbnailProfileName = "JPEG_TN";

        /// <summary>
        /// The 24 zeros that follow the eight significant hex digits of <c>DLNA.ORG_FLAGS</c>.
        /// </summary>
        /// <remarks>
        /// The field is 32 characters wide and only the first byte-and-a-half carries anything; the rest
        /// is reserved and always sent as zeros.
        /// </remarks>
        private const string ReservedFlagDigits = "000000000000000000000000";

        /// <summary>
        /// <b>DLNA.ORG_OP</b> for everything this server serves.
        /// </summary>
        /// <remarks>
        /// Time-seek only, which renders as <c>01</c>. The reference advertises exactly this even though
        /// its media endpoint serves HTTP byte ranges, so <see cref="DlnaOrgOperation.ByteSeekSupported"/>
        /// is deliberately under-declared: the devices in use were validated against this value, and
        /// adding byte-seek changes how a renderer scrubs. That is why the flag is named here and left
        /// out rather than simply forgotten.
        /// </remarks>
        public static readonly string OperationTimeSeekOnly = Format(DlnaOrgOperation.TimeSeekSupported);

        /// <summary>
        /// <b>DLNA.ORG_CI</b> for the media itself.
        /// </summary>
        public static readonly string ContentIndexNone = Format(DlnaOrgContentIndex.NoSpecificIndex);

        /// <summary>
        /// <b>DLNA.ORG_CI</b> marking a resource as a thumbnail rather than the item.
        /// </summary>
        public static readonly string ContentIndexThumbnail = Format(DlnaOrgContentIndex.Thumbnail);

        /// <summary>
        /// <b>DLNA.ORG_FLAGS</b> for audio and video: it may be streamed while it arrives.
        /// </summary>
        /// <remarks>
        /// Resolves to <c>21F00000</c> plus the reserved zeros, which is byte for byte what the reference
        /// sends. Spelling the bits out is the point - the hexadecimal said nothing about which
        /// capabilities were being claimed, and a wrong bit here is a television refusing a file with no
        /// explanation.
        /// </remarks>
        public static readonly string FlagsStreaming = Format(
            DlnaOrgFlags.ByteSeekOperation
            | DlnaOrgFlags.StreamingTransferMode
            | DlnaOrgFlags.InteractiveTransferMode
            | DlnaOrgFlags.BackgroundTransferMode
            | DlnaOrgFlags.ConnectionStall
            | DlnaOrgFlags.DlnaV15);

        /// <summary>
        /// <b>DLNA.ORG_FLAGS</b> for images: fetched on demand, never streamed.
        /// </summary>
        /// <remarks>
        /// <see cref="FlagsStreaming"/> without <see cref="DlnaOrgFlags.StreamingTransferMode"/>, which
        /// resolves to <c>20F00000</c>. There is nothing to stream in a photograph.
        /// </remarks>
        public static readonly string FlagsInteractive = Format(
            DlnaOrgFlags.ByteSeekOperation
            | DlnaOrgFlags.InteractiveTransferMode
            | DlnaOrgFlags.BackgroundTransferMode
            | DlnaOrgFlags.ConnectionStall
            | DlnaOrgFlags.DlnaV15);

        /// <summary>
        /// Whether content of this kind may be consumed as it arrives rather than fetched whole.
        /// </summary>
        /// <remarks>
        /// The single decision behind two wire fields: it picks between <see cref="FlagsStreaming"/> and
        /// <see cref="FlagsInteractive"/> for <c>DLNA.ORG_FLAGS</c>, and it is what
        /// <c>DlnaResponseHeaders</c> answers <c>transferMode.dlna.org</c> with. Those were separate
        /// switches keyed off different sets of members, and they disagreed for <see cref="DlnaMedia.Subtitle"/>
        /// and <see cref="DlnaMedia.Unknown"/> - flags claiming the streaming bit while the header said
        /// <c>Interactive</c>. A renderer compares the header against the <c>protocolInfo</c> it browsed
        /// with, so that is a refusal to play with no diagnostic.
        /// <para>
        /// Everything but an image, which is the reference's own test
        /// (<c>BrowseItemMapper.GetResourceProtocolInfo</c>) and not to be widened without it: the flag
        /// values here are matched literally by devices that were validated against them.
        /// </para>
        /// </remarks>
        public static bool IsStreamed(DlnaMedia media)
        {
            return media is not DlnaMedia.Image;
        }

        /// <summary>
        /// Renders <c>DLNA.ORG_FLAGS</c>: eight hex digits, then the reserved zeros.
        /// </summary>
        public static string Format(DlnaOrgFlags flags)
        {
            return ((ulong)flags).ToString("X8", CultureInfo.InvariantCulture) + ReservedFlagDigits;
        }

        /// <summary>
        /// Renders <c>DLNA.ORG_OP</c>: two binary digits, byte-seek first then time-seek.
        /// </summary>
        public static string Format(DlnaOrgOperation operation)
        {
            return ((short)operation).ToString("B2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Renders <c>DLNA.ORG_CI</c>: a single decimal digit.
        /// </summary>
        public static string Format(DlnaOrgContentIndex contentIndex)
        {
            return ((short)contentIndex).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds the full <c>protocolInfo</c> value for a playable resource.
        /// </summary>
        /// <remarks>
        /// Shape: <c>http-get:*:&lt;mime&gt;:DLNA.ORG_PN=&lt;profile&gt;;DLNA.ORG_OP=01;DLNA.ORG_CI=0;DLNA.ORG_FLAGS=&lt;flags&gt;</c>.
        /// When no profile is configured the reference falls back to the upper-cased extension without
        /// its dot, so that behaviour is kept.
        /// </remarks>
        public static string ForResource(DlnaMime mime, string? dlnaProfileName, string fileExtension)
        {
            ArgumentNullException.ThrowIfNull(fileExtension);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"http-get:*:{mime.ToMimeString()}:{ContentFeaturesFor(mime, dlnaProfileName, fileExtension)}");
        }

        /// <summary>
        /// <b>contentFeatures.dlna.org</b><br />
        /// Builds the response-header form of the same feature list a resource advertises in
        /// <c>res@protocolInfo</c>, which is that string without its leading transport and MIME fields.
        /// </summary>
        /// <remarks>
        /// Deliberately shares its construction with <see cref="ForResource"/>: a renderer compares the
        /// header against the <c>protocolInfo</c> it was given when browsing, and a mismatch between the
        /// two - a differing seek or flags field - is a refusal to play with no diagnostic.
        /// </remarks>
        public static string ContentFeaturesFor(DlnaMime mime, string? dlnaProfileName, string fileExtension)
        {
            ArgumentNullException.ThrowIfNull(fileExtension);

            var profile = ResolveProfile(mime, dlnaProfileName, fileExtension);

            var flags = IsStreamed(mime.ToMedia())
                ? FlagsStreaming
                : FlagsInteractive;

            return string.Create(
                CultureInfo.InvariantCulture,
                $"DLNA.ORG_PN={profile};DLNA.ORG_OP={OperationTimeSeekOnly};DLNA.ORG_CI={ContentIndexNone};DLNA.ORG_FLAGS={flags}");
        }

        /// <summary>
        /// Builds the <c>protocolInfo</c> value for a thumbnail resource.
        /// </summary>
        /// <remarks>
        /// Thumbnails advertise no seek support and always use the interactive flags.
        /// </remarks>
        public static string ForThumbnail(DlnaMime mime, string? dlnaProfileName)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"http-get:*:{mime.ToMimeString()}:{ContentFeaturesForThumbnail(mime, dlnaProfileName)}");
        }

        /// <summary>
        /// Builds the <c>protocolInfo</c> value for a subtitle or lyrics resource.
        /// </summary>
        /// <remarks>
        /// The fourth field is a bare <c>*</c>: a subtitle has no DLNA profile, and renderers that read
        /// subtitles from DIDL-Lite match on the MIME alone.
        /// </remarks>
        public static string ForSubtitle(DlnaMime mime)
        {
            return string.Create(CultureInfo.InvariantCulture, $"http-get:*:{mime.ToMimeString()}:*");
        }

        /// <summary>
        /// <b>contentFeatures.dlna.org</b><br />
        /// The header form of <see cref="ForThumbnail"/>, carrying the thumbnail content-index marker.
        /// </summary>
        public static string ContentFeaturesForThumbnail(DlnaMime mime, string? dlnaProfileName)
        {
            var profile = dlnaProfileName ?? mime.ToMainProfileName() ?? string.Empty;

            return string.Create(
                CultureInfo.InvariantCulture,
                $"DLNA.ORG_PN={profile};DLNA.ORG_OP=00;DLNA.ORG_CI={ContentIndexThumbnail};DLNA.ORG_FLAGS={FlagsInteractive}");
        }

        /// <summary>
        /// <b>SourceProtocolInfo</b><br />
        /// Builds the comma-separated list ConnectionManager's <c>GetProtocolInfo</c> reports as
        /// <c>Source</c>: what this server can serve, one entry per distinct MIME type.
        /// </summary>
        /// <remarks>
        /// The fourth field is a wildcard rather than a <c>DLNA.ORG_PN</c> profile. A renderer matches an
        /// item's <c>res@protocolInfo</c> against these entries, so listing literal profiles would
        /// require an entry per profile and reject anything not enumerated - where <c>*</c> says "any
        /// profile of this MIME type", which is what is actually true.
        /// </remarks>
        public static string BuildSourceList(IEnumerable<DlnaMime> mimes)
        {
            ArgumentNullException.ThrowIfNull(mimes);

            var entries = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var mime in mimes)
            {
                _ = entries.Add($"http-get:*:{mime.ToMimeString()}:*");
            }

            return string.Join(',', entries);
        }

        private static string ResolveProfile(DlnaMime mime, string? dlnaProfileName, string fileExtension)
        {
            if (!string.IsNullOrWhiteSpace(dlnaProfileName))
            {
                return dlnaProfileName;
            }

            if (mime.ToMainProfileName() is string fromCatalog)
            {
                return fromCatalog;
            }

            return fileExtension.TrimStart('.').ToUpperInvariant();
        }
    }
}
