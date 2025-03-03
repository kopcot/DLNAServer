using System.Net;

namespace DLNAServer.Types.UPNP
{
    public record UPNPDevice
    {
        public readonly IPEndPoint Endpoint;
        public readonly IPAddress Address;
        public readonly uint Port;
        public readonly Uri Descriptor;
        public readonly string Type;
        public readonly string USN;
        public readonly Guid UUID;
        public UPNPDevice(
            IPAddress _address,
            uint _port,
            Uri _descriptor,
            string _type,
            Guid _uuid)
        {
            Address = _address;
            Port = _port;
            Descriptor = _descriptor;
            Type = string.Intern(_type);
            UUID = _uuid;

            Endpoint = new IPEndPoint(Address, (int)Port);

            if (Type.StartsWith("uuid:", StringComparison.Ordinal))
            {
                USN = string.Intern(Type);
            }
            else
            {
                USN = string.Intern($"uuid:{UUID}::{Type}");
            }
        }
    }
}
