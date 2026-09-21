using DlnaServer.Core.Dlna;
using DlnaServer.Upnp.Constants;

namespace DlnaServer.Host.Delivery
{
    /// <summary>
    /// Writes the DLNA headers a renderer reads off a media response.
    /// </summary>
    /// <remarks>
    /// The reference sends none of these, which is a gap rather than a decision: a renderer that trusts
    /// headers over the browse result has to guess whether it may seek or how the stream is meant to be
    /// consumed. They are behind <see cref="Core.Configuration.CompatibilityOptions.SendDlnaResponseHeaders"/>
    /// so a device that regresses can be put back on the reference's behaviour without a rebuild.
    /// </remarks>
    internal static class DlnaResponseHeaders
    {
        /// <summary>
        /// <b>transferMode.dlna.org</b><br />
        /// How the renderer intends to consume the response: streamed as it plays, fetched for immediate
        /// display, or pulled in the background.
        /// </summary>
        public const string TransferMode = "transferMode.dlna.org";

        /// <summary>
        /// <b>contentFeatures.dlna.org</b><br />
        /// Profile, seek support and flags for this resource - the same values its <c>res@protocolInfo</c>
        /// advertised when the renderer browsed to it.
        /// </summary>
        public const string ContentFeatures = "contentFeatures.dlna.org";

        /// <summary>
        /// <b>realTimeInfo.dlna.org</b><br />
        /// Always <c>DLNA.ORG_TLAG=*</c>: this server serves stored files, never a live stream with a
        /// time-based tag.
        /// </summary>
        public const string RealTimeInfo = "realTimeInfo.dlna.org";

        /// <summary>
        /// <b>transferMode.dlna.org: Streaming</b><br />
        /// Audio and video, consumed continuously while playing.
        /// </summary>
        public const string StreamingMode = "Streaming";

        /// <summary>
        /// <b>transferMode.dlna.org: Interactive</b><br />
        /// Images and thumbnails, fetched whole for display.
        /// </summary>
        public const string InteractiveMode = "Interactive";

        /// <summary>
        /// <b>transferMode.dlna.org: Background</b><br />
        /// A bulk transfer at low priority. Only ever echoed, never chosen by this server.
        /// </summary>
        public const string BackgroundMode = "Background";

        private const string RealTimeInfoValue = "DLNA.ORG_TLAG=*";

        /// <summary>
        /// Applies the three headers to a response that is about to serve <paramref name="media"/>.
        /// </summary>
        /// <remarks>
        /// <paramref name="contentFeatures"/> comes from
        /// <see cref="DlnaServer.Upnp.Constants.DlnaProtocolInfo.ContentFeaturesFor"/> or its thumbnail counterpart,
        /// so the header cannot drift from what the same resource advertised in DIDL-Lite.
        /// </remarks>
        public static void Apply(HttpRequest request, HttpResponse response, DlnaMedia media, string contentFeatures)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(contentFeatures);

            response.Headers[TransferMode] = ResolveTransferMode(request, media);
            response.Headers[ContentFeatures] = contentFeatures;
            response.Headers[RealTimeInfo] = RealTimeInfoValue;
        }

        /// <summary>
        /// The mode the renderer asked for, when it asked for one this server can honour.
        /// </summary>
        /// <remarks>
        /// DLNA has the server confirm the transfer mode by echoing it. An absent or unrecognised value
        /// falls back to the mode the content kind implies, which is what most renderers expect anyway.
        /// <para>
        /// That fallback comes from <see cref="DlnaProtocolInfo.IsStreamed"/> rather than from a switch of
        /// its own. It used to name <see cref="DlnaMedia.Audio"/> and <see cref="DlnaMedia.Video"/>
        /// explicitly, which is the same answer for every kind this server actually serves except
        /// <see cref="DlnaMedia.Subtitle"/> and <see cref="DlnaMedia.Unknown"/> - and for those two it
        /// contradicted the <c>DLNA.ORG_FLAGS</c> the very same resource advertised.
        /// </para>
        /// </remarks>
        private static string ResolveTransferMode(HttpRequest request, DlnaMedia media)
        {
            var requested = request.Headers[TransferMode].ToString();

            if (requested is StreamingMode or InteractiveMode or BackgroundMode)
            {
                return requested;
            }

            return DlnaProtocolInfo.IsStreamed(media) ? StreamingMode : InteractiveMode;
        }
    }
}
