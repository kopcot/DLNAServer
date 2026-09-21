namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Outcome of checking the configuration file before the host reads it.
    /// </summary>
    /// <param name="Status">What had to be done.</param>
    /// <param name="FilePath">The configuration file that is now in place.</param>
    /// <param name="BackupPath">
    /// Where the unreadable file was moved, when <paramref name="Status"/> is
    /// <see cref="ConfigurationFileStatus.Replaced"/>. Null otherwise. The original is kept rather than
    /// deleted so settings can be recovered by hand.
    /// </param>
    /// <param name="Reason">Why the file was rejected, for the log. Null when nothing was wrong.</param>
    public sealed record ConfigurationFileResult(
        ConfigurationFileStatus Status,
        string FilePath,
        string? BackupPath,
        string? Reason);
}
