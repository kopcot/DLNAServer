using System.Text.Json;
using System.Text.Json.Nodes;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Files;

namespace DlnaServer.Host.Configuration
{
    /// <inheritdoc cref="ISettingsWriter"/>
    internal sealed partial class SettingsWriter : ISettingsWriter, IDisposable
    {
        /// <remarks>
        /// Read-mutate-write over a single file, from a singleton two admin circuits can reach at once.
        /// Without this, concurrent saves raced on the same <c>.saving</c> temporary and the later
        /// <see cref="File.Move(string, string, bool)"/> could publish a half-written file - which the
        /// configuration provider is watching for and would read.
        /// </remarks>
        private readonly SemaphoreSlim _saveGate = new(1, 1);

        private readonly string _configurationPath;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SettingsWriter> _logger;

        public SettingsWriter(
            string configurationPath,
            TimeProvider timeProvider,
            ILogger<SettingsWriter> logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

            _configurationPath = configurationPath;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task SaveAsync(DlnaOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            await _saveGate.WaitAsync(cancellationToken);

            try
            {
                // Rewrites only the Dlna section, so anything else a deployment keeps in this file survives an
                // edit made through the admin UI.
                var root = await ReadRootAsync(cancellationToken);

                root[DlnaOptions.SectionName] = JsonSerializer.SerializeToNode(options, DlnaConfigurationJson.WriteOptions);

                Backup();

                var json = root.ToJsonString(DlnaConfigurationJson.WriteOptions);

                // Written to a temporary file and moved into place: the configuration provider watches this
                // path, and a partial write would be read as a corrupt file by the very reload it triggers.
                var temporaryPath = _configurationPath + ".saving";

                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
                File.Move(temporaryPath, _configurationPath, overwrite: true);

                LogSaved(_configurationPath);
            }
            finally
            {
                _ = _saveGate.Release();
            }
        }

        public void Dispose()
        {
            _saveGate.Dispose();
        }

        private async Task<JsonObject> ReadRootAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_configurationPath))
            {
                return new JsonObject();
            }

            try
            {
                var existing = await File.ReadAllTextAsync(_configurationPath, cancellationToken);

                // Same leniency the configuration system reads it with, so a hand-edited file carrying a
                // comment is preserved rather than silently discarded and rewritten from scratch.
                return JsonNode.Parse(existing, nodeOptions: null, DlnaConfigurationJson.ReadOptions) as JsonObject
                    ?? new JsonObject();
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                // A file that cannot be read is replaced rather than blocking the save. The guard that
                // runs at startup would have moved a corrupt file aside anyway.
                LogUnreadable(_configurationPath, exception.Message);

                return new JsonObject();
            }
        }

        private void Backup()
        {
            try
            {
                if (!File.Exists(_configurationPath))
                {
                    return;
                }

                // BackupFilePath.CreateSaved, not this class's own naming: both kinds of backup now go
                // into one 'backup' folder beside the original, and having two places that decide where
                // is how they end up disagreeing. It keeps the .saved- suffix, which is the distinction
                // that matters - .corrupt- means a file that could not be used, and calling a settings
                // file an operator deliberately replaced 'corrupt' would be a lie to whoever finds it.
                var backupPath = BackupFilePath.CreateSaved(
                    _configurationPath,
                    _timeProvider.GetUtcNow().UtcDateTime);

                File.Copy(_configurationPath, backupPath, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed backup must not stop the save: the operator asked for a change, and refusing
                // it because the previous file could not be copied would be the more surprising outcome.
                LogBackupFailed(exception.Message);
            }
        }
    }
}
