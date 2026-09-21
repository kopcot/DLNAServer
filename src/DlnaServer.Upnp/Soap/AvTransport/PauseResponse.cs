using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// Reply to <c>Pause</c>. The action returns no arguments.
    /// </summary>
    /// <remarks>
    /// Accepted and acknowledged without effect. This server has nothing to pause: a renderer fetches
    /// content over HTTP and drives its own playback, so AVTransport exists only because the device
    /// description advertises it.
    /// </remarks>
    [MessageContract(WrapperName = "PauseResponse")]
    [XmlRoot(ElementName = "PauseResponse")]
    public sealed class PauseResponse
    {
    }
}
