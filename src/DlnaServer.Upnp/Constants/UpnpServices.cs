namespace DlnaServer.Upnp.Constants
{
    /// <summary>
    /// UPnP device and service identifiers advertised by this server.
    /// </summary>
    /// <remarks>
    /// Verbatim from the reference. A renderer matches these strings exactly when deciding whether it
    /// can talk to the server at all.
    /// </remarks>
    public static class UpnpServices
    {
        /// <summary>
        /// <b>NT / ST</b><br />
        /// Root device announcement target.
        /// </summary>
        public const string RootDevice = "upnp:rootdevice";

        /// <summary>
        /// <b>deviceType</b><br />
        /// Declares this server as a UPnP MediaServer.
        /// </summary>
        public const string MediaServer = "urn:schemas-upnp-org:device:MediaServer:1";

        /// <summary>
        /// <c>serviceType</c> values, as they appear in <c>description.xml</c> and SSDP.
        /// </summary>
        public static class ServiceType
        {
            /// <summary>
            /// <b>service/serviceType</b><br />
            /// Playback control. Advertised even though this is a server, because the reference does and
            /// some renderers probe for it.
            /// </summary>
            public const string AvTransport = "urn:schemas-upnp-org:service:AVTransport:1";
            /// <summary>
            /// <b>service/serviceType</b><br />
            /// Negotiates protocols and transfer modes; a renderer reads it before requesting a stream.
            /// </summary>
            public const string ConnectionManager = "urn:schemas-upnp-org:service:ConnectionManager:1";
            /// <summary>
            /// <b>service/serviceType</b><br />
            /// The browse surface, and the only service a renderer genuinely needs to list the library.
            /// </summary>
            public const string ContentDirectory = "urn:schemas-upnp-org:service:ContentDirectory:1";
            /// <summary>
            /// <b>service/serviceType</b><br />
            /// Microsoft's registrar. Xbox and some Windows clients will not browse a server without it.
            /// </summary>
            public const string MediaReceiverRegistrar = "urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1";
        }

        /// <summary>
        /// <c>serviceId</c> values. Note the Microsoft-scoped identifier for the registrar service.
        /// </summary>
        public static class ServiceId
        {
            public const string AvTransport = "urn:upnp-org:serviceId:AVTransport";
            public const string ConnectionManager = "urn:upnp-org:serviceId:ConnectionManager";
            public const string ContentDirectory = "urn:upnp-org:serviceId:ContentDirectory";
            public const string MediaReceiverRegistrar = "urn:microsoft.com:serviceId:X_MS_MediaReceiverRegistrar";
        }

        /// <summary>
        /// SOAP control endpoint paths.
        /// </summary>
        public static class ControlPath
        {
            public const string AvTransport = "/AVTransportService.asmx";
            public const string ConnectionManager = "/ConnectionManagerService.asmx";
            public const string ContentDirectory = "/ContentDirectoryService.asmx";
            public const string MediaReceiverRegistrar = "/MediaReceiverRegistrarService.asmx";
        }
    }
}
