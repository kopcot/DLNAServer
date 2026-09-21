namespace DlnaServer.Persistence.Entities
{
    /// <summary>
    /// Common identity and audit columns for every persisted entity.
    /// </summary>
    internal abstract class EntityBase
    {
        /// <summary>
        /// Surrogate primary key, and the target of every foreign key inside the database.
        /// </summary>
        /// <remarks>
        /// An integer rather than a GUID because it is what the indexes and joins are built on:
        /// 4 bytes per key instead of 16, sequential so inserts append rather than fragment.
        /// It never leaves this assembly.
        /// </remarks>
        public int Id { get; set; }

        /// <summary>
        /// Stable identifier used outside the database - DTOs, DLNA ObjectIDs and admin URLs all carry this.
        /// </summary>
        /// <remarks>
        /// Kept separate from <see cref="Id"/> so that nothing external depends on a value that reveals
        /// row counts or insertion order, and so the internal key can stay a compact integer.
        /// </remarks>
        public Guid PublicId { get; set; }

        /// <summary>
        /// When the row was created, in UTC. The reference stored local time, which shifts meaning
        /// across a timezone change and cannot be compared between machines.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        public DateTime? ModifiedUtc { get; set; }
    }
}
