using DlnaServer.Upnp.Constants;
using DlnaServer.Upnp.Ssdp;

namespace DlnaServer.Host.Upnp.Discovery
{
    /// <summary>
    /// The set of notification targets advertised for one device identity.
    /// </summary>
    /// <remarks>
    /// Seven entries per interface, in the order the reference sends them. A renderer may listen for any
    /// one of them, so all seven are announced and all seven are answerable in an M-SEARCH reply.
    /// </remarks>
    internal static class SsdpAdvertisement
    {
        /// <summary>
        /// Notification types advertised for a device, in announcement order.
        /// </summary>
        public static IReadOnlyList<string> NotificationTypesFor(UpnpDeviceIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);

            return
            [
                UpnpServices.RootDevice,
                UpnpServices.MediaServer,
                UpnpServices.ServiceType.ContentDirectory,
                UpnpServices.ServiceType.AvTransport,
                UpnpServices.ServiceType.ConnectionManager,
                UpnpServices.ServiceType.MediaReceiverRegistrar,
                $"uuid:{identity.DeviceId}",
            ];
        }

        /// <summary>
        /// Decides whether a search target should be answered for a given notification type.
        /// </summary>
        /// <remarks>
        /// Correct SSDP semantics: <c>ssdp:all</c> matches everything, and otherwise the target must equal
        /// the advertised type exactly.
        /// <para>
        /// The reference tested <c>!searchTarget.Contains(deviceType)</c> - a negation - so it answered
        /// for every type <b>except</b> the one asked for. A device sending <c>ssdp:all</c> was unaffected,
        /// which is why the server works at all today, but a device doing a targeted search never received
        /// a valid reply. <paramref name="useLegacyInvertedMatch"/> restores that behaviour if a renderer
        /// turns out to depend on the flood of wrong answers.
        /// </para>
        /// </remarks>
        public static bool ShouldAnswer(
            string searchTarget,
            string notificationType,
            bool useLegacyInvertedMatch)
        {
            ArgumentNullException.ThrowIfNull(searchTarget);
            ArgumentNullException.ThrowIfNull(notificationType);

            if (useLegacyInvertedMatch)
            {
                return !searchTarget.Contains(notificationType, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(searchTarget, "ssdp:all", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(searchTarget, notificationType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
