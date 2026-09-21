using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class MediaFileTagEntityConfiguration : EntityBaseConfiguration<MediaFileTagEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<MediaFileTagEntity> builder)
        {
            _ = builder.ToTable("MediaFileTags");

            // Read one whole file at a time and never searched across files, so the file is the index and
            // there is no index on the name: adding one would cost a write per tag on a library-wide
            // metadata pass to serve a query nothing makes.
            _ = builder.HasIndex(static e => e.MediaFileId);

            // Declared from this side with no inverse navigation, so adding tags leaves MediaFileEntity
            // and its configuration untouched. Deleting a file still takes its tags with it.
            _ = builder.HasOne<MediaFileEntity>()
                .WithMany()
                .HasForeignKey(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.Property(static e => e.Name)
                .HasMaxLength(128);

            // Lyrics arrive here, and a comment tag can hold an entire description, so this is the one
            // child column that is deliberately not short. Truncation happens before the insert - see
            // MediaFileRepository.SaveMetadataAsync - because SQLite does not enforce the length itself
            // and an oversized value would otherwise be stored and only surprise a later reader.
            _ = builder.Property(static e => e.Value)
                .HasMaxLength(8192);
        }
    }
}
