namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// Where a file sits among the playable files of its folder, and what is either side of it.
    /// </summary>
    /// <remarks>
    /// Exists so the preview page can draw its Previous and Next buttons without materialising the whole
    /// folder. It used to read every file in the directory as a full <see cref="MediaFileDto"/> and keep
    /// two of them, which on a folder of 1,564 photographs is a transient array of 1,564 records with
    /// every path, stamp and metadata field on each - to render two buttons and a position.
    /// <para>
    /// Identifiers rather than DTOs because that is all the page does with them: a null check to disable
    /// a button, and the identifier to navigate to. Fetching the neighbour itself is the next page's job.
    /// </para>
    /// </remarks>
    public sealed record MediaFileNeighboursDto
    {
        /// <summary>
        /// The playable file before this one, or <see langword="null"/> when it is the first.
        /// </summary>
        public Guid? PreviousPublicId { get; init; }

        /// <summary>
        /// The playable file after this one, or <see langword="null"/> when it is the last.
        /// </summary>
        public Guid? NextPublicId { get; init; }

        /// <summary>
        /// Zero-based position among the playable files, or <c>-1</c> when this file is not one of them.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// How many playable files the folder holds.
        /// </summary>
        public int Count { get; init; }
    }
}
