using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// Reply to <c>Stop</c>. The action returns no arguments.
    /// </summary>
    /// <remarks>
    /// Accepted and acknowledged without effect. This server has nothing to stop: a renderer fetches
    /// content over HTTP and drives its own playback, so AVTransport exists only because the device
    /// description advertises it.
    /// </remarks>
    [MessageContract(WrapperName = "StopResponse")]
    [XmlRoot(ElementName = "StopResponse")]
    public sealed class StopResponse
    {
    }
}
