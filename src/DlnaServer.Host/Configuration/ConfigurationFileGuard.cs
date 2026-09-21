using System.Text.Json;
using System.Text.Json.Serialization;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Files;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Makes sure a readable <c>config.json</c> is in place before the configuration pipeline touches it.
    /// </summary>
    /// <remarks>
    /// Must run before <c>AddJsonFile</c>. The configuration provider throws on malformed JSON while the
    /// host is being built, which is too late to recover from - the process simply dies with a parse error.
    /// <para>
    /// Modelled on the reference's <c>JsonSerialization.LoadFromJsonOrCreateNew</c>, which backs the broken
    /// file up and writes defaults. Its <c>SaveToJson</c> used <c>FileMode.CreateNew</c>, which throws when
    /// the file already exists, so every save over an existing config fell into the catch and silently
    /// replaced the user's settings with defaults. This writes with <c>FileMode.Create</c>.
    /// </para>
    /// </remarks>
    internal static class ConfigurationFileGuard
    {
        // The shipped config.json's ports, not ServerOptions' defaults. See RecreateDefaults.
        private const int RecoveryMediaPort = 26_852;
        private const int RecoveryAdminPort = 26_853;


        /// <summary>
        /// Ensures <paramref name="filePath"/> exists and parses. Replaces it with defaults if it does not.
        /// </summary>
        public static ConfigurationFileResult EnsureUsable(string filePath, TimeProvider timeProvider)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(timeProvider);

            if (!File.Exists(filePath))
            {
                WriteDefaults(filePath);

                return new ConfigurationFileResult(
                    ConfigurationFileStatus.Created,
                    filePath,
                    BackupPath: null,
                    Reason: null);
            }

            var rejection = FindRejectionReason(filePath);

            if (rejection is null)
            {
                return new ConfigurationFileResult(
                    ConfigurationFileStatus.Valid,
                    filePath,
                    BackupPath: null,
                    Reason: null);
            }

            var backupPath = MoveAside(filePath, timeProvider);
            WriteDefaults(filePath);

            return new ConfigurationFileResult(
                ConfigurationFileStatus.Replaced,
                filePath,
                backupPath,
                rejection);
        }

        /// <summary>
        /// Returns why the file is unusable, or null when it is fine.
        /// </summary>
        /// <remarks>
        /// Checks only that the file reads and is well-formed JSON. It deliberately does NOT try to
        /// deserialize into the options types: the configuration binder accepts values that a strict
        /// deserializer rejects (a port written as <c>"26851"</c>, for example), so binding here would
        /// condemn a perfectly usable file. Wrong values are caught later by options validation, which
        /// reports them instead of overwriting them.
        /// </remarks>
        private static string? FindRejectionReason(string filePath)
        {
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                if (stream.Length == 0)
                {
                    return "the file is empty";
                }

                // Parsed the way the configuration system parses it, not the way System.Text.Json does by
                // default. Rejecting a comment or a trailing comma here condemned files the server would
                // have read, and the punishment is losing the operator's settings to a backup.
                using var document = JsonDocument.Parse(stream, DlnaConfigurationJson.ReadOptions);

                return document.RootElement.ValueKind == JsonValueKind.Object
                    ? null
                    : $"the root of the file is {document.RootElement.ValueKind}, not a JSON object";
            }
            catch (JsonException exception)
            {
                return $"the file is not valid JSON ({exception.Message})";
            }
            catch (IOException exception)
            {
                return $"the file could not be read ({exception.Message})";
            }
            catch (UnauthorizedAccessException exception)
            {
                return $"the file could not be read ({exception.Message})";
            }
        }

        private static string MoveAside(string filePath, TimeProvider timeProvider)
        {
            var backupPath = BackupFilePath.CreateUnique(filePath, timeProvider.GetUtcNow().UtcDateTime);

            // overwrite: false - the path is guaranteed free, so a collision here is a real problem.
            File.Move(filePath, backupPath, overwrite: false);

            return backupPath;
        }

        private static void WriteDefaults(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            // Not a bare new DlnaOptions(): ServerOptions defaults to 26851/26852, and 26851 is the port
            // the reference server listens on. Writing those into a recovered config.json made the next
            // start throw AddressAlreadyInUse on a machine running both, turning a damaged file into a
            // server that will not boot. These are the ports the shipped config.json uses.
            var options = new DlnaOptions();
            options.Server.Port = RecoveryMediaPort;
            options.Server.AdminPort = RecoveryAdminPort;

            var defaults = new Dictionary<string, DlnaOptions>(StringComparer.Ordinal)
            {
                [DlnaOptions.SectionName] = options,
            };

            // Create, not CreateNew - the file may already exist and must be overwritten.
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, defaults, DlnaConfigurationJson.WriteOptions);
        }
    }
}
