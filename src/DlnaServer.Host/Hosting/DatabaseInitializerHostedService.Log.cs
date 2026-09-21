namespace DlnaServer.Host.Hosting
{
    internal sealed partial class DatabaseInitializerHostedService
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Applied {MigrationCount} pending database migration(s)")]
        private partial void LogMigrationsApplied(int migrationCount);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Database was last opened on '{PreviousMachineName}' but this machine is '{CurrentMachineName}'. "
                + "Indexed paths may not resolve here. The index is left untouched - reindex explicitly if it is wrong.")]
        private partial void LogMachineNameChanged(string previousMachineName, string currentMachineName);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "Database ready on {MachineName}")]
        private partial void LogDatabaseReady(string machineName);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "No database found. Created an empty one - the library will be indexed on the next scan.")]
        private partial void LogDatabaseCreated();

        /// <remarks>
        /// Warning, and distinct from every other outcome. Recreating the database is the most
        /// destructive thing the admin UI can do - it discards every PublicId a renderer has cached and
        /// costs a full rescan - and it used to fall into the same silent branch as an ordinary start, so
        /// the log gave no way to tell afterwards whether it had happened at all.
        /// </remarks>
        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "The database was deleted and rebuilt empty because Recreate database was requested. "
                + "Every file will be indexed again and given a new identifier.")]
        private partial void LogDatabaseReset();

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Database was unusable and has been rebuilt empty. The damaged file was kept at '{BackupPath}'. "
                + "The library will be re-indexed on the next scan.")]
        private partial void LogDatabaseRecreated(string backupPath);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Critical,
            Message = "Database initialisation failed, so the server is stopping rather than serving. "
                + "Nothing that answers a renderer or the admin UI can work without a schema, and "
                + "continuing would look like an empty library instead of a failure.")]
        private partial void LogInitialisationFailed(Exception exception);
    }
}
