using System.Linq.Expressions;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Core.Contracts;
using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DlnaServer.Persistence.Repositories
{
    /// <inheritdoc cref="IMediaDirectoryRepository"/>
    internal sealed class MediaDirectoryRepository : IMediaDirectoryRepository
    {
        /// <summary>
        /// Ceiling on a search, however much the caller asks for.
        /// </summary>
        private const int MaxSearchResults = 1_000;

        // Aliased rather than redeclared: HiddenPathQuery owns the value, and the patterns built
        // there and the ones built here must escape identically.
        private const string LikeEscape = HiddenPathQuery.LikeEscape;

        /// <summary>
        /// Path separators, each with the character that follows it in code-point order.
        /// </summary>
        /// <remarks>
        /// Appending the pair to a folder's path gives the half-open range holding exactly its subtree:
        /// <c>/media/Films/</c> to <c>/media/Films0</c> contains every path under <c>/media/Films</c> and
        /// no other, because <c>0</c> is <c>/</c> plus one and <c>]</c> is <c>\</c> plus one. A sibling
        /// named <c>Films2</c> sorts above the upper bound and is therefore excluded, which a prefix
        /// match written as <c>StartsWith</c> would have to be told separately.
        /// </remarks>
        private const string PosixSeparator = "/";
        private const string PosixSeparatorSuccessor = "0";
        private const string WindowsSeparator = "\\";
        private const string WindowsSeparatorSuccessor = "]";

        private readonly DlnaDbContext _dbContext;
        private readonly IOptionsMonitor<DlnaOptions> _options;
        private readonly ITemporaryFolderVisibility _visibility;

        public MediaDirectoryRepository(
            DlnaDbContext dbContext,
            IOptionsMonitor<DlnaOptions> options,
            ITemporaryFolderVisibility visibility)
        {
            _dbContext = dbContext;
            _options = options;
            _visibility = visibility;
        }

        /// <remarks>
        /// Filtered by <see cref="ITemporaryFolderVisibility.HiddenFromDelivery"/> only - see the matching
        /// remark on <c>MediaFileRepository.GetByPublicIdAsync</c> for why the excluded folders are exempt
        /// here and the temporarily hidden ones are not.
        /// </remarks>
        public Task<MediaDirectoryDto?> GetByPublicIdAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            return ProjectDirectories(
                    d => d.PublicId == publicId,
                    hiddenFolders: _visibility.HiddenFromDelivery)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<MediaDirectoryDto?> GetByPathAsync(
            string fullPath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

            return ProjectDirectories(d => d.FullPath == fullPath).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaDirectoryDto>> GetSourceRootsAsync(
            CancellationToken cancellationToken = default)
        {
            return await ProjectDirectories(static d => d.IsSourceRoot, listing: true)
                .OrderBy(static d => d.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountChildrenAsync(
            Guid parentPublicId,
            CancellationToken cancellationToken = default)
        {
            return ProjectDirectories(
                    d => d.ParentDirectory != null && d.ParentDirectory.PublicId == parentPublicId,
                    listing: true)
                .CountAsync(cancellationToken);
        }

        /// <remarks>
        /// See <c>MediaFileRepository.GetByDirectoryPageAsync</c> - Browse pages across containers and
        /// files as one list, so both halves have to be sliceable in SQL for either to be.
        /// </remarks>
        public async Task<IReadOnlyList<MediaDirectoryDto>> GetChildrenPageAsync(
            Guid parentPublicId,
            int skip,
            int take,
            bool sortByDate,
            bool descending,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(skip);

            if (take <= 0)
            {
                return [];
            }

            var query = ProjectDirectories(
                d => d.ParentDirectory != null && d.ParentDirectory.PublicId == parentPublicId,
                listing: true);

            query = (sortByDate, descending) switch
            {
                (true, false) => query.OrderBy(static d => d.CreatedUtc),
                (true, true) => query.OrderByDescending(static d => d.CreatedUtc),
                (false, false) => query.OrderBy(static d => d.Name),
                (false, true) => query.OrderByDescending(static d => d.Name),
            };

            return await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaDirectoryDto>> GetChildrenAsync(
            Guid parentPublicId,
            CancellationToken cancellationToken = default)
        {
            return await ProjectDirectories(
                    d => d.ParentDirectory != null && d.ParentDirectory.PublicId == parentPublicId,
                    listing: true)
                .OrderBy(static d => d.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, MediaDirectoryDto>> GetExistingByPathAsync(
            IReadOnlyCollection<string> fullPaths,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fullPaths);

            if (fullPaths.Count == 0)
            {
                return new Dictionary<string, MediaDirectoryDto>(StringComparer.Ordinal);
            }

            var found = await ProjectDirectories(d => fullPaths.Contains(d.FullPath))
                .ToListAsync(cancellationToken);

            var byPath = new Dictionary<string, MediaDirectoryDto>(found.Count, StringComparer.Ordinal);

            for (var index = 0; index < found.Count; index++)
            {
                var directory = found[index];

                byPath[directory.FullPath] = directory;
            }

            return byPath;
        }

        public async Task<IReadOnlyList<IndexedDirectoryDto>> GetIndexedPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            var query = _dbContext.Directories.AsNoTracking();

            if (afterFullPath is not null)
            {
                query = query.Where(d => string.Compare(d.FullPath, afterFullPath) > 0);
            }

            return await query
                .OrderBy(static d => d.FullPath)
                .Take(take)
                .Select(static d => new IndexedDirectoryDto
                {
                    PublicId = d.PublicId,
                    FullPath = d.FullPath,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<IndexedDirectoryDto>> GetEmptyLeafPageAsync(
            string? afterFullPath,
            int take,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

            // Both counts are FK lookups against an index, so this stays a seek per candidate rather than
            // a scan. Emptiness here is about what is INDEXED, not about what is visible - hiding must
            // never be able to delete anything.
            var query = _dbContext.Directories
                .AsNoTracking()
                .Where(d => !_dbContext.Directories.Any(child => child.ParentDirectoryId == d.Id))
                .Where(d => !_dbContext.Files.Any(f => f.DirectoryId == d.Id));

            if (afterFullPath is not null)
            {
                query = query.Where(d => string.Compare(d.FullPath, afterFullPath) > 0);
            }

            return await query
                .OrderBy(static d => d.FullPath)
                .Take(take)
                .Select(static d => new IndexedDirectoryDto
                {
                    PublicId = d.PublicId,
                    FullPath = d.FullPath,
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaDirectoryDto>> SearchAsync(
            MediaDirectorySearchRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Filtering the projection rather than the entity query: the two properties matched here map
            // straight to their columns, so EF still pushes both predicates into SQL, and the projection
            // stays in one place instead of being repeated for this one query.
            var query = ProjectDirectories(predicate: null, listing: true);

            if (!string.IsNullOrWhiteSpace(request.NameContains))
            {
                // LIKE rather than Contains: SQLite folds case for LIKE, which is what makes the search
                // capital-irrelevant. The term is escaped first so a name containing % or _ is searched
                // for literally instead of behaving as a wildcard.
                var pattern = $"%{HiddenPathQuery.EscapeLikeTerm(request.NameContains.Trim())}%";

                query = query.Where(d => EF.Functions.Like(d.Name, pattern, LikeEscape));
            }

            if (!string.IsNullOrWhiteSpace(request.PathContains))
            {
                var pattern = $"%{HiddenPathQuery.EscapeLikeTerm(request.PathContains.Trim())}%";

                query = query.Where(d => EF.Functions.Like(d.FullPath, pattern, LikeEscape));
            }

            return await query
                .OrderBy(static d => d.FullPath)
                .Take(Math.Clamp(request.Take, 1, MaxSearchResults))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> SetHierarchyAsync(
            Guid publicId,
            Guid? parentPublicId,
            bool isSourceRoot,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.Directories
                .FirstOrDefaultAsync(d => d.PublicId == publicId, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            int? parentKey = null;

            if (parentPublicId is { } parent)
            {
                parentKey = await _dbContext.Directories
                    .AsNoTracking()
                    .Where(d => d.PublicId == parent)
                    .Select(static d => (int?)d.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (parentKey is null)
                {
                    throw new InvalidOperationException(
                        $"Directory '{parent}' cannot be a parent because it is not indexed.");
                }
            }

            entity.ParentDirectoryId = parentKey;
            entity.IsSourceRoot = isSourceRoot;

            _ = await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return _dbContext.Directories.CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<MediaDirectoryDto>> AddRangeAsync(
            IReadOnlyCollection<MediaDirectoryCreateDto> directories,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(directories);

            if (directories.Count == 0)
            {
                return [];
            }

            var parentKeys = await ResolveParentKeysAsync(directories, cancellationToken);
            var entities = new List<MediaDirectoryEntity>(directories.Count);

            foreach (var directory in directories)
            {
                int? parentId = null;

                if (directory.ParentDirectoryPublicId is Guid parentPublicId)
                {
                    if (!parentKeys.TryGetValue(parentPublicId, out var resolvedId))
                    {
                        throw new InvalidOperationException(
                            $"Parent directory '{parentPublicId}' is not indexed, so '{directory.FullPath}' cannot be added.");
                    }

                    parentId = resolvedId;
                }

                entities.Add(new MediaDirectoryEntity
                {
                    FullPath = directory.FullPath,
                    Name = directory.Name,
                    ParentDirectoryId = parentId,
                    Depth = directory.Depth,
                    IsSourceRoot = directory.IsSourceRoot,

                    // See MediaFileRepository.InsertAsync: left at default so StampTimestamps fills it
                    // with the clock, and that "only if unset" is the seam a first fill rides on.
                    CreatedUtc = directory.IndexedUtc ?? default,
                });
            }

            await _dbContext.Directories.AddRangeAsync(entities, cancellationToken);

            try
            {
                _ = await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // See MediaFileRepository.InsertAsync: without this a failed save stays tracked and
                // every later batch in the same pass re-submits it.
                _dbContext.ForgetTrackedEntities();
                throw;
            }

            // See MediaFileRepository.InsertAsync: one scope per scan means the tracker would otherwise
            // accumulate every directory inserted so far and re-walk them on each save.
            _dbContext.ForgetTrackedEntities();

            var storedIds = entities.Select(static e => e.PublicId).ToArray();

            return await ProjectDirectories(d => storedIds.Contains(d.PublicId)).ToListAsync(cancellationToken);
        }

        public async Task<int> RemoveByPublicIdsAsync(
            IReadOnlyCollection<Guid> publicIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(publicIds);

            if (publicIds.Count == 0)
            {
                return 0;
            }

            // Subdirectories and files follow by cascade. ExecuteDelete bypasses the change tracker,
            // so this must not run while the same context holds tracked copies of those rows.
            return await _dbContext.Directories
                .Where(d => publicIds.Contains(d.PublicId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        private async Task<Dictionary<Guid, int>> ResolveParentKeysAsync(
            IReadOnlyCollection<MediaDirectoryCreateDto> directories,
            CancellationToken cancellationToken)
        {
            var wanted = directories
                .Select(static d => d.ParentDirectoryPublicId)
                .OfType<Guid>()
                .ToHashSet();

            if (wanted.Count == 0)
            {
                return [];
            }

            return await _dbContext.Directories
                .AsNoTracking()
                .Where(d => wanted.Contains(d.PublicId))
                .ToDictionaryAsync(static d => d.PublicId, static d => d.Id, cancellationToken);
        }

        /// <summary>
        /// Drops folders whose subtree holds no file a renderer would be shown.
        /// </summary>
        /// <remarks>
        /// A scan indexes every folder on the volume, so QNAP's own <c>.streams</c> and
        /// <c>.@upload_cache</c> arrived as top-level containers with nothing playable anywhere under
        /// them. It also closes the gap the read-time exclusion opens: hiding a folder leaves its parent
        /// indexed, and a parent whose only media has just been hidden is an empty container too.
        /// <para>
        /// Matched as a path range rather than a prefix so SQLite can seek the unique index on
        /// <c>Files.FullPath</c> instead of scanning it once per candidate folder. Both separators are
        /// tried because the stored paths come from whichever host indexed them - reading
        /// <see cref="Path.DirectorySeparatorChar"/> instead would match nothing at all on a Windows box
        /// holding a NAS index, and would do it silently.
        /// </para>
        /// </remarks>
        private IQueryable<MediaDirectoryEntity> ExcludeWithoutVisibleMedia(
            IQueryable<MediaDirectoryEntity> query,
            IList<string> hiddenFolders)
        {
            var visibleFiles = HiddenPathQuery.ExcludeHiddenFiles(_dbContext.Files.AsNoTracking(), hiddenFolders);

            return query.Where(d => visibleFiles.Any(f =>
                (string.Compare(f.FullPath, d.FullPath + PosixSeparator) > 0
                    && string.Compare(f.FullPath, d.FullPath + PosixSeparatorSuccessor) < 0)
                || (string.Compare(f.FullPath, d.FullPath + WindowsSeparator) > 0
                    && string.Compare(f.FullPath, d.FullPath + WindowsSeparatorSuccessor) < 0)));
        }

        private IQueryable<MediaDirectoryDto> ProjectDirectories(
            Expression<Func<MediaDirectoryEntity, bool>>? predicate = null,
            bool listing = false,
            IList<string>? hiddenFolders = null)
        {
            IQueryable<MediaDirectoryEntity> query = _dbContext.Directories.AsNoTracking();

            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            if (listing)
            {
                var hiddenFromListings = _visibility.HiddenFromListings;

                query = ExcludeWithoutVisibleMedia(
                    HiddenPathQuery.ExcludeHiddenDirectories(query, hiddenFromListings),
                    hiddenFromListings);
            }
            else if (hiddenFolders is { Count: > 0 })
            {
                query = HiddenPathQuery.ExcludeHiddenDirectories(query, hiddenFolders);
            }

            return query.Select(static d => new MediaDirectoryDto
            {
                PublicId = d.PublicId,
                FullPath = d.FullPath,
                Name = d.Name,
                ParentDirectoryPublicId = d.ParentDirectory != null ? d.ParentDirectory.PublicId : null,
                Depth = d.Depth,
                IsSourceRoot = d.IsSourceRoot,
                CreatedUtc = d.CreatedUtc,
            });
        }

    }
}
