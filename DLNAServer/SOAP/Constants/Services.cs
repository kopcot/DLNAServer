namespace DLNAServer.SOAP.Constants
{
    public static class Services
    {
        public const string RootDevice = "upnp:rootdevice";
        public const string MediaServer = "urn:schemas-upnp-org:device:MediaServer:1";
        public static class ServiceType
        {
            public const string AVTransport = "urn:schemas-upnp-org:service:AVTransport:1";
            public const string ConnectionManager = "urn:schemas-upnp-org:service:ConnectionManager:1";
            public const string ContentDirectory = "urn:schemas-upnp-org:service:ContentDirectory:1";
            public const string X_MS_MediaReceiverRegistrar = "urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1";
        }
        public static class ServiceId
        {
            public const string AVTransport = "urn:upnp-org:serviceId:AVTransport";
            public const string ConnectionManager = "urn:upnp-org:serviceId:ConnectionManager";
            public const string ContentDirectory = "urn:upnp-org:serviceId:ContentDirectory";
            public const string X_MS_MediaReceiverRegistrar = "urn:microsoft.com:serviceId:X_MS_MediaReceiverRegistrar";
        }
    }
}
