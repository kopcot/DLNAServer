using DlnaServer.Host.Gena;
using Microsoft.AspNetCore.Mvc;
using DlnaServer.Core.Gena;

namespace DlnaServer.Host.Controllers
{
    /// <summary>
    /// Serves the GENA subscription handshake at the <c>eventSubURL</c> every service advertises.
    /// </summary>
    /// <remarks>
    /// <c>description.xml</c> points all four services at <c>/event/eventAction/{serviceId}</c>, so a
    /// renderer subscribes here before it browses. Nothing in this rewrite served that path until now,
    /// which means every SUBSCRIBE was answered with a 404.
    /// <para>
    /// <b>No NOTIFY is ever sent.</b> That is a recorded decision (<c>docs/decisions.md</c> section 2), not an
    /// oversight: the library changes rarely and a renderer re-browses anyway. What this endpoint owes a
    /// renderer is an honest handshake - an identifier the server recognises later, a lifetime in the
    /// form the header grammar defines, and an UNSUBSCRIBE that actually forgets.
    /// </para>
    /// <para>
    /// Status codes follow the UPnP Device Architecture: <c>400</c> for incompatible headers, <c>412</c>
    /// for a missing or unusable one, <c>503</c> when no further subscription can be accepted. The
    /// reference answers <c>400</c> for everything and <c>200</c> for an UNSUBSCRIBE it ignores.
    /// </para>
    /// </remarks>
    [ApiController]
    public sealed partial class EventController : ControllerBase
    {
        private readonly ISubscriptionStore _subscriptions;
        private readonly ILogger<EventController> _logger;

        public EventController(ISubscriptionStore subscriptions, ILogger<EventController> logger)
        {
            _subscriptions = subscriptions;
            _logger = logger;
        }

        /// <summary>
        /// Creates or renews a subscription to one service's events.
        /// </summary>
        /// <remarks>
        /// A request carrying <c>SID</c> is a renewal and must not also carry <c>CALLBACK</c> or
        /// <c>NT</c>; one carrying <c>CALLBACK</c> is a new subscription. The reference requires
        /// <c>CALLBACK</c> on every request, so a renewal - which by definition sends only <c>SID</c> and
        /// <c>TIMEOUT</c> - is rejected with a 400 and the renderer's subscription lapses.
        /// </remarks>
        [HttpSubscribe("/event/eventAction/{serviceId}")]
        public IActionResult Subscribe([FromRoute] string serviceId)
        {
            var sid = Request.Headers[GenaHeaders.SubscriptionId].ToString();
            var callbackHeader = Request.Headers[GenaHeaders.Callback].ToString();
            var notificationType = Request.Headers[GenaHeaders.NotificationType].ToString();
            var granted = GenaTimeout.Grant(Request.Headers[GenaHeaders.Timeout].ToString());

            var hasSid = !string.IsNullOrWhiteSpace(sid);
            var hasCallback = !string.IsNullOrWhiteSpace(callbackHeader);
            var hasNotificationType = !string.IsNullOrWhiteSpace(notificationType);

            if (hasSid && (hasCallback || hasNotificationType))
            {
                LogIncompatibleHeaders(serviceId);
                return BadRequest();
            }

            // Checked only when present. The specification requires it on a new subscription, but the
            // reference never looked at it, so demanding it now could refuse a renderer that works today.
            if (hasNotificationType
                && !string.Equals(notificationType, GenaHeaders.EventNotificationType, StringComparison.OrdinalIgnoreCase))
            {
                LogUnsupportedNotificationType(notificationType);
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            return hasSid
                ? RenewSubscription(sid, granted)
                : CreateSubscription(serviceId, callbackHeader, granted);
        }

        /// <summary>
        /// Ends a subscription.
        /// </summary>
        [HttpUnsubscribe("/event/eventAction/{serviceId}")]
        public IActionResult Unsubscribe([FromRoute] string serviceId)
        {
            var sid = Request.Headers[GenaHeaders.SubscriptionId].ToString();

            if (!string.IsNullOrWhiteSpace(Request.Headers[GenaHeaders.Callback].ToString())
                || !string.IsNullOrWhiteSpace(Request.Headers[GenaHeaders.NotificationType].ToString()))
            {
                LogIncompatibleHeaders(serviceId);
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(sid))
            {
                LogMissingSubscriptionId(serviceId);
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            if (!_subscriptions.Remove(sid))
            {
                LogUnknownSubscription(sid);
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            LogUnsubscribed(sid, serviceId, _subscriptions.Count);

            return EmptySuccess();
        }

        private StatusCodeResult CreateSubscription(string serviceId, string callbackHeader, TimeSpan granted)
        {
            var callbackUrls = GenaCallback.Parse(callbackHeader);

            if (callbackUrls.Count == 0)
            {
                LogUnusableCallback(callbackHeader);
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            var subscription = _subscriptions.Add(serviceId, callbackUrls, granted);

            if (subscription is null)
            {
                LogStoreFull(serviceId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            LogSubscribed(subscription.Sid, serviceId, callbackUrls[0], granted, _subscriptions.Count);

            return SubscriptionAccepted(subscription);
        }

        private StatusCodeResult RenewSubscription(string sid, TimeSpan granted)
        {
            var subscription = _subscriptions.Renew(sid, granted);

            if (subscription is null)
            {
                LogUnknownSubscription(sid);
                return StatusCode(StatusCodes.Status412PreconditionFailed);
            }

            LogRenewed(sid, subscription.ServiceId, granted);

            return SubscriptionAccepted(subscription);
        }

        private StatusCodeResult SubscriptionAccepted(EventSubscription subscription)
        {
            Response.Headers[GenaHeaders.SubscriptionId] = subscription.Sid;
            Response.Headers[GenaHeaders.Timeout] = GenaTimeout.Format(subscription.Granted);

            return EmptySuccess();
        }

        /// <summary>
        /// An empty 200, which is what a GENA reply is.
        /// </summary>
        /// <remarks>
        /// The body must be empty - the reference sends the text "subscribed", which is not part of the
        /// protocol. <c>200</c> rather than <c>204</c> because the specification names that code, and a
        /// renderer matching on it exactly would not recognise a 204. <c>SERVER</c> and <c>DATE</c> are
        /// optional on this reply and are not sent, as in the reference.
        /// </remarks>
        private StatusCodeResult EmptySuccess()
        {
            Response.ContentLength = 0;

            return StatusCode(StatusCodes.Status200OK);
        }
    }
}
