using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DlnaServer.Persistence.Configurations
{
    /// <summary>
    /// Applies the columns every entity shares. Derived configurations override
    /// <see cref="ConfigureEntity"/> and leave the base columns alone.
    /// </summary>
    /// <remarks>
    /// An explicit base class rather than the reference's reflection-driven generic configuration,
    /// so a mistake surfaces at compile time instead of during model building.
    /// </remarks>
    internal abstract class EntityBaseConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
        where TEntity : EntityBase
    {
        /// <summary>
        /// Collation giving case-insensitive comparison of names and titles.
        /// </summary>
        /// <remarks>
        /// Replaces the reference's duplicated <c>LC_</c> computed columns, which doubled the width of
        /// every text-bearing table and were generated with MySQL-style backtick quoting.
        /// </remarks>
        protected const string CaseInsensitiveCollation = "NOCASE";

        /// <summary>
        /// Whether this entity is ever queried by <see cref="EntityBase.CreatedUtc"/>.
        /// </summary>
        /// <remarks>
        /// Opt-in, and it used to be unconditional - so all nine tables carried an
        /// <c>IX_&lt;Table&gt;_CreatedUtc</c> while only two are ever ordered or filtered by it. The other
        /// seven paid a B-tree write per row for a query nothing makes, which is exactly the trade
        /// <c>MediaFileTagEntityConfiguration</c> refuses by hand for its own <c>Name</c> column - on a
        /// table that can hold hundreds of rows per file. <c>BrowseCoveringIndexes</c> had already
        /// dropped five unused single-column indexes for the same reason and simply never reached the
        /// ones this base class was adding.
        /// </remarks>
        protected virtual bool IsQueriedByCreatedUtc => false;

        public void Configure(EntityTypeBuilder<TEntity> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.HasKey(static e => e.Id);

            _ = builder.Property(static e => e.Id)
                .ValueGeneratedOnAdd();

            // Sequential rather than random so the unique index appends instead of fragmenting.
            _ = builder.Property(static e => e.PublicId)
                .IsRequired()
                .ValueGeneratedOnAdd()
                .HasValueGenerator<SequentialGuidValueGenerator>();

            _ = builder.HasIndex(static e => e.PublicId)
                .IsUnique();

            _ = builder.Property(static e => e.CreatedUtc)
                .IsRequired();

            _ = builder.Property(static e => e.ModifiedUtc);

            if (IsQueriedByCreatedUtc)
            {
                _ = builder.HasIndex(static e => e.CreatedUtc);
            }

            ConfigureEntity(builder);
        }

        protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
    }
}
