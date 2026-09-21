using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Soap.ConnectionManager;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// Implements the ConnectionManager service.
    /// </summary>
    /// <remarks>
    /// This server negotiates nothing: a renderer is handed an HTTP URL in the browse result and fetches
    /// it directly, so there is one connection - the default - and it is always available. The value of
    /// the service is <c>GetProtocolInfo</c>, which is how a renderer learns what formats to expect
    /// before it has browsed anything.
    /// </remarks>
    internal sealed partial class ConnectionManagerService : IConnectionManagerService
    {
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<ConnectionManagerService> _logger;

        public ConnectionManagerService(
            IOptionsMonitor<DlnaOptions> options,
            ILogger<ConnectionManagerService> logger)
        {
            _options = options;
            _logger = logger;
        }

        public GetProtocolInfoResponse GetProtocolInfo()
        {
            var source = DlnaProtocolInfo.BuildSourceList(ResolveServableMimes());

            LogProtocolInfoRequested(source);

            return new GetProtocolInfoResponse
            {
                Source = source,
            };
        }

        public GetCurrentConnectionIdsResponse GetCurrentConnectionIDs()
        {
            return new GetCurrentConnectionIdsResponse();
        }

        public GetCurrentConnectionInfoResponse GetCurrentConnectionInfo(int ConnectionID)
        {
            LogConnectionInfoRequested(ConnectionID);

            return new GetCurrentConnectionInfoResponse();
        }

        public PrepareForConnectionResponse PrepareForConnection(
            string RemoteProtocolInfo,
            string PeerConnectionManager,
            int PeerConnectionID,
            string Direction)
        {
            LogPrepareForConnectionRequested(RemoteProtocolInfo, Direction);

            return new PrepareForConnectionResponse();
        }

        public ConnectionCompleteResponse ConnectionComplete(int ConnectionID)
        {
            LogConnectionCompleteRequested(ConnectionID);

            return new ConnectionCompleteResponse();
        }

        /// <summary>
        /// The MIME types this deployment can actually serve: its configured extensions, and everything
        /// scanning is able to infer from an extension on its own.
        /// </summary>
        /// <remarks>
        /// Read per call rather than cached: the extension map is reloadable, and this action is called
        /// once or twice per renderer rather than per request.
        /// <para>
        /// The second tier has to be here, or the server indexes and streams files whose MIME it never
        /// claimed - a renderer matches an item's <c>res@protocolInfo</c> against this list and refuses
        /// anything absent from it. It deliberately over-approximates: a MIME whose every extension is
        /// claimed by a lower-numbered sibling can be listed here and never actually indexed, and
        /// over-claiming a capability costs nothing where under-claiming costs playback.
        /// </para>
        /// </remarks>
        private IEnumerable<DlnaMime> ResolveServableMimes()
        {
            // Mime is configured as the name of a DlnaMime member, so an unrecognised value is skipped
            // rather than reported - options validation is what refuses a bad configuration.
            foreach (var configured in _options.CurrentValue.Library.MediaFileExtensions.Values)
            {
                if (Enum.TryParse<DlnaMime>(configured.Mime, ignoreCase: true, out var mime)
                    && Enum.IsDefined(mime))
                {
                    yield return mime;
                }
            }

            foreach (var info in DlnaMimeCatalog.All)
            {
                // The same predicate the admin editor offers from, so what a file may be given and what
                // this server advertises cannot drift apart. Iteration order is left alone deliberately -
                // it is enum order and it goes on the wire.
                if (info.Mime.IsServable())
                {
                    yield return info.Mime;
                }
            }
        }
    }
}
