using DlnaServer.Core.Configuration;
using DlnaServer.Core.Delivery;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Scanning;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Configuration;
using DlnaServer.Media.Scanning;
using DlnaServer.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Indexing
{
    /// <summary>
    /// Brings the index into line with what is on disk.
    /// </summary>
    /// <remarks>
    /// Orchestration lives in the host because it needs both the scanner and the repositories, and the
    /// architecture rules keep <c>Media</c> and <c>Persistence</c> from referencing each other.
    /// </remarks>
    internal sealed partial class LibraryIndexer : ILibraryIndexer
    {
        /// <summary>
        /// Rows written per round trip. Large enough to amortise the transaction, small enough that a
        /// 20,000-file library never sits in memory at once.
        /// </summary>
        private const int BatchSize = 500;

        /// <summary>
        /// How many leaf-removal passes <see cref="PruneEmptyDirectoriesAsync"/> will make.
        /// </summary>
        /// <remarks>
        /// One pass collapses one level, so this is a folder-depth ceiling, not a limit anyone should
        /// reach - the deepest empty chain seen on the real library is three. It is here so that a cycle
        /// in the parent data, which nothing enforces against, cannot spin inside a scan.
        /// </remarks>
        private const int MaxPrunePasses = 64;

        private readonly ILibraryScanner _scanner;
        private readonly IMediaDirectoryRepository _directories;
        private readonly IMediaFileRepository _files;
        private readonly ISourceFolderChecker _sourceFolders;
        private readonly IServedFileCache _fileCache;
        private readonly ILibraryIndexLock _indexLock;
        private readonly ILibraryChangeSignal _changes;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ILogger<LibraryIndexer> _logger;

        public LibraryIndexer(
            ILibraryScanner scanner,
            IMediaDirectoryRepository directories,
            IMediaFileRepository files,
            ISourceFolderChecker sourceFolders,
            IServedFileCache fileCache,
            ILibraryIndexLock indexLock,
            ILibraryChangeSignal changes,
            IOptionsMonitor<DlnaOptions> options,
            ILogger<LibraryIndexer> logger)
        {
            _scanner = scanner;
            _directories = directories;
            _files = files;
            _sourceFolders = sourceFolders;
            _fileCache = fileCache;
            _indexLock = indexLock;
            _changes = changes;
            _options = options;
            _logger = logger;
        }

        /// <remarks>
        /// The lock was a private static <c>SemaphoreSlim</c> here. It is
        /// <see cref="ILibraryIndexLock"/> now because a third caller needs it and cannot reach a private
        /// static: the admin UI's <i>Rebuild index</i> deletes every row and runs <c>VACUUM</c>, which was
        /// able to run straight into a scan already in flight.
        /// <para>
        /// Every pass marks <see cref="ILibraryChangeSignal"/>, whatever it found and however it ended: a
        /// pass that changed nothing costs a reader one recount, and one that failed half-way may still
        /// have inserted or removed rows before it did.
        /// </para>
        /// </remarks>
        public async Task<LibraryIndexResult> IndexAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _indexLock.RunAsync(IndexUnderGateAsync, cancellationToken);
            }
            finally
            {
                _changes.MarkChanged();
            }
        }

        private async Task<LibraryIndexResult> IndexUnderGateAsync(CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            var scanOptions = BuildScanOptions(options);

            LogScanStarted(scanOptions.SourceFolders.Count);

            // Before anything is inserted: which source folders had never been indexed. Files found
            // under one of those are a bulk first-time import, not arrivals, and take their index date
            // from the filesystem - see ResolveFirstFillRootsAsync.
            var firstFillRoots = await ResolveFirstFillRootsAsync(scanOptions, cancellationToken);

            var directoryKeys = await IndexDirectoriesAsync(scanOptions, firstFillRoots, cancellationToken);

            // What this pass inserts, so reconciliation can recognise a move. See IndexFilesAsync.
            var insertedPaths = new HashSet<string>(StringComparer.Ordinal);

            var addedFiles = await IndexFilesAsync(
                scanOptions,
                directoryKeys,
                firstFillRoots,
                insertedPaths,
                cancellationToken);

            var updatedFiles = 0;
            var removedFiles = 0;
            var removedDirectories = 0;
            var unusable = FindUnusableSourceFolders(scanOptions.SourceFolders);

            if (unusable.Count == 0)
            {
                // Reconcile after adding, so a file that moved is re-added under its new path before the old
                // row is removed - the alternative briefly drops it out of the library. Reconciliation then
                // recognises the two halves as one file and carries the original row across.
                var (updated, removed, movedOrRenamed) = await ReconcileFilesAsync(insertedPaths, cancellationToken);

                // A move or a rename is one row that changed path, so it is reported as an update - and the
                // insert it superseded is taken back off the added count, which would otherwise report a
                // moved file as both an arrival and a departure.
                updatedFiles = updated + movedOrRenamed;
                removedFiles = removed;
                addedFiles -= movedOrRenamed;
                removedDirectories = await ReconcileDirectoriesAsync(
                    scanOptions,
                    DlnaOptionsDefaults.IsSourceFolderFallback(options.Library),
                    cancellationToken);
                removedDirectories += await PruneEmptyDirectoriesAsync(
                    scanOptions,
                    directoryKeys,
                    cancellationToken);
            }
            else
            {
                LogReconcileSkipped(string.Join(", ", unusable));
            }

            var totalFiles = await _files.CountAsync(cancellationToken);
            var totalDirectories = await _directories.CountAsync(cancellationToken);

            LogScanFinished(
                addedFiles,
                updatedFiles,
                removedFiles,
                removedDirectories,
                totalFiles,
                totalDirectories);

            return new LibraryIndexResult(
                addedFiles,
                updatedFiles,
                removedFiles,
                removedDirectories,
                totalFiles,
                totalDirectories);
        }

        /// <summary>
        /// The configured source folders that hold no indexed row yet, so this pass is their first fill.
        /// </summary>
        /// <remarks>
        /// <b>Reported by a customer.</b> Recently added orders by the row's own indexing time, and a
        /// bulk import gives every row the same value - which is no ordering at all. Recreating the
        /// database therefore wiped the operator's Recently added list, and adding a source folder
        /// dumped its whole contents in at one timestamp. On a first fill the filesystem's own date is
        /// the only information there is, so it is used instead; a file arriving into a folder already
        /// indexed is genuinely new and keeps the current time.
        /// <para>
        /// Keyed on "does this folder hold any indexed FILE", not on <c>IsSourceRoot</c> and not on the
        /// folder's own row: a folder previously indexed as a child and later promoted to a source folder
        /// already has its files, so it is not a first fill. <c>RerootKnownDirectoriesAsync</c> is what
        /// promotes it.
        /// <b>The folder row is the wrong question</b> - see
        /// <see cref="IMediaFileRepository.AnyUnderPathAsync"/> for the empty-mountpoint case that made
        /// asking it reintroduce the customer's bug.
        /// </para>
        /// <para>
        /// An empty database needs no separate case - nothing holds a file, so every folder is a first
        /// fill. A pass interrupted midway leaves the rest of that folder to be indexed as arrivals on
        /// the next pass; that is a real limitation and not worth a marker row to avoid.
        /// </para>
        /// </remarks>
        private async Task<IReadOnlyList<string>> ResolveFirstFillRootsAsync(
            LibraryScanRequest scanOptions,
            CancellationToken cancellationToken)
        {
            var roots = scanOptions.SourceFolders;

            if (roots.Count == 0)
            {
                return [];
            }

            var firstFill = new List<string>(roots.Count);

            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];

                // Asked of the FILES, not of the folder's own row. A directory row is created by
                // IndexDirectoriesAsync, which runs before the unusable-folder gate and takes any
                // readable root - so one pass over an empty mountpoint used to make the next pass
                // believe the folder had already been filled. See AnyUnderPathAsync's remarks.
                if (!await _files.AnyUnderPathAsync(root, cancellationToken))
                {
                    firstFill.Add(root);
                }
            }

            if (firstFill.Count > 0)
            {
                LogFirstFill(string.Join(", ", firstFill));
            }

            return firstFill;
        }

        /// <summary>
        /// Whether the path lies in a source folder being indexed for the first time.
        /// </summary>
        /// <remarks>
        /// A prefix test on a segment boundary, so <c>/share/Media</c> does not claim
        /// <c>/share/MediaOld</c>. The list is the configured source folders, so it holds a handful of
        /// entries at most and a loop per file costs nothing worth measuring.
        /// </remarks>
        private static bool IsUnderFirstFillRoot(string fullPath, IReadOnlyList<string> firstFillRoots)
        {
            for (var index = 0; index < firstFillRoots.Count; index++)
            {
                var root = firstFillRoots[index];

                if (!fullPath.StartsWith(root, StringComparison.Ordinal))
                {
                    continue;
                }

                if (fullPath.Length == root.Length || StoredPath.IsSeparator(fullPath[root.Length]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The configured source folders that cannot be read right now, described for a log line.
        /// </summary>
        /// <remarks>
        /// This gates reconciliation, and that gate is the difference between a transient filesystem
        /// problem and a destroyed index. Both reconcile passes delete every indexed row whose file or
        /// directory is not on disk, cascading through streams and thumbnails. A source folder that is
        /// merely unreachable - a NAS volume spinning up, an unmounted share, a permissions blip, a
        /// mistyped path saved from the admin UI - makes every one of those checks fail at once, so the
        /// pass would empty the index and force a full re-ffprobe of the library.
        /// <para>
        /// Any unusable folder stops both passes, not just its own subtree, because reconciliation walks
        /// the whole index rather than one root: with folder A up and folder B down, reconciling would
        /// still delete everything under B. Adding is unaffected - a scan of a folder that is not there
        /// simply finds nothing.
        /// </para>
        /// <para>
        /// A folder that is present and readable but <b>empty</b> counts as unusable here, and that is the
        /// case this gate originally missed. A QNAP volume that fails to mount leaves its mountpoint
        /// behind as an existing, readable, empty directory - so every check passed, both passes ran, and
        /// the whole index was deleted. An empty folder is still a legitimate thing to configure, which is
        /// why the admin UI reports it separately rather than as a problem.
        /// </para>
        /// </remarks>
        private List<string> FindUnusableSourceFolders(IReadOnlyList<string> sourceFolders)
        {
            var checks = _sourceFolders.Check(sourceFolders);
            var unusable = new List<string>();

            for (var index = 0; index < checks.Count; index++)
            {
                var check = checks[index];

                if (check.IsSafeToReconcile)
                {
                    continue;
                }

                unusable.Add($"'{check.Path}' ({check.Problem ?? "present but empty"})");
            }

            return unusable;
        }

        /// <summary>
        /// Walks the indexed files, dropping those whose file is gone and re-stamping those that changed.
        /// </summary>
        /// <remarks>
        /// Paged by path rather than by offset: rows are deleted as the pass runs, and an offset would
        /// skip records once earlier ones disappear.
        /// </remarks>
        private async Task<(int Updated, int Removed, int MovedOrRenamed)> ReconcileFilesAsync(
            IReadOnlySet<string> insertedPaths,
            CancellationToken cancellationToken)
        {
            var updated = 0;
            var removed = 0;
            var movedOrRenamed = 0;
            string? cursor = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await _files.GetIndexedPageAsync(cursor, BatchSize, cancellationToken);

                if (page.Count == 0)
                {
                    break;
                }

                cursor = page[^1].FullPath;

                var missing = new List<IndexedFileDto>();
                var changed = new List<MediaFileContentUpdateDto>();

                // Kept beside changed rather than added to the DTO: the update contract is keyed by
                // identifier and has no reason to carry a path, and the rows being walked already have
                // one to hand.
                var changedPaths = new List<string>();

                foreach (var indexed in page)
                {
                    var info = new FileInfo(indexed.FullPath);

                    if (!info.Exists)
                    {
                        // Exists is false both for "not there" and for "could not tell" - it swallows
                        // UnauthorizedAccessException and every IO error. Deleting on "could not tell" is
                        // how an rsync that briefly chmods a folder to 0700 removes every row under it.
                        if (!IsDefinitelyAbsent(indexed.FullPath))
                        {
                            LogFileAbsenceUnknown(indexed.FullPath);
                            continue;
                        }

                        missing.Add(indexed);
                        continue;
                    }

                    var stamp = ContentStamp.From(info.Length, info.LastWriteTimeUtc);

                    if (string.Equals(stamp, indexed.ContentStamp, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    LogFileChanged(indexed.FullPath);

                    changedPaths.Add(indexed.FullPath);

                    changed.Add(new MediaFileContentUpdateDto
                    {
                        PublicId = indexed.PublicId,
                        SizeInBytes = info.Length,
                        FileModifiedUtc = info.LastWriteTimeUtc,
                        ContentStamp = stamp,
                    });
                }

                // Before anything is deleted: a row missing from its path is usually a deletion, but it
                // is sometimes the same file seen at a new one. IndexFilesAsync has already inserted
                // that new path in this pass, so the pair is both present right now, and this is the
                // only moment they can be recognised as one file.
                var moved = await MoveOrRenameFilesAsync(missing, insertedPaths, cancellationToken);

                movedOrRenamed += moved.Count;

                var deletable = new List<Guid>(missing.Count - moved.Count);

                for (var index = 0; index < missing.Count; index++)
                {
                    if (!moved.Contains(missing[index].PublicId))
                    {
                        deletable.Add(missing[index].PublicId);
                    }
                }

                var (deleted, abandonedThumbnailPaths) = await _files.RemoveByPublicIdsAsync(
                    deletable,
                    cancellationToken);

                removed += deleted;

                // The preview outlives the media it was made from: it sits in a folder scanning is
                // configured to skip, so nothing would ever look at it again. The same clean-up a move
                // does, for the same reason.
                for (var index = 0; index < abandonedThumbnailPaths.Count; index++)
                {
                    TryDeleteAbandonedThumbnail(abandonedThumbnailPaths[index]);
                }

                updated += await _files.UpdateContentAsync(changed, cancellationToken);

                // A file whose bytes changed at the same path: the cache is keyed by path, so the old
                // payload would keep being served while DIDL advertised the new size. A renderer seeking
                // by advertised size then gets a short read and gives up. Deliberately not done for the
                // removals above - a renderer mid-stream must not be cut off because the file went away.
                for (var index = 0; index < changedPaths.Count; index++)
                {
                    _ = _fileCache.Evict(changedPaths[index]);
                }
            }

            return (updated, removed, movedOrRenamed);
        }

        /// <summary>
        /// Recognises rows that have not been deleted but renamed or moved, and carries each onto its new path.
        /// </summary>
        /// <remarks>
        /// A move is a vanished row and an inserted row describing the same bytes, and the only evidence
        /// available is the content stamp - there is no hash and no inode. A stamp is size and
        /// modification time, which two copies of one file share, so a match is acted on ONLY when it is
        /// unambiguous: exactly one candidate, at a path that exists, that is not itself vanishing. Every
        /// other shape falls through to the delete-and-insert this replaces, which is correct but loses
        /// the row.
        /// <para>
        /// One query per page rather than one per row, because a folder of a thousand deleted files would
        /// otherwise be a thousand scans of an unindexed column.
        /// </para>
        /// </remarks>
        private async Task<HashSet<Guid>> MoveOrRenameFilesAsync(
            List<IndexedFileDto> missing,
            IReadOnlySet<string> insertedPaths,
            CancellationToken cancellationToken)
        {
            var movedOrRenamed = new HashSet<Guid>();

            if (missing.Count == 0)
            {
                return movedOrRenamed;
            }

            var stamps = new HashSet<string>(missing.Count, StringComparer.Ordinal);
            var vanishing = new HashSet<Guid>(missing.Count);

            for (var index = 0; index < missing.Count; index++)
            {
                _ = stamps.Add(missing[index].ContentStamp);
                _ = vanishing.Add(missing[index].PublicId);
            }

            var candidates = await _files.FindByContentStampsAsync(stamps, cancellationToken);

            if (candidates.Count == 0)
            {
                return movedOrRenamed;
            }

            var arrivals = new Dictionary<string, List<IndexedFileDto>>(StringComparer.Ordinal);

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];

                // Only a path THIS pass inserted can be where a vanished file went. Without that test,
                // deleting one of two byte-identical copies pairs the deleted row with the surviving
                // copy's long-standing row - and renaming onto it destroys that row's own identifier
                // and metadata, for a file nobody touched.
                if (!insertedPaths.Contains(candidate.FullPath))
                {
                    continue;
                }

                // A row that is itself vanishing is the other half of this pass's deletions, not a
                // destination; a row whose file is not there is stale and about to be reconciled away.
                if (vanishing.Contains(candidate.PublicId) || !File.Exists(candidate.FullPath))
                {
                    continue;
                }

                if (!arrivals.TryGetValue(candidate.ContentStamp, out var forStamp))
                {
                    forStamp = new List<IndexedFileDto>(1);
                    arrivals[candidate.ContentStamp] = forStamp;
                }

                forStamp.Add(candidate);
            }

            for (var index = 0; index < missing.Count; index++)
            {
                var gone = missing[index];

                if (!arrivals.TryGetValue(gone.ContentStamp, out var forStamp) || forStamp.Count != 1)
                {
                    continue;
                }

                var arrival = forStamp[0];

                var (moved, abandonedThumbnailPath) = await _files.MoveOrRenameAsync(
                    gone.PublicId,
                    arrival.PublicId,
                    cancellationToken);

                if (!moved)
                {
                    continue;
                }

                // Removed from the map so two rows sharing one stamp cannot both claim the same arrival -
                // the second call would fail anyway, but silently, and the row would look carried across.
                _ = arrivals.Remove(gone.ContentStamp);
                _ = movedOrRenamed.Add(gone.PublicId);

                TryDeleteAbandonedThumbnail(abandonedThumbnailPath);

                // Two words for one operation, because they are two different events to an operator
                // reading the log: a file that changed folder was moved, one that changed only its name
                // in place was renamed.
                if (string.Equals(
                    Path.GetDirectoryName(gone.FullPath),
                    Path.GetDirectoryName(arrival.FullPath),
                    StringComparison.Ordinal))
                {
                    LogFileRenamed(gone.FullPath, arrival.FullPath);
                }
                else
                {
                    LogFileMoved(gone.FullPath, arrival.FullPath);
                }
            }

            return movedOrRenamed;
        }

        /// <summary>
        /// Deletes the preview a moved file left behind in its old folder, without ever throwing.
        /// </summary>
        /// <remarks>
        /// Previews sit beside the media, so a move that did not clean up would leave an image in a
        /// folder whose media is gone - and nothing else ever removes one, because the reconciliation
        /// that prunes rows does not look inside the excluded preview folder.
        /// </remarks>
        private void TryDeleteAbandonedThumbnail(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            _ = _fileCache.Evict(filePath);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogThumbnailCleanupFailed(filePath, exception.Message);
            }
        }

        /// <summary>
        /// Whether a file is known to be gone, as opposed to merely unreadable.
        /// </summary>
        /// <remarks>
        /// A file is absent only when its own folder can be read and does not hold it. Every other answer
        /// - a permission change, an IO error, a share that went away - is "unknown", and an unknown
        /// answer must never delete a row. Enumerating is what draws the distinction: it throws where
        /// <see cref="FileInfo.Exists"/> silently answers false. <c>LibraryScanner.TryDescribe</c> already
        /// refuses to draw that conclusion on the insert side; this is the delete side, where a wrong
        /// answer costs the row rather than one scan.
        /// </remarks>
        private static bool IsDefinitelyAbsent(string fullPath)
        {
            var directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            try
            {
                return !Directory.EnumerateFileSystemEntries(directory, Path.GetFileName(fullPath)).Any();
            }
            catch (DirectoryNotFoundException)
            {
                // A folder that is not there cannot be holding the file, so this IS a definite absence.
                // It has to be caught ahead of the arm below, which it derives from - and it is the case
                // a row can reach by being indexed before its folder was excluded, then deleted.
                return true;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // The folder is there but would not answer. That is not the same as the file being gone.
                return false;
            }
        }

        /// <summary>
        /// Whether a folder is known to be gone, as opposed to merely unreadable.
        /// </summary>
        /// <remarks>
        /// The directory half of <see cref="IsDefinitelyAbsent"/>, and it matters <b>more</b> than the
        /// file half rather than less: <c>MediaDirectoryEntityConfiguration</c> cascades a directory
        /// delete into its subdirectories and their files, metadata and thumbnails, so one wrong answer
        /// here takes a subtree - including rows the file pass had just deliberately kept.
        /// <para>
        /// <see cref="Directory.Exists(string)"/> alone was the defect. It answers false for a folder
        /// that is genuinely gone and for one that merely refused to answer, and a backup run leaving a
        /// folder at mode 0700 for the length of one pass, or an <c>ESTALE</c> from a submount, is the
        /// second. <c>FindUnusableSourceFolders</c> does not cover it either - the checker probes only
        /// the configured roots, and a root stays perfectly readable while a folder beneath it does not.
        /// </para>
        /// </remarks>
        private static bool IsDirectoryDefinitelyAbsent(string fullPath)
        {
            var parent = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(parent))
            {
                // A root has no parent to ask, so nothing here can establish a definite absence.
                return false;
            }

            try
            {
                return !Directory.EnumerateDirectories(parent, Path.GetFileName(fullPath)).Any();
            }
            catch (DirectoryNotFoundException)
            {
                // The parent is gone, so the folder cannot be there. Caught ahead of the IOException arm
                // it derives from, exactly as the file side does.
                return true;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Drops indexed directories that no longer exist, and those configuration no longer covers.
        /// Subdirectories and files follow by cascade.
        /// </summary>
        /// <remarks>
        /// Existence alone is not the test. Narrowing <c>SourceFolders</c> from a folder to two of its
        /// children leaves the old parent present on disc, so a pass that only asked whether the path
        /// existed kept it indexed and kept serving its files - content the operator had deliberately
        /// stopped sharing. <see cref="IndexDirectoriesAsync"/> re-roots the surviving children before
        /// this runs, so removing the old parent does not cascade into them.
        /// <para>
        /// Folders that have merely stopped holding media are not this pass's business - see
        /// <see cref="PruneEmptyDirectoriesAsync"/>, which runs next and has to work leaf-upwards for
        /// reasons this pass does not share.
        /// </para>
        /// <para>
        /// <b>The coverage half does not apply while the source folders are the fallback.</b> Nobody chose
        /// that folder: it is what a missing <c>config.json</c> becomes, and on the NAS such a restart
        /// removed the two indexed source folders as "no longer covered" and cascaded every file row with
        /// them - every identifier regenerated and a full preview pass to follow. A folder that is
        /// genuinely gone from disc is still removed.
        /// </para>
        /// </remarks>
        private async Task<int> ReconcileDirectoriesAsync(
            LibraryScanRequest scanOptions,
            bool isSourceFolderFallback,
            CancellationToken cancellationToken)
        {
            var removed = 0;
            var keptUncovered = 0;
            string? cursor = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await _directories.GetIndexedPageAsync(cursor, BatchSize, cancellationToken);

                if (page.Count == 0)
                {
                    break;
                }

                cursor = page[^1].FullPath;

                var missing = new List<Guid>(page.Count);

                foreach (var indexed in page)
                {
                    if (IsDirectoryDefinitelyAbsent(indexed.FullPath))
                    {
                        missing.Add(indexed.PublicId);
                        continue;
                    }

                    if (IsInsideAnySourceFolder(indexed.FullPath, scanOptions.SourceFolders))
                    {
                        continue;
                    }

                    if (isSourceFolderFallback)
                    {
                        keptUncovered++;
                        continue;
                    }

                    missing.Add(indexed.PublicId);
                }

                removed += await _directories.RemoveByPublicIdsAsync(missing, cancellationToken);
            }

            if (keptUncovered > 0)
            {
                LogFallbackKeptUncoveredFolders(string.Join(", ", scanOptions.SourceFolders), keptUncovered);
            }

            return removed;
        }

        /// <summary>
        /// Drops indexed folders that lead to no media, a level at a time, until nothing is left to drop.
        /// </summary>
        /// <remarks>
        /// <c>ILibraryScanner.EnumerateDirectories</c> yields only folders that lead to media, so a folder
        /// it did not offer holds nothing playable any more - QNAP's <c>.streams</c> and its 66
        /// subfolders, or a folder whose last film was deleted. The same set decides insertion, which is
        /// what makes this idempotent: a folder pruned on one pass is not re-added by the next.
        /// <para>
        /// <b>Leaves only, repeatedly, and that shape is load-bearing.</b> Deleting a directory cascades
        /// into its subdirectories and their files, so removing an empty <i>ancestor</i> in one shot would
        /// carry away a subtree <c>ExcludeFolders</c> was only meant to hide - measured, before this was
        /// split out: excluding <c>Private</c> left <c>Films</c> holding nothing discovered, and deleting
        /// <c>Films</c> took the excluded folder and its rows with it. Removing only leaves means the
        /// excluded folder itself is never removed, so its parent never becomes a leaf, so the exemption
        /// actually holds.
        /// </para>
        /// <para>
        /// Each pass therefore collapses one level, and a chain of empty folders needs as many passes as
        /// it is deep. The loop is bounded rather than trusted: a page of leaves that are all exempt has
        /// to end the pass, and a cycle in the parent data - which nothing enforces against - must not
        /// spin here while a scan is running.
        /// </para>
        /// </remarks>
        private async Task<int> PruneEmptyDirectoriesAsync(
            LibraryScanRequest scanOptions,
            Dictionary<string, Guid> discovered,
            CancellationToken cancellationToken)
        {
            var removed = 0;

            for (var pass = 0; pass < MaxPrunePasses; pass++)
            {
                var removedThisPass = 0;
                string? cursor = null;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = await _directories.GetEmptyLeafPageAsync(cursor, BatchSize, cancellationToken);

                    if (page.Count == 0)
                    {
                        break;
                    }

                    cursor = page[^1].FullPath;

                    // Hidden folders are exempt: hiding is meant to retire a folder without discarding its
                    // rows, and the scanner does not enumerate an excluded folder at all, so without this
                    // adding a name to ExcludeFolders would cost the subtree's metadata and thumbnails.
                    var prunable = page
                        .Where(indexed =>
                            !discovered.ContainsKey(indexed.FullPath)
                            && !PathExclusion.IsHidden(indexed.FullPath, scanOptions.ExcludedFolderNames))
                        .Select(static indexed => indexed.PublicId)
                        .ToArray();

                    removedThisPass += await _directories.RemoveByPublicIdsAsync(prunable, cancellationToken);
                }

                if (removedThisPass == 0)
                {
                    return removed;
                }

                removed += removedThisPass;
            }

            LogPruneIncomplete(MaxPrunePasses);

            return removed;
        }

        /// <summary>
        /// Creates directory rows for anything not already indexed, parents before children.
        /// </summary>
        /// <remarks>
        /// Ordering by depth matters: a child cannot be inserted until its parent has an identifier.
        /// Nothing above a configured source folder is ever created - the reference walked
        /// <c>DirectoryInfo.Parent</c> to the filesystem root and re-attempted an insert of <c>/</c> on
        /// every watcher event, producing 1390 unique-constraint failures in one day on the live server.
        /// <para>
        /// <paramref name="firstFillRoots"/> is what dates the rows. Folders under a source folder being
        /// filled for the first time take their date from the filesystem, exactly as their files do -
        /// without it every folder in a bulk import shared one clock timestamp and Browse's date sort
        /// over containers degenerated into insertion order.
        /// </para>
        /// </remarks>
        private async Task<Dictionary<string, Guid>> IndexDirectoriesAsync(
            LibraryScanRequest scanOptions,
            IReadOnlyList<string> firstFillRoots,
            CancellationToken cancellationToken)
        {
            var sourceRoots = scanOptions.SourceFolders
                .Select(static folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)))
                .ToHashSet(StringComparer.Ordinal);

            var keys = new Dictionary<string, Guid>(StringComparer.Ordinal);

            var discovered = _scanner.EnumerateDirectories(scanOptions, cancellationToken)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            // One query for the rows, not one query per row. This was GetExistingPathsAsync followed by
            // a GetByPathAsync per known path - and RelinkAsync below did it a second time, so a pass
            // over the live library spent ~2,110 round trips fetching what a single batched read
            // already had. The dictionary is threaded through to RelinkAsync rather than rebuilt there.
            var storedByPath = await _directories.GetExistingByPathAsync(discovered, cancellationToken);

            foreach (var (path, stored) in storedByPath)
            {
                keys[path] = stored.PublicId;
            }

            // One depth level at a time, shallowest first. A child cannot be inserted until its parent
            // has an identifier, so every level must be committed before the next one is built.
            //
            // Tested against storedByPath directly. A separate HashSet over its own keys was a leftover
            // from the N+1 refactor that introduced the dictionary: the dictionary is already
            // Ordinal-keyed, so the set was a second index over the same ~1,000 strings.
            var levels = discovered
                .Where(path => !storedByPath.ContainsKey(path))
                .GroupBy(static path => path.Count(StoredPath.IsSeparator))
                .OrderBy(static level => level.Key);

            foreach (var level in levels)
            {
                var pending = new List<MediaDirectoryCreateDto>();

                foreach (var path in level.OrderBy(static path => path, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var isSourceRoot = sourceRoots.Contains(path);
                    var parentPath = isSourceRoot ? null : Path.GetDirectoryName(path);

                    Guid? parentPublicId = null;

                    if (parentPath is not null && keys.TryGetValue(parentPath, out var parentKey))
                    {
                        parentPublicId = parentKey;
                    }

                    pending.Add(new MediaDirectoryCreateDto
                    {
                        FullPath = path,
                        Name = Path.GetFileName(path) is { Length: > 0 } name ? name : path,
                        ParentDirectoryPublicId = parentPublicId,
                        Depth = level.Key,
                        IsSourceRoot = isSourceRoot,

                        // Null for an ordinary arrival, which lets the store stamp the current time.
                        IndexedUtc = IsUnderFirstFillRoot(path, firstFillRoots)
                            ? ResolveDirectoryDate(path)
                            : null,
                    });

                    if (pending.Count >= BatchSize)
                    {
                        await FlushDirectoriesAsync(pending, keys, cancellationToken);
                    }
                }

                await FlushDirectoriesAsync(pending, keys, cancellationToken);
            }

            await RerootKnownDirectoriesAsync(storedByPath, sourceRoots, keys, cancellationToken);

            return keys;
        }

        /// <summary>
        /// Brings already-indexed directories back into line with configuration.
        /// </summary>
        /// <remarks>
        /// Whether a directory is a source root, and which directory is its parent, both come from
        /// configuration rather than from the filesystem - and the insert path decides them once, for rows
        /// it creates. A folder that was indexed as a child and is later configured as a source folder is
        /// therefore already known, skips the insert entirely, and would keep its old parent and its
        /// <c>IsSourceRoot=false</c> forever: it never appears at the top level, and a restart does not
        /// help because nothing re-reads it.
        /// <para>
        /// This also has to run before <see cref="ReconcileDirectoriesAsync"/> removes the old root, since
        /// deleting a directory cascades into its children - detaching them first is what saves their
        /// files, metadata and thumbnails from going with it.
        /// </para>
        /// <para>
        /// Takes the rows its caller already fetched. It used to re-read each one with its own
        /// <c>GetByPathAsync</c>, which doubled the round trips of a pass for data that was in hand.
        /// </para>
        /// </remarks>
        private async Task RerootKnownDirectoriesAsync(
            IReadOnlyDictionary<string, MediaDirectoryDto> storedByPath,
            HashSet<string> sourceRoots,
            Dictionary<string, Guid> keys,
            CancellationToken cancellationToken)
        {
            // Iterated as pairs. This took a separate set of the same keys and then looked each row up
            // again, which could only ever find what the pair already held.
            foreach (var (path, stored) in storedByPath)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!keys.TryGetValue(path, out var publicId))
                {
                    continue;
                }

                var isSourceRoot = sourceRoots.Contains(path);
                var parentPath = isSourceRoot ? null : Path.GetDirectoryName(path);

                Guid? parentPublicId = parentPath is not null && keys.TryGetValue(parentPath, out var parentKey)
                    ? parentKey
                    : null;

                if (stored.IsSourceRoot == isSourceRoot && stored.ParentDirectoryPublicId == parentPublicId)
                {
                    continue;
                }

                LogDirectoryReRooted(path, isSourceRoot);

                _ = await _directories.SetHierarchyAsync(publicId, parentPublicId, isSourceRoot, cancellationToken);
            }
        }

        private async Task FlushDirectoriesAsync(
            List<MediaDirectoryCreateDto> pending,
            Dictionary<string, Guid> keys,
            CancellationToken cancellationToken)
        {
            if (pending.Count == 0)
            {
                return;
            }

            var stored = await _directories.AddRangeAsync(pending, cancellationToken);

            foreach (var directory in stored)
            {
                keys[directory.FullPath] = directory.PublicId;
            }

            pending.Clear();
        }

        /// <remarks>
        /// <paramref name="insertedPaths"/> collects what this pass actually created, which is what
        /// reconciliation needs to tell a moved file from a second copy of one: only a path inserted just
        /// now can be where a vanished row went. It is held for the length of the pass and dropped with
        /// it - a first fill therefore carries every path briefly, and a first fill is also the one case
        /// where nothing can be missing, so nothing reads it.
        /// </remarks>
        private async Task<int> IndexFilesAsync(
            LibraryScanRequest scanOptions,
            IReadOnlyDictionary<string, Guid> directoryKeys,
            IReadOnlyList<string> firstFillRoots,
            HashSet<string> insertedPaths,
            CancellationToken cancellationToken)
        {
            var added = 0;
            var batch = new List<ScannedFile>(BatchSize);

            foreach (var scanned in _scanner.EnumerateFiles(scanOptions, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch.Add(scanned);

                if (batch.Count >= BatchSize)
                {
                    added += await FlushFilesAsync(
                        batch,
                        directoryKeys,
                        firstFillRoots,
                        insertedPaths,
                        cancellationToken);
                }
            }

            added += await FlushFilesAsync(
                batch,
                directoryKeys,
                firstFillRoots,
                insertedPaths,
                cancellationToken);

            return added;
        }

        /// <summary>
        /// Inserts the files in the batch that are not already indexed.
        /// </summary>
        private async Task<int> FlushFilesAsync(
            List<ScannedFile> batch,
            IReadOnlyDictionary<string, Guid> directoryKeys,
            IReadOnlyList<string> firstFillRoots,
            HashSet<string> insertedPaths,
            CancellationToken cancellationToken)
        {
            if (batch.Count == 0)
            {
                return 0;
            }

            // Distinct as well as normalised source folders: a symlink or a case variation can still
            // surface the same path twice, and a duplicate inside one batch fails the whole insert.
            var paths = batch
                .Select(static file => file.FullPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var known = await _files.GetExistingPathsAsync(paths, cancellationToken);

            var toCreate = new List<MediaFileCreateDto>(batch.Count);

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var scanned in batch)
            {
                if (known.Contains(scanned.FullPath) || !seen.Add(scanned.FullPath))
                {
                    continue;
                }

                if (!directoryKeys.TryGetValue(scanned.DirectoryPath, out var directoryPublicId))
                {
                    // The parent was excluded or vanished between the two enumerations. Skip the file
                    // rather than attach it to nothing - the reference threw here and lost the whole batch.
                    LogParentDirectoryMissing(scanned.FullPath, scanned.DirectoryPath);
                    continue;
                }

                toCreate.Add(new MediaFileCreateDto
                {
                    FullPath = scanned.FullPath,
                    FileName = scanned.FileName,
                    Title = Path.GetFileNameWithoutExtension(scanned.FileName),
                    Extension = scanned.Extension,
                    DirectoryPublicId = directoryPublicId,
                    Mime = scanned.Mime,
                    DlnaProfileName = scanned.DlnaProfileName ?? scanned.Mime.ToMainProfileName(),
                    UpnpClass = scanned.Mime.ToDefaultItemClass(),
                    SizeInBytes = scanned.SizeInBytes,
                    FileCreatedUtc = scanned.CreatedUtc,
                    FileModifiedUtc = scanned.ModifiedUtc,

                    // Null for an ordinary arrival, which lets the store stamp the current time.
                    IndexedUtc = IsUnderFirstFillRoot(scanned.FullPath, firstFillRoots)
                        ? scanned.FileSystemCreatedUtc
                        : null,
                    ContentStamp = scanned.ContentStamp,
                });

                _ = insertedPaths.Add(scanned.FullPath);
            }

            batch.Clear();

            if (toCreate.Count == 0)
            {
                return 0;
            }

            // The count overload: this only ever wanted the number, and the DTO-returning one re-reads
            // every inserted row through the full projection to produce it.
            return await _files.AddRangeReturningCountAsync(toCreate, cancellationToken);
        }

        private static LibraryScanRequest BuildScanOptions(DlnaOptions options)
        {
            var mappings = new Dictionary<string, MediaExtensionMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (var (extension, configured) in options.Library.MediaFileExtensions)
            {
                // IsDefined as well as TryParse: TryParse accepts a NUMERIC string, so "999" in config.json
                // parsed happily into an undefined enum value that no MIME, profile or media kind
                // maps from - past the admin editor's own validation, which only offers real names.
                if (!Enum.TryParse<DlnaMime>(configured.Mime, ignoreCase: true, out var mime)
                    || !Enum.IsDefined(mime))
                {
                    continue;
                }

                mappings[extension] = new MediaExtensionMapping(
                    mime,
                    configured.ProfileName ?? mime.ToMainProfileName());
            }

            return new LibraryScanRequest
            {
                SourceFolders = NormaliseSourceFolders(options.Library.SourceFolders),
                UseFileCreationDateTime = options.Library.UseFileCreationDateTime,
                ExcludedFolderNames = options.Library.ExcludeFolders.ToArray(),
                ExtensionMappings = mappings,
            };
        }

        /// <summary>
        /// Reduces the configured folders to a set that cannot yield the same file twice.
        /// </summary>
        /// <remarks>
        /// Duplicates and nested folders both cause a path to be discovered more than once in a single
        /// pass, and the second insert violates the unique index on the path. Configuring
        /// <c>/share/Media</c> together with <c>/share/Media/Movies</c> is a perfectly reasonable mistake,
        /// so it is resolved here rather than left to fail at the database.
        /// </remarks>
        /// <remarks>
        /// <c>internal</c> so <c>FileWatcherHostedService</c> can ask the same question. It used to hand
        /// the watcher the raw configured strings while this pass used these, so the two disagreed about
        /// what a source folder is - a relative path or a trailing separator had the watch attached to
        /// one thing and the scan reconciling another.
        /// </remarks>
        internal static List<string> NormaliseSourceFolders(IEnumerable<string> configured)
        {
            // Ordinal, not OrdinalIgnoreCase. On the case-sensitive filesystem this runs on in production
            // two roots differing only in case are genuinely different directories, and collapsing them
            // silently dropped a configured source folder with no warning and no validation failure.
            // IndexDirectoriesAsync already uses StringComparer.Ordinal and MediaDirectoryEntityConfiguration
            // deliberately makes FullPath case-sensitive, so this was the outlier.
            var normalised = configured
                .Where(static folder => !string.IsNullOrWhiteSpace(folder))
                .Select(static folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static folder => folder.Length)
                .ToList();

            var roots = new List<string>(normalised.Count);

            foreach (var candidate in normalised)
            {
                var nested = roots.Any(root =>
                    candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));

                if (!nested)
                {
                    roots.Add(candidate);
                }
            }

            return roots;
        }

        /// <remarks>
        /// A source folder covers itself and everything beneath it. Compared the same way
        /// <see cref="NormaliseSourceFolders"/> compares them, so a folder is never in scope for the
        /// nesting check and out of scope here.
        /// </remarks>
        private static bool IsInsideAnySourceFolder(string fullPath, IReadOnlyList<string> sourceFolders)
        {
            foreach (var folder in sourceFolders)
            {
                // Ordinal for the same reason as NormaliseSourceFolders, and the separator is read from
                // the stored path rather than taken from the host: these paths come out of the database
                // and may have been written by the other operating system.
                var separator = StoredPath.SeparatorOf(fullPath);

                if (string.Equals(fullPath, folder, StringComparison.Ordinal)
                    || fullPath.StartsWith(folder + separator, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <remarks>
        /// One stat per new folder, and only on a first fill. <see cref="DirectoryInfo.Exists"/> performs
        /// it and caches the result, so the two timestamp reads below cost nothing more.
        /// <see cref="ILibraryScanner.EnumerateDirectories"/> deliberately yields bare paths and touches
        /// no disc, which is why this happens here rather than there.
        /// </remarks>
        private static DateTime? ResolveDirectoryDate(string fullPath)
        {
            var info = new DirectoryInfo(fullPath);

            if (!info.Exists)
            {
                // Vanished between the two enumerations. Null gives it the current time, which is the
                // same answer an ordinary arrival gets - better than the 1601 sentinel an unstattable
                // folder reports for both of its timestamps.
                return null;
            }

            return FileSystemDate.Resolve(info.CreationTimeUtc, info.LastWriteTimeUtc);
        }
    }
}
