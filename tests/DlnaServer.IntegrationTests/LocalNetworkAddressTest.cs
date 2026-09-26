using System.Net;
using DlnaServer.Host;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Pins the one definition of "local network" that the <c>/manage</c> filter, the admin port and the
    /// SSDP listener all share.
    /// </summary>
    [TestFixture]
    internal sealed class LocalNetworkAddressTest
    {
        [TestCase("127.0.0.1")]
        [TestCase("::1")]
        [TestCase("10.1.2.3")]
        [TestCase("172.16.0.1")]
        [TestCase("172.31.255.255")]
        [TestCase("192.168.1.50")]
        [TestCase("169.254.10.20")]
        [TestCase("100.64.0.1")]
        [TestCase("100.127.255.255")]
        [TestCase("fe80::1")]
        [TestCase("fd12:3456:789a::1")]
        [TestCase("::ffff:192.168.1.50")]
        public void IsLocal_ForAPrivateOrLoopbackAddress_IsTrue(string address)
        {
            // Arrange
            var candidate = IPAddress.Parse(address);

            // Act
            var isLocal = LocalNetworkAddress.IsLocal(candidate);

            // Assert
            isLocal.Should().BeTrue(
                "because loopback, the private and link-local ranges and the carrier-grade NAT range "
                + "Tailscale uses are all the local network");
        }

        [TestCase("8.8.8.8")]
        [TestCase("172.32.0.1")]
        [TestCase("100.128.0.1")]
        [TestCase("192.169.1.1")]
        [TestCase("2001:db8::1")]
        [TestCase("::ffff:8.8.8.8")]
        public void IsLocal_ForAPublicAddress_IsFalse(string address)
        {
            // Arrange
            var candidate = IPAddress.Parse(address);

            // Act
            var isLocal = LocalNetworkAddress.IsLocal(candidate);

            // Assert
            isLocal.Should().BeFalse(
                "because an address outside every private range is the internet, and the guards that ask "
                + "exist to refuse it");
        }

        [Test]
        public void IsLocal_WithNoAddress_IsTrue()
        {
            // Arrange
            // Act
            var isLocal = LocalNetworkAddress.IsLocal(address: null);

            // Assert
            isLocal.Should().BeTrue(
                "because no address means no socket behind the request, which is an in-process caller");
        }

        [TestCase("2001:db8:1:2::50", "2001:db8:1:2::1", true)]
        [TestCase("2001:db8:1:3::50", "2001:db8:1:2::1", false)]
        [TestCase("2a00:1450::1", "2001:db8:1:2::1", false)]
        [TestCase("192.168.1.50", "192.168.1.1", false)]
        [TestCase("::ffff:192.168.1.50", "::ffff:192.168.1.1", false)]
        public void IsSameIPv6Link_ComparesTheSixtyFourBitPrefix(string remote, string local, bool expected)
        {
            // Arrange
            var remoteAddress = IPAddress.Parse(remote);
            var localAddress = IPAddress.Parse(local);

            // Act
            var isSameLink = LocalNetworkAddress.IsSameIPv6Link(remote: remoteAddress, local: localAddress);

            // Assert
            isSameLink.Should().Be(expected,
                "because only a global IPv6 peer inside the server's own /64 is on its link, and IPv4 is "
                + "answered by the private ranges instead");
        }
    }
}
