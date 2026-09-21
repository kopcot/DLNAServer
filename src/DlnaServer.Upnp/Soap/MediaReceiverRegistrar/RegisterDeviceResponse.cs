using System.ServiceModel;
using System.Xml.Serialization;

namespace DlnaServer.Upnp.Soap.MediaReceiverRegistrar
{
    /// <summary>
    /// Reply to <c>RegisterDevice</c> on the Microsoft MediaReceiverRegistrar service.
    /// </summary>
    [MessageContract(WrapperName = "RegisterDeviceResponse")]
    [XmlRoot(ElementName = "RegisterDeviceResponse")]
    public sealed class RegisterDeviceResponse
    {
        /// <summary>
        /// <b>RegistrationRespMsg</b><br />
        /// Base64 registration response. Empty: nothing is registered, because nothing is refused - see
        /// <see cref="IsValidatedResponse"/>.
        /// </summary>
        [XmlElement(ElementName = "RegistrationRespMsg")]
        public string RegistrationRespMsg { get; set; } = string.Empty;
    }
}
