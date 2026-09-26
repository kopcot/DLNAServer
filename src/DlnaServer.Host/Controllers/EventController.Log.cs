using System.Net;

namespace DlnaServer.Host.Controllers
{
    public sealed partial class EventController
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Subscribed {Sid} to {ServiceId}, callback {Callback}, granted {Granted}; "
                + "{SubscriptionCount} subscription(s) held")]
        private partial void LogSubscribed(
            string sid,
            string serviceId,
            string callback,
            TimeSpan granted,
            int subscriptionCount);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Renewed {Sid} on {ServiceId} for {Granted}")]
        private partial void LogRenewed(string sid, string serviceId, TimeSpan granted);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "Unsubscribed {Sid} from {ServiceId}; {SubscriptionCount} subscription(s) held")]
        private partial void LogUnsubscribed(string sid, string serviceId, int subscriptionCount);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Event request for {ServiceId} carried both a subscription identifier and "
                + "subscription-creating headers, which the protocol does not allow")]
        private partial void LogIncompatibleHeaders(string serviceId);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Event request asked for notification type '{NotificationType}'; only upnp:event exists")]
        private partial void LogUnsupportedNotificationType(string notificationType);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Subscription refused: callback header '{Callback}' held no absolute HTTP URL")]
        private partial void LogUnusableCallback(string callback);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "Event request for {ServiceId} carried no subscription identifier")]
        private partial void LogMissingSubscriptionId(string serviceId);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Warning,
            Message = "Subscription {Sid} is unknown or has lapsed")]
        private partial void LogUnknownSubscription(string sid);

        /// <summary>
        /// Error, not warning: the store is bounded, so reaching the limit means subscriptions are
        /// accumulating without being released - a renderer misbehaving, or a leak here. The same holds
        /// for one device reaching its own share.
        /// </summary>
        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Error,
            Message = "Cannot accept a subscription to {ServiceId} from {Subscriber}: the subscription store "
                + "is full, or that device already holds its share of it")]
        private partial void LogStoreFull(string serviceId, IPAddress? subscriber);
    }
}
