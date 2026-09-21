using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class UploadDeviceEntityConfiguration : EntityBaseConfiguration<UploadDeviceEntity>
    {
        protected override void ConfigureEntity(EntityTypeBuilder<UploadDeviceEntity> builder)
        {
            _ = builder.ToTable("UploadDevices");

            // The lookup on every visit to the upload page, and the reason a device has exactly one row.
            _ = builder.Property(static e => e.Fingerprint)
                .IsRequired()
                .HasMaxLength(128);

            _ = builder.HasIndex(static e => e.Fingerprint)
                .IsUnique();

            _ = builder.Property(static e => e.RemoteAddress)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(static e => e.UserAgent)
                .IsRequired()
                .HasMaxLength(512);

            _ = builder.Property(static e => e.AcceptLanguage)
                .HasMaxLength(256);

            // A destination is a path, so it takes the same width every other path column has.
            _ = builder.Property(static e => e.LastDestination)
                .IsRequired()
                .HasMaxLength(4096);
        }
    }
}
