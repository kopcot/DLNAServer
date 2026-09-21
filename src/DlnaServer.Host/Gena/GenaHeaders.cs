namespace DlnaServer.Host.Gena
{
    /// <summary>
    /// The headers GENA subscription requests and replies are made of.
    /// </summary>
    /// <remarks>
    /// GENA is UPnP eventing: a renderer SUBSCRIBEs to a service, and the server would then push NOTIFY
    /// messages when a state variable changes. Everything here concerns the subscription handshake only -
    /// no NOTIFY is ever sent, which is a recorded decision rather than an omission (<c>PLAN.md</c>
    /// section 2).
    /// </remarks>
    internal static class GenaHeaders
    {
        /// <summary>
        /// <b>NT</b><br />
        /// Notification type. The only legal value on a SUBSCRIBE is <see cref="EventNotificationType"/>.
        /// </summary>
        public const string NotificationType = "NT";

        /// <summary>
        /// <b>CALLBACK</b><br />
        /// One or more URLs in angle brackets, where NOTIFY messages would be delivered.
        /// </summary>
        public const string Callback = "CALLBACK";

        /// <summary>
        /// <b>SID</b><br />
        /// Subscription identifier, <c>uuid:</c> followed by a GUID. Sent by the server on the first
        /// SUBSCRIBE, and by the renderer on every renewal and on UNSUBSCRIBE.
        /// </summary>
        public const string SubscriptionId = "SID";

        /// <summary>
        /// <b>TIMEOUT</b><br />
        /// Requested, then granted, subscription lifetime - <c>Second-1800</c> or <c>Second-infinite</c>.
        /// </summary>
        public const string Timeout = "TIMEOUT";

        /// <summary>
        /// <b>NT: upnp:event</b><br />
        /// The only notification type GENA defines for a subscription.
        /// </summary>
        public const string EventNotificationType = "upnp:event";
    }
}
