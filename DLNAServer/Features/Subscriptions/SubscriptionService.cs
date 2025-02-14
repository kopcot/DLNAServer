using DLNAServer.Features.Subscriptions.Data;
using DLNAServer.Features.Subscriptions.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DLNAServer.Features.Subscriptions
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IMemoryCache MemoryCache;
        public SubscriptionService(IMemoryCache memoryCache)
        {
            MemoryCache = memoryCache;
        }
        public Subscription GetOrAddSubscription(string sid, string callback, TimeSpan timeout)
        {
            var subscription = MemoryCache.GetOrCreate(
                       CreateMemoryCacheKey(sid),
                       entry =>
                       {
                           var result = new Subscription
                           {
                               SID = sid,
                               Callback = callback,
                               Timeout = timeout,
                               LastNotifyTimeUtc = DateTime.UtcNow,
                           };
                           _ = entry.SetValue(result);
                           _ = entry.SetSlidingExpiration(timeout);
                           entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                           entry.Size = 1;

                           return result;
                       });
            return subscription ?? throw new ArgumentNullException(nameof(subscription));
        }
        public void TryRemoveSubscription(string sid)
        {
            MemoryCache.Remove(CreateMemoryCacheKey(sid));
        }
        public bool UpdateLastNotifyTime(string sid)
        {
            if (GetSubscription(sid) is Subscription subscription)
            {
                subscription.LastNotifyTimeUtc = DateTime.UtcNow;
                _ = MemoryCache.Set(
                    CreateMemoryCacheKey(sid),
                    subscription,
                    new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = subscription.Timeout,
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                        Size = 1
                    }
                    );
                return true;
            }
            return false;
        }
        public Subscription? GetSubscription(string sid)
        {
            return MemoryCache.Get<Subscription>(CreateMemoryCacheKey(sid));
        }
        private string CreateMemoryCacheKey(string sid) => $"{nameof(SubscriptionService)} {typeof(Subscription).Name} {sid}";
    }
}
