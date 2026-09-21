using System.ComponentModel.DataAnnotations;

namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Escape hatches for renderer-specific behaviour. Each flag exists because the reference implementation
    /// behaved one way and the corrected behaviour is a risk to devices validated against the old one.
    /// </summary>
    public sealed class CompatibilityOptions
    {
        /// <summary>
        /// Send <c>contentFeatures.dlna.org</c> and <c>transferMode.dlna.org</c> on media responses.
        /// The reference never sent them; strict renderers expect them.
        /// </summary>
        public bool SendDlnaResponseHeaders { get; set; } = true;

        /// <summary>
        /// Reproduce the reference's inverted SSDP M-SEARCH match, which answers for every service type
        /// except the one requested. Only for falling back if a device regresses on correct matching.
        /// </summary>
        public bool UseLegacyInvertedSearchTargetMatch { get; set; }

        /// <summary>
        /// Interval between SSDP alive announcements. The reference used 30 seconds against a
        /// <c>max-age</c> of 600, which is far more chatty than the spec suggests.
        /// </summary>
        [Range(1, 3600)]
        public int SsdpAliveIntervalInSeconds { get; set; } = 30;

        /// <summary>
        /// Also send SSDP announcements to the limited broadcast address, not just the multicast group.
        /// Non-standard, but the reference did it and some networks drop multicast.
        /// </summary>
        public bool AlsoNotifyBroadcastAddress { get; set; } = true;

        /// <summary>
        /// Maximum items returned for a single Browse request. A request for more is capped here;
        /// a request for zero means "all", bounded by this value.
        /// </summary>
        /// <remarks>
        /// Thirty-two, chosen to keep a serialised page off the large object heap. One DIDL-Lite item is
        /// ~1,100 characters escaped, measured from this project's own golden file, so a 100-item page is
        /// ~110,000 chars - and <c>StringWriter.ToString()</c> hands back a 220 KB string, 2.6x over the
        /// 85,000-byte threshold, which SoapCore then escapes into the envelope as a second one. That is
        /// ~400 KB of large-object allocation per full page, dead within milliseconds, with nothing
        /// compacting the LOH. Thirty-two items is ~70 KB, so both strings are gen0.
        /// <para>
        /// Nothing on the wire changes shape: renderers page anyway, and <c>RequestedCount=0</c> still
        /// means "the ceiling". A folder simply takes more round trips, each about three times cheaper.
        /// </para>
        /// </remarks>
        [Range(1, 10_000)]
        public int MaxBrowseRequestedCount { get; set; } = 32;
    }
}
