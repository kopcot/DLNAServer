namespace DlnaServer.Core.Contracts
{
    /// <summary>
    /// The language tags the indexed library actually contains, for offering as search filters.
    /// </summary>
    /// <remarks>
    /// Read from the index rather than from a fixed list of the world's languages, so the search only ever
    /// offers a tick that can match something. Both lists are sorted and hold the container's own tags,
    /// usually ISO 639-2.
    /// </remarks>
    public sealed record MediaLanguagesDto
    {
        public required IReadOnlyList<string> Audio { get; init; }

        public required IReadOnlyList<string> Subtitle { get; init; }
    }
}
