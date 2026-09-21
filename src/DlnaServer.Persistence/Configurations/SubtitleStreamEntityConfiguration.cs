using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class SubtitleStreamEntityConfiguration : EntityBaseConfiguration<SubtitleStreamEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<SubtitleStreamEntity> builder)
        {
            _ = builder.ToTable("SubtitleStreams");

            // Many per file, unlike audio and video - the reference stored only the first track.
            // The search page's language filter reads distinct languages and then filters on one.
            _ = builder.HasIndex(static e => e.Language);

            _ = builder.HasIndex(static e => new { e.MediaFileId, e.StreamIndex })
                .IsUnique();

            _ = builder.Property(static e => e.Codec)
                .HasMaxLength(64);

            _ = builder.Property(static e => e.Language)
                .HasMaxLength(32);

            _ = builder.Property(static e => e.ExternalFilePath)
                .HasMaxLength(4096);
        }
    }
}
