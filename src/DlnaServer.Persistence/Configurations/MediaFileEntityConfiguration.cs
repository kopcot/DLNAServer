using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DlnaServer.Persistence.Configurations
{
    internal sealed class MediaFileEntityConfiguration : EntityBaseConfiguration<MediaFileEntity>
    {
        /// <remarks>
        /// <c>GetRecentlyAddedAsync</c> orders by it and <c>GetPendingProcessingAsync</c> filters on it.
        /// </remarks>
        protected override bool IsQueriedByCreatedUtc => true;

        protected override void ConfigureEntity(EntityTypeBuilder<MediaFileEntity> builder)
        {
            _ = builder.ToTable("Files");

            // Case-SENSITIVE, matching Linux filesystem semantics. See MediaDirectoryEntityConfiguration.
            _ = builder.Property(static e => e.FullPath)
                .IsRequired()
                .HasMaxLength(4096);

            _ = builder.HasIndex(static e => e.FullPath)
                .IsUnique();

            _ = builder.Property(static e => e.FileName)
                .IsRequired()
                .HasMaxLength(1024)
                .UseCollation(CaseInsensitiveCollation);

            // Browse results are ordered by title, and the collation is what makes that ordering
            // case-insensitive without a second lower-cased column.
            _ = builder.Property(static e => e.Title)
                .IsRequired()
                .HasMaxLength(1024)
                .UseCollation(CaseInsensitiveCollation);


            _ = builder.Property(static e => e.Extension)
                .IsRequired()
                .HasMaxLength(32)
                .UseCollation(CaseInsensitiveCollation);


            _ = builder.Property(static e => e.DlnaProfileName)
                .HasMaxLength(128);

            _ = builder.Property(static e => e.ContentStamp)
                .IsRequired()
                .HasMaxLength(64);

            _ = builder.Property(static e => e.MetadataStamp)
                .HasMaxLength(64);

            _ = builder.Property(static e => e.ThumbnailStamp)
                .HasMaxLength(64);

            _ = builder.Property(static e => e.Mime)
                .IsRequired()
                .HasConversion<int>();

            _ = builder.Property(static e => e.UpnpClass)
                .IsRequired()
                .HasConversion<int>();

            // Both listings Browse and the admin library produce: a folder's files ordered by title, and
            // the same ordered by date. Single-column indexes let SQLite satisfy the filter or the order
            // but never both, so it sorted the matched rows in a temp B-tree - and UseMemoryTempStore is
            // false, which puts that B-tree on the platter, on the exact path the p50/p90 targets measure.
            // FileCreatedUtc had NO index at all (distinct from the indexed CreatedUtc), while
            // GetByDirectoryPageAsync's own doc comment claimed both sort keys were indexed.
            //
            // These replace the bare DirectoryId, Title and Extension indexes. DirectoryId is the leading
            // column here, so a directory-only filter or count uses these just as well; every Title
            // ordering in the repository is directory-filtered, so the bare Title index served no query;
            // and Extension was only ever written and projected, never filtered or ordered on.
            _ = builder.HasIndex(static e => new { e.DirectoryId, e.Title });
            _ = builder.HasIndex(static e => new { e.DirectoryId, e.FileCreatedUtc });

            _ = builder.HasOne(static e => e.Directory)
                .WithMany(static e => e.Files)
                .HasForeignKey(static e => e.DirectoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Real foreign keys, replacing the reference's denormalised path string on every child table.
            // Cascade means removing a file cleans up its metadata rather than leaving orphans behind.
            _ = builder.HasMany(static e => e.AudioStreams)
                .WithOne(static e => e.MediaFile)
                .HasForeignKey(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(static e => e.Video)
                .WithOne(static e => e.MediaFile)
                .HasForeignKey<VideoStreamEntity>(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasMany(static e => e.Subtitles)
                .WithOne(static e => e.MediaFile)
                .HasForeignKey(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(static e => e.Thumbnail)
                .WithOne(static e => e.MediaFile)
                .HasForeignKey<ThumbnailEntity>(static e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
