using Microsoft.Extensions.Configuration.Json;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Reads <c>config.json</c>, and answers a failed re-read with the values from the last good one.
    /// </summary>
    /// <remarks>
    /// See <see cref="LastGoodJsonConfigurationSource"/> for why this exists at all. The restore has to
    /// happen here rather than in the load-exception handler: by the time that handler runs, the base
    /// class has already replaced <c>Data</c>, so the previous values are only reachable to something
    /// holding a reference from before <c>base.Load()</c>.
    /// </remarks>
    internal sealed class LastGoodJsonConfigurationProvider : JsonConfigurationProvider
    {
        public LastGoodJsonConfigurationProvider(LastGoodJsonConfigurationSource source)
            : base(source)
        {
        }

        private new LastGoodJsonConfigurationSource Source => (LastGoodJsonConfigurationSource)base.Source;

        public override void Load()
        {
            // Captured before the base class runs, because every one of its paths - parsed, missing and
            // failed - assigns a NEW dictionary rather than mutating this one, so the reference stays
            // valid and holds what was last read successfully.
            var previous = Data;

            Source.LoadFailed = false;

            base.Load();

            var reason = FindLostDataReason();

            if (reason is null || previous.Count == 0)
            {
                return;
            }

            Data = previous;

            Source.OnValuesRetained?.Invoke(reason);

            // Raised a second time, on purpose. The base class already raised it while Data was empty,
            // so every consumer has just rebound onto code defaults; without this they would hold that
            // until the file changed again. Nothing in this application subscribes to
            // IOptionsMonitor.OnChange, so the only observer is the options cache, which this simply
            // makes re-read.
            OnReload();
        }

        /// <summary>
        /// Why this load produced nothing usable, or null when it read the file normally.
        /// </summary>
        /// <remarks>
        /// A file that genuinely contains <c>{}</c> is NOT a lost load - it parses, and an operator who
        /// empties the file is asking for defaults. That is the whole reason this asks two specific
        /// questions instead of testing whether <c>Data</c> came back empty.
        /// </remarks>
        private string? FindLostDataReason()
        {
            if (Source.LoadFailed)
            {
                return "it could not be parsed";
            }

            // The silent variant, and the more dangerous one: a missing file on RELOAD raises no
            // exception at all, because Load(reload: true) treats it as optional - so the load-exception
            // handler never runs and nothing is reported. An ordinary editor save reaches it, not just a
            // deletion: vim's default backupcopy=auto writes a temporary file and renames it over the
            // target, which the watcher sees as a delete followed by a create.
            var file = Source.FileProvider?.GetFileInfo(Source.Path!);

            return file is null || !file.Exists
                ? "it was missing when the change was noticed, which an ordinary editor save can cause"
                : null;
        }
    }
}
