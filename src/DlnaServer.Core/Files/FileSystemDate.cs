namespace DlnaServer.Core.Files
{
    /// <summary>
    /// The one rule for turning what a filesystem reports about an entry's age into a date worth storing.
    /// </summary>
    /// <remarks>
    /// Shared by the file scanner and by the indexer's directory pass, which is why it lives here rather
    /// than privately in either: the first fill dates files and folders alike from the filesystem, and
    /// two copies of a plausibility rule is how the two halves of a rule stop agreeing.
    /// </remarks>
    public static class FileSystemDate
    {
        /// <summary>
        /// Below this, a reported creation time is taken as absent rather than real.
        /// </summary>
        /// <remarks>
        /// A filesystem with no birth time reports the Unix epoch or DateTime.MinValue, and 1980 is
        /// comfortably below any media file while being far above both.
        /// </remarks>
        private const int EarliestPlausibleYear = 1980;

        /// <summary>
        /// The most trustworthy date the filesystem offers for an entry.
        /// </summary>
        /// <remarks>
        /// Creation time when it is plausible, otherwise last-write time. <b>Not every filesystem
        /// records a birth time</b>, and .NET has nothing to report when it is absent: the value comes
        /// back as the Unix epoch or as <see cref="DateTime.MinValue"/> depending on the platform and the
        /// mount. Using it unchecked would give every entry on such a volume the same 1970 date, which is
        /// exactly the no-ordering this is meant to avoid - and would look like a working feature.
        /// <para>
        /// A creation time in the future is treated the same way. A clock that ran ahead, or an archive
        /// unpacked with bad metadata, otherwise pins entries to the top of Recently added permanently.
        /// Last-write time is always populated and, for a copied entry, is often the better answer anyway.
        /// </para>
        /// <para>
        /// Never returns <c>default(DateTime)</c>, which the first-fill path relies on: an explicit
        /// <see cref="DateTime.MinValue"/> and a null collapse into the same thing at
        /// <c>DlnaDbContext.StampTimestamps</c>, so a row would silently take the clock instead.
        /// </para>
        /// </remarks>
        public static DateTime Resolve(DateTime creationTimeUtc, DateTime modifiedUtc)
        {
            var isPlausible = creationTimeUtc.Year > EarliestPlausibleYear
                && creationTimeUtc <= DateTime.UtcNow.AddDays(1);

            return isPlausible ? creationTimeUtc : modifiedUtc;
        }
    }
}
