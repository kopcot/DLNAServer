using DlnaServer.Core.Gena;
using DlnaServer.Host.Upnp;
using DlnaServer.Host.Upnp.Discovery;
using DlnaServer.Upnp.Soap.AvTransport;
using DlnaServer.Upnp.Soap.ConnectionManager;
using DlnaServer.Upnp.Soap.ContentDirectory;
using DlnaServer.Upnp.Soap.MediaReceiverRegistrar;
using DlnaServer.Upnp.Ssdp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Pins the lifetimes the UPnP registrations had while they were written out inline in <c>Program.cs</c>.
    /// </summary>
    [TestFixture]
    internal sealed class UpnpServiceCollectionExtensionsTest
    {
        [TestCase(typeof(ILocalAddressProvider), ServiceLifetime.Singleton)]
        [TestCase(typeof(IUpnpDeviceRegistry), ServiceLifetime.Singleton)]
        [TestCase(typeof(ISubscriptionStore), ServiceLifetime.Singleton)]
        [TestCase(typeof(IContentDirectoryService), ServiceLifetime.Scoped)]
        [TestCase(typeof(IConnectionManagerService), ServiceLifetime.Scoped)]
        [TestCase(typeof(IAvTransportService), ServiceLifetime.Scoped)]
        [TestCase(typeof(IMediaReceiverRegistrarService), ServiceLifetime.Scoped)]
        public void AddDlnaUpnp_RegistersEachServiceWithItsLifetime(Type serviceType, ServiceLifetime lifetime)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.AddDlnaUpnp(port: 26_852);

            // Assert
            services.Should().ContainSingle(descriptor => descriptor.ServiceType == serviceType,
                    "because each UPnP service is registered exactly once")
                .Which.Lifetime.Should().Be(lifetime,
                    "because moving the registrations out of Program.cs must not change a lifetime - a SOAP "
                    + "service holds a scoped DbContext, and the registry and store are process-wide state");
        }

        [Test]
        public void AddDlnaUpnp_RegistersTheNotifierBeforeTheListener()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.AddDlnaUpnp(port: 26_852);

            // Assert
            services
                .Where(static descriptor => descriptor.ServiceType == typeof(IHostedService))
                .Select(static descriptor => descriptor.ImplementationType)
                .Should().Equal(
                    [typeof(SsdpNotifierHostedService), typeof(SsdpListenerHostedService)],
                    "because the two hosted services keep the order they had in Program.cs");
        }
    }
}
