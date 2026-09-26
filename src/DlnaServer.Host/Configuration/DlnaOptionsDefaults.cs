using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Fills in the options a deployment can reasonably omit, before validation sees them.
    /// </summary>
    /// <remarks>
    /// Applied through <c>PostConfigure</c>, so it runs after binding, before
    /// <see cref="DlnaOptionsValidator"/>, and again on every configuration reload.
    /// </remarks>
    internal static class DlnaOptionsDefaults
    {
        /// <summary>
        /// Folder name the generated thumbnail cache is excluded under when it sits inside a source folder.
        /// </summary>
        private const string FallbackThumbnailExclusion = "thumbnails";

        /// <summary>
        /// The application's own asset folder, excluded when the application folder becomes the library.
        /// </summary>
        /// <remarks>
        /// It holds the device icons and SCPD documents, and the icons are JPEGs - so without this a
        /// fresh deployment presents the server's own artwork to a television as a photo album.
        /// </remarks>
        private const string ResourcesExclusion = "Resources";

        /// <summary>
        /// The exclusions a NAS deployment always wants, used when configuration names none of its own.
        /// </summary>
        /// <remarks>
        /// The thumbnail sub-folder has to be here or <c>DlnaOptionsValidator</c> refuses to boot, since
        /// thumbnails are written beside the media; <c>@Recycle</c> is QNAP's trash and holds deleted
        /// copies of the very files that are still in the library.
        /// </remarks>
        private static readonly string[] _defaultExclusions = [".@__thumb", "@Recycle"];

        public static void Apply(DlnaOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            // Exclusions first: the source-folder fallback adds its own two names through Exclude, which
            // already de-duplicates, and seeding afterwards would see a non-empty list and skip.
            ApplyExcludeFolderDefaults(options);

            // Nothing is ever seeded here - the list stays empty until an operator names something - but
            // the admin UI writes it back on every save exactly as it does the excluded folders, so it
            // needs the same repair against the ConfigurationBinder append described below.
            options.Library.TemporarilyHiddenFolders = Deduplicate(options.Library.TemporarilyHiddenFolders);

            ApplySubtitleFileExtensionDefaults(options.Library);
            ApplySourceFolderFallback(options);
        }

        /// <summary>
        /// Seeds the shipped subtitle types when configuration names none, and puts every key in the form
        /// the file types editor stores - lower case, with a leading dot - either way.
        /// </summary>
        /// <remarks>
        /// Always a fresh case-insensitive map, whatever the binder built, so every reader can look an
        /// extension up in it directly. An empty list is not a way to switch linking off - it would link
        /// nothing and send nothing - so it means "not configured"; <c>Compatibility.SendSubtitles</c> is the
        /// switch for that.
        /// </remarks>
        private static void ApplySubtitleFileExtensionDefaults(LibraryOptions library)
        {
            var normalised = new Dictionary<string, DlnaMedia>(library.SubtitleFileExtensions.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var (extension, kind) in library.SubtitleFileExtensions)
            {
                var key = extension.Trim().ToLowerInvariant();

                if (key.Length == 0)
                {
                    continue;
                }

                if (!key.StartsWith('.'))
                {
                    key = "." + key;
                }

                normalised[key] = kind;
            }

            library.SubtitleFileExtensions = normalised.Count == 0
                ? SubtitleFileExtensionDefaults.Create()
                : normalised;
        }

        /// <summary>
        /// Seeds the standard exclusions when configuration names none, and drops repeats either way.
        /// </summary>
        /// <remarks>
        /// The de-duplication is not tidiness. <c>ConfigurationBinder</c> adds to an existing
        /// <see cref="IList{T}"/> rather than replacing it, so every list this ever bound to a non-empty
        /// default grew by that default's contents - and the admin UI saved the result back, so the file
        /// itself grew on every save. Removing the default fixes new saves; this repairs a file that has
        /// already been through it.
        /// </remarks>
        private static void ApplyExcludeFolderDefaults(DlnaOptions options)
        {
            var configured = options.Library.ExcludeFolders;

            if (!HasUsableExclusion(configured))
            {
                options.Library.ExcludeFolders = [.. _defaultExclusions];
                Exclude(options.Library, options.Thumbnails.SubFolderName);

                return;
            }

            options.Library.ExcludeFolders = Deduplicate(configured);

            // Unconditional, not a default: thumbnails are written beside the media, so a scan that does
            // not skip this folder re-ingests every preview as media and then previews those.
            Exclude(options.Library, options.Thumbnails.SubFolderName);
        }

        private static bool HasUsableExclusion(IList<string> excludeFolders)
        {
            for (var index = 0; index < excludeFolders.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(excludeFolders[index]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// With no source folder configured, serve the folder the application is running from.
        /// </summary>
        /// <remarks>
        /// A fresh deployment then starts instead of refusing to boot, and an operator can drop media in
        /// beside the binaries and rescan. Previously this was a fatal configuration error, which meant
        /// the first run on a new machine always failed.
        /// <para>
        /// The thumbnail cache defaults to a subdirectory of that same folder, so the fallback would
        /// otherwise trip the rule that a cache must not sit inside a source folder - the cache's own
        /// folder name is added to the exclusions instead, which is how the reference kept its thumbnails
        /// out of its media tree.
        /// </para>
        /// </remarks>
        private static void ApplySourceFolderFallback(DlnaOptions options)
        {
            if (HasUsableSourceFolder(options.Library))
            {
                return;
            }

            options.Library.SourceFolders = [AppContext.BaseDirectory];

            Exclude(options.Library, ResolveCacheFolderName(options.Thumbnails.CacheDirectory));
            Exclude(options.Library, ResourcesExclusion);
        }

        /// <summary>
        /// Whether the source folders are the application-folder fallback rather than folders an operator
        /// named.
        /// </summary>
        /// <remarks>
        /// The indexer needs the difference: a narrowed configuration is an operator deciding to stop
        /// sharing a folder, while the fallback is what a missing or emptied <c>config.json</c> produces,
        /// and treating it as a decision deleted a whole live library. An operator who names this exact
        /// folder is indistinguishable from the fallback and loses nothing but that one deletion rule.
        /// </remarks>
        internal static bool IsSourceFolderFallback(LibraryOptions library)
        {
            ArgumentNullException.ThrowIfNull(library);

            return library.SourceFolders is [var only]
                && string.Equals(only, AppContext.BaseDirectory, StringComparison.Ordinal);
        }

        /// <summary>
        /// Adds a folder name to the exclusions once. Reloads re-run this, so appending blindly would
        /// grow the list on every configuration change.
        /// </summary>
        /// <summary>
        /// The entries with blanks and repeats removed, each kept in the operator's own spelling.
        /// </summary>
        /// <remarks>
        /// De-duplicated on a canonical form. An entry may be a path, and <c>PathExclusion</c> treats the
        /// two separators as equivalent and ignores the surrounding ones, so one folder written three ways
        /// is one entry - and the admin UI writes these lists straight back on every save.
        /// </remarks>
        private static List<string> Deduplicate(IList<string> entries)
        {
            var kept = new List<string>(entries.Count + 1);
            var seen = new HashSet<string>(entries.Count, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < entries.Count; index++)
            {
                var name = entries[index];

                if (!string.IsNullOrWhiteSpace(name) && seen.Add(Canonicalise(name)))
                {
                    kept.Add(name);
                }
            }

            return kept;
        }

        private static void Exclude(LibraryOptions library, string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            var canonical = Canonicalise(folderName);

            for (var index = 0; index < library.ExcludeFolders.Count; index++)
            {
                var existing = library.ExcludeFolders[index];

                if (!string.IsNullOrWhiteSpace(existing)
                    && string.Equals(Canonicalise(existing), canonical, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            library.ExcludeFolders.Add(folderName);
        }

        /// <summary>
        /// The form two exclusion entries are compared in, matching how they are matched.
        /// </summary>
        /// <remarks>
        /// Both separators fold to one and the surrounding ones are dropped, because
        /// <see cref="Core.Files.PathExclusion"/> ignores exactly those differences when it matches. A
        /// comparison stricter than the matcher would let the same folder be listed twice, and this list
        /// is rewritten by the admin UI on every save - the shape that already grew the file by two
        /// entries per save once.
        /// </remarks>
        private static string Canonicalise(string entry)
        {
            return entry.Trim().Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Whether configuration named at least one folder. A list of blank entries counts as none - it is
        /// the same mistake as leaving it out, and reporting "contains a blank entry" would be unhelpful.
        /// </summary>
        private static bool HasUsableSourceFolder(LibraryOptions library)
        {
            foreach (var folder in library.SourceFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveCacheFolderName(string? cacheDirectory)
        {
            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                return FallbackThumbnailExclusion;
            }

            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(cacheDirectory));

            return string.IsNullOrEmpty(name) ? FallbackThumbnailExclusion : name;
        }
    }
}
