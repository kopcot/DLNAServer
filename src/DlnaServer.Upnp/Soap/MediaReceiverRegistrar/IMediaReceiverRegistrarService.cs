using System.ServiceModel;

namespace DlnaServer.Upnp.Soap.MediaReceiverRegistrar
{
    /// <summary>
    /// The Microsoft <c>X_MS_MediaReceiverRegistrar</c> service: whether a client is allowed to receive
    /// content from this server.
    /// </summary>
    /// <remarks>
    /// Xbox and Windows Media Player clients call <c>IsValidated</c> before they will browse. The
    /// reference declares this interface with <b>no operations at all</b> while advertising the service in
    /// <c>description.xml</c> and serving its SCPD, so such a client asks a question the server cannot
    /// answer. Both advertised actions are implemented here.
    /// </remarks>
    [ServiceContract(Namespace = Constants.UpnpServices.ServiceType.MediaReceiverRegistrar)]
    public interface IMediaReceiverRegistrarService
    {
        /// <summary>
        /// <b>IsValidated</b><br />
        /// Whether the calling device may receive content.
        /// </summary>
        [OperationContract(Name = "IsValidated")]
        IsValidatedResponse IsValidated(string DeviceID);

        /// <summary>
        /// <b>RegisterDevice</b><br />
        /// Registers a device that presented a registration message.
        /// </summary>
        [OperationContract(Name = "RegisterDevice")]
        RegisterDeviceResponse RegisterDevice(string RegistrationReqMsg);
    }
}
