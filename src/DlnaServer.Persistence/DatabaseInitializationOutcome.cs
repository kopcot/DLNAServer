namespace DlnaServer.Persistence
{
    /// <summary>
    /// What the initializer had to do to reach a usable database.
    /// </summary>
    public enum DatabaseInitializationOutcome
    {
        /// <summary>
        /// An existing, healthy database was opened and any pending migrations applied.
        /// </summary>
        Opened = 1,

        /// <summary>
        /// No database file was present, so an empty one was created from the migrations.
        /// </summary>
        Created = 2,

        /// <summary>
        /// The existing database failed its integrity check or could not be opened, so it was moved
        /// aside and rebuilt empty. The index is derived data - a rescan restores it.
        /// </summary>
        Recreated = 3,

        /// <summary>
        /// A reset was asked for, so the previous database was deleted and an empty one built from the
        /// migrations. Distinct from <see cref="Recreated"/> because nothing was wrong with it: the file
        /// is deleted rather than kept, since an operator who asked for a clean database does not want
        /// the old one left behind taking up the space they were reclaiming.
        /// </summary>
        Reset = 4,
    }
}
