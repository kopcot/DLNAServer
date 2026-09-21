namespace DlnaServer.Upnp.Ssdp
{
    /// <summary>
    /// The device identities this server advertises, one per usable network interface.
    /// </summary>
    public interface IUpnpDeviceRegistry
    {
        /// <summary>
        /// Every identity, as of the last resolution.
        /// </summary>
        IReadOnlyList<UpnpDeviceIdentity> Identities { get; }

        /// <summary>
        /// Re-reads the local interfaces, returning whether the advertised set changed.
        /// </summary>
        /// <remarks>
        /// Resolving once at construction was wrong for the two cases that matter on a NAS. A service
        /// that starts before DHCP finishes finds no interface at all, and the notifier then returned
        /// from <c>ExecuteAsync</c> - so nothing was advertised for the life of the process, and every
        /// <c>res@url</c> fell back to loopback. A lease that changes later is the same defect from the
        /// other end: the notifier binds a stale address, gets a <c>SocketException</c> every interval,
        /// and the server is silently unreachable while looking healthy.
        /// </remarks>
        bool Refresh();

        /// <summary>
        /// The identity matching a local address, or the first one when the address is unknown.
        /// </summary>
        /// <remarks>
        /// Used when answering an HTTP request so the URLs handed back point at the interface the
        /// renderer actually reached the server on.
        /// </remarks>
        UpnpDeviceIdentity Resolve(System.Net.IPAddress? localAddress);
    }
}
