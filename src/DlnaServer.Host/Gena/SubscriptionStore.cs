using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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

        /// <summary>
        /// Most subscriptions one device may hold, so a device subscribing in a loop cannot take every
        /// slot above and leave each television after it with a 503.
        /// </summary>
        /// <remarks>
        /// A renderer subscribes to at most four services, so this is one full set plus a second from a
        /// reboot that never unsubscribed the first - which lapses within the granted lifetime anyway.
        /// </remarks>
        private const int MaxSubscriptionsPerSubscriber = 8;

        // The bytes of an IPv6 address that name its network: a SLAAC prefix is always /64.
        private const int LinkPrefixBytes = 8;

        // Serialises prune + count + add. Only Add contends, and only to keep the cap honest.
        private readonly object _addGate = new();

        private readonly ConcurrentDictionary<string, Entry> _subscriptions = new(StringComparer.Ordinal);

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

        public EventSubscription? Add(
            string serviceId,
            IReadOnlyList<string> callbackUrls,
            TimeSpan granted,
            IPAddress? subscriber)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
            ArgumentNullException.ThrowIfNull(callbackUrls);

            // Under one lock: pruning, counting and inserting were three separate steps, and because each
            // caller mints a fresh Guid the TryAdd could never collide - so N concurrent SUBSCRIBE
            // requests all read Count < MaxSubscriptions and all inserted, and the cap held only for
            // sequential callers. The store is tiny and off every hot path, so a lock costs nothing here.
            lock (_addGate)
            {
                return AddUnderLock(serviceId, callbackUrls, granted, Normalise(subscriber));
            }
        }

        private EventSubscription? AddUnderLock(
            string serviceId,
            IReadOnlyList<string> callbackUrls,
            TimeSpan granted,
            IPAddress? subscriber)
        {
            PruneExpired();

            if (_subscriptions.Count >= MaxSubscriptions
                || CountHeldBy(subscriber) >= MaxSubscriptionsPerSubscriber)
            {
                return null;
            }

            var subscription = new EventSubscription(
                $"uuid:{Guid.NewGuid()}",
                serviceId,
                callbackUrls,
                granted,
                _timeProvider.GetUtcNow().Add(granted));

            return _subscriptions.TryAdd(subscription.Sid, new Entry(subscription, subscriber))
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

            if (existing.Subscription.ExpiresUtc <= _timeProvider.GetUtcNow())
            {
                // Lapsed. Removed rather than revived: the specification has the renderer subscribe
                // afresh, and reviving it would hand back an identifier the renderer believes is dead.
                _ = _subscriptions.TryRemove(sid, out _);
                return null;
            }

            var renewed = existing.Subscription with
            {
                Granted = granted,
                ExpiresUtc = _timeProvider.GetUtcNow().Add(granted),
            };

            return _subscriptions.TryUpdate(sid, existing with { Subscription = renewed }, existing)
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

            foreach (var (sid, entry) in _subscriptions)
            {
                if (entry.Subscription.ExpiresUtc <= now)
                {
                    _ = _subscriptions.TryRemove(sid, out _);
                }
            }
        }

        // A dual-stack socket reports an IPv4 peer as ::ffff:a.b.c.d, which would otherwise count as a
        // second device next to the same peer arriving over IPv4. An IPv6 device is keyed by its /64: it
        // can pick any address inside its own prefix, so counting per address would not cap it at all.
        private static IPAddress? Normalise(IPAddress? subscriber)
        {
            if (subscriber is { IsIPv4MappedToIPv6: true })
            {
                return subscriber.MapToIPv4();
            }

            if (subscriber is not { AddressFamily: AddressFamily.InterNetworkV6 })
            {
                return subscriber;
            }

            Span<byte> bytes = stackalloc byte[16];
            _ = subscriber.TryWriteBytes(bytes, out _);
            bytes[LinkPrefixBytes..].Clear();

            return new IPAddress(bytes);
        }

        private int CountHeldBy(IPAddress? subscriber)
        {
            var held = 0;

            foreach (var (_, entry) in _subscriptions)
            {
                if (Equals(entry.Subscriber, subscriber))
                {
                    held++;
                }
            }

            return held;
        }

        public IReadOnlyList<EventSubscription> List()
        {
            var nowUtc = _timeProvider.GetUtcNow();

            // Expired-but-not-yet-removed entries are filtered the same way Count does, so the listing
            // and the count can never disagree.
            return _subscriptions.Values
                .Select(static entry => entry.Subscription)
                .Where(subscription => subscription.ExpiresUtc > nowUtc)
                .OrderBy(static subscription => subscription.ServiceId, StringComparer.Ordinal)
                .ThenBy(static subscription => subscription.Sid, StringComparer.Ordinal)
                .ToArray();
        }

        // A subscription and the address that made it, which the renderer-facing record does not carry.
        private sealed record Entry(EventSubscription Subscription, IPAddress? Subscriber);
    }
}
