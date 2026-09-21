namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// Records which machine last opened the database.
    /// </summary>
    /// <remarks>
    /// A mismatch means the database file moved between machines, so the indexed absolute paths are
    /// unlikely to resolve. That is reported and left for an explicit reindex - the reference silently
    /// dropped and recreated the entire database.
    /// </remarks>
    internal sealed class ServerInstanceEntity : EntityBase
    {
        public required string MachineName { get; set; }

        public DateTime LastStartedUtc { get; set; }
    }
}
