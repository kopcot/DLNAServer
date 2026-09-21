namespace DlnaServer.Persistence
{
    /// <summary>
    /// Brings the database into a usable state at startup and reports on the machine that owns it.
    /// </summary>
    /// <remarks>
    /// Exists because <c>DlnaDbContext</c> is internal to this assembly - the host cannot reach it to
    /// run migrations itself, which is the point of the entity boundary.
    /// </remarks>
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Ensures a healthy, migrated database exists.
        /// </summary>
        /// <remarks>
        /// Creates one when the file is missing, and rebuilds it from scratch when the existing file
        /// fails its integrity check or cannot be opened. The unusable file is moved aside rather than
        /// deleted. Recreating is safe here because the index is derived data: rescanning the source
        /// folders restores it. This is not the same as the reference's behaviour, which also wiped a
        /// perfectly healthy database whenever the recorded machine name changed.
        /// </remarks>
        Task<DatabaseInitializationResult> InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Machine recorded by the previous startup, or null when the database is new.
        /// </summary>
        Task<string?> GetLastMachineNameAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Records this machine as the most recent to open the database.
        /// </summary>
        Task RecordStartupAsync(string machineName, CancellationToken cancellationToken = default);
    }
}
