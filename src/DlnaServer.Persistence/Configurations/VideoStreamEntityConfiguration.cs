using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class VideoStreamEntityConfiguration : EntityBaseConfiguration<VideoStreamEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<VideoStreamEntity> builder)
        {
            _ = builder.ToTable("VideoStreams");

            _ = builder.HasIndex(static e => e.MediaFileId)
                .IsUnique();

            _ = builder.Property(static e => e.Codec)
                .HasMaxLength(64);

            _ = builder.Property(static e => e.AspectRatio)
                .HasMaxLength(32);

            _ = builder.Property(static e => e.PixelFormat)
                .HasMaxLength(64);
        }
    }
}
