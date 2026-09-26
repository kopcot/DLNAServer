namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// How many files the visible library holds, and how that splits by kind.
    /// </summary>
    /// <remarks>
    /// Counted over the same rows a listing would show - anything under a hidden folder is left out - so
    /// these agree with the Library page rather than with the row count of the table. The kind is not
    /// stored: it is derived from each file's MIME, so anything whose MIME maps to no kind lands in
    /// <see cref="Other"/>, which is what keeps the four parts summing to <see cref="Total"/>.
    /// </remarks>
    public sealed record LibraryCountsDto
    {
        public required int Total { get; init; }

        public required int Video { get; init; }

        public required int Audio { get; init; }

        public required int Image { get; init; }

        public required int Other { get; init; }

        /// <summary>
        /// Linked subtitle and lyrics files, leaving out links the operator removed.
        /// </summary>
        /// <remarks>
        /// Not part of <see cref="Total"/>: a linked file belongs to a media file rather than being one of the
        /// library's own files, so the four kinds above still add up to the total on their own.
        /// </remarks>
        public required int Subtitles { get; init; }
    }
}
