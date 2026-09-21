using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.ConnectionManager
{
    /// <summary>
    /// Reply to <c>GetCurrentConnectionIDs</c>.
    /// </summary>
    /// <remarks>
    /// The element is <c>ConnectionIDs</c>, plural, as <c>/SCPD/connectionManager.xml</c> declares. The
    /// reference emits <c>ConnectionID</c>, which no renderer parsing that document will find.
    /// </remarks>
    [MessageContract(WrapperName = "GetCurrentConnectionIDsResponse")]
    [XmlRoot(ElementName = "GetCurrentConnectionIDsResponse")]
    public sealed class GetCurrentConnectionIdsResponse
    {
        /// <summary>
        /// <b>ConnectionIDs</b><br />
        /// Comma-separated list of active connection identifiers. Always <c>0</c>, the default connection
        /// every UPnP device is required to have.
        /// </summary>
        [XmlElement(ElementName = "ConnectionIDs")]
        public string ConnectionIDs { get; set; } = "0";
    }
}
