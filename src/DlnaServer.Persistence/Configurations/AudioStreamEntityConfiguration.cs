using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class AudioStreamEntityConfiguration : EntityBaseConfiguration<AudioStreamEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<AudioStreamEntity> builder)
        {
            _ = builder.ToTable("AudioStreams");

            // Many per file, like subtitles: a film with several dubs has one row per language. The unique
            // index this replaces was on MediaFileId alone and would have rejected the second track.
            // The search page's language filter reads distinct languages and then filters on one.
            _ = builder.HasIndex(static e => e.Language);

            _ = builder.HasIndex(static e => new { e.MediaFileId, e.StreamIndex })
                .IsUnique();

            _ = builder.Property(static e => e.Codec)
                .HasMaxLength(64);

            _ = builder.Property(static e => e.Language)
                .HasMaxLength(32);

            _ = builder.Property(static e => e.Title)
                .HasMaxLength(256);
        }
    }
}
