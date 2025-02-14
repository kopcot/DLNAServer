namespace DLNAServer.Features.Subscriptions.Data
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public class Subscription
    {
        public string SID { get; set; }
        public string Callback { get; set; }
        public TimeSpan Timeout { get; set; }
        public DateTime LastNotifyTimeUtc { get; set; }
        public bool IsExpired() => DateTime.UtcNow >= LastNotifyTimeUtc.Add(Timeout);
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
