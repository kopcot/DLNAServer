using DLNAServer.Features.Subscriptions.Data;

namespace DLNAServer.Features.Subscriptions.Interfaces
{
    public interface ISubscriptionService
    {
        Subscription GetOrAddSubscription(string sid, string callback, TimeSpan timeout);
        void TryRemoveSubscription(string sid);
        bool UpdateLastNotifyTime(string sid);
        Subscription? GetSubscription(string sid);
    }
}
