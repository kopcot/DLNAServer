using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class MediaDirectoryEntityConfiguration : EntityBaseConfiguration<MediaDirectoryEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<MediaDirectoryEntity> builder)
        {
            _ = builder.ToTable("Directories");

            // Case-SENSITIVE uniqueness on purpose. The server's target is Linux, where Foo.mkv and foo.mkv
            // are two files; the reference made this index case-insensitive and would have silently rejected
            // the second one.
            _ = builder.Property(static e => e.FullPath)
                .IsRequired()
                .HasMaxLength(4096);

            _ = builder.HasIndex(static e => e.FullPath)
                .IsUnique();

            _ = builder.Property(static e => e.Name)
                .IsRequired()
                .HasMaxLength(512)
                .UseCollation(CaseInsensitiveCollation);


            _ = builder.Property(static e => e.Depth)
                .IsRequired();

            _ = builder.Property(static e => e.IsSourceRoot)
                .IsRequired();

            // IsSourceRoot only. Depth was never filtered or ordered on - only written and projected -
            // and ParentDirectoryId is now the leading column of the two composites below, which SQLite
            // uses for a parent-only lookup just as well as a dedicated index would.
            _ = builder.HasIndex(static e => e.IsSourceRoot);

            // Both listings a folder click produces: children ordered by name, and children ordered by
            // date. Single-column indexes let SQLite satisfy the filter or the order but never both, so
            // it sorted the matched rows in a temp B-tree - and UseMemoryTempStore is false, which puts
            // that B-tree on the platter, on the exact path the p50/p90 targets measure.
            _ = builder.HasIndex(static e => new { e.ParentDirectoryId, e.Name });
            _ = builder.HasIndex(static e => new { e.ParentDirectoryId, e.CreatedUtc });

            _ = builder.HasOne(static e => e.ParentDirectory)
                .WithMany(static e => e.Subdirectories)
                .HasForeignKey(static e => e.ParentDirectoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
