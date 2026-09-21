namespace DlnaServer.Core.Gena
{
    /// <summary>
    /// One renderer's live subscription to one service's events.
    /// </summary>
    /// <param name="Sid">Identifier the renderer quotes on renewal and on UNSUBSCRIBE.</param>
    /// <param name="ServiceId">Service subscribed to, from the <c>eventSubURL</c> that was called.</param>
    /// <param name="CallbackUrls">Where NOTIFY messages would be delivered.</param>
    /// <param name="Granted">Lifetime granted, which may be less than was requested.</param>
    /// <param name="ExpiresUtc">When the subscription lapses unless renewed.</param>
    public sealed record EventSubscription(
        string Sid,
        string ServiceId,
        IReadOnlyList<string> CallbackUrls,
        TimeSpan Granted,
        DateTimeOffset ExpiresUtc);
}
