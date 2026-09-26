using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DlnaServer.Persistence
{
    /// <summary>
    /// EF Core context for the media index.
    /// </summary>
    /// <remarks>
    /// The schema is owned by migrations, not by <c>EnsureCreated</c>. The reference used the latter and
    /// dropped the whole database whenever its schema probe or machine-name check failed.
    /// </remarks>
    internal sealed class DlnaDbContext : DbContext
    {
        /// <summary>
        /// Forgets every tracked entity, after a batch has been saved.
        /// </summary>
        /// <remarks>
        /// A scan runs in one scope and AddRangeAsync leaves every inserted entity tracked, so
        /// StampTimestamps' pass over <c>Entries&lt;EntityBase&gt;()</c> and the DetectChanges it forces
        /// grew with everything already written: batch <i>k</i> paid O(500k), which is quadratic across a
        /// 20,000-file library and held the whole of it resident against a 60 MB managed-heap target.
        /// Nothing downstream depends on the tracked copies - reads project into DTOs and re-read
        /// AsNoTracking.
        /// </remarks>
        public void ForgetTrackedEntities()
        {
            ChangeTracker.Clear();
        }

        private readonly TimeProvider _timeProvider;

        /// <remarks>
        /// One constructor taking only <see cref="DbContextOptions{TContext}"/>, because that is what
        /// <c>AddDbContextPool</c> requires - a pooled context is reused rather than constructed, so EF
        /// insists on a shape it can build without asking the container for anything.
        /// <para>
        /// <see cref="TimeProvider"/> therefore comes out of the options' own application service
        /// provider rather than the constructor. It falls back to <see cref="TimeProvider.System"/> when
        /// there is no container at all, which is the design-time factory and the tests that build a
        /// context directly against a throwaway file.
        /// </para>
        /// </remarks>
        public DlnaDbContext(DbContextOptions<DlnaDbContext> options)
            : base(options)
        {
            _timeProvider = options
                .FindExtension<CoreOptionsExtension>()?
                .ApplicationServiceProvider?
                .GetService<TimeProvider>()
                ?? TimeProvider.System;
        }

        public DbSet<MediaDirectoryEntity> Directories => Set<MediaDirectoryEntity>();

        public DbSet<MediaFileEntity> Files => Set<MediaFileEntity>();

        public DbSet<AudioStreamEntity> AudioStreams => Set<AudioStreamEntity>();

        public DbSet<VideoStreamEntity> VideoStreams => Set<VideoStreamEntity>();

        public DbSet<SubtitleStreamEntity> SubtitleStreams => Set<SubtitleStreamEntity>();

        public DbSet<MediaFileTagEntity> MediaFileTags => Set<MediaFileTagEntity>();

        public DbSet<SubtitleFileEntity> SubtitleFiles => Set<SubtitleFileEntity>();

        public DbSet<ThumbnailEntity> Thumbnails => Set<ThumbnailEntity>();

        public DbSet<ThumbnailContentEntity> ThumbnailContents => Set<ThumbnailContentEntity>();

        public DbSet<ServerInstanceEntity> ServerInstances => Set<ServerInstanceEntity>();

        public DbSet<UploadDeviceEntity> UploadDevices => Set<UploadDeviceEntity>();

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampTimestamps();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            StampTimestamps();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DlnaDbContext).Assembly);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            ArgumentNullException.ThrowIfNull(configurationBuilder);

            base.ConfigureConventions(configurationBuilder);

            // SQLite has no date type, so a round-tripped DateTime comes back as Unspecified.
            // Forcing Utc on read keeps every comparison in the application unambiguous.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        private void StampTimestamps()
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (EntityEntry<EntityBase> entry in ChangeTracker.Entries<EntityBase>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        if (entry.Entity.CreatedUtc == default)
                        {
                            entry.Entity.CreatedUtc = nowUtc;
                        }

                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedUtc = nowUtc;
                        break;
                }
            }
        }
    }
}
