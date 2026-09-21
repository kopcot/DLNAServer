using System.Net;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.HostFiltering;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Replaces the wildcard <c>AllowedHosts</c> with the names and addresses this machine actually
    /// answers on, so a rewritten <c>Host</c> header cannot pass as a local one.
    /// </summary>
    /// <remarks>
    /// This is the DNS-rebinding guard. Nothing in the server reads <see cref="HttpRequest.Host"/> - every
    /// confinement control keys on the connection's local port instead - so a rewritten host does not
    /// bypass port confinement, but it does defeat the browser's origin isolation, which is the only thing
    /// separating a hostile page from an unauthenticated same-origin admin UI. Given a wildcard, a page
    /// that resolves its own name to the NAS reads the configuration, enumerates every indexed path,
    /// rewrites <c>config.json</c> through the Settings page, and reads any media file's bytes.
    /// <see cref="RejectCrossSiteEndpointFilter"/> does not help there: after rebinding the browser
    /// reports <c>same-origin</c>, because as far as it knows it is.
    /// <para>
    /// Rebinding needs a hostname, so literal addresses plus this machine's own name is a complete
    /// answer. The list is resolved once per host build rather than hard-coded, so a deployment carries
    /// no addresses in a file - the cost is that an address the server acquires *later* is not in it,
    /// and a browser reaching the box on that address gets a 400 until the next restart. The machine name
    /// and loopback keep working throughout. A machine that resolves no address of its own <b>still gets
    /// the guard</b>, narrowed to loopback and its own names, rather than keeping the wildcard: an empty
    /// list means the address lookup came back empty, not that the box is unreachable.
    /// </para>
    /// </remarks>
    internal static class AllowedHostsDefaults
    {
        private const string Wildcard = "*";

        public static void Apply(HostFilteringOptions options, ILocalAddressProvider addresses)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(addresses);

            // An operator who named hosts in appsettings.json meant those hosts. Only the wildcard - the
            // framework's own default, and what ships here - is replaced.
            if (options.AllowedHosts is { Count: > 0 } configured
                && !configured.Contains(Wildcard, StringComparer.Ordinal))
            {
                return;
            }

            var resolved = addresses.GetBroadcastableAddresses();

            // Deliberately NOT an early return when nothing resolved. "No address" is not "unreachable":
            // GetBroadcastableAddresses filters by default gateway rather than by reachability, and its
            // fallback yields nothing at all on a SocketException - so a static IP on a gateway-less
            // segment, or a service that started before the DHCP lease landed, produced an empty list on
            // a perfectly reachable machine and kept the wildcard for the whole process lifetime, with
            // the rebinding guard silently off. Failing closed to the base set below cannot cut the
            // machine off from itself, because that set is loopback plus this machine's own names.
            //
            // The cost is real and bounded: with nothing resolved, a browser reaching the box by LAN
            // address gets a 400 until the next restart. An explicit AllowedHosts list in
            // appsettings.json is the escape hatch, and it is honoured above.

            // Host names are case-insensitive, and the machine name can repeat what DNS resolved.
            var hosts = new HashSet<string>(resolved.Count + 5, StringComparer.OrdinalIgnoreCase)
            {
                "localhost",
                IPAddress.Loopback.ToString(),
                $"[{IPAddress.IPv6Loopback}]",
                Environment.MachineName,

                // How the NAS answers to mDNS, which is how a browser on the LAN usually reaches it.
                $"{Environment.MachineName}.local",
            };

            foreach (var address in resolved)
            {
                _ = hosts.Add(address.ToString());
            }

            options.AllowedHosts = [.. hosts];
        }
    }
}
