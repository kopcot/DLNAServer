using DlnaServer.Upnp.Soap.AvTransport;

namespace DlnaServer.Host.Upnp.Control
{
    /// <summary>
    /// Implements the advertised AVTransport service as a stub with no side effects.
    /// </summary>
    /// <remarks>
    /// A MediaServer has no transport: playback lives entirely in the renderer, which fetches the URL it
    /// was given and controls itself. Every action still answers, because an advertised action that
    /// faults tells a television the device is broken, and the replies report an idle transport using
    /// only values the SCPD's <c>allowedValueList</c> permits.
    /// <para>
    /// Every call is logged at Information. The reference logs these as warnings with the note "not found
    /// operation in real usage" - meaning no renderer it ever met called them. If one does, that is worth
    /// knowing about, and it is the evidence that would justify implementing the service properly.
    /// </para>
    /// </remarks>
    internal sealed partial class AvTransportService : IAvTransportService
    {
        private readonly ILogger<AvTransportService> _logger;

        public AvTransportService(ILogger<AvTransportService> logger)
        {
            _logger = logger;
        }

        public SetAvTransportUriResponse SetAVTransportURI(string CurrentURI, string CurrentURIMetaData)
        {
            LogTransportUriSet(CurrentURI);

            return new SetAvTransportUriResponse();
        }

        public PlayResponse Play(int InstanceID, string Speed)
        {
            LogActionRequested(nameof(Play), InstanceID);

            return new PlayResponse();
        }

        public PauseResponse Pause(int InstanceID)
        {
            LogActionRequested(nameof(Pause), InstanceID);

            return new PauseResponse();
        }

        public StopResponse Stop(int InstanceID)
        {
            LogActionRequested(nameof(Stop), InstanceID);

            return new StopResponse();
        }

        public SeekResponse Seek(int InstanceID, string Unit, string Target)
        {
            LogSeekRequested(InstanceID, Unit, Target);

            return new SeekResponse();
        }

        public GetTransportInfoResponse GetTransportInfo(int InstanceID)
        {
            LogStateQueried(nameof(GetTransportInfo), InstanceID);

            return new GetTransportInfoResponse();
        }

        public GetPositionInfoResponse GetPositionInfo(int InstanceID)
        {
            LogStateQueried(nameof(GetPositionInfo), InstanceID);

            return new GetPositionInfoResponse();
        }
    }
}
