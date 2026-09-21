using System.Net;

namespace DlnaServer.Upnp.Ssdp
{
    /// <inheritdoc cref="IUpnpDeviceRegistry"/>
    public sealed class UpnpDeviceRegistry : IUpnpDeviceRegistry
    {
        private readonly ILocalAddressProvider _addresses;
        private readonly string _machineName;
        private readonly int _port;

        public UpnpDeviceRegistry(ILocalAddressProvider addresses, string machineName, int port)
        {
            ArgumentNullException.ThrowIfNull(addresses);

            _addresses = addresses;
            _machineName = machineName;
            _port = port;

            Identities = ResolveIdentities();
        }

        // Volatile because Refresh runs on the notifier's loop while Resolve(IPAddress) is read from
        // every request thread. The list is replaced wholesale rather than mutated, so a reader sees
        // either the old set or the new one, never a half-built one.
        private volatile IReadOnlyList<UpnpDeviceIdentity> _identities = [];

        public IReadOnlyList<UpnpDeviceIdentity> Identities
        {
            get => _identities;
            private set => _identities = value;
        }

        public bool Refresh()
        {
            var resolved = ResolveIdentities();
            var previous = Identities;

            if (resolved.Length == previous.Count)
            {
                var changed = false;

                for (var index = 0; index < resolved.Length; index++)
                {
                    if (!resolved[index].Address.Equals(previous[index].Address))
                    {
                        changed = true;
                        break;
                    }
                }

                if (!changed)
                {
                    return false;
                }
            }

            Identities = resolved;

            return true;
        }

        private UpnpDeviceIdentity[] ResolveIdentities()
        {
            return _addresses.GetBroadcastableAddresses()
                .Select(address => new UpnpDeviceIdentity(
                    UpnpDeviceIdentity.CreateStableDeviceId(_machineName, address, _port),
                    address,
                    _port))
                .ToArray();
        }

        public UpnpDeviceIdentity Resolve(IPAddress? localAddress)
        {
            if (Identities.Count == 0)
            {
                throw new InvalidOperationException(
                    "No usable network interface was found, so the server cannot advertise itself.");
            }

            if (localAddress is null)
            {
                return Identities[0];
            }

            var mapped = localAddress.IsIPv4MappedToIPv6
                ? localAddress.MapToIPv4()
                : localAddress;

            foreach (var identity in Identities)
            {
                if (identity.Address.Equals(mapped))
                {
                    return identity;
                }
            }

            return Identities[0];
        }
    }
}
