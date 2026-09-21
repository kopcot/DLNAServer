using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// Reply to <c>SetAVTransportURI</c>.
    /// </summary>
    /// <remarks>
    /// <c>/SCPD/avTransport.xml</c> declares <c>InstanceID</c> as this action's only <i>output</i>
    /// argument, which is unusual - the specification has it as an input with no outputs. That SCPD is
    /// served byte-for-byte from the reference and is what a renderer parses, so the reply matches it.
    /// </remarks>
    [MessageContract(WrapperName = "SetAVTransportURIResponse")]
    [XmlRoot(ElementName = "SetAVTransportURIResponse")]
    public sealed class SetAvTransportUriResponse
    {
        /// <summary>
        /// <b>InstanceID</b><br />
        /// Echoes the only transport instance this device has, <c>0</c>.
        /// </summary>
        [XmlElement(ElementName = "InstanceID")]
        public int InstanceID { get; set; }
    }
}
