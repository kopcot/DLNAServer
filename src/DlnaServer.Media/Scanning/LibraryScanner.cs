using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Scanning;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;

namespace DlnaServer.Media.Scanning
{
    /// <inheritdoc cref="ILibraryScanner"/>
    internal sealed partial class LibraryScanner : ILibraryScanner
    {
        private readonly ILogger<LibraryScanner> _logger;

        public LibraryScanner(ILogger<LibraryScanner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Enumeration settings shared by every scan.
        /// </summary>
        /// <remarks>
        /// <c>IgnoreInaccessible</c> is true, unlike the reference: it used false, so a single
        /// permission-denied folder threw and aborted the entire startup scan, leaving the library
        /// unindexed. Skipping what cannot be read and carrying on is the only sane behaviour on a NAS.
        /// </remarks>
        private static readonly EnumerationOptions _enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // FileAttributes.Hidden is deliberately NOT skipped. On Linux .NET derives it from the NAME -
            // anything starting with a dot - so skipping it silently dropped every dot-prefixed media
            // folder and its entire subtree in production, while on the Windows dev box the same folder
            // carries no hidden bit and was indexed normally. Hiding folders is what ExcludeFolders is
            // for, and it works the same on both platforms.
            AttributesToSkip = FileAttributes.System
                | FileAttributes.Temporary
                | FileAttributes.SparseFile

                // ReparsePoint is how .NET reports a SYMLINK on Linux, so this silently skips a symlinked
                // folder and everything under it - and symlinking media into the tree is a normal QNAP
                // arrangement. Kept because it is also the only loop protection there is: a link back to
                // an ancestor would otherwise recurse until MaxRecursionDepth, re-indexing the subtree
                // under a second path that violates the unique index on FullPath. The trade is deliberate,
                // and the silence is the part to fix if it ever bites - not the exclusion.
                | FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            MaxRecursionDepth = int.MaxValue,
            ReturnSpecialDirectories = false,
        };

        public IEnumerable<ScannedFile> EnumerateFiles(
            LibraryScanRequest options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            foreach (var sourceFolder in options.SourceFolders)
            {
                if (!Directory.Exists(sourceFolder))
                {
                    LogSourceFolderMissing(sourceFolder);
                    continue;
                }

                foreach (var file in EnumerateFolder(sourceFolder, options, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Folders that lead to media - each folder holding an indexable file, and its ancestors up to
        /// the source root, which is always yielded.
        /// </summary>
        /// <remarks>
        /// Not every folder on the volume, which is what this used to yield: a QNAP holds
        /// <c>.streams</c>, <c>.@upload_cache</c> and their whole subtrees, none of which contains
        /// anything playable, and all of which arrived as containers a television was offered. Deriving
        /// the set from the files instead means an empty folder is never indexed rather than being
        /// indexed and then filtered, so the index carries only what can actually be browsed to.
        /// <para>
        /// This is also what lets reconciliation delete a folder that has stopped holding media: the same
        /// set decides what is inserted and what survives, so a folder cannot be pruned on one pass and
        /// re-added on the next.
        /// </para>
        /// <para>
        /// Candidates are matched on the path alone, without a <see cref="FileInfo"/>, because this pass
        /// only needs to know that a folder is worth indexing. The size and timestamp reads happen once,
        /// in <see cref="EnumerateFiles"/>.
        /// </para>
        /// </remarks>
        public IEnumerable<string> EnumerateDirectories(
            LibraryScanRequest options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            foreach (var sourceFolder in options.SourceFolders)
            {
                if (!Directory.Exists(sourceFolder))
                {
                    continue;
                }

                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));

                yield return root;

                // Seeded with the root so the walk up the ancestors stops there rather than climbing to
                // the filesystem root - the reference did climb, and re-attempted an insert of '/' on
                // every watcher event.
                var seen = new HashSet<string>(StringComparer.Ordinal) { root };

                foreach (var path in Directory.EnumerateFiles(sourceFolder, "*", _enumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!IsMediaCandidate(path, options))
                    {
                        continue;
                    }

                    var directory = TrimmedDirectoryName(path);

                    while (directory is not null && seen.Add(directory))
                    {
                        yield return directory;

                        directory = TrimmedDirectoryName(directory);
                    }
                }
            }
        }

        /// <remarks>
        /// Null once the walk runs out of parents, which cannot happen before the source root is reached
        /// while that root is in the caller's seen-set.
        /// </remarks>
        private static string? TrimmedDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) is { Length: > 0 } parent
                ? Path.TrimEndingDirectorySeparator(parent)
                : null;
        }

        /// <remarks>
        /// The path-only half of <see cref="TryDescribe"/>: same exclusion and same extension mapping,
        /// without touching the disc.
        /// </remarks>
        private static bool IsMediaCandidate(string path, LibraryScanRequest options)
        {
            if (PathExclusion.IsExcluded(path, options.ExcludedFolderNames))
            {
                return false;
            }

            var extension = Path.GetExtension(path);

            return extension.Length > 0 && TryResolveMime(extension, options, out _);
        }

        /// <summary>
        /// Walks one source folder, yielding as it goes.
        /// </summary>
        /// <remarks>
        /// Streaming rather than collecting: the reference built a
        /// <c>Dictionary&lt;DlnaMime, HashSet&lt;string&gt;&gt;</c> of every path and then copied it into
        /// arrays, holding a 20,000-file library in memory twice before a single row was written.
        /// </remarks>
        private IEnumerable<ScannedFile> EnumerateFolder(
            string sourceFolder,
            LibraryScanRequest options,
            CancellationToken cancellationToken)
        {
            foreach (var path in Directory.EnumerateFiles(sourceFolder, "*", _enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scanned = TryDescribe(path, options);

                if (scanned is not null)
                {
                    yield return scanned;
                }
            }
        }

        private ScannedFile? TryDescribe(string path, LibraryScanRequest options)
        {
            if (PathExclusion.IsExcluded(path, options.ExcludedFolderNames))
            {
                return null;
            }

            var extension = Path.GetExtension(path);

            if (extension.Length == 0)
            {
                return null;
            }

            if (!TryResolveMime(extension, options, out var mapping))
            {
                return null;
            }

            try
            {
                var info = new FileInfo(path);

                if (!info.Exists || info.Length == 0)
                {
                    return null;
                }

                var modifiedUtc = info.LastWriteTimeUtc;
                var directoryPath = Path.TrimEndingDirectorySeparator(info.DirectoryName ?? string.Empty);

                // One resolution for both fields below. Resolve reads the clock for its future-date test,
                // so calling it twice leaves a window where a creation time sitting on the boundary
                // answers differently for each - and with UseFileCreationDateTime on the two are
                // documented to be the same value.
                var fileSystemCreatedUtc = FileSystemDate.Resolve(info.CreationTimeUtc, modifiedUtc);

                return new ScannedFile
                {
                    FullPath = info.FullName,
                    FileName = info.Name,
                    DirectoryPath = directoryPath,
                    Extension = extension.ToLowerInvariant(),
                    Mime = mapping.Mime,
                    DlnaProfileName = mapping.DlnaProfileName,
                    SizeInBytes = info.Length,
                    // Through the same plausibility check as FileSystemCreatedUtc below, which it used
                    // not to be: the check was added for the new field and never applied to this one.
                    // This feeds FileCreatedUtc, the column Browse's date sort orders by, so on a
                    // filesystem that records no birth time - common on Linux - turning
                    // UseFileCreationDateTime on pinned every file to 1970 in the one field the sort uses.
                    CreatedUtc = options.UseFileCreationDateTime
                        ? fileSystemCreatedUtc
                        : DateTime.UtcNow,
                    FileSystemCreatedUtc = fileSystemCreatedUtc,
                    ModifiedUtc = modifiedUtc,
                    ContentStamp = ContentStamp.From(info.Length, modifiedUtc),
                };
            }
            catch (IOException exception)
            {
                // A file can vanish or be locked between enumeration and stat. One unreadable file must
                // never end the scan - the reference let exactly this abort the whole pass.
                LogFileUnreadable(path, exception.Message);
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                LogFileUnreadable(path, exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Configuration wins over the built-in catalog, because that is where device quirks live.
        /// </summary>
        /// <remarks>
        /// Two tiers, and it was one for a long time: the catalog was consulted by nothing, while three
        /// separate doc comments described the fallback below as though it existed. An extension the
        /// catalog knows perfectly well - <c>.webm</c>, <c>.flac</c>, <c>.ts</c> - was therefore not
        /// media unless the operator had typed it into <c>config.json</c>, and nothing said so.
        /// <para>
        /// Configuration still wins outright, which is what keeps the shipped <c>.mp3</c> quirk intact:
        /// it maps to <c>audio/mp4</c> for LG televisions where the catalog would say
        /// <c>audio/mpeg3</c>. Only kinds a renderer can be offered are inferred
        /// (<see cref="DlnaMimeCatalog.IsPresentableMedia"/>) - a subtitle sidecar is not a library item.
        /// </para>
        /// </remarks>
        private static bool TryResolveMime(
            string extension,
            LibraryScanRequest options,
            out MediaExtensionMapping mapping)
        {
            if (options.ExtensionMappings.TryGetValue(extension, out var configured)
                && configured.Mime != DlnaMime.Undefined)
            {
                mapping = configured;
                return true;
            }

            if (DlnaMimeCatalog.TryGetByFileExtension(extension, out var fromCatalog)
                && fromCatalog.IsPresentableMedia())
            {
                mapping = new MediaExtensionMapping(fromCatalog, fromCatalog.ToMainProfileName());
                return true;
            }

            mapping = new MediaExtensionMapping(DlnaMime.Undefined, DlnaProfileName: null);

            return false;
        }
    }
}
