using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// A <c>config.json</c> source whose provider keeps the last values it read successfully.
    /// </summary>
    /// <remarks>
    /// Exists because <c>OnLoadException</c> with <c>context.Ignore = true</c> does NOT keep them, which
    /// is what the code here used to rely on. <c>FileConfigurationProvider.Load(reload: true)</c>
    /// replaces <c>Data</c> with an empty dictionary <b>before</b> it consults
    /// <c>OnLoadException</c>, and raises its change token afterwards either way - so ignoring the
    /// failure only suppresses the rethrow, and every <c>Dlna:*</c> key is gone by the time anything
    /// looks. Measured: rewriting the file as <c>{ this is not json</c> made
    /// <c>configuration["Dlna:Server:Port"]</c> return null while the callback fired.
    /// <para>
    /// That mattered far more than a wrong port. With the section empty, <c>DlnaOptionsDefaults</c>
    /// falls <c>SourceFolders</c> back to the application's own folder and validation <b>passes</b> -
    /// so the file watcher moves onto the publish folder, its own log writes settle, a scan runs, and
    /// <c>ReconcileDirectoriesAsync</c> deletes every indexed directory whose path no longer sits
    /// inside a source folder, cascading to the files and taking their <c>PublicId</c> and
    /// <c>CreatedUtc</c> with it. A trailing comma typed over SSH cost the whole index.
    /// </para>
    /// </remarks>
    internal sealed class LastGoodJsonConfigurationSource : JsonConfigurationSource
    {
        /// <summary>
        /// Set by the load-exception handler when a re-read failed, and cleared before each load.
        /// </summary>
        /// <remarks>
        /// Lives on the source rather than the provider because the handler is wired at configuration
        /// time, before <see cref="Build"/> has created a provider to record it on.
        /// </remarks>
        public bool LoadFailed { get; set; }

        /// <summary>
        /// Called with the reason when a failed re-read was answered with the previous values.
        /// </summary>
        public Action<string>? OnValuesRetained { get; set; }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);

            return new LastGoodJsonConfigurationProvider(this);
        }
    }
}
