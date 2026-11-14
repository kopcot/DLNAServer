using DlnaServer.Core.Files;
using DlnaServer.Host.Configuration;
using Microsoft.Data.Sqlite;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// The files the server writes for itself - its database and its settings - which a watch over the
    /// folder they live in reports like any other change.
    /// </summary>
    /// <remarks>
    /// Resolved once, from the same configuration file path and connection string the server opens, so
    /// a moved database or a <c>DLNA_CONFIG_FILE</c> is followed rather than guessed at. None of these can
    /// change what the library holds, whatever happened to them - deletes included: SQLite removes its
    /// <c>-wal</c> and <c>-shm</c> when the last connection closes, and the pass a delete scheduled
    /// reopened the database and recreated them.
    /// <para>
    /// Not expressed through <c>Library.ExcludeFolders</c>: those are folders, the database is a file, and
    /// the Settings page writes that list back into <c>config.json</c> on every save.
    /// </para>
    /// </remarks>
    internal sealed class ServerOwnedFiles
    {
        // What SQLite writes beside the database file: the write-ahead log, its shared-memory index, and the
        // rollback journal used outside WAL mode.
        private static readonly string[] _databaseSidecarSuffixes = ["-wal", "-shm", "-journal"];

        private readonly HashSet<string> _paths = new(StringComparer.Ordinal);

        // Path prefixes of the backups BackupFilePath writes for each of the files above.
        private readonly List<string> _backupPrefixes = new(capacity: 2);

        /// <param name="configurationPath">The <c>config.json</c> the server reads and the Settings page saves.</param>
        /// <param name="databaseConnectionString">The connection string the database is opened with.</param>
        public ServerOwnedFiles(
            string configurationPath,
            string databaseConnectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
            ArgumentNullException.ThrowIfNull(databaseConnectionString);

            var settingsPath = Path.GetFullPath(configurationPath);

            _ = _paths.Add(settingsPath);
            _ = _paths.Add(settingsPath + SettingsWriter.TemporaryFileSuffix);
            _backupPrefixes.Add(BackupPrefix(settingsPath));

            // The same resolution DatabaseInitializer makes: relative to the working directory, which Program
            // has already set to the application folder, and nothing on disc for an in-memory database.
            var dataSource = new SqliteConnectionStringBuilder(databaseConnectionString).DataSource;

            if (string.IsNullOrWhiteSpace(dataSource) || dataSource.StartsWith(":memory:", StringComparison.Ordinal))
            {
                return;
            }

            var databasePath = Path.GetFullPath(dataSource);

            _ = _paths.Add(databasePath);
            _backupPrefixes.Add(BackupPrefix(databasePath));

            foreach (var suffix in _databaseSidecarSuffixes)
            {
                _ = _paths.Add(databasePath + suffix);
            }
        }

        /// <summary>
        /// Whether <paramref name="fullPath"/> is one of the server's own files or a backup of one.
        /// </summary>
        public bool Contains(string fullPath)
        {
            ArgumentNullException.ThrowIfNull(fullPath);

            if (_paths.Contains(fullPath))
            {
                return true;
            }

            foreach (var prefix in _backupPrefixes)
            {
                if (fullPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // backup/<name>. - covers both kinds BackupFilePath writes, <name>.saved-<stamp> and <name>.corrupt-<stamp>.
        private static string BackupPrefix(string fullPath)
        {
            return Path.Combine(
                Path.GetDirectoryName(fullPath) ?? string.Empty,
                BackupFilePath.FolderName,
                Path.GetFileName(fullPath) + ".");
        }
    }
}
