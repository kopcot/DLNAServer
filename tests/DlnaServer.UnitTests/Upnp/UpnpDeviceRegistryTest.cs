using System.Net;
using DlnaServer.Upnp.Ssdp;

namespace DlnaServer.UnitTests.Upnp
{
    /// <summary>
    /// Covers the registry that every Browse asks which address to put in its URLs.
    /// </summary>
    [TestFixture]
    internal sealed class UpnpDeviceRegistryTest
    {
        private static readonly IPAddress _lanAddress = IPAddress.Parse("192.168.1.50");

        /// <summary>
        /// <c>DidlMapper.ResolveEndpoint</c> catches <see cref="InvalidOperationException"/> and nothing
        /// else, so any other exception out of <c>Resolve</c> is a 500 on a Browse.
        /// </summary>
        /// <remarks>
        /// A race, so a stress loop rather than an exact interleaving: <c>Resolve</c> read the volatile
        /// list once for its count and again for its first entry, and an interface going away between
        /// the two reads threw <see cref="ArgumentOutOfRangeException"/>.
        /// </remarks>
        [Test]
        public async Task Resolve_WhileRefreshEmptiesTheList_ThrowsOnlyTheExceptionCallersCatch()
        {
            // Arrange
            var registry = new UpnpDeviceRegistry(
                addresses: new AlternatingAddressProvider(_lanAddress),
                machineName: "nas",
                port: 26_852);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            Exception? unexpected = null;

            var refresher = Task.Run(
                () =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        _ = registry.Refresh();
                    }
                },
                CancellationToken.None);

            // Act
            var resolver = Task.Run(
                () =>
                {
                    while (!stop.IsCancellationRequested && unexpected is null)
                    {
                        try
                        {
                            _ = registry.Resolve(localAddress: null);
                        }
                        catch (InvalidOperationException)
                        {
                            // The documented answer for "no interface", which callers handle.
                        }
                        catch (Exception exception)
                        {
                            unexpected = exception;
                        }
                    }
                },
                CancellationToken.None);

            await Task.WhenAll(refresher, resolver);

            // Assert
            unexpected.Should().BeNull(
                "because the list must be read once per call, so a concurrent refresh can only ever produce "
                + "the old answer, the new one, or the InvalidOperationException callers already catch");
        }

        [Test]
        public void Resolve_WithAnUnknownAddress_FallsBackToTheFirstIdentity()
        {
            // Arrange
            var registry = new UpnpDeviceRegistry(
                addresses: new FixedAddressProvider(_lanAddress),
                machineName: "nas",
                port: 26_852);

            // Act
            var identity = registry.Resolve(localAddress: IPAddress.Parse("10.0.0.9"));

            // Assert
            identity.Address.Should().Be(_lanAddress,
                "because an address the server does not own is answered with its first interface");
        }

        private sealed class FixedAddressProvider : ILocalAddressProvider
        {
            private readonly IPAddress[] _addresses;

            public FixedAddressProvider(IPAddress address)
            {
                _addresses = [address];
            }

            public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
            {
                return _addresses;
            }
        }

        /// <summary>
        /// Answers one address and then none, alternately - an interface bouncing on every refresh.
        /// </summary>
        private sealed class AlternatingAddressProvider : ILocalAddressProvider
        {
            private readonly IPAddress[] _addresses;
            private int _calls;

            public AlternatingAddressProvider(IPAddress address)
            {
                _addresses = [address];
            }

            public IReadOnlyList<IPAddress> GetBroadcastableAddresses()
            {
                return Interlocked.Increment(ref _calls) % 2 == 0
                    ? []
                    : _addresses;
            }
        }
    }
}
