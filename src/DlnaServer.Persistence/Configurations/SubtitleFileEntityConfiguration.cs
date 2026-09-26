using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class SubtitleFileEntityConfiguration : EntityBaseConfiguration<SubtitleFileEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<SubtitleFileEntity> builder)
        {
            _ = builder.ToTable("SubtitleFiles");

            // Also the index every "has subtitles" check and every per-page lookup reads by.
            _ = builder.HasIndex(static e => new { e.MediaFileId, e.RelativePath })
                .IsUnique();

            _ = builder.HasOne(static e => e.MediaFile)
                .WithMany(static f => f.SubtitleFiles)
                .HasForeignKey(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.Property(static e => e.RelativePath)
                .HasMaxLength(1024);

            _ = builder.Property(static e => e.Language)
                .HasMaxLength(32);
        }
    }
}
