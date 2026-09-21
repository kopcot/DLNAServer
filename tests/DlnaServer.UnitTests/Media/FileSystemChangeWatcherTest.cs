using DlnaServer.Core.Contracts.Watching;
using DlnaServer.Media.Watching;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// Covers the queue-full escalation, which was unreachable.
    /// </summary>
    /// <remarks>
    /// The channel was created with <c>BoundedChannelFullMode.DropWrite</c>, which makes
    /// <c>TryWrite</c> return <b>true</b> while discarding the item - so the <c>RequestResync()</c> call
    /// behind the failed-write branch could never run, and a bulk copy past the queue capacity dropped
    /// events with nothing asking for a rescan. The sibling <c>MediaCacheBacklog</c> documents this exact
    /// trap and gets it right, which is what made the omission worth a test rather than just a fix.
    /// </remarks>
    [TestFixture]
    internal sealed class FileSystemChangeWatcherTest
    {
        [Test]
        public void Publish_WhenTheQueueIsFull_AsksForAFullRescan()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act
            // Nothing drains the reader, so this fills the bounded channel and then overflows it. The
            // count is above QueueCapacity by design - the escalation is what happens after capacity.
            for (var index = 0; index < 10_050; index++)
            {
                watcher.Publish(
                    $"/media/movies/file-{index}.mkv",
                    oldFullPath: null,
                    kind: FileChangeKind.CreatedOrChanged,
                    excludedFolderNames: []);
            }

            // Assert
            watcher.ConsumeResyncRequest().Should().BeTrue(
                "because once individual events start being dropped the only correct recovery is a full "
                + "rescan, and DropWrite hides the drop from TryWrite");
        }

        /// <summary>
        /// Deleting a file on a QNAP moves it into <c>@Recycle</c>, which is an excluded folder living
        /// inside the watched tree - so inotify reports the whole thing as one rename.
        /// </summary>
        /// <remarks>
        /// The exclusion was tested against the destination only, so the event was dropped whole and the
        /// departure from the library was never announced. The row survived, and the item stayed listed
        /// on the television and refused to play until some unrelated change triggered a full pass.
        /// </remarks>
        [Test]
        public void Publish_WhenARenameLandsInAnExcludedFolder_StillAnnouncesTheDeparture()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act
            watcher.Publish(
                fullPath: "/share/Media/@Recycle/film.mkv",
                oldFullPath: "/share/Media/Films/film.mkv",
                kind: FileChangeKind.Renamed,
                excludedFolderNames: ["@Recycle"]);

            // Assert
            watcher.Events.TryRead(out var raised).Should().BeTrue(
                "because the file left the library, and testing only where it went threw that away");

            raised!.FullPath.Should().Be("/share/Media/Films/film.mkv",
                "because the event that matters is the departure from the visible path, not the arrival "
                + "at the hidden one");
            raised.Kind.Should().Be(FileChangeKind.Deleted,
                "because from the library's point of view the file is gone");
            raised.OldFullPath.Should().BeNull(
                "because there is no rename to report - the destination is not part of the library");
        }

        [Test]
        public void Publish_WhenBothEndsOfARenameAreExcluded_AnnouncesNothing()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act
            watcher.Publish(
                fullPath: "/share/Media/@Recycle/b.mkv",
                oldFullPath: "/share/Media/@Recycle/a.mkv",
                kind: FileChangeKind.Renamed,
                excludedFolderNames: ["@Recycle"]);

            // Assert
            watcher.Events.TryRead(out _).Should().BeFalse(
                "because nothing the library can see has changed, and waking a scan for it would undo "
                + "the point of excluding the folder");
        }

        [Test]
        public void Start_ReturnsOnlyTheFoldersItAttachedTo()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            var present = Directory.CreateTempSubdirectory("dlna-watch-").FullName;
            var absent = Path.Combine(present, "not-mounted-yet");

            try
            {
                // Act
                var attached = watcher.Start([present, absent], excludedFolderNames: []);

                // Assert
                attached.Should().BeEquivalentTo([present],
                    "because a folder that is not there cannot be watched, and the caller has to be able "
                    + "to tell that from having watched everything it asked for - comparing its own "
                    + "request against its own previous request always matched, so the folder stayed "
                    + "deaf for the life of the process");
            }
            finally
            {
                Directory.Delete(present, recursive: true);
            }
        }

        /// <summary>
        /// A restart has to leave the event stream open. Disposing completes the channel writer, so
        /// rebuilding a faulted watch by disposing and re-creating would have ended the consumer for
        /// good - the watch would come back and nothing would ever read from it again.
        /// </summary>
        [Test]
        public void Restart_KeepsTheEventStreamOpen()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act - no folders, so nothing is actually watched; the channel is what is under test.
            watcher.Restart(sourceFolders: [], excludedFolderNames: []);

            watcher.Publish(
                "/media/movies/after-restart.mkv",
                oldFullPath: null,
                kind: FileChangeKind.CreatedOrChanged,
                excludedFolderNames: []);

            // Assert
            watcher.Events.TryRead(out var raised).Should().BeTrue(
                "because a rebuilt watch writes to the same reader the consumer is already draining");
            raised!.FullPath.Should().Be("/media/movies/after-restart.mkv",
                "because the event published after the restart is the one that must arrive");
        }

        [Test]
        public void ConsumeRestartRequest_WithNoFault_IsFalse()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act
            var requested = watcher.ConsumeRestartRequest();

            // Assert
            requested.Should().BeFalse(
                "because a healthy watch must not be torn down and rebuilt on every tick");
        }

        [Test]
        public void RequestResync_AfterConsuming_AsksAgain()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            watcher.RequestResync();
            _ = watcher.ConsumeResyncRequest();

            // Act - what the consumer does when the scan it triggered failed.
            watcher.RequestResync();

            // Assert
            watcher.ConsumeResyncRequest().Should().BeTrue(
                "because consuming clears the request before the scan has run, so a failed pass would "
                + "otherwise drop it for good and leave the index wrong with no further signal");
        }

        [Test]
        public void ConsumeResyncRequest_WithNothingDropped_ReturnsFalse()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            // Act
            watcher.Publish(
                "/media/movies/one.mkv",
                oldFullPath: null,
                kind: FileChangeKind.CreatedOrChanged,
                excludedFolderNames: []);

            // Assert
            watcher.ConsumeResyncRequest().Should().BeFalse(
                "because a single event fits in the queue and needs no rescan");
        }

        [Test]
        public void ConsumeResyncRequest_ClearsTheFlag()
        {
            // Arrange
            using var watcher = new FileSystemChangeWatcher(
                TimeProvider.System,
                NullLogger<FileSystemChangeWatcher>.Instance);

            for (var index = 0; index < 10_050; index++)
            {
                watcher.Publish(
                    $"/media/movies/file-{index}.mkv",
                    oldFullPath: null,
                    kind: FileChangeKind.CreatedOrChanged,
                    excludedFolderNames: []);
            }

            // Act
            var first = watcher.ConsumeResyncRequest();
            var second = watcher.ConsumeResyncRequest();

            // Assert
            first.Should().BeTrue("because a rescan was requested");
            second.Should().BeFalse(
                "because consuming the request must clear it, or every later tick would rescan the "
                + "whole library for one historical overflow");
        }
    }
}
