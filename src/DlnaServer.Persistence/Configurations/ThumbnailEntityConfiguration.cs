using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class ThumbnailEntityConfiguration : EntityBaseConfiguration<ThumbnailEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ThumbnailEntity> builder)
        {
            _ = builder.ToTable("Thumbnails");

            _ = builder.HasIndex(static e => e.MediaFileId)
                .IsUnique();

            _ = builder.Property(static e => e.FilePath)
                .IsRequired()
                .HasMaxLength(4096);

            _ = builder.HasIndex(static e => e.FilePath)
                .IsUnique();

            _ = builder.Property(static e => e.Mime)
                .IsRequired()
                .HasConversion<int>();

            // Deleting the thumbnail row takes its blob with it, which needs the foreign key on the BLOB
            // side: cascade flows principal to dependent, so with the key on this table the cascade ran
            // the wrong way and deleting a thumbnail orphaned its bytes instead. The blob is a separate
            // table only so that listing thumbnails does not load every image into memory.
            _ = builder.HasOne(static e => e.Content)
                .WithOne(static c => c!.Thumbnail)
                .HasForeignKey<ThumbnailContentEntity>(static c => c.ThumbnailId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
