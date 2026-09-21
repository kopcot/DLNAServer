using System.Net;
using DlnaServer.Host.Configuration;
using DlnaServer.Upnp.Ssdp;
using Microsoft.AspNetCore.HostFiltering;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Proves the wildcard is actually replaced, and that the two ways of locking an operator out are not
    /// reachable.
    /// </summary>
    /// <remarks>
    /// The gap is DNS rebinding: nothing in the server reads the <c>Host</c> header, so a rewritten one
    /// does not defeat port confinement, but it does defeat the browser's origin isolation - which is the
    /// only thing between a hostile page and an unauthenticated same-origin admin UI. The cross-site
    /// filter cannot help, because after rebinding the browser correctly reports <c>same-origin</c>.
    /// </remarks>
    [TestFixture]
    internal sealed class AllowedHostsDefaultsTest
    {
        [Test]
        public void Apply_ReplacesTheWildcardWithTheResolvedHosts()
        {
            // Arrange
            var options = new HostFilteringOptions { AllowedHosts = ["*"] };
            var addresses = new StubAddressProvider([IPAddress.Parse("192.168.1.50")]);

            // Act
            AllowedHostsDefaults.Apply(options, addresses);

            // Assert
            options.AllowedHosts.Should().NotContain("*",
                "because a wildcard accepts the rebound host name that the whole guard exists to refuse");
            options.AllowedHosts.Should().Contain("192.168.1.50",
                "because that is the address renderers and the operator's browser actually reach the server on");
            options.AllowedHosts.Should().Contain(Environment.MachineName,
                "because the machine's own name has to keep working when its address changes");
            options.AllowedHosts.Should().Contain("localhost",
                "because an operator on the NAS itself must not be locked out");
        }

        /// <summary>
        /// An operator who named hosts meant those hosts.
        /// </summary>
        /// <remarks>
        /// This is the documented escape hatch for the one case the generated list cannot cover - an
        /// address acquired after startup, or an interface with no default gateway, both of which
        /// <see cref="LocalAddressProvider"/> deliberately skips.
        /// </remarks>
        [Test]
        public void Apply_LeavesAnExplicitListAlone()
        {
            // Arrange
            var options = new HostFilteringOptions { AllowedHosts = ["nas.local", "10.0.0.7"] };
            var addresses = new StubAddressProvider([IPAddress.Parse("192.168.1.50")]);

            // Act
            AllowedHostsDefaults.Apply(options, addresses);

            // Assert
            options.AllowedHosts.Should().BeEquivalentTo(["nas.local", "10.0.0.7"],
                "because configuration that names hosts is the operator's decision, not a default to improve on");
        }

        /// <summary>
        /// A machine that can resolve no address of its own still gets the guard, narrowed to loopback
        /// and its own names.
        /// </summary>
        /// <remarks>
        /// <b>Rewritten from <c>..._KeepsTheWildcard</c>, which pinned the opposite.</b> That contract
        /// read "resolving nothing already means the server is undiscoverable, so narrowing would make it
        /// worse" - but an empty list does not mean unreachable. <c>GetBroadcastableAddresses</c> filters
        /// by default gateway rather than reachability and yields nothing at all on a
        /// <c>SocketException</c>, so a static IP on a gateway-less segment, or a start before the DHCP
        /// lease lands, hits this path on a perfectly reachable machine - and kept the wildcard, and the
        /// rebinding guard off, for the whole process lifetime.
        /// <para>
        /// Failing closed here cannot lock the operator out of the box itself: the set still holds
        /// loopback, the machine name and its mDNS name. What it does cost is reaching the server by LAN
        /// address until the next restart, and an explicit <c>AllowedHosts</c> list is the escape hatch.
        /// </para>
        /// </remarks>
        [Test]
        public void Apply_WithNoResolvableAddress_StillNarrowsToThisMachine()
        {
            // Arrange
            var options = new HostFilteringOptions { AllowedHosts = ["*"] };
            var addresses = new StubAddressProvider([]);

            // Act
            AllowedHostsDefaults.Apply(options, addresses);

            // Assert
            options.AllowedHosts.Should().NotContain("*",
                "because keeping the wildcard leaves the rebinding guard off on a machine that is "
                + "reachable but whose address lookup came back empty");
            options.AllowedHosts.Should().Contain("localhost",
                "because the safe set must never cut the machine off from itself");
            options.AllowedHosts.Should().Contain(Environment.MachineName,
                "because the machine's own name is how a browser on the LAN usually reaches it");
        }

        [Test]
        public void Apply_DeduplicatesCaseInsensitively()
        {
            // Arrange
            var options = new HostFilteringOptions { AllowedHosts = ["*"] };
            var addresses = new StubAddressProvider(
                [IPAddress.Parse("192.168.1.50"), IPAddress.Parse("192.168.1.50")]);

            // Act
            AllowedHostsDefaults.Apply(options, addresses);

            // Assert
            options.AllowedHosts.Should().OnlyHaveUniqueItems(
                "because host names are case-insensitive and the same address can be reported twice");
        }

        private sealed class StubAddressProvider : ILocalAddressProvider
        {
            private readonly IReadOnlyList<IPAddress> _addresses;

            public StubAddressProvider(IReadOnlyList<IPAddress> addresses)
            {
                _addresses = addresses;
            }

            public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
            {
                return _addresses;
            }
        }
    }
}
