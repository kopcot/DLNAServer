using System.Diagnostics.CodeAnalysis;

namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// The bits of <c>DLNA.ORG_FLAGS</c>, the field in <c>res@protocolInfo</c> that tells a renderer what
    /// it may do with a resource.
    /// </summary>
    /// <remarks>
    /// Named bits rather than a hard-coded string, so what is being advertised - and why - is readable at
    /// the point it is decided. The wire form is the first eight hex digits followed by 24 zeros, which is
    /// what <see cref="DlnaProtocolInfo.Format(DlnaOrgFlags)"/> produces.
    /// <para>
    /// Bit numbers below are counted from the most significant bit of the first byte, which is how the DLNA
    /// guidelines number them; the shift is the same bit expressed the way C# addresses it. Only bits 0-11
    /// are defined - the rest are reserved.
    /// </para>
    /// </remarks>
    [Flags]
    [SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "DLNA.ORG_FLAGS is the name of the field this type represents, and every "
            + "renderer and every DLNA document calls it that. Renaming it away from the wire name "
            + "would make the type harder to match against the specification, which is the only "
            + "reason it exists.")]
    public enum DlnaOrgFlags : ulong
    {
        None = 0,

        /// <summary>
        /// The server controls the pace at which it sends, rather than the renderer pulling as fast as it
        /// can. Not advertised here: this server streams over plain HTTP and lets the client set the pace.
        /// </summary>
        SenderPaced = 1UL << 31,

        /// <summary>
        /// The server can start from a given time position, so a renderer may scrub to a timestamp.
        /// </summary>
        TimeSeekOperation = 1UL << 30,

        /// <summary>
        /// The server can serve a requested byte range, which is what makes seeking and resuming work over
        /// HTTP. Advertised for everything this server serves.
        /// </summary>
        ByteSeekOperation = 1UL << 29,

        /// <summary>
        /// The server can play a whole container - a playlist or an album - as one resource. Not supported.
        /// </summary>
        PlayContainer = 1UL << 28,

        /// <summary>
        /// The renderer may increase play speed from the start of the resource, as in fast-forward.
        /// </summary>
        S0Increase = 1UL << 27,

        /// <summary>
        /// The renderer may increase play speed from an arbitrary point in the resource.
        /// </summary>
        SNIncrease = 1UL << 26,

        /// <summary>
        /// The server honours RTSP pause. Not supported - this server speaks HTTP only.
        /// </summary>
        RtspPause = 1UL << 25,

        /// <summary>
        /// The resource may be delivered as a live stream, played while it arrives. Advertised for audio
        /// and video, and deliberately not for images - there is nothing to stream in a photograph.
        /// </summary>
        StreamingTransferMode = 1UL << 24,

        /// <summary>
        /// The renderer may fetch the parts it wants when it wants them, rather than being streamed at.
        /// This is what lets a television ask for one thumbnail out of a folder.
        /// </summary>
        InteractiveTransferMode = 1UL << 23,

        /// <summary>
        /// The resource may be downloaded in the background for playing later.
        /// </summary>
        BackgroundTransferMode = 1UL << 22,

        /// <summary>
        /// The server tolerates a transfer that stalls - a renderer pausing mid-file and resuming - rather
        /// than treating the gap as a dropped connection.
        /// </summary>
        ConnectionStall = 1UL << 21,

        /// <summary>
        /// The resource follows the DLNA 1.5 guidelines. Renderers use this to pick which rules to apply.
        /// </summary>
        DlnaV15 = 1UL << 20,
    }
}
