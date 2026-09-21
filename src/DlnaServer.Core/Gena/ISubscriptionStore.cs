namespace DlnaServer.Core.Gena
{
    /// <summary>
    /// Holds the live GENA subscriptions.
    /// </summary>
    /// <remarks>
    /// Nothing reads these to send events yet - no NOTIFY is pushed, by decision (<c>PLAN.md</c> section
    /// 2). The store exists so the subscription handshake is honest: an identifier the server hands out
    /// is one it recognises on renewal, and one it forgets on UNSUBSCRIBE.
    /// </remarks>
    public interface ISubscriptionStore
    {
        /// <summary>
        /// Creates a subscription and returns it, or null when the store is full.
        /// </summary>
        /// <remarks>
        /// Null means the server must answer <c>503 Service Unavailable</c>, which is what the
        /// specification prescribes for a device that cannot accept another subscriber.
        /// </remarks>
        EventSubscription? Add(string serviceId, IReadOnlyList<string> callbackUrls, TimeSpan granted);

        /// <summary>
        /// Extends an existing subscription, or returns null when the identifier is unknown or lapsed.
        /// </summary>
        EventSubscription? Renew(string sid, TimeSpan granted);

        /// <summary>
        /// Forgets a subscription. False when the identifier was not held.
        /// </summary>
        /// <remarks>
        /// The reference's UNSUBSCRIBE returns success while removing nothing, so a renderer that
        /// unsubscribes stays subscribed for the rest of its granted lifetime.
        /// </remarks>
        bool Remove(string sid);

        /// <summary>
        /// How many subscriptions are currently held, expired ones excluded.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Every live subscription, for the management endpoint.
        /// </summary>
        /// <remarks>
        /// Until now the store was written and never read, so a renderer's subscription was invisible
        /// from outside the process. The reference has a <c>/Manage/subscriptions</c> endpoint but it
        /// returns an empty <c>Ok()</c>, so this reports what that one only promises.
        /// </remarks>
        IReadOnlyList<EventSubscription> List();
    }
}
