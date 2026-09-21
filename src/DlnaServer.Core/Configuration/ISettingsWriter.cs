namespace DlnaServer.Core.Configuration
{
    /// <summary>
    /// Saves edited settings back to <c>config.json</c>.
    /// </summary>
    /// <remarks>
    /// The file is the source of truth, not the running options object: writing it is what makes a change
    /// survive a restart, and the configuration provider watches the file, so a successful write is also
    /// what applies the change to the running server.
    /// <para>
    /// Not every setting takes effect immediately. <c>FileCache.MaxTotalSizeInMegabytes</c> is fixed for
    /// the life of the cache store and the two ports are read before the host exists, so those need a
    /// restart - which is why the admin UI says so beside them rather than implying otherwise.
    /// </para>
    /// </remarks>
    public interface ISettingsWriter
    {
        /// <summary>
        /// Writes the options to the configuration file, replacing the <c>Dlna</c> section.
        /// </summary>
        /// <remarks>
        /// The previous file is backed up first, so a bad edit is recoverable by hand. Validation is the
        /// caller's job: this writes what it is given.
        /// </remarks>
        Task SaveAsync(DlnaOptions options, CancellationToken cancellationToken = default);
    }
}
