namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// One tag a media file carries about itself, stored as a name and a value.
    /// </summary>
    /// <remarks>
    /// A row per tag rather than a column per tag: the set of tags is chosen by whatever wrote the file,
    /// so any fixed schema is wrong as soon as a file uses one that was not anticipated. The typed
    /// columns on <see cref="MediaFileEntity"/> and the stream tables stay exactly as they are - those
    /// are the values browsing and DLNA reason about, and these are for an operator to read. Nothing
    /// already in the schema is altered to make room for this table.
    /// </remarks>
    internal sealed class MediaFileTagEntity : EntityBase
    {
        /// <summary>
        /// Owning file. A plain foreign key with no navigation on either side, deliberately: this table
        /// was added without altering any existing one, so <see cref="MediaFileEntity"/> does not know
        /// about it and the relationship is declared from here alone.
        /// </summary>
        public int MediaFileId { get; set; }

        /// <summary>
        /// Position of the track the tag belongs to, or null when it describes the whole file.
        /// </summary>
        public int? StreamIndex { get; set; }

        public required string Name { get; set; }

        public required string Value { get; set; }
    }
}
