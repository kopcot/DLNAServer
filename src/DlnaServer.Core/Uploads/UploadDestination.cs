using DlnaServer.Core.Configuration;
using DlnaServer.Core.Files;

namespace DlnaServer.Core.Uploads
{
    /// <summary>
    /// Decides which folders an upload may be sent to, and turns the operator's choice into a path.
    /// </summary>
    /// <remarks>
    /// One rule, used by both halves: the page offers <see cref="Roots"/> and the endpoint that receives
    /// the files calls <see cref="TryResolve"/> on whatever came back. The endpoint never trusts the page
    /// - a form is a text field the browser can be made to say anything in - so every check is here
    /// rather than in the markup.
    /// </remarks>
    public static class UploadDestination
    {
        /// <summary>
        /// The folders uploads may be sent to: the one the operator pinned, or every source folder.
        /// </summary>
        /// <remarks>
        /// Resolved to full paths rather than returned as configured, so that the folder the page offers,
        /// the folder the endpoint writes into and the folder remembered against the device are all the
        /// same string. They were not: everything downstream runs the path through
        /// <see cref="Path.GetFullPath(string)"/>, so a source folder written in any other form - a short
        /// 8.3 path on Windows is the one that caught this - was offered in one spelling and recorded in
        /// another, and the remembered destination then matched no root and was silently dropped.
        /// A path too malformed to resolve is left out; validation reports it separately.
        /// </remarks>
        public static IReadOnlyList<string> Roots(DlnaOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var pinned = options.Upload.DestinationFolder;

            var configured = string.IsNullOrWhiteSpace(pinned)
                ? options.Library.SourceFolders
                : [pinned];

            var roots = new List<string>(configured.Count);

            foreach (var entry in configured)
            {
                if (!string.IsNullOrWhiteSpace(entry) && TryResolveFullPath(entry, out var resolved))
                {
                    roots.Add(resolved);
                }
            }

            return roots;
        }

        private static bool TryResolveFullPath(string path, out string resolved)
        {
            try
            {
                resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));

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
        /// Turns a chosen root and an optional sub-folder into the folder to write into.
        /// </summary>
        /// <param name="options">The current configuration.</param>
        /// <param name="root">The root the operator chose, which must be one of <see cref="Roots"/>.</param>
        /// <param name="subFolder">A relative folder under it, created if it does not exist. May be empty.</param>
        /// <param name="fullPath">The folder to write into.</param>
        /// <param name="problem">Why the choice was refused, in the operator's terms.</param>
        /// <returns>True when <paramref name="fullPath"/> may be written into.</returns>
        public static bool TryResolve(
            DlnaOptions options,
            string? root,
            string? subFolder,
            out string fullPath,
            out string problem)
        {
            ArgumentNullException.ThrowIfNull(options);

            fullPath = string.Empty;

            var roots = Roots(options);
            var chosen = string.IsNullOrWhiteSpace(root) ? null : root.Trim();

            if (chosen is null)
            {
                problem = "No destination folder was chosen.";

                return false;
            }

            if (!TryResolveFullPath(chosen, out var chosenRoot)
                || !roots.Contains(chosenRoot, StringComparer.Ordinal))
            {
                problem = "That destination folder is not one this server accepts uploads into.";

                return false;
            }

            if (!TryCombine(chosenRoot, subFolder, out var combined, out problem))
            {
                return false;
            }

            // The whole resolved path is tested, not just the part typed in: a sub-folder called
            // ".@__thumb" would put media inside the preview cache, which the scanner never descends into.
            if (PathExclusion.IsExcluded(combined, [.. options.Library.ExcludeFolders]))
            {
                problem = "That folder is one the library is configured to skip, so nothing put there "
                    + "would ever appear.";

                return false;
            }

            fullPath = combined;
            problem = string.Empty;

            return true;
        }

        private static bool TryCombine(string root, string? subFolder, out string combined, out string problem)
        {
            // The root arrives already resolved: the caller matched it against the folders this server
            // offers, which are themselves full paths.
            combined = string.Empty;

            if (string.IsNullOrWhiteSpace(subFolder))
            {
                combined = root;
                problem = string.Empty;

                return true;
            }

            var normalised = subFolder.Trim().Replace('\\', '/');
            var relative = normalised.Trim('/');

            if (relative.Length == 0)
            {
                combined = root;
                problem = string.Empty;

                return true;
            }

            // Rootedness is tested before the separators are trimmed: Trim('/') turns "/tmp/x" into a
            // relative path, which defeated this guard on Linux - the platform the NAS runs - while
            // Windows happened to catch the same input on the ':' instead.
            if (Path.IsPathRooted(normalised) || normalised.Contains(':', StringComparison.Ordinal))
            {
                problem = "The folder inside it must be a relative path, not a full one.";

                return false;
            }

            if (HasDotSegment(relative))
            {
                problem = "The folder inside it cannot contain '.' or '..'.";

                return false;
            }

            string resolved;

            try
            {
                resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, relative)));
            }
            catch (Exception exception)
                when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                problem = "The folder inside it is not a usable path.";

                return false;
            }

            // Belt and braces after the dot-segment check: the resolved path is compared against the root
            // it must sit under, so anything that escaped by another route is caught by where it landed
            // rather than by how it was spelled.
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                problem = "That folder is outside the destination folder.";

                return false;
            }

            if (HasLinkedSegment(root, resolved))
            {
                problem = "That folder is reached through a link, which this server does not upload into.";

                return false;
            }

            combined = resolved;
            problem = string.Empty;

            return true;
        }

        /// <summary>
        /// Whether any segment of the entry is <c>.</c> or <c>..</c>.
        /// </summary>
        /// <remarks>
        /// Walked by hand rather than with <c>Split</c>, matching <see cref="PathExclusion"/>: the
        /// enumerable span split is a .NET 9 API and <c>global.json</c> pins the 8.0 SDK.
        /// </remarks>
        private static bool HasDotSegment(ReadOnlySpan<char> entry)
        {
            var start = 0;

            for (var index = 0; index <= entry.Length; index++)
            {
                if (index != entry.Length && entry[index] is not ('/' or '\\'))
                {
                    continue;
                }

                var segment = entry[start..index];

                if (segment is "." or "..")
                {
                    return true;
                }

                start = index + 1;
            }

            return false;
        }

        /// <summary>
        /// Whether any folder between the root and the resolved destination is a link.
        /// </summary>
        /// <remarks>
        /// The containment test above is lexical: <see cref="Path.GetFullPath(string)"/> collapses <c>.</c>
        /// and <c>..</c> as text and never touches the filesystem, so a sub-folder that is a symlink out of
        /// the tree spells like an ordinary segment and passes. This is the half that looks at the disc.
        /// A linked folder is also invisible to the library - the scanner puts
        /// <see cref="FileAttributes.ReparsePoint"/> in its skip list - so a file written through one would
        /// never be indexed, and refusing it costs nothing an operator would want.
        /// </remarks>
        private static bool HasLinkedSegment(string root, string resolved)
        {
            if (string.Equals(root, resolved, StringComparison.Ordinal))
            {
                return false;
            }

            var current = root;

            foreach (var segment in resolved[(root.Length + 1)..].Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);

                var folder = new DirectoryInfo(current);

                // The first segment that does not exist ends the walk: nothing under it exists either,
                // so there is no link left to find.
                if (!folder.Exists)
                {
                    return false;
                }

                if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
