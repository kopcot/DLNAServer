using DlnaServer.Core.Gena;
using DlnaServer.Host.Gena;
using DlnaServer.Host.Upnp.Control;
using DlnaServer.Host.Upnp.Discovery;
using DlnaServer.Upnp.Soap;
using DlnaServer.Upnp.Soap.AvTransport;
using DlnaServer.Upnp.Soap.ConnectionManager;
using DlnaServer.Upnp.Soap.ContentDirectory;
using DlnaServer.Upnp.Soap.MediaReceiverRegistrar;
using DlnaServer.Upnp.Ssdp;
using SoapCore;

namespace DlnaServer.Host.Upnp
{
    /// <summary>
    /// Registers the UPnP surface: device identity, the four SOAP services, GENA subscriptions and SSDP
    /// discovery.
    /// </summary>
    /// <remarks>
    /// Lives in the Host rather than beside <c>AddDlnaMedia</c> and <c>AddDlnaPersistence</c> in its own
    /// project, because it cannot: the SOAP services and the SSDP hosted services are Host types, and
    /// <c>DlnaServer.Upnp</c> must not take a dependency on SoapCore or ASP.NET Core to register them.
    /// </remarks>
    internal static class UpnpServiceCollectionExtensions
    {
        /// <param name="services">The container being built.</param>
        /// <param name="port">The media port, which every advertised URL carries.</param>
        public static IServiceCollection AddDlnaUpnp(this IServiceCollection services, int port)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Device identities are resolved once at startup: one per usable network interface, each with
            // a stable identifier so renderers do not see a new device after every restart.
            _ = services.AddSingleton<ILocalAddressProvider, LocalAddressProvider>();

            _ = services.AddSingleton<IUpnpDeviceRegistry>(provider =>
                new UpnpDeviceRegistry(
                    provider.GetRequiredService<ILocalAddressProvider>(),
                    Environment.MachineName,
                    port));

            _ = services.AddSoapCore<CustomEnvelopeMessage>();
            _ = services.AddScoped<IContentDirectoryService, ContentDirectoryService>();
            _ = services.AddScoped<IConnectionManagerService, ConnectionManagerService>();
            _ = services.AddScoped<IAvTransportService, AvTransportService>();
            _ = services.AddScoped<IMediaReceiverRegistrarService, MediaReceiverRegistrarService>();

            // GENA subscriptions outlive a request, so the store is a singleton.
            _ = services.AddSingleton<ISubscriptionStore, SubscriptionStore>();

            _ = services.AddHostedService<SsdpNotifierHostedService>();
            _ = services.AddHostedService<SsdpListenerHostedService>();

            return services;
        }
    }
}
