using System.Globalization;

namespace DlnaServer.Core.Files
{
    /// <summary>
    /// Builds the path a file is moved or copied to when it is replaced, so the original is never lost.
    /// </summary>
    /// <remarks>
    /// Everything this server keeps a copy of goes into a <c>backup</c> folder beside the original rather
    /// than sitting next to it. Both kinds accumulate - a settings backup on every save from the admin
    /// pages, a database backup on every failed start - and left in place they bury <c>config.json</c>
    /// and <c>dlna.sqlite</c> in a deployment folder an operator has to read.
    /// <para>
    /// The two suffixes mean different things and are deliberately not merged. <c>.corrupt-</c> is a file
    /// that could not be used and was moved aside; <c>.saved-</c> is a perfectly good file that was
    /// replaced on purpose. Calling the second one corrupt would be a lie to whoever finds it.
    /// </para>
    /// </remarks>
    public static class BackupFilePath
    {
        /// <summary>
        /// Folder, relative to the original file, that every backup is written into.
        /// </summary>
        public const string FolderName = "backup";

        /// <summary>
        /// Path for a file that could not be used, of the form
        /// <c>backup/&lt;name&gt;.corrupt-yyyy-MM-dd_HHmm</c>, which does not yet exist.
        /// </summary>
        /// <remarks>
        /// Timestamps carry minutes but not seconds, per project convention, so two failures inside the
        /// same minute would collide. A numeric suffix is appended in that case - without it the second
        /// backup silently overwrites the first, destroying the very file it was meant to preserve.
        /// </remarks>
        public static string CreateUnique(string originalPath, DateTime utcNow)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);

            var candidate = Resolve(originalPath, "corrupt", utcNow);

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            for (var attempt = 2; attempt < int.MaxValue; attempt++)
            {
                var numbered = $"{candidate}-{attempt.ToString(CultureInfo.InvariantCulture)}";

                if (!File.Exists(numbered))
                {
                    return numbered;
                }
            }

            throw new IOException($"Unable to find a free backup name for '{originalPath}'.");
        }

        /// <summary>
        /// Path for a usable file about to be replaced, of the form
        /// <c>backup/&lt;name&gt;.saved-yyyy-MM-dd_HHmm</c>.
        /// </summary>
        /// <remarks>
        /// Not made unique, unlike <see cref="CreateUnique"/>: two saves inside one minute overwrite one
        /// another on purpose. This exists to undo a mistaken edit, not to be an audit trail, and an
        /// operator adjusting one field at a time would otherwise fill the folder in a sitting.
        /// </remarks>
        public static string CreateSaved(string originalPath, DateTime utcNow)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);

            return Resolve(originalPath, "saved", utcNow);
        }

        /// <summary>
        /// The backup folder for a given file, created if it is not there yet.
        /// </summary>
        /// <remarks>
        /// Creating it here rather than at each call site is a deliberate side effect in something named
        /// for paths. Three callers move or copy a file straight into the returned path, and every one of
        /// them would otherwise need the same guard - <see cref="File.Move(string, string)"/> throws
        /// <see cref="DirectoryNotFoundException"/> rather than creating anything. Callers already treat
        /// a failure here as a failed backup.
        /// </remarks>
        public static string EnsureFolder(string originalPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);

            // An originalPath with no directory part is a bare file name, which resolves against the
            // working directory - so the backup folder has to as well, rather than landing at the root.
            var directory = Path.GetDirectoryName(originalPath);
            var folder = string.IsNullOrEmpty(directory)
                ? FolderName
                : Path.Combine(directory, FolderName);

            _ = Directory.CreateDirectory(folder);

            return folder;
        }

        private static string Resolve(string originalPath, string kind, DateTime utcNow)
        {
            var stamp = utcNow.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture);
            var name = Path.GetFileName(originalPath);

            return Path.Combine(EnsureFolder(originalPath), $"{name}.{kind}-{stamp}");
        }
    }
}
