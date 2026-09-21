using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// Reply to <c>ConnectionComplete</c>. The action returns no arguments.
    /// </summary>
    [MessageContract(WrapperName = "ConnectionCompleteResponse")]
    [XmlRoot(ElementName = "ConnectionCompleteResponse")]
    public sealed class ConnectionCompleteResponse
    {
    }
}
