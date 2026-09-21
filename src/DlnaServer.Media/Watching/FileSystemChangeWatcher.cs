using System.Collections.Concurrent;
using System.Threading.Channels;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Watching;
using DlnaServer.Core.Files;

namespace DlnaServer.Media.Watching
{
    /// <inheritdoc cref="IFileSystemChangeWatcher"/>
    internal sealed partial class FileSystemChangeWatcher : IFileSystemChangeWatcher, IDisposable
    {
        /// <summary>
        /// Upper bound on queued events.
        /// </summary>
        /// <remarks>
        /// Bounded on purpose. The reference used an unbounded channel, so copying a few thousand files
        /// into a watched folder queued an unbounded number of events and the memory to hold them.
        /// When this fills, further events are dropped and a full rescan is requested instead - losing
        /// individual events is recoverable, unbounded growth on a memory-constrained NAS is not.
        /// </remarks>
        private const int QueueCapacity = 10_000;

        /// <summary>
        /// Raised events land in the OS buffer first; a larger buffer loses fewer of them during a burst.
        /// </summary>
        private const int WatcherBufferSize = 64 * 1024;

        // ConcurrentBag, not List: Start appends from the caller's thread while Dispose enumerates from
        // whichever thread the container disposes on, and a fast stop threw "Collection was modified" out
        // of ServiceProvider.DisposeAsync while leaking the inotify handle of any watcher added after the
        // snapshot - which matters on the restart path.
        private readonly ConcurrentBag<FileSystemWatcher> _watchers = [];

        private readonly Channel<FileChangeEvent> _channel;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<FileSystemChangeWatcher> _logger;
        private int _resyncRequested;
        private int _restartRequested;

        public FileSystemChangeWatcher(TimeProvider timeProvider, ILogger<FileSystemChangeWatcher> logger)
        {
            _timeProvider = timeProvider;
            _logger = logger;

            _channel = Channel.CreateBounded<FileChangeEvent>(
                new BoundedChannelOptions(QueueCapacity)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    // Wait, not DropWrite. The dropping modes make TryWrite return TRUE while discarding
                    // the item, so the queue-full branch below - the one that asks for a full rescan -
                    // could never run: a bulk copy past QueueCapacity silently lost events and the index
                    // drifted with nothing logged. TryWrite never blocks on any FullMode, so this is safe
                    // to call from a FileSystemWatcher callback thread. MediaCacheBacklog documents the
                    // same trap and has always used Wait.
                    FullMode = BoundedChannelFullMode.Wait,
                });
        }

        public ChannelReader<FileChangeEvent> Events => _channel.Reader;

        public bool ConsumeResyncRequest()
        {
            return Interlocked.Exchange(ref _resyncRequested, 0) != 0;
        }

        public bool ConsumeRestartRequest()
        {
            return Interlocked.Exchange(ref _restartRequested, 0) != 0;
        }

        public IReadOnlyList<string> Restart(
            IReadOnlyList<string> sourceFolders,
            IReadOnlyList<string> excludedFolderNames)
        {
            StopWatchers();

            return Start(sourceFolders, excludedFolderNames);
        }

        public IReadOnlyList<string> Start(
            IReadOnlyList<string> sourceFolders,
            IReadOnlyList<string> excludedFolderNames)
        {
            ArgumentNullException.ThrowIfNull(sourceFolders);
            ArgumentNullException.ThrowIfNull(excludedFolderNames);

            // Returned rather than discarded, because a folder that is absent right now is skipped and
            // the caller had no way to find out. Its own record of what it asked for then matched what it
            // asked for last time, forever, so the watch was never rebuilt and that folder stayed deaf
            // for the life of the process - with nothing scheduled to notice, since UsePeriodicRescan
            // ships off.
            var attached = new List<string>(sourceFolders.Count);

            foreach (var folder in sourceFolders)
            {
                if (!Directory.Exists(folder))
                {
                    LogFolderNotAttached(folder);
                    continue;
                }

                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    // Honoured only by the Windows ReadDirectoryChangesW backend; the Linux inotify
            // implementation ignores it entirely, so in production the burst-loss mitigation is the
            // resync escalation below, not this.
            InternalBufferSize = WatcherBufferSize,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                };

                watcher.Created += (_, args) => Publish(args.FullPath, null, FileChangeKind.CreatedOrChanged, excludedFolderNames);
                watcher.Changed += (_, args) => Publish(args.FullPath, null, FileChangeKind.CreatedOrChanged, excludedFolderNames);
                watcher.Deleted += (_, args) => Publish(args.FullPath, null, FileChangeKind.Deleted, excludedFolderNames);
                watcher.Renamed += (_, args) => Publish(args.FullPath, args.OldFullPath, FileChangeKind.Renamed, excludedFolderNames);
                watcher.Error += (_, args) => OnWatcherError(folder, args.GetException());

                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
                attached.Add(folder);

                LogWatching(folder);
            }

            return attached;
        }

        public void Dispose()
        {
            StopWatchers();

            // Only on disposal, never on a restart: completing the writer ends the consumer's stream for
            // good, and a rebuilt watch has to keep feeding the same reader.
            _ = _channel.Writer.TryComplete();
        }

        private void StopWatchers()
        {
            // Drains the bag rather than iterating then clearing. Start appends from the caller's thread,
            // so a snapshot taken here could miss a watcher added meanwhile and leak its inotify handle -
            // the same race the ConcurrentBag comment above records for Dispose.
            while (_watchers.TryTake(out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }

        // internal rather than private so the queue-full escalation can be tested. That escalation was
        // unreachable dead code, and this project's own rule is that a guard must be observed working -
        // driving it through a real FileSystemWatcher would mean a flaky, timing-dependent test.
        internal void Publish(
            string fullPath,
            string? oldFullPath,
            FileChangeKind kind,
            IReadOnlyList<string> excludedFolderNames)
        {
            if (PathExclusion.IsExcluded(fullPath, excludedFolderNames))
            {
                // The destination is hidden, so there is nothing to announce arriving - but a rename OUT
                // of the library still has to be announced leaving, and testing only the new path threw
                // the whole event away. That is how a QNAP delete looks: the file is moved into
                // @Recycle, a default exclusion that lives INSIDE the source folder and therefore inside
                // the watched tree, so inotify reports MOVED_FROM/MOVED_TO as one Renamed event. The row
                // survived, and the item stayed listed and unplayable on the television until some
                // unrelated change happened to trigger a full pass.
                if (oldFullPath is not null && !PathExclusion.IsExcluded(oldFullPath, excludedFolderNames))
                {
                    Write(new FileChangeEvent(oldFullPath, null, FileChangeKind.Deleted, _timeProvider.GetTimestamp()));
                }

                return;
            }

            // _timeProvider, not DateTime.UtcNow: the settle window is measured against this stamp, so
            // mixing the two left it as the one part of this service a test clock could not drive. A
            // monotonic reading rather than the wall clock, because an NTP step must not decide whether
            // a file has finished copying - see FileChangeEvent.
            Write(new FileChangeEvent(fullPath, oldFullPath, kind, _timeProvider.GetTimestamp()));
        }

        private void Write(FileChangeEvent raised)
        {
            if (_channel.Writer.TryWrite(raised))
            {
                return;
            }

            // The queue is full. Individual events are now unreliable, so ask for a full rescan rather
            // than let the index drift silently.
            RequestResync();
        }

        /// <summary>
        /// The OS buffer overflowed, so an unknown number of events were lost.
        /// </summary>
        private void OnWatcherError(string folder, Exception exception)
        {
            LogWatcherError(folder, exception.Message);
            RequestResync();

            // The watch itself may be dead, not merely behind. On Linux an inotify queue overflow tears
            // the running instance down, and the folder was then deaf for the rest of the process
            // lifetime - one log line, then new media silently stopped appearing. Requested rather than
            // done here: this runs on the callback thread of the watcher that would be disposed.
            if (Interlocked.Exchange(ref _restartRequested, 1) == 0)
            {
                LogRestartRequested(folder);
            }
        }

        public void RequestResync()
        {
            if (Interlocked.Exchange(ref _resyncRequested, 1) == 0)
            {
                LogResyncRequested();
            }
        }
    }
}
