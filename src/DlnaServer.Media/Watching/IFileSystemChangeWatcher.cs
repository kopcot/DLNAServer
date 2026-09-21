using System.Threading.Channels;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Watching;

namespace DlnaServer.Media.Watching
{
    /// <summary>
    /// Watches the source folders and publishes filesystem changes.
    /// </summary>
    /// <remarks>
    /// Reports raw events. Debouncing, coalescing and deciding what to do about them belong to the
    /// consumer, which keeps this side free of timing logic and therefore testable.
    /// </remarks>
    public interface IFileSystemChangeWatcher
    {
        /// <summary>
        /// Stream of observed changes.
        /// </summary>
        ChannelReader<FileChangeEvent> Events { get; }

        /// <summary>
        /// Attaches a watch to each source folder that exists, and returns the ones it attached to.
        /// </summary>
        /// <remarks>
        /// A folder that is absent is skipped rather than refused, because one unmounted share must not
        /// stop the rest of the library being watched. The caller is told which ones attached so it can
        /// tell "watching everything asked for" from "watching what happened to be there" - without
        /// that it compared its own request against its own previous request, which always matched, and
        /// a folder missing at startup was never picked up again.
        /// </remarks>
        IReadOnlyList<string> Start(IReadOnlyList<string> sourceFolders, IReadOnlyList<string> excludedFolderNames);

        /// <summary>
        /// Tears the current watches down and starts fresh ones over the folders given.
        /// </summary>
        /// <remarks>
        /// Two callers, two reasons. A watcher whose <c>Error</c> fired may be dead rather than merely
        /// behind: on Linux an inotify queue overflow tears the running instance down, and the folder was
        /// then deaf for the rest of the process lifetime after a single log line - new media never
        /// appeared and deletions never pruned. And <c>SourceFolders</c>/<c>ExcludeFolders</c> are
        /// hot-reloadable, so a folder added through the admin UI has to be picked up here too; it used to
        /// be indexed by the next scan but never watched.
        /// <para>
        /// The channel survives, deliberately. Disposing this watcher completes the writer, which would
        /// end the consumer for good - a restart has to leave the stream open.
        /// </para>
        /// </remarks>
        IReadOnlyList<string> Restart(IReadOnlyList<string> sourceFolders, IReadOnlyList<string> excludedFolderNames);

        /// <summary>
        /// Returns true once when events have been lost and a full rescan is needed, clearing the request.
        /// </summary>
        /// <remarks>
        /// Set when the queue fills or the OS watcher buffer overflows. Individual events cannot be
        /// trusted after either, so the index is rebuilt from a scan rather than left to drift.
        /// </remarks>
        bool ConsumeResyncRequest();

        /// <summary>
        /// Asks for the rescan again, for a caller whose own attempt at one failed.
        /// </summary>
        /// <remarks>
        /// <see cref="ConsumeResyncRequest"/> clears the flag before the scan it triggers has run, so a
        /// pass that failed on a transient error dropped the request permanently and the index stayed
        /// wrong with no further signal. The pending-event set beside it already handles failure this way.
        /// </remarks>
        void RequestResync();

        /// <summary>
        /// Returns true once when a watch has faulted and needs rebuilding, clearing the request.
        /// </summary>
        /// <remarks>
        /// Separate from the resync request because they are different repairs: one rebuilds the index,
        /// the other rebuilds the watch that feeds it. Consumed by the owning service rather than acted
        /// on inline, so the teardown never runs on the callback thread of the watcher being torn down.
        /// </remarks>
        bool ConsumeRestartRequest();
    }
}
