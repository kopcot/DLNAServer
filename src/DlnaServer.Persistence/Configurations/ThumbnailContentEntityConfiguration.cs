using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class ThumbnailContentEntityConfiguration : EntityBaseConfiguration<ThumbnailContentEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ThumbnailContentEntity> builder)
        {
            _ = builder.ToTable("ThumbnailContents");

            _ = builder.Property(static e => e.Data)
                .IsRequired();
        }
    }
}
