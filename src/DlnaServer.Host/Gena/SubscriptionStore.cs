using System.Collections.Concurrent;
using DlnaServer.Core.Gena;

namespace DlnaServer.Host.Gena
{
    /// <inheritdoc cref="ISubscriptionStore"/>
    internal sealed class SubscriptionStore : ISubscriptionStore
    {
        /// <summary>
        /// Most subscriptions held at once. A LAN has a handful of renderers, each subscribing to at most
        /// four services, so this is generous - and it is a bound rather than a target, because a renderer
        /// that subscribes without ever unsubscribing must not be able to grow the store without limit.
        /// </summary>
        private const int MaxSubscriptions = 64;

        // Serialises prune + count + add. Only Add contends, and only to keep the cap honest.
        private readonly object _addGate = new();

        private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions =
            new(StringComparer.Ordinal);

        private readonly TimeProvider _timeProvider;

        public SubscriptionStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public int Count
        {
            get
            {
                PruneExpired();

                return _subscriptions.Count;
            }
        }

        public EventSubscription? Add(string serviceId, IReadOnlyList<string> callbackUrls, TimeSpan granted)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
            ArgumentNullException.ThrowIfNull(callbackUrls);

            // Under one lock: pruning, counting and inserting were three separate steps, and because each
            // caller mints a fresh Guid the TryAdd could never collide - so N concurrent SUBSCRIBE
            // requests all read Count < MaxSubscriptions and all inserted, and the cap held only for
            // sequential callers. The store is tiny and off every hot path, so a lock costs nothing here.
            lock (_addGate)
            {
                return AddUnderLock(serviceId, callbackUrls, granted);
            }
        }

        private EventSubscription? AddUnderLock(
            string serviceId,
            IReadOnlyList<string> callbackUrls,
            TimeSpan granted)
        {
            PruneExpired();

            if (_subscriptions.Count >= MaxSubscriptions)
            {
                return null;
            }

            var subscription = new EventSubscription(
                $"uuid:{Guid.NewGuid()}",
                serviceId,
                callbackUrls,
                granted,
                _timeProvider.GetUtcNow().Add(granted));

            return _subscriptions.TryAdd(subscription.Sid, subscription)
                ? subscription
                : null;
        }

        public EventSubscription? Renew(string sid, TimeSpan granted)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sid);

            if (!_subscriptions.TryGetValue(sid, out var existing))
            {
                return null;
            }

            if (existing.ExpiresUtc <= _timeProvider.GetUtcNow())
            {
                // Lapsed. Removed rather than revived: the specification has the renderer subscribe
                // afresh, and reviving it would hand back an identifier the renderer believes is dead.
                _ = _subscriptions.TryRemove(sid, out _);
                return null;
            }

            var renewed = existing with
            {
                Granted = granted,
                ExpiresUtc = _timeProvider.GetUtcNow().Add(granted),
            };

            return _subscriptions.TryUpdate(sid, renewed, existing)
                ? renewed
                : null;
        }

        public bool Remove(string sid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sid);

            return _subscriptions.TryRemove(sid, out _);
        }

        /// <summary>
        /// Drops lapsed subscriptions. Called on the paths that would otherwise let them accumulate.
        /// </summary>
        /// <remarks>
        /// No timer: subscriptions only ever arrive through a request, so pruning where one is added is
        /// enough, and a background timer would wake an otherwise idle server to do nothing.
        /// </remarks>
        private void PruneExpired()
        {
            var now = _timeProvider.GetUtcNow();

            foreach (var (sid, subscription) in _subscriptions)
            {
                if (subscription.ExpiresUtc <= now)
                {
                    _ = _subscriptions.TryRemove(sid, out _);
                }
            }
        }

        public IReadOnlyList<EventSubscription> List()
        {
            var nowUtc = _timeProvider.GetUtcNow();

            // Expired-but-not-yet-removed entries are filtered the same way Count does, so the listing
            // and the count can never disagree.
            return _subscriptions.Values
                .Where(subscription => subscription.ExpiresUtc > nowUtc)
                .OrderBy(static subscription => subscription.ServiceId, StringComparer.Ordinal)
                .ThenBy(static subscription => subscription.Sid, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
