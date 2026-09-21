using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class ServerInstanceEntityConfiguration : EntityBaseConfiguration<ServerInstanceEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<ServerInstanceEntity> builder)
        {
            _ = builder.ToTable("ServerInstances");

            _ = builder.Property(static e => e.MachineName)
                .IsRequired()
                .HasMaxLength(128);

            _ = builder.HasIndex(static e => e.MachineName)
                .IsUnique();
        }
    }
}
