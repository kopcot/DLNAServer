using System.Diagnostics.CodeAnalysis;
using System.ServiceModel;

namespace DlnaServer.Upnp.Soap.AvTransport
{
    /// <summary>
    /// The AVTransport service, as advertised by this device.
    /// </summary>
    /// <remarks>
    /// A MediaServer has no transport to control - the renderer fetches content over HTTP and drives its
    /// own playback. The service is advertised in <c>description.xml</c> and its SCPD anyway, carried over
    /// from the reference, so every action must answer: an advertised action that faults is worse than one
    /// that acknowledges. Nothing here has a side effect, and the replies report an idle transport.
    /// <para>
    /// Argument names come from <c>/SCPD/avTransport.xml</c> and are PascalCase because SoapCore binds
    /// each one from the body element of the same name. Note <c>Seek</c> takes <c>Unit</c>, which is what
    /// that SCPD declares - the reference names the parameter <c>SeekMode</c>, so a renderer sending
    /// <c>&lt;Unit&gt;</c> binds nothing and the argument arrives null.
    /// </para>
    /// </remarks>
    [ServiceContract(Namespace = Constants.UpnpServices.ServiceType.AvTransport)]
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Stop is the action name /SCPD/avTransport.xml advertises. SoapCore dispatches on "
            + "that name, so renaming it would make the action unreachable.")]
    public interface IAvTransportService
    {
        /// <summary>
        /// <b>SetAVTransportURI</b><br />
        /// Names the resource a renderer intends to play.
        /// </summary>
        [OperationContract(Name = "SetAVTransportURI")]
        SetAvTransportUriResponse SetAVTransportURI(string CurrentURI, string CurrentURIMetaData);

        /// <summary>
        /// <b>Play</b><br />
        /// Starts playback at the given speed.
        /// </summary>
        [OperationContract(Name = "Play")]
        PlayResponse Play(int InstanceID, string Speed);

        /// <summary>
        /// <b>Pause</b><br />
        /// Suspends playback, keeping the position.
        /// </summary>
        [OperationContract(Name = "Pause")]
        PauseResponse Pause(int InstanceID);

        /// <summary>
        /// <b>Stop</b><br />
        /// Ends playback and returns the transport to its idle state.
        /// </summary>
        [OperationContract(Name = "Stop")]
        StopResponse Stop(int InstanceID);

        /// <summary>
        /// <b>Seek</b><br />
        /// Moves the playback position. <paramref name="Unit"/> names what
        /// <paramref name="Target"/> is measured in, such as <c>REL_TIME</c> or <c>ABS_COUNT</c>.
        /// </summary>
        [OperationContract(Name = "Seek")]
        SeekResponse Seek(int InstanceID, string Unit, string Target);

        /// <summary>
        /// <b>GetTransportInfo</b><br />
        /// Reports whether the transport is playing, paused or stopped.
        /// </summary>
        [OperationContract(Name = "GetTransportInfo")]
        GetTransportInfoResponse GetTransportInfo(int InstanceID);

        /// <summary>
        /// <b>GetPositionInfo</b><br />
        /// Reports the current track and position within it.
        /// </summary>
        [OperationContract(Name = "GetPositionInfo")]
        GetPositionInfoResponse GetPositionInfo(int InstanceID);
    }
}
