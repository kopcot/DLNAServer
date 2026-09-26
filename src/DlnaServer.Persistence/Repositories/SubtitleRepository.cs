using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Core.Subtitles;
using DlnaServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.Persistence.Repositories
{
    /// <inheritdoc cref="ISubtitleRepository"/>
    internal sealed class SubtitleRepository : ISubtitleRepository
    {
        private const int LanguageMaxLength = 32;

        // The indexer's own write batch, so a first fill never holds every link in the change tracker.
        private const int InsertBatchSize = 500;

        private readonly DlnaDbContext _dbContext;

        public SubtitleRepository(DlnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <remarks>
        /// A subtitle is matched against the media in its own folder first, and only when nothing there
        /// fits, against the media of the folder directly above - so <c>Film/Subs/film.en.srt</c> links to
        /// <c>Film/film.mkv</c>, and never anything further up.
        /// </remarks>
        public async Task<(int Added, int Dropped)> SyncAutomaticAsync(
            IReadOnlyDictionary<string, HashSet<string>> fileNamesByDirectory,
            IReadOnlyList<string> excludedFolders,
            Func<string, bool> isDefinitelyAbsent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileNamesByDirectory);
            ArgumentNullException.ThrowIfNull(excludedFolders);
            ArgumentNullException.ThrowIfNull(isDefinitelyAbsent);

            var existing = await _dbContext.SubtitleFiles
                .AsNoTracking()
                .Select(static s => new
                {
                    s.Id,
                    s.MediaFileId,
                    s.RelativePath,
                    s.Source,
                    MediaFullPath = s.MediaFile!.FullPath,
                })
                .ToListAsync(cancellationToken);

            var directories = new HashSet<string>(StringComparer.Ordinal);

            foreach (var directory in fileNamesByDirectory.Keys)
            {
                directories.Add(directory);

                if (Path.GetDirectoryName(directory) is { Length: > 0 } parent)
                {
                    directories.Add(parent);
                }
            }

            var mediaByDirectory = (await _dbContext.Files
                .AsNoTracking()
                .Where(f => f.Directory != null && directories.Contains(f.Directory.FullPath))
                .Select(static f => new { f.Id, f.FileName, f.Mime, Directory = f.Directory!.FullPath })
                .ToListAsync(cancellationToken))
                .GroupBy(static f => f.Directory, StringComparer.Ordinal)
                .ToDictionary(
                    static g => g.Key,
                    static g => g.Select(static f => new SubtitleMedia<int>(f.Id, f.FileName, f.Mime.ToMedia())).ToList(),
                    StringComparer.Ordinal);

            var wanted = new Dictionary<(int MediaFileId, string RelativePath), string?>();

            foreach (var (directory, fileNames) in fileNamesByDirectory)
            {
                var matchedHere = new HashSet<string>(StringComparer.Ordinal);

                if (mediaByDirectory.TryGetValue(directory, out var ownMedia))
                {
                    foreach (var match in SubtitleMatcher.Match(ownMedia, fileNames))
                    {
                        wanted[(match.Key, match.FileName)] = match.Language;
                        matchedHere.Add(match.FileName);
                    }
                }

                if (Path.GetDirectoryName(directory) is { Length: > 0 } parent
                    && mediaByDirectory.TryGetValue(parent, out var parentMedia))
                {
                    var folderName = Path.GetFileName(directory);
                    var leftOver = fileNames.Where(n => !matchedHere.Contains(n)).ToList();

                    foreach (var match in SubtitleMatcher.Match(parentMedia, leftOver))
                    {
                        wanted[(match.Key, $"{folderName}/{match.FileName}")] = match.Language;
                    }
                }
            }

            var withManualLinks = existing
                .Where(static s => s.Source == SubtitleSource.Manual)
                .Select(static s => s.MediaFileId)
                .ToHashSet();
            var known = existing
                .Select(static s => (s.MediaFileId, s.RelativePath))
                .ToHashSet();

            var toDrop = new List<int>();

            bool WasSeen(string fullPath)
            {
                return fileNamesByDirectory.TryGetValue(Path.GetDirectoryName(fullPath) ?? string.Empty, out var seen)
                    && seen.Contains(Path.GetFileName(fullPath));
            }

            bool IsGone(string fullPath)
            {
                return !WasSeen(fullPath) && isDefinitelyAbsent(fullPath);
            }

            foreach (var link in existing)
            {
                var isAutomatic = link.Source == SubtitleSource.Automatic;
                var fullPath = ResolveFullPath(link.MediaFullPath, link.RelativePath);

                // The operator's choice is kept until it cannot work: its file gone - deleted, or left
                // behind when the film moved - or its folder excluded, which a manual link may not point into.
                if (link.Source == SubtitleSource.Manual)
                {
                    if (PathExclusion.IsHidden(fullPath, excludedFolders) || IsGone(fullPath))
                    {
                        toDrop.Add(link.Id);
                    }

                    continue;
                }

                // Before the match test: a manual link replaces the automatic ones even when they still match -
                // one added while this scan was reading would otherwise leave both sets for good.
                if (isAutomatic && withManualLinks.Contains(link.MediaFileId))
                {
                    toDrop.Add(link.Id);
                    continue;
                }

                // A folder the scan skips is never seen, so probing it would wake its disc every pass to
                // change nothing. An automatic link there goes; a removed marker stays for when it is shown.
                if (PathExclusion.IsHidden(fullPath, excludedFolders))
                {
                    if (isAutomatic)
                    {
                        toDrop.Add(link.Id);
                    }

                    continue;
                }

                if (wanted.ContainsKey((link.MediaFileId, link.RelativePath)))
                {
                    continue;
                }

                // A removed link is only a marker; it goes once its file does. An automatic link also goes
                // when its file is still there but no longer matches.
                var isDropped = isAutomatic
                    ? WasSeen(fullPath) || isDefinitelyAbsent(fullPath)
                    : IsGone(fullPath);

                if (isDropped)
                {
                    toDrop.Add(link.Id);
                }
            }

            var toAdd = wanted
                .Where(w => !known.Contains(w.Key) && !withManualLinks.Contains(w.Key.MediaFileId))
                .Select(static w => new SubtitleFileEntity
                {
                    MediaFileId = w.Key.MediaFileId,
                    RelativePath = w.Key.RelativePath,
                    Language = w.Value,
                    Source = SubtitleSource.Automatic,
                })
                .ToList();

            if (toDrop.Count > 0)
            {
                _ = await _dbContext.SubtitleFiles
                    .Where(s => toDrop.Contains(s.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            foreach (var chunk in toAdd.Chunk(InsertBatchSize))
            {
                _dbContext.SubtitleFiles.AddRange(chunk);
                _ = await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }

            return (toAdd.Count, toDrop.Count);
        }

        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<SubtitleFileDto>>> GetForFilesAsync(
            IReadOnlyCollection<Guid> mediaFilePublicIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(mediaFilePublicIds);

            if (mediaFilePublicIds.Count == 0)
            {
                return new Dictionary<Guid, IReadOnlyList<SubtitleFileDto>>();
            }

            // Two seeks rather than one join: filtering on MediaFileId is what the table's index covers,
            // whichever way SQLite would have ordered the join - docs/decisions.md 7b, W10.
            var mediaFileIds = await _dbContext.Files
                .Where(f => mediaFilePublicIds.Contains(f.PublicId))
                .Select(static f => f.Id)
                .ToListAsync(cancellationToken);

            var links = await Project(_dbContext.SubtitleFiles
                    .AsNoTracking()
                    .Where(s => mediaFileIds.Contains(s.MediaFileId) && s.Source != SubtitleSource.Removed))
                .ToListAsync(cancellationToken);

            return links
                .GroupBy(static s => s.MediaFilePublicId)
                .ToDictionary(
                    static g => g.Key,
                    static g => (IReadOnlyList<SubtitleFileDto>)[.. g.OrderBy(static s => s.RelativePath, StringComparer.Ordinal)]);
        }

        public Task<SubtitleFileDto?> GetByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            return Project(_dbContext.SubtitleFiles
                    .AsNoTracking()
                    .Where(s => s.PublicId == publicId && s.Source != SubtitleSource.Removed))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> AddManualAsync(
            Guid mediaFilePublicId,
            string relativePath,
            string? language,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

            var mediaFileId = await _dbContext.Files
                .Where(f => f.PublicId == mediaFilePublicId)
                .Select(static f => (int?)f.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (mediaFileId is not { } id)
            {
                return false;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // A manual link replaces the automatic ones outright, so a television never sees both sets.
            _ = await _dbContext.SubtitleFiles
                .Where(s => s.MediaFileId == id && s.Source == SubtitleSource.Automatic && s.RelativePath != relativePath)
                .ExecuteDeleteAsync(cancellationToken);

            var row = await _dbContext.SubtitleFiles
                .FirstOrDefaultAsync(s => s.MediaFileId == id && s.RelativePath == relativePath, cancellationToken);

            if (row is null)
            {
                row = new SubtitleFileEntity { MediaFileId = id, RelativePath = relativePath };
                _ = _dbContext.SubtitleFiles.Add(row);
            }

            row.Source = SubtitleSource.Manual;
            row.Language = NormaliseLanguage(language) ?? row.Language;

            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            return true;
        }

        /// <remarks>
        /// A marker for a hand-added link too, not a delete: one whose name also matches would otherwise be
        /// linked straight back by the next scan. The marker goes once its file does.
        /// </remarks>
        public async Task RemoveAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            _ = await _dbContext.SubtitleFiles
                .Where(s => s.PublicId == publicId)
                .ExecuteUpdateAsync(static u => u.SetProperty(static s => s.Source, SubtitleSource.Removed), cancellationToken);
        }

        public async Task SetLanguageAsync(Guid publicId, string? language, CancellationToken cancellationToken = default)
        {
            var normalised = NormaliseLanguage(language);

            _ = await _dbContext.SubtitleFiles
                .Where(s => s.PublicId == publicId)
                .ExecuteUpdateAsync(u => u.SetProperty(static s => s.Language, normalised), cancellationToken);
        }

        private static IQueryable<SubtitleFileDto> Project(IQueryable<SubtitleFileEntity> query)
        {
            return query.Select(static s => new SubtitleFileDto
            {
                PublicId = s.PublicId,
                MediaFilePublicId = s.MediaFile!.PublicId,
                MediaFileFullPath = s.MediaFile.FullPath,
                RelativePath = s.RelativePath,
                Language = s.Language,
                Source = s.Source,
            });
        }

        private static string ResolveFullPath(string mediaFullPath, string relativePath)
        {
            return Path.Combine([Path.GetDirectoryName(mediaFullPath) ?? string.Empty, .. relativePath.Split('/')]);
        }

        private static string? NormaliseLanguage(string? language)
        {
            var trimmed = language?.Trim().ToLowerInvariant();

            return string.IsNullOrEmpty(trimmed)
                ? null
                : trimmed.Length > LanguageMaxLength
                    ? trimmed[..LanguageMaxLength]
                    : trimmed;
        }
    }
}
