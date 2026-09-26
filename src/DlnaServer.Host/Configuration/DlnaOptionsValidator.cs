using System.Buffers;
using System.ComponentModel.DataAnnotations;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Upnp.Ssdp;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// Validates <see cref="DlnaOptions"/> at startup, covering the cross-property rules that
    /// data annotations on individual members cannot express.
    /// </summary>
    internal sealed class DlnaOptionsValidator : IValidateOptions<DlnaOptions>
    {
        // Three characters is arbitrary, and now only a typo guard: "@Recycle" and ".@__thumb" are the
        // real entries, and a one-character line in the Settings page is far likelier to be a slip than a
        // folder. It used to carry real weight, when matching was a substring and two characters could
        // hide half the library - segment alignment took that away rather than this rule.
        private const int MinimumExcludeFolderLength = 3;

        // What an extension may not contain past its leading dot - the set the file types editor refuses.
        private static readonly SearchValues<char> _rejectedExtensionCharacters = SearchValues.Create(" \t\r\n/\\:*?\"<>|");

        /// <remarks>
        /// Every failure this class produces is rendered verbatim by the admin Settings page, which is for
        /// an operator rather than a developer - so a message names the field as that page labels it, not
        /// the property behind it. Keys are <c>nameof</c> so a rename still breaks the build; only the
        /// values are prose. Anything absent falls back to its own name, which is a legible last resort
        /// rather than a wrong label.
        /// </remarks>
        private static readonly Dictionary<string, string> OperatorLabels = new(StringComparer.Ordinal)
        {
            [nameof(DlnaOptions.Server)] = "Server",
            [nameof(DlnaOptions.Library)] = "Library",
            [nameof(DlnaOptions.Thumbnails)] = "Previews",
            [nameof(DlnaOptions.FileCache)] = "File cache",
            [nameof(DlnaOptions.Database)] = "Database",
            [nameof(DlnaOptions.Compatibility)] = "Compatibility",
            [nameof(DlnaOptions.Upload)] = "Uploads",

            [nameof(ServerOptions.Port)] = "media port",
            [nameof(ServerOptions.AdminPort)] = "admin port",
            [nameof(ServerOptions.FriendlyName)] = "friendly name",

            [nameof(LibraryOptions.SourceFolders)] = "source folders",
            [nameof(LibraryOptions.ExcludeFolders)] = "excluded folders",
            [nameof(LibraryOptions.TemporarilyHiddenFolders)] = "temporarily hidden folders",
            [nameof(LibraryOptions.RecentlyAddedCount)] = "recently added count",
            [nameof(LibraryOptions.FileSettleSeconds)] = "wait before adding a file",
            [nameof(LibraryOptions.RescanIntervalMinutes)] = "how often to look for changes",
            [nameof(LibraryOptions.SubtitleFileExtensions)] = "subtitle types",

            [nameof(ThumbnailOptions.SubFolderName)] = "preview folder name",
            [nameof(ThumbnailOptions.CacheDirectory)] = "preview cache folder",
            [nameof(ThumbnailOptions.MaxWidth)] = "maximum preview width",
            [nameof(ThumbnailOptions.MaxHeight)] = "maximum preview height",
            [nameof(ThumbnailOptions.Quality)] = "preview quality",

            [nameof(FileCacheOptions.MaxTotalSizeInMegabytes)] = "total cache budget",
            [nameof(FileCacheOptions.MaxFileSizeInMegabytes)] = "largest cached file",
            [nameof(FileCacheOptions.MediaSlidingExpirationInMinutes)] = "media lifetime",
            [nameof(FileCacheOptions.ThumbnailSlidingExpirationInMinutes)] = "preview lifetime",

            [nameof(CompatibilityOptions.SsdpAliveIntervalInSeconds)] = "announce interval",
            [nameof(CompatibilityOptions.MaxBrowseRequestedCount)] = "maximum items per request",

            [nameof(UploadOptions.DestinationFolder)] = "upload folder",
            [nameof(UploadOptions.MaxSizeInMegabytes)] = "largest uploaded file",
        };

        public ValidateOptionsResult Validate(string? name, DlnaOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = new List<string>();

            ValidateAnnotations(options.Server, nameof(DlnaOptions.Server), failures);
            ValidateAnnotations(options.Library, nameof(DlnaOptions.Library), failures);
            ValidateAnnotations(options.Thumbnails, nameof(DlnaOptions.Thumbnails), failures);
            ValidateAnnotations(options.FileCache, nameof(DlnaOptions.FileCache), failures);

            // Database and Compatibility were both missing, which made every [Range] on them dead: a
            // MaxBrowseRequestedCount of -1 reached Paginate and threw ArgumentOutOfRangeException out of
            // GetRange on every Browse from every renderer, permanently, with the library simply gone.
            ValidateAnnotations(options.Database, nameof(DlnaOptions.Database), failures);
            ValidateAnnotations(options.Compatibility, nameof(DlnaOptions.Compatibility), failures);
            ValidateAnnotations(options.Upload, nameof(DlnaOptions.Upload), failures);

            if (options.Server.Port == options.Server.AdminPort)
            {
                failures.Add(
                    $"The {Label(nameof(ServerOptions.Port))} and the {Label(nameof(ServerOptions.AdminPort))} "
                    + "must be different, so these pages stay off the port your televisions use.");
            }

            if (options.Thumbnails.MaxWidth < 1 || options.Thumbnails.MaxHeight < 1)
            {
                failures.Add("The maximum preview size must be at least one pixel in each direction.");
            }

            if (options.FileCache.MaxFileSizeInMegabytes > options.FileCache.MaxTotalSizeInMegabytes)
            {
                failures.Add(
                    $"The {Label(nameof(FileCacheOptions.MaxFileSizeInMegabytes))} cannot be larger than the "
                    + $"{Label(nameof(FileCacheOptions.MaxTotalSizeInMegabytes))}, or no file would ever fit.");
            }

            // The announce interval and the advertised lifetime are independent values, and [Range] let
            // the interval reach 3600 against a max-age fixed at 600 - so a legal configuration produced
            // a device that expired on every renderer before the next announcement renewed it, appearing
            // and disappearing from the network for as long as it stayed set. Half is the usual margin:
            // one announcement may be lost without the device going away.
            if (options.Compatibility.SsdpAliveIntervalInSeconds * 2 > SsdpMessageBuilder.CacheControlMaxAgeSeconds)
            {
                failures.Add(
                    $"The {Label(nameof(CompatibilityOptions.SsdpAliveIntervalInSeconds))} must be at most "
                    + $"{SsdpMessageBuilder.CacheControlMaxAgeSeconds / 2} seconds. Televisions are told this "
                    + $"server is valid for {SsdpMessageBuilder.CacheControlMaxAgeSeconds} seconds, so "
                    + "announcing less often than half of that makes it vanish from the network between "
                    + "announcements.");
            }

            failures.AddRange(ValidateSourceFolders(options));
            failures.AddRange(ValidateThumbnailSubFolderName(options));
            failures.AddRange(ValidateExcludeFolders(options));
            failures.AddRange(ValidateTemporarilyHiddenFolders(options));
            failures.AddRange(ValidateSubtitleFileExtensions(options));
            failures.AddRange(ValidateThumbnailCacheOutsideLibrary(options));
            failures.AddRange(ValidateUploadFolder(options));

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        /// <summary>
        /// Checks the shape of the configured source folders, deliberately not their availability.
        /// </summary>
        /// <remarks>
        /// This used to call <see cref="Directory.Exists(string)"/>, which put filesystem availability
        /// inside options validation - and validation re-runs on every configuration reload, throwing out
        /// of <c>IOptionsMonitor.CurrentValue</c>. That property is read on every media request, every
        /// listing and every executed database command, and the monitor caches only successes, so one
        /// unmounted share plus any touch of config.json returned 500 for everything, including the
        /// Settings page that would have repaired it. Availability belongs to
        /// <c>ISourceFolderChecker</c>, which reports it to the operator, and to
        /// <c>LibraryIndexer.FindUnusableSourceFolders</c>, which stops reconciliation destroying the
        /// index over it. Both already existed.
        /// </remarks>
        private static IEnumerable<string> ValidateSourceFolders(DlnaOptions options)
        {
            if (options.Library.SourceFolders.Count == 0)
            {
                yield return "At least one source folder must be listed, or there is nothing to share.";
                yield break;
            }

            foreach (var folder in options.Library.SourceFolders)
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    yield return "One of the source folders is blank.";
                }
                else if (!Path.IsPathRooted(folder.Trim()))
                {
                    // Shape, not availability - see the remark on this method for why existence is not
                    // checked here. A relative folder resolves against the working directory, so what it
                    // points at depends on how the server was started.
                    yield return
                        $"The source folder '{folder}' must be a full path. A partial one points somewhere "
                        + "different depending on how the server was started.";
                }
            }
        }

        /// <summary>
        /// A thumbnail sub-folder has to be one folder name, not a path.
        /// </summary>
        /// <remarks>
        /// Two things consume it, and a path breaks both. <c>MediaProcessingHostedService</c> hands it to
        /// <see cref="Path.Combine(string, string, string)"/>, which discards everything to the left of a
        /// rooted segment - so an absolute value collapses every folder's thumbnails onto one path, which
        /// carries a unique index. And <c>DlnaOptionsDefaults</c> adds it to <c>ExcludeFolders</c>, where
        /// matching is by whole path segment - so a value with a separator never matches, the scanner
        /// stops skipping the folder, and every generated preview is re-ingested as media and previewed
        /// in turn.
        /// </remarks>
        private static IEnumerable<string> ValidateThumbnailSubFolderName(DlnaOptions options)
        {
            var subFolder = options.Thumbnails.SubFolderName;

            if (string.IsNullOrWhiteSpace(subFolder))
            {
                // [Required] already reports a blank one; saying it twice is noise.
                yield break;
            }

            var isSingleSegment = !Path.IsPathRooted(subFolder)
                && subFolder.AsSpan().IndexOfAny('/', '\\') < 0
                && subFolder.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && subFolder is not ("." or "..");

            if (!isSingleSegment)
            {
                yield return
                    $"The {Label(nameof(ThumbnailOptions.SubFolderName))} ('{subFolder}') must be one folder "
                    + "name, not a path. It is created inside each of your media folders, and it has to be "
                    + "skipped when the server looks for media.";
            }
        }

        /// <summary>
        /// Exclusion entries have to be folder names, partial paths or full paths that cannot be typos.
        /// </summary>
        /// <remarks>
        /// A <b>path is now allowed</b> - <c>Films/Private</c>, or the whole path as it reads on this
        /// server. This used to refuse any entry containing a separator, which was half of a
        /// customer-reported defect: the other half was that matching was a raw substring, so
        /// <c>path1</c> also hid <c>path1L</c>. Both halves had to be fixed together, because allowing a
        /// path without aligning the match to segment boundaries would have made the over-matching worse
        /// rather than better. Either separator may be used; they are equivalent.
        /// <para>
        /// A rooted entry is <b>not</b> refused, and an earlier version of this validator did refuse one.
        /// <see cref="Core.Files.PathExclusion"/> trims an entry's surrounding separators and then looks
        /// for it as a run of whole segments anywhere in the path, so a leading <c>/</c> or a drive
        /// letter is simply part of that run and matches - which is what makes pasting a folder's full
        /// path the obvious thing an operator would try, and correct.
        /// </para>
        /// <para>
        /// The minimum length is kept as a typo guard, though segment alignment has taken most of its
        /// force: a one-character entry now hides only folders named exactly that character, where before
        /// a single letter typed into the Settings page hid the entire library from renderers and from
        /// the admin UI while the scanner carried on indexing it, with nothing saying so.
        /// </para>
        /// </remarks>
        /// <summary>
        /// The folder uploads are pinned to has to be one the library actually reads.
        /// </summary>
        /// <remarks>
        /// Like every rule here it never touches the filesystem - whether the folder exists is
        /// <c>ISourceFolderChecker</c>'s question, asked where a throw cannot poison the options monitor.
        /// This checks only that the path is spelled sanely, sits under a source folder and is not inside a
        /// folder the scan skips, because a destination outside the library would take files and then never
        /// show them. The skip test is the one <c>UploadDestination.TryResolve</c> applies to every upload,
        /// so a folder this accepts is not one each upload then refuses.
        /// </remarks>
        private static IEnumerable<string> ValidateUploadFolder(DlnaOptions options)
        {
            var folder = options.Upload.DestinationFolder;

            if (string.IsNullOrWhiteSpace(folder))
            {
                yield break;
            }

            var label = Label(nameof(UploadOptions.DestinationFolder));

            if (PathSegments.HasDotSegment(folder))
            {
                yield return
                    $"The {label} must not contain '.' or '..'. Those are shorthand for \"this folder\" "
                    + "and \"the folder above\", so a destination built from them is not the one you meant.";

                yield break;
            }

            if (!TryResolveFullPath(folder, out var resolved))
            {
                yield return $"The {label} '{folder}' is not a usable path.";

                yield break;
            }

            // The preview folder is named as well as the list: DlnaOptionsDefaults adds it to the list
            // before validation, but a caller that skips the defaults must not get a different answer.
            if (PathExclusion.IsExcluded(resolved, [.. options.Library.ExcludeFolders, options.Thumbnails.SubFolderName]))
            {
                yield return
                    $"The {label} '{folder}' is inside one of your {Label(nameof(LibraryOptions.ExcludeFolders))} "
                    + $"or the {Label(nameof(ThumbnailOptions.SubFolderName))}, which the library skips, so "
                    + "every upload there would be refused.";

                yield break;
            }

            foreach (var source in options.Library.SourceFolders)
            {
                if (string.IsNullOrWhiteSpace(source) || !TryResolveFullPath(source, out var sourceRoot))
                {
                    continue;
                }

                if (string.Equals(resolved, sourceRoot, StringComparison.Ordinal)
                    || resolved.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    yield break;
                }
            }

            yield return
                $"The {label} '{folder}' is not inside any of your "
                + $"{Label(nameof(LibraryOptions.SourceFolders))}, so anything uploaded there would never "
                + "appear in the library.";
        }

        private static IEnumerable<string> ValidateExcludeFolders(DlnaOptions options)
        {
            // DlnaOptionsDefaults adds the preview folder to this list unconditionally, before validation
            // runs. Measuring the typo guard over it meant a short preview folder name - ".t", "th" -
            // passed its own rule and then refused the boot complaining about an excluded folder, naming
            // a list the operator cannot see that entry in and has no way to correct.
            var injected = options.Thumbnails.SubFolderName?.Trim().Trim('/', '\\') ?? string.Empty;

            foreach (var name in options.Library.ExcludeFolders)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    // Skipped everywhere it is consumed, so it is harmless rather than wrong.
                    continue;
                }

                // Measured on the trimmed value: a separator is now legal, so " /a/ " must not pass a
                // length check that a bare "a" would fail.
                var trimmed = name.Trim().Trim('/', '\\');

                // Case-insensitively, matching how DlnaOptionsDefaults de-duplicates the list.
                var isInjected = injected.Length > 0
                    && string.Equals(trimmed, injected, StringComparison.OrdinalIgnoreCase);

                if (trimmed.Length < MinimumExcludeFolderLength && !isInjected)
                {
                    yield return
                        $"The excluded folder '{name}' is too short. Fewer than "
                        + $"{MinimumExcludeFolderLength} characters is far more likely to be a typing slip "
                        + "than a folder anyone meant to hide.";
                }

                if (PathSegments.HasDotSegment(trimmed))
                {
                    yield return
                        $"The excluded folder '{name}' must not contain '.' or '..'. Those are shorthand "
                        + "for \"this folder\" and \"the folder above\", so they never match a real folder.";
                }
            }
        }

        /// <summary>
        /// The same per-entry rules as the excluded folders, on the list that hides without excluding.
        /// </summary>
        /// <remarks>
        /// Entries take the same form and are matched by the same rule, so a slip that is a typo in one
        /// list is a typo in the other. Nothing is ever injected into this one, so there is no exemption
        /// to make - and it deliberately takes no part in the thumbnail-cache containment check, which is
        /// about what the scanner skips, and these folders are scanned.
        /// </remarks>
        private static IEnumerable<string> ValidateTemporarilyHiddenFolders(DlnaOptions options)
        {
            foreach (var name in options.Library.TemporarilyHiddenFolders)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var trimmed = name.Trim().Trim('/', '\\');

                if (trimmed.Length < MinimumExcludeFolderLength)
                {
                    yield return
                        $"The temporarily hidden folder '{name}' is too short. Fewer than "
                        + $"{MinimumExcludeFolderLength} characters is far more likely to be a typing slip "
                        + "than a folder anyone meant to hide.";
                }

                if (PathSegments.HasDotSegment(trimmed))
                {
                    yield return
                        $"The temporarily hidden folder '{name}' must not contain '.' or '..'. Those are "
                        + "shorthand for \"this folder\" and \"the folder above\", so they never match a "
                        + "real folder.";
                }
            }
        }

        /// <summary>
        /// Checks each subtitle type as <c>DlnaOptionsDefaults</c> left it - lower case, with a leading dot.
        /// </summary>
        /// <remarks>
        /// A type that is also media is the one mistake here that fails quietly: the scanner resolves media
        /// first, so those files would be indexed as items of their own and never linked. The catalog counts
        /// as well as <c>MediaFileExtensions</c>, because the scanner falls back to it.
        /// </remarks>
        private static IEnumerable<string> ValidateSubtitleFileExtensions(DlnaOptions options)
        {
            var label = Label(nameof(LibraryOptions.SubtitleFileExtensions));
            var mediaExtensions = new HashSet<string>(options.Library.MediaFileExtensions.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (var (extension, kind) in options.Library.SubtitleFileExtensions)
            {
                if (extension.Length <= 1 || extension.AsSpan(1).ContainsAny(_rejectedExtensionCharacters))
                {
                    yield return
                        $"'{extension}' in the {label} cannot be used as an extension - give one such as .srt, "
                        + "without spaces or slashes.";

                    continue;
                }

                if (kind is not (DlnaMedia.Video or DlnaMedia.Audio))
                {
                    yield return
                        $"'{extension}' in the {label} has to go with Video, for subtitles, or Audio, for lyrics.";
                }

                if (mediaExtensions.Contains(extension)
                    || (DlnaMimeCatalog.TryGetByFileExtension(extension, out var mime) && mime.IsPresentableMedia()))
                {
                    yield return
                        $"'{extension}' is in the {label} but is also a media file type, so those files would be "
                        + "added to the library on their own instead of being linked as subtitles.";
                }
            }
        }

        /// <summary>
        /// Resolves a configured path, reporting failure rather than throwing out of validation.
        /// </summary>
        /// <remarks>
        /// <see cref="Path.GetFullPath(string)"/> throws on a malformed path, and an exception that is
        /// not an <c>OptionsValidationException</c> escapes the options machinery entirely - so a single
        /// bad character in <c>config.json</c> replaced the operator-readable failure list with a bare
        /// <c>ArgumentException</c> at startup, and poisoned every <c>CurrentValue</c> read on a reload.
        /// <c>Settings.razor</c> already catches exactly this around the same property; the
        /// hand-edited-file path had nothing.
        /// </remarks>
        private static bool TryResolveFullPath(string path, out string resolved)
        {
            try
            {
                resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

                return true;
            }
            catch (Exception exception)
                when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                resolved = string.Empty;

                return false;
            }
        }

        /// <summary>
        /// A thumbnail cache inside a source folder would be rediscovered as media on the next scan,
        /// unless the scanner is told to skip it.
        /// </summary>
        /// <remarks>
        /// Excluding the cache's folder name is what makes the arrangement safe, and it is how the
        /// reference kept its own thumbnails out of its media tree. It is also what the source-folder
        /// fallback in <see cref="DlnaOptionsDefaults"/> relies on: with no folder configured the
        /// application's own directory becomes the library, and the cache lives inside it by default.
        /// </remarks>
        private static IEnumerable<string> ValidateThumbnailCacheOutsideLibrary(DlnaOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Thumbnails.CacheDirectory))
            {
                yield break;
            }

            if (!TryResolveFullPath(options.Thumbnails.CacheDirectory, out var cacheRoot))
            {
                yield return
                    $"The {Label(nameof(ThumbnailOptions.CacheDirectory))} "
                    + $"('{options.Thumbnails.CacheDirectory}') is not a usable path.";

                yield break;
            }

            foreach (var folder in options.Library.SourceFolders)
            {
                // Directory.Exists used to be tested here as well, and it was the last of the filesystem
                // touches this class removed everywhere else - see ValidateSourceFolders' remark for what
                // they cost. Whether the cache sits inside a configured source folder is a question about
                // the configured paths, so it must answer the same way whether or not a share happens to
                // be mounted at this instant; availability belongs to ISourceFolderChecker.
                if (string.IsNullOrWhiteSpace(folder))
                {
                    continue;
                }

                // Ordinal, not OrdinalIgnoreCase. The target is Linux, where two paths differing only in
                // case are two different directories - and MediaDirectoryEntityConfiguration makes
                // FullPath case-sensitive for that reason. LibraryIndexer.IsInsideAnySourceFolder does
                // the identical containment check ordinally; this was the outlier, and folding case here
                // could refuse to boot over a directory that merely shares a prefix.
                if (!TryResolveFullPath(folder, out var sourceRoot))
                {
                    // A source folder that is not a usable path is ValidateSourceFolders' finding to
                    // report, not this one's - saying it twice in different words helps nobody.
                    continue;
                }
                var isInside = cacheRoot.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || string.Equals(cacheRoot, sourceRoot, StringComparison.Ordinal);

                if (!isInside)
                {
                    continue;
                }

                if (string.Equals(cacheRoot, sourceRoot, StringComparison.Ordinal))
                {
                    yield return
                        $"The {Label(nameof(ThumbnailOptions.CacheDirectory))} ('{cacheRoot}') is the source "
                        + "folder itself, so the previews it makes would be added to your library as media. "
                        + "Nothing can prevent that except moving it.";
                    continue;
                }

                if (!IsExcludedFromScanning(cacheRoot, options.Library.ExcludeFolders))
                {
                    yield return
                        $"The {Label(nameof(ThumbnailOptions.CacheDirectory))} ('{cacheRoot}') is inside the "
                        + $"source folder '{sourceRoot}', and none of the folder names leading to it are in "
                        + "the excluded list. The previews it makes would be added to your library as media.";
                }
            }
        }

        /// <summary>
        /// Whether scanning would skip the cache, asked of the same rule the scanner uses.
        /// </summary>
        /// <remarks>
        /// This used to be a <b>third</b> implementation of the exclusion rule, and a narrower one: it
        /// split the path below the source root and compared single segments with no trimming, so a
        /// legal multi-segment entry - <c>Films/.@__thumb</c>, the form the customer fix introduced -
        /// could never satisfy it. The validator then refused to boot, asserting the cache was
        /// unprotected over a cache scanning would in fact have skipped, with a message that was simply
        /// wrong. Asking <see cref="PathExclusion"/> is what makes the answer the scanner's own.
        /// <para>
        /// The whole path is tested rather than the part below the source root, which is also what the
        /// scanner does: an entry matching a segment of the root's own path really would make it skip
        /// everything. That can only ever make this <i>less</i> likely to refuse a boot.
        /// </para>
        /// </remarks>
        private static bool IsExcludedFromScanning(string cacheRoot, IList<string> excludedEntries)
        {
            // Materialised because PathExclusion takes IReadOnlyList and IList does not convert to it.
            // Once per validation, and only when CacheDirectory is set at all.
            return PathExclusion.IsExcluded(cacheRoot.AsSpan(), [.. excludedEntries]);
        }

        private static void ValidateAnnotations(object instance, string sectionName, List<string> failures)
        {
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true))
            {
                return;
            }

            foreach (var result in results)
            {
                failures.Add($"{Label(sectionName)}: {Describe(result)}");
            }
        }

        private static string Label(string memberName)
        {
            return OperatorLabels.TryGetValue(memberName, out var label)
                ? label
                : memberName;
        }

        /// <remarks>
        /// A data-annotation message names the property it came from - "The field MaxWidth must be between
        /// 16 and 4096" - so the property name is swapped for the operator's label rather than the message
        /// being replaced. Rewriting them properly would mean an explicit <c>ErrorMessage</c> on all
        /// twenty-five attributes across six options classes, which is a wider change than this one.
        /// </remarks>
        private static string Describe(ValidationResult result)
        {
            var message = result.ErrorMessage ?? "is not valid.";

            foreach (var member in result.MemberNames)
            {
                if (OperatorLabels.TryGetValue(member, out var label))
                {
                    message = message.Replace(member, label, StringComparison.Ordinal);
                }
            }

            return message;
        }
    }
}
