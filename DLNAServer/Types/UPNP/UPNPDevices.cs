using DLNAServer.Configuration;
using DLNAServer.Types.DLNA;
using DLNAServer.Types.IP.Interfaces;
using DLNAServer.Types.UPNP.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace DLNAServer.Types.UPNP
{
    public class UPNPDevices : IUPNPDevices
    {
        private readonly Lazy<IIP> _ipLazy;
        private readonly ServerConfig _serverConfig;
        private IIP IP => _ipLazy.Value;
        public UPNPDevices(Lazy<IIP> ipLazy,
            ServerConfig serverConfig)
        {
            _ipLazy = ipLazy;
            _serverConfig = serverConfig;
        }
        private static ConcurrentDictionary<IPEndPoint, List<UPNPDevice>> Devices { get; set; } = [];
        private static UPNPDevice[]? AllUPNPDevicesArray { get; set; }
        public UPNPDevice[] AllUPNPDevices => AllUPNPDevicesArray ?? throw new ArgumentNullException(nameof(AllUPNPDevicesArray), $"Uninitialized property '{nameof(AllUPNPDevicesArray)}'");

        public async Task InitializeAsync()
        {
            foreach (var address in IP.ExternalIPAddresses.ToList())
            {
                var uuid = Guid.NewGuid();
                var types = new[] {
                    XmlNamespaces.NS_RootDevice,
                    XmlNamespaces.NS_MediaServer,
                    XmlNamespaces.NS_ServiceType_ContentDirectory,
                    XmlNamespaces.NS_ServiceType_ConnectionManager,
                    XmlNamespaces.NS_ServiceType_X_MS_MediaReceiverRegistrar,
                    "uuid:" + uuid
                }.Select(t => new UPNPDevice(
                    _address: address,
                    _port: _serverConfig.ServerPort,
                    _descriptor: new Uri($"http://{address}:{_serverConfig.ServerPort}/media/description.xml?uuid={uuid}"),
                    _uuid: uuid,
                    _type: t
                )).ToList();

                _ = Devices.TryAdd(new IPEndPoint(address, (int)_serverConfig.ServerPort), types);
            }
            AllUPNPDevicesArray = Devices.SelectMany(static (x) => x.Value).ToArray();
            await Task.CompletedTask;
        }

        public async Task TerminateAsync()
        {
            AllUPNPDevicesArray = [];
            Devices.Clear();
            await Task.CompletedTask;
        }
    }
}
