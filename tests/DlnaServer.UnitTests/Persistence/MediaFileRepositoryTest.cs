using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Files;
using DlnaServer.Core.Subtitles;
using DlnaServer.Persistence.Entities;
using DlnaServer.Core.Configuration;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DlnaServer.UnitTests.Persistence
{
    [TestFixture]
    internal sealed class MediaFileRepositoryTest
    {
        private SqliteTestDatabase _database = null!;

        [SetUp]
        public void SetUp()
        {
            _database = new SqliteTestDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            _database.Dispose();
        }

        [Test]
        public async Task AddRangeAsync_ThenGetByPath_RoundTripsTheFile()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Sample.mkv")],
                cancellationToken: CancellationToken.None);
            var actual = await repository.GetByPathAsync("/media/movies/Sample.mkv", CancellationToken.None);

            // Assert
            actual.Should().NotBeNull("because the file was just inserted under that exact path");
            actual!.Title.Should().Be("Sample", "because the title defaults to the file name without extension");
            actual.Mime.Should().Be(DlnaMime.VideoXMatroska, "because the MIME is persisted as an int and read back");
            actual.PublicId.Should().Be(stored[0].PublicId,
                "because the identifier handed out on insert is the one later reads resolve by");
        }

        /// <summary>
        /// The internal integer key must never reach a caller - only PublicId crosses the boundary.
        /// </summary>
        [Test]
        public async Task AddRangeAsync_AssignsNonEmptyPublicId_WhileKeepingIntegerKeyInternal()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/a.mkv"), CreateFile("/media/b.mkv")],
                cancellationToken: CancellationToken.None);

            // Assert
            stored.Should().HaveCount(2, "because both files were inserted");
            stored.Should().OnlyContain(f => f.PublicId != Guid.Empty,
                "because every row is assigned a public identifier on insert");
            stored.Select(static f => f.PublicId).Distinct().Should().HaveCount(2,
                "because public identifiers are unique per row");

            var entityKeys = await context.Files.AsNoTracking()
                .Select(static f => f.Id)
                .ToListAsync(CancellationToken.None);
            entityKeys.Should().OnlyContain(id => id > 0,
                "because the integer surrogate key is assigned by the database and used for every foreign key");
        }

        [Test]
        public async Task SaveChanges_ForNewFile_StoresCreatedTimestampAsUtc()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Utc.mkv")],
                cancellationToken: CancellationToken.None);

            // Assert
            stored[0].CreatedUtc.Kind.Should().Be(DateTimeKind.Utc,
                "because SQLite loses the kind on round-trip and the converter must restore it - "
                + "the reference stored local time and never restored the kind at all");
            stored[0].CreatedUtc.Should().NotBe(default,
                "because the context stamps CreatedUtc on insert");
        }

        /// <summary>
        /// The reference gave paths a case-insensitive unique index, which on Linux would reject a
        /// legitimate second file differing only in case.
        /// </summary>
        [Test]
        public async Task AddRangeAsync_ForPathsDifferingOnlyByCase_TreatsThemAsDistinctFiles()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/Movie.mkv"), CreateFile("/media/movie.mkv")],
                cancellationToken: CancellationToken.None);
            var count = await repository.CountAsync(CancellationToken.None);

            // Assert
            count.Should().Be(2, "because on a case-sensitive filesystem those are two different files");
        }

        [Test]
        public async Task RemoveByPublicIdsAsync_ForFileWithMetadata_CascadesToChildRows()
        {
            // Arrange
            await using var setupContext = _database.CreateContext();
            var repository = CreateRepository(setupContext);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/WithMetadata.mkv")],
                cancellationToken: CancellationToken.None);

            var entity = await setupContext.Files.FirstAsync(CancellationToken.None);
            entity.Video = new VideoStreamEntity { Codec = "h264", Width = 1920, Height = 1080 };
            entity.AudioStreams.Add(new AudioStreamEntity { StreamIndex = 0, Codec = "aac", Channels = 2 });
            entity.Subtitles.Add(new SubtitleStreamEntity { StreamIndex = 0, Language = "eng" });
            entity.Subtitles.Add(new SubtitleStreamEntity { StreamIndex = 1, Language = "ces" });
            _ = await setupContext.SaveChangesAsync(CancellationToken.None);

            // Act
            await using var deleteContext = _database.CreateContext();
            var (removed, abandonedThumbnailPaths) = await CreateRepository(deleteContext).RemoveByPublicIdsAsync(
                publicIds: [stored[0].PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            removed.Should().Be(1, "because exactly one file matched the supplied identifier");
            abandonedThumbnailPaths.Should().BeEmpty("because this file never had a preview image made for it");

            await using var verifyContext = _database.CreateContext();
            (await verifyContext.VideoStreams.CountAsync(CancellationToken.None)).Should().Be(0,
                "because deleting a file must not leave its video metadata orphaned");
            (await verifyContext.AudioStreams.CountAsync(CancellationToken.None)).Should().Be(0,
                "because deleting a file must not leave its audio metadata orphaned");
            (await verifyContext.SubtitleStreams.CountAsync(CancellationToken.None)).Should().Be(0,
                "because deleting a file must not leave its subtitle tracks orphaned");
        }

        [Test]
        public async Task GetWithDetailsAsync_ForFileWithMultipleSubtitles_ReturnsEveryTrackInOrder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/MultiSub.mkv")],
                cancellationToken: CancellationToken.None);

            var entity = await context.Files.FirstAsync(CancellationToken.None);
            entity.Subtitles.Add(new SubtitleStreamEntity { StreamIndex = 2, Language = "deu" });
            entity.Subtitles.Add(new SubtitleStreamEntity { StreamIndex = 0, Language = "eng" });
            entity.Subtitles.Add(new SubtitleStreamEntity { StreamIndex = 1, Language = "ces" });
            _ = await context.SaveChangesAsync(CancellationToken.None);

            // Act
            var actual = await CreateRepository(_database.CreateContext())
                .GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            actual.Should().NotBeNull("because the file was just inserted");
            actual!.Subtitles.Select(static s => s.Language).Should().Equal(
                ["eng", "ces", "deu"],
                "because every subtitle track is kept and ordered by stream index - "
                + "the reference stored only the first");
        }

        [Test]
        public async Task GetPendingProcessingAsync_ReturnsOnlyFilesWhoseContentStampMoved()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/processed.mkv"),
                    CreateFile("/media/changed.mkv"),
                    CreateFile("/media/new.mkv"),
                    CreateFile("/media/broken.mkv"),
                ],
                cancellationToken: CancellationToken.None);

            foreach (var entity in await context.Files.ToListAsync(CancellationToken.None))
            {
                switch (entity.FullPath)
                {
                    case "/media/processed.mkv":
                        entity.MetadataStamp = entity.ContentStamp;
                        entity.ThumbnailStamp = entity.ContentStamp;
                        break;

                    case "/media/changed.mkv":
                        entity.MetadataStamp = "stale-stamp";
                        entity.ThumbnailStamp = "stale-stamp";
                        break;

                    case "/media/broken.mkv":
                        entity.MetadataFailureCount = 10;
                        entity.ThumbnailFailureCount = 10;
                        break;
                }
            }

            _ = await context.SaveChangesAsync(CancellationToken.None);

            // Act
            var pending = await CreateRepository(_database.CreateContext()).GetPendingProcessingAsync(
                maxCount: 50,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);

            // Assert
            pending.Select(static f => f.FullPath).Should().BeEquivalentTo(
                ["/media/changed.mkv", "/media/new.mkv"],
                "because a file is pending when its content changed or was never processed, "
                + "and a repeatedly failing file must stop being retried");
        }

        [Test]
        public async Task GetExistingPathsAsync_ReturnsOnlyThePathsAlreadyIndexed()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/known.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var existing = await repository.GetExistingPathsAsync(
                fullPaths: ["/media/known.mkv", "/media/unknown.mkv"],
                cancellationToken: CancellationToken.None);

            // Assert
            existing.Should().BeEquivalentTo(["/media/known.mkv"],
                "because scanning uses this to add only files that are not already indexed");
        }

        /// <summary>
        /// The read behind <c>/fileserver/thumbnail/{id}</c>: resolve a thumbnail by the identifier a
        /// renderer was handed in <c>albumArtURI</c>, without dragging the image bytes along.
        /// </summary>
        [Test]
        public async Task GetThumbnailByPublicIdAsync_ReturnsTheThumbnailWithoutItsBytes()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Thumbed.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/38/16/3816.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 4,
                    Content: [1, 2, 3, 4],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            var file = await repository.GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            // Act
            var thumbnail = await repository.GetThumbnailByPublicIdAsync(
                file!.Thumbnail!.PublicId,
                CancellationToken.None);

            // Assert
            thumbnail.Should().NotBeNull("because the thumbnail was just stored against that file");
            thumbnail!.FilePath.Should().Be("/cache/38/16/3816.jpg",
                "because the endpoint serves the file at the recorded path when memory and database miss");
            thumbnail.Width.Should().Be(480, "because the generated size is persisted as-is");
            thumbnail.MediaFilePublicId.Should().Be(stored[0].PublicId,
                "because a thumbnail is always reachable back to the file it previews");
            thumbnail.HasStoredContent.Should().BeTrue(
                "because a database copy was supplied, and that flag is what makes the endpoint read it");
        }

        /// <summary>
        /// The cursor a thumbnail page hands back walks to the next page with no gap and no repeat.
        /// </summary>
        /// <remarks>
        /// <c>/manage/thumbnail</c> accepted an <c>after</c> and echoed no cursor at all, so a caller
        /// could not page it. The reason was that the only path on <c>ThumbnailDto</c> was the
        /// <c>.@__thumb</c> image, while this read orders and seeks on the <b>media</b> file's path -
        /// paging on the image path would have walked a different sequence entirely.
        /// </remarks>
        [Test]
        public async Task GetThumbnailPageAsync_PagesOnTheMediaPathItOrdersBy()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/a.mkv"),
                    CreateFile("/media/b.mkv"),
                    CreateFile("/media/c.mkv"),
                ],
                cancellationToken: CancellationToken.None);

            foreach (var file in stored)
            {
                // Image names deliberately in the opposite order to the media paths, so a read that
                // paged on the thumbnail's own path would visibly walk the wrong sequence.
                await repository.SaveThumbnailAsync(
                    file.PublicId,
                    new GeneratedThumbnail(
                        $"/cache/{'z' - file.FileName[0]}.jpg",
                        DlnaMime.ImageJpeg,
                        Width: 16,
                        Height: 16,
                        SizeInBytes: 1,
                        Content: [1],
                        WasAdopted: false),
                    "1:1",
                    CancellationToken.None);
            }

            // Act
            var first = await repository.GetThumbnailPageAsync(
                afterFullPath: null,
                take: 2,
                cancellationToken: CancellationToken.None);
            var second = await repository.GetThumbnailPageAsync(
                afterFullPath: first[^1].MediaFileFullPath,
                take: 2,
                cancellationToken: CancellationToken.None);

            // Assert
            first.Select(static t => t.MediaFileFullPath).Should().Equal(["/media/a.mkv", "/media/b.mkv"],
                "because the page is ordered by the media path, ascending");
            second.Select(static t => t.MediaFileFullPath).Should().Equal(["/media/c.mkv"],
                "because the cursor is the last row's media path, and paging resumes strictly after it");
            first[^1].MediaFileFullPath.Should().NotBe(first[^1].FilePath,
                "because the cursor is the media path and not the image path - conflating the two is the "
                + "defect this guards");
        }

        [Test]
        public async Task GetThumbnailByPublicIdAsync_ForAnUnknownIdentifier_ReturnsNull()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var thumbnail = await repository.GetThumbnailByPublicIdAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            thumbnail.Should().BeNull("because an unknown thumbnail identifier must produce a 404, not an error");
        }

        [Test]
        public async Task GetThumbnailContentAsync_ReturnsTheStoredBytes()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Blob.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/aa/bb/aabb.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 120,
                    Height: 90,
                    SizeInBytes: 3,
                    Content: [7, 8, 9],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            var file = await repository.GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            // Act
            var content = await repository.GetThumbnailContentAsync(
                file!.Thumbnail!.PublicId,
                CancellationToken.None);

            // Assert
            content.Should().Equal([7, 8, 9],
                "because the endpoint prefers the database copy over a second seek to the thumbnail file");
        }

        /// <summary>
        /// A thumbnail stored without a database copy must not report bytes it does not have.
        /// </summary>
        [Test]
        public async Task GetThumbnailContentAsync_WhenOnlyTheFileCopyExists_ReturnsNull()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/FileOnly.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/cc/dd/ccdd.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 120,
                    Height: 90,
                    SizeInBytes: 0,
                    Content: null,
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            var file = await repository.GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            // Act
            var content = await repository.GetThumbnailContentAsync(
                file!.Thumbnail!.PublicId,
                CancellationToken.None);

            // Assert
            file.Thumbnail!.HasStoredContent.Should().BeFalse(
                "because no database copy was supplied");
            content.Should().BeNull(
                "because the endpoint must fall through to the file rather than serve an empty body");
        }

        [Test]
        public async Task MarkExcludedFromCacheAsync_RecordsThatCachingIsNotWorthRetrying()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Huge.mkv")],
                cancellationToken: CancellationToken.None);

            stored[0].IsExcludedFromCache.Should().BeFalse(
                "because a newly indexed file has not failed to cache yet");

            // Act
            await repository.MarkExcludedFromCacheAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            reloaded!.IsExcludedFromCache.Should().BeTrue(
                "because a file that cannot be buffered must stop being re-read on every request");
        }

        /// <summary>
        /// The exclusion must not outlive the content that earned it.
        /// </summary>
        /// <remarks>
        /// The flag was written in one place and cleared nowhere, so a single failed read condemned the
        /// path permanently - a file replaced by a smaller one stayed uncacheable forever. The serving
        /// path skips the cache entirely for an excluded file, so nothing else would ever reconsider it.
        /// </remarks>
        [Test]
        public async Task UpdateContentAsync_ClearsTheCacheExclusion()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Replaced.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.MarkExcludedFromCacheAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Act
            var updated = await repository.UpdateContentAsync(
                updates:
                [
                    new MediaFileContentUpdateDto
                    {
                        PublicId = stored[0].PublicId,
                        SizeInBytes = 2048,
                        FileModifiedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                        ContentStamp = "2048:638100000000000000",
                    },
                ],
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().Be(1,
                "because exactly one row was addressed");
            reloaded!.IsExcludedFromCache.Should().BeFalse(
                "because the bytes changed, so a previous failure to cache says nothing about the new "
                + "content - the file must get another chance");
        }

        /// <summary>
        /// The UPnP class follows the MIME rather than being chosen separately.
        /// </summary>
        /// <remarks>
        /// A file left claiming <c>imageItem</c> after being retyped as video disappears from a
        /// television's video listing while still advertising a video MIME, which is the failure this
        /// pins.
        /// </remarks>
        [Test]
        public async Task UpdateDlnaMappingAsync_RetypingAFile_RewritesTheProfileAndTheUpnpClass()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Mistyped.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.ImageJpeg,
                dlnaProfileName: "JPEG_LRG",
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().BeTrue("because the file exists and was rewritten");
            reloaded!.Mime.Should().Be(DlnaMime.ImageJpeg,
                "because the operator corrected the type the extension map had guessed");
            reloaded.DlnaProfileName.Should().Be("JPEG_LRG",
                "because the chosen profile is what res@protocolInfo advertises");
            reloaded.UpnpClass.Should().Be(DlnaItemClass.ImageItem,
                "because the class is derived from the new MIME, not left at the old one");
        }

        [Test]
        public async Task UpdateDlnaMappingAsync_WithABlankProfile_StoresTheCatalogueDefault()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Profiled.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.VideoXMatroska,
                dlnaProfileName: "MATROSKA",
                cancellationToken: CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.VideoXMatroska,
                dlnaProfileName: "   ",
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().BeTrue("because the file exists and was rewritten");
            reloaded!.DlnaProfileName.Should().Be(DlnaMime.VideoXMatroska.ToMainProfileName(),
                "because blank resolves to the catalogue's own profile for the type - the same value the "
                + "insert path stores, so two files identical on the wire are not left in two states");
        }

        /// <summary>
        /// Retyping across media kinds re-queues the file, because everything stored describes the old one.
        /// </summary>
        /// <remarks>
        /// The metadata probe and the thumbnail generator both dispatch on the kind, so a video retyped to
        /// a picture otherwise keeps a duration and a video codec on what is now an imageItem.
        /// </remarks>
        [Test]
        public async Task UpdateDlnaMappingAsync_WhenTheMediaKindChanges_ReQueuesTheFile()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Retyped.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.ImageJpeg,
                dlnaProfileName: null,
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().BeTrue("because the file exists and the new type is servable");
            reloaded!.MetadataStamp.Should().BeNull(
                "because a null stamp is what re-queues the file, and the stored details describe a video");
            reloaded.IsThumbnailRebuildForced.Should().BeTrue(
                "because the preview was made by the video pipeline and must not be adopted again");
        }

        [Test]
        public async Task UpdateDlnaMappingAsync_WhenOnlyTheProfileChanges_LeavesTheStoredDetailsAlone()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/SameKind.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.VideoMp4,
                dlnaProfileName: null,
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().BeTrue("because the file exists and the new type is servable");
            reloaded!.MetadataStamp.Should().NotBeNull(
                "because the kind did not change, so the extracted details still describe this file and "
                + "re-probing an entire library after a container correction would be wasted work");
        }

        /// <remarks>
        /// The dropdown is client-side, and <c>@bind</c> on an enum parses a numeric string happily - the
        /// same hole <c>LibraryIndexer.BuildScanOptions</c> already guards for the extension map.
        /// </remarks>
        [TestCase(DlnaMime.SubtitleSubrip, TestName = "UpdateDlnaMappingAsync_ToASubtitleType_IsRefused")]
        [TestCase((DlnaMime)9999, TestName = "UpdateDlnaMappingAsync_ToAnUndefinedEnumValue_IsRefused")]
        public async Task UpdateDlnaMappingAsync_ToAMimeTheServerCannotAdvertise_IsRefused(DlnaMime mime)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Guarded.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: mime,
                dlnaProfileName: null,
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            updated.Should().BeFalse("because the server would then advertise a type it never claimed to serve");
            reloaded!.Mime.Should().Be(DlnaMime.VideoXMatroska, "because a refused update must change nothing");
        }

        [TestCase("JPEG_LRG", true, TestName = "UpdateDlnaMappingAsync_WithAProfileToken_IsAccepted")]
        [TestCase("AVC_MP4;DLNA.ORG_FLAGS=0", false, TestName = "UpdateDlnaMappingAsync_WithASemicolon_IsRefused")]
        public async Task UpdateDlnaMappingAsync_ValidatesTheProfileName(string profileName, bool expected)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Profiled.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: stored[0].PublicId,
                mime: DlnaMime.ImageJpeg,
                dlnaProfileName: profileName,
                cancellationToken: CancellationToken.None);

            // Assert
            updated.Should().Be(expected,
                "because the profile is interpolated into DLNA.ORG_PN={profile};... where a semicolon "
                + "appends fields of its own, and nothing else bounds it - SQLite ignores the declared "
                + "column length");
        }

        [Test]
        public async Task UpdateDlnaMappingAsync_ForAnUnknownFile_ReportsThatNothingWasChanged()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var updated = await repository.UpdateDlnaMappingAsync(
                publicId: Guid.NewGuid(),
                mime: DlnaMime.VideoMp4,
                dlnaProfileName: null,
                cancellationToken: CancellationToken.None);

            // Assert
            updated.Should().BeFalse(
                "because the page holding the identifier can be older than the last reconciliation pass");
        }

        [Test]
        public async Task MarkExcludedFromCacheAsync_ForAnUnknownFile_DoesNothing()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var act = async () => await repository.MarkExcludedFromCacheAsync(
                Guid.NewGuid(),
                cancellationToken: CancellationToken.None);

            // Assert
            await act.Should().NotThrowAsync(
                "because the background filler can race a reconciliation that already removed the row");
        }

        [Test]
        public async Task ResetCacheExclusionAsync_ForOneFile_LetsItBackIntoTheCache()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Barred.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.MarkExcludedFromCacheAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ResetCacheExclusionAsync(
                scope: MediaFileScope.ForFile(stored[0].PublicId),
                cancellationToken: CancellationToken.None);

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            affected.Should().Be(1, "because the one file in scope was barred and is no longer");
            reloaded!.IsExcludedFromCache.Should().BeFalse(
                "because nothing else ever clears this flag, so the operator's reset is the only way back in");
        }

        /// <summary>
        /// The count is what changed, not what the scope covered.
        /// </summary>
        /// <remarks>
        /// The admin UI reports this number straight to the operator, so counting untouched rows would
        /// say a folder of healthy files had just been repaired.
        /// </remarks>
        [Test]
        public async Task ResetCacheExclusionAsync_ForAFolder_CountsOnlyTheFilesThatWereBarred()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var directories = CreateDirectoryRepository(context);
            var folders = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var directoryId = folders[0].PublicId;

            var stored = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/Films/Barred.mkv") with { DirectoryPublicId = directoryId },
                    CreateFile("/media/Films/Healthy.mkv") with { DirectoryPublicId = directoryId },
                ],
                cancellationToken: CancellationToken.None);

            await repository.MarkExcludedFromCacheAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ResetCacheExclusionAsync(
                scope: MediaFileScope.ForDirectory(directoryId, includeSubdirectories: false),
                cancellationToken: CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because only one of the folder's two files was barred, and the other was already allowed");
        }

        /// <summary>
        /// Clearing metadata must also reset the failure count, or retired files stay retired.
        /// </summary>
        /// <remarks>
        /// The pending-work query excludes files that have failed <c>MaxFailureCount</c> times. Clearing
        /// only the stamp would schedule every file except the ones that most need re-reading.
        /// </remarks>
        [Test]
        public async Task ClearAllMetadataAsync_ResetsTheStampAndTheFailureCount()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Probed.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            await repository.RecordProcessingFailureAsync(
                stored[0].PublicId,
                metadataFailed: true,
                thumbnailFailed: false,
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ClearAllMetadataAsync(CancellationToken.None);

            // Assert
            affected.Should().BeGreaterThan(0, "because one file had a stamp or a failure recorded");

            var reloaded = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            reloaded!.MetadataStamp.Should().BeNull(
                "because a null stamp is what schedules the file for re-reading");

            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().Contain(f => f.PublicId == stored[0].PublicId,
                "because the failure count was reset too, so the file is eligible again");
        }

        [Test]
        public async Task ClearAllThumbnailsAsync_RemovesTheRowAndTheStamp()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Previewed.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/aa/bb/aabb.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: [1, 2, 3],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var affected = await repository.ClearAllThumbnailsAsync(CancellationToken.None);

            // Assert
            affected.Should().BeGreaterThan(0, "because the file had a thumbnail stamp");

            (await repository.GetThumbnailByPublicIdAsync(stored[0].PublicId, CancellationToken.None))
                .Should().BeNull(
                    "because the row goes with the stamp - a thumbnail kept beside a null stamp would be "
                    + "served while its replacement was being generated");
        }

        [Test]
        public async Task ResetProcessingAsync_ForOneFile_ClearsBothStamps()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/One.mkv"), CreateFile("/media/movies/Two.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                CancellationToken.None);
            await repository.SaveMetadataAsync(
                stored[1].PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var reset = await repository.ResetProcessingAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            reset.Should().BeTrue("because the file exists");

            var first = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            var second = await repository.GetByPublicIdAsync(stored[1].PublicId, CancellationToken.None);

            first!.MetadataStamp.Should().BeNull("because this file was the one reset");
            second!.MetadataStamp.Should().NotBeNull(
                "because a single-file reset must not touch the rest of the library");
        }

        [Test]
        public async Task ResetProcessingAsync_ForAnUnknownFile_ReportsFalse()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var reset = await repository.ResetProcessingAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            reset.Should().BeFalse(
                "because the endpoint answers 404 on this, rather than reporting a reset that never happened");
        }

        /// <summary>
        /// The name filter must ignore capitals, which is what a search box is expected to do.
        /// </summary>
        [Test]
        public async Task SearchAsync_ByName_IgnoresCapitals()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Naruto Shippuden.mkv"), CreateFile("/media/movies/Bleach.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { NameContains = "naruto" },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because one file matches regardless of case")
                .Which.FileName.Should().Be("Naruto Shippuden.mkv",
                    "because the lower-case term must find the capitalised name");
        }

        /// <summary>
        /// A term containing LIKE wildcards must be searched for literally.
        /// </summary>
        /// <remarks>
        /// Filenames are full of underscores, and an unescaped <c>_</c> in a LIKE pattern matches any
        /// single character - so without escaping this search would return rows that do not contain the
        /// term at all, which is worse than returning none.
        /// </remarks>
        [Test]
        public async Task SearchAsync_ByNameContainingWildcards_TreatsThemAsLiteralText()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/a_b.mkv"), CreateFile("/media/axb.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { NameContains = "a_b" },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because only the literal underscore matches")
                .Which.FileName.Should().Be("a_b.mkv",
                    "because an unescaped underscore would also have matched axb.mkv");
        }

        /// <summary>
        /// The operator's "what still needs work" search: only files with no recorded metadata stamp.
        /// </summary>
        [Test]
        public async Task SearchAsync_ForDetailsNotRead_ReturnsOnlyTheFilesStillWithoutThem()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Probed.mkv"), CreateFile("/media/movies/Pending.mkv")],
                cancellationToken: CancellationToken.None);

            var probed = await repository.GetByPathAsync("/media/movies/Probed.mkv", CancellationToken.None);

            await repository.SaveMetadataAsync(
                probed!.PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasMetadata = false },
                cancellationToken: CancellationToken.None);

            // Assert
            stored.Should().HaveCount(2, "because both files were inserted before the filter was applied");

            found.Should().ContainSingle("because only one of the two files still has no details")
                .Which.FileName.Should().Be("Pending.mkv",
                    "because the file whose metadata was saved has a stamp and is no longer outstanding");
        }

        /// <summary>
        /// The same filter the other way round, so the two halves partition the library.
        /// </summary>
        [Test]
        public async Task SearchAsync_ForDetailsRead_ReturnsOnlyTheProcessedFiles()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Probed.mkv"), CreateFile("/media/movies/Pending.mkv")],
                cancellationToken: CancellationToken.None);

            var probed = await repository.GetByPathAsync("/media/movies/Probed.mkv", CancellationToken.None);

            await repository.SaveMetadataAsync(
                probed!.PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasMetadata = true },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because exactly one file has had its details read")
                .Which.FileName.Should().Be("Probed.mkv",
                    "because true and false must select complementary sets, not overlapping ones");
        }

        /// <summary>
        /// A file that can never have a preview must not be reported as missing one.
        /// </summary>
        /// <remarks>
        /// <c>MarkThumbnailNotApplicableAsync</c> records the work as done by stamping the row without
        /// generating an image, which is how audio is handled. Reading the thumbnail row instead of the
        /// stamp would hand an operator an entire music collection as outstanding work that no amount of
        /// reprocessing could ever clear.
        /// </remarks>
        [Test]
        public async Task SearchAsync_ForAPreviewNotMade_TreatsAFileThatCanNeverHaveOneAsDone()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Song.mkv"), CreateFile("/media/movies/NoPreview.mkv")],
                cancellationToken: CancellationToken.None);

            var song = await repository.GetByPathAsync("/media/music/Song.mkv", CancellationToken.None);

            await repository.MarkThumbnailNotApplicableAsync(
                song!.PublicId,
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasThumbnail = false },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because the not-applicable file is stamped and so counts as done")
                .Which.FileName.Should().Be("NoPreview.mkv",
                    "because only a file that could still gain a preview is outstanding");
        }

        /// <summary>
        /// The new filters are subject to folder hiding like every other listing.
        /// </summary>
        /// <remarks>
        /// <c>SearchAsync</c> applies hiding before any filter, so this holds by construction - it is
        /// pinned because a filter added later could be written against an unfiltered query and would
        /// leak the contents of a folder the operator had hidden.
        /// </remarks>
        [Test]
        public async Task SearchAsync_ForDetailsNotRead_StillHidesAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "Private");

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/Films/Private/Hidden.mkv"),
                    CreateFile("/media/Films/Shown.mkv"),
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasMetadata = false },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because the excluded folder's file must never reach a listing")
                .Which.FileName.Should().Be("Shown.mkv",
                    "because hiding is applied before the filter, not after it");
        }

        /// <summary>
        /// A file that has failed every attempt is still missing its details, and the count says why.
        /// </summary>
        [Test]
        public async Task SearchAsync_ForDetailsNotRead_IncludesAFileWhoseAttemptsFailedAndReportsTheCount()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Broken.mkv"), CreateFile("/media/movies/Probed.mkv")],
                cancellationToken: CancellationToken.None);

            var broken = await repository.GetByPathAsync("/media/movies/Broken.mkv", CancellationToken.None);

            await repository.RecordProcessingFailureAsync(
                broken!.PublicId,
                metadataFailed: true,
                thumbnailFailed: false,
                cancellationToken: CancellationToken.None);

            // A second file that HAS been read, so the filter has to do the work: with only the broken
            // file in the library this test passed even with no filter applied at all.
            var probed = await repository.GetByPathAsync("/media/movies/Probed.mkv", CancellationToken.None);

            await repository.SaveMetadataAsync(
                probed!.PublicId,
                new MediaMetadataResult([], null, []),
                "1024:638000000000000000",
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasMetadata = false },
                cancellationToken: CancellationToken.None);

            // Assert
            var single = found.Should().ContainSingle(
                "because a failed attempt leaves the stamp null, so the file is still outstanding").Subject;

            single.MetadataFailureCount.Should().Be(1,
                "because the count is what separates a failed file from one merely waiting its turn");
        }

        [Test]
        public async Task SearchAsync_BySizeRange_ReturnsOnlyFilesInsideIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/small.mkv") with { SizeInBytes = 500 },
                    CreateFile("/media/medium.mkv") with { SizeInBytes = 5_000 },
                    CreateFile("/media/large.mkv") with { SizeInBytes = 50_000 },
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { MinSizeInBytes = 1_000, MaxSizeInBytes = 10_000 },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().Equal(["medium.mkv"],
                "because the range is inclusive of its bounds and excludes everything outside them");
        }

        /// <remarks>
        /// The filter is written in the operator's direction and the column in the opposite one, so both
        /// halves are pinned - a single inverted comparison would otherwise pass one of them.
        /// </remarks>
        [TestCase(true, "allowed.mkv", TestName = "SearchAsync_KeptInMemory_ReturnsOnlyTheAllowedFiles")]
        [TestCase(false, "barred.mkv", TestName = "SearchAsync_NotKeptInMemory_ReturnsOnlyTheBarredFiles")]
        public async Task SearchAsync_ByKeptInMemory_ReturnsOnlyThatSide(
            bool isKeptInMemory,
            string expectedFileName)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/allowed.mkv"), CreateFile("/media/barred.mkv")],
                cancellationToken: CancellationToken.None);

            var barred = stored.Single(static f => f.FileName == "barred.mkv");

            await repository.MarkExcludedFromCacheAsync(
                barred.PublicId,
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { IsKeptInMemory = isKeptInMemory },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().Equal([expectedFileName],
                "because a file is kept in memory unless reading it has already failed");
        }

        /// <summary>
        /// NOTES items 7i and 7j: "has subtitles" is a linked subtitle file or a track inside the file, and
        /// an automatic link the operator removed does not count.
        /// </summary>
        [TestCase(true, "linked.mkv", TestName = "SearchAsync_WithSubtitles_ReturnsOnlyTheLinkedFile")]
        [TestCase(false, "plain.mkv,removed.mkv", TestName = "SearchAsync_WithoutSubtitles_ReturnsTheRest")]
        public async Task SearchAsync_BySubtitles_ReturnsOnlyThatSide(bool hasSubtitles, string expectedFileNames)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var subtitles = new SubtitleRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/linked.mkv"), CreateFile("/media/removed.mkv"), CreateFile("/media/plain.mkv")],
                cancellationToken: CancellationToken.None);

            _ = await subtitles.AddManualAsync(
                mediaFilePublicId: stored.Single(static f => f.FileName == "linked.mkv").PublicId,
                relativePath: "linked.srt",
                language: null,
                cancellationToken: CancellationToken.None);

            var removedId = await context.Files
                .Where(static f => f.FileName == "removed.mkv")
                .Select(static f => f.Id)
                .SingleAsync(CancellationToken.None);
            _ = context.SubtitleFiles.Add(new SubtitleFileEntity
            {
                MediaFileId = removedId,
                RelativePath = "removed.srt",
                Source = SubtitleSource.Removed,
            });
            _ = await context.SaveChangesAsync(CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { HasSubtitles = hasSubtitles },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().BeEquivalentTo(expectedFileNames.Split(','),
                "because only a live link counts, and a link the operator removed does not");
            found.Should().OnlyContain(f => f.HasSubtitles == hasSubtitles,
                "because the listing's own flag has to agree with the filter that selected it");
        }

        [Test]
        public async Task SearchAsync_ByModifiedRange_ReturnsOnlyFilesInsideIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/old.mkv") with
                    {
                        FileModifiedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    },
                    CreateFile("/media/recent.mkv") with
                    {
                        FileModifiedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    },
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest
                {
                    ModifiedFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().Equal(["recent.mkv"],
                "because only one file was modified after the lower bound");
        }

        /// <summary>
        /// Filters combine with AND, so a file has to satisfy every one that is set.
        /// </summary>
        [Test]
        public async Task SearchAsync_WithSeveralFilters_RequiresAllOfThem()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/match.mkv") with { SizeInBytes = 5_000 },
                    CreateFile("/media/match-too-big.mkv") with { SizeInBytes = 500_000 },
                    CreateFile("/media/other.mkv") with { SizeInBytes = 5_000 },
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { NameContains = "match", MaxSizeInBytes = 10_000 },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().Equal(["match.mkv"],
                "because the name matches three files and the size narrows it to one");
        }

        [Test]
        public async Task SearchAsync_WithNoFilters_IsStillBoundedByTake()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/a.mkv"), CreateFile("/media/b.mkv"), CreateFile("/media/c.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { Take = 2 },
                CancellationToken.None);

            // Assert
            found.Should().HaveCount(2,
                "because an unfiltered search must not try to return a whole library");
        }

        /// <summary>
        /// Clearing has to leave the file alone afterwards, which is the only thing separating it from
        /// recreating - both delete what is there.
        /// </summary>
        [Test]
        public async Task ClearMetadataAsync_ForOneFile_SuppressesMetadataWithoutTouchingTheThumbnail()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ClearMetadataAsync(
                MediaFileScope.ForFile(stored[0].PublicId),
                CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because the scope named exactly one indexed file");

            var entity = await context.Files.AsNoTracking().FirstAsync(CancellationToken.None);
            entity.IsMetadataSuppressed.Should().BeTrue(
                "because the suppression flag is the only thing that stops the pass producing it again");
            entity.IsThumbnailSuppressed.Should().BeFalse(
                "because the two dimensions are independent and only metadata was cleared");

            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().ContainSingle(
                "because the file is still waiting for its thumbnail, which this did not touch");
        }

        /// <summary>
        /// Only when both dimensions are cleared does a file drop out of the processing queue entirely.
        /// </summary>
        [Test]
        public async Task ClearMetadataAndThumbnails_ForOneFile_TakesItOutOfThePendingWork()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);
            var scope = MediaFileScope.ForFile(stored[0].PublicId);

            // Act
            _ = await repository.ClearMetadataAsync(scope, CancellationToken.None);
            _ = await repository.ClearThumbnailsAsync(scope, CancellationToken.None);

            // Assert
            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().BeEmpty(
                "because a cleared file must stay cleared rather than be produced again on the next pass");
        }

        [Test]
        public async Task RecreateMetadataAsync_AfterClearing_PutsTheFileBackIntoThePendingWork()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);
            var scope = MediaFileScope.ForFile(stored[0].PublicId);
            _ = await repository.ClearMetadataAsync(scope, CancellationToken.None);

            // Act
            var affected = await repository.RecreateMetadataAsync(scope, CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because the same single file is in scope");

            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().ContainSingle(
                "because recreating lifts the suppression that clearing left behind");
        }

        /// <summary>
        /// A directory scope must stop at that directory unless subdirectories are asked for.
        /// </summary>
        [Test]
        public async Task ClearMetadataAsync_ForADirectory_LeavesSubdirectoriesAloneByDefault()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context);
            var repository = CreateRepository(context);
            var (parent, child) = await CreateDirectoryPairAsync(directories);

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/top.mkv") with { DirectoryPublicId = parent },
                    CreateFile("/media/sub/deep.mkv") with { DirectoryPublicId = child },
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ClearMetadataAsync(
                MediaFileScope.ForDirectory(parent, includeSubdirectories: false),
                CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because only the file sitting directly in that directory is in scope");

            var suppressed = await context.Files.AsNoTracking()
                .Where(static f => f.IsMetadataSuppressed)
                .Select(static f => f.FileName)
                .ToListAsync(CancellationToken.None);
            suppressed.Should().BeEquivalentTo(["top.mkv"],
                "because a directory scope stops at that directory unless subdirectories are asked for");
        }

        [Test]
        public async Task ClearMetadataAsync_ForADirectoryIncludingSubdirectories_ReachesTheWholeTree()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context);
            var repository = CreateRepository(context);
            var (parent, child) = await CreateDirectoryPairAsync(directories);

            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/top.mkv") with { DirectoryPublicId = parent },
                    CreateFile("/media/sub/deep.mkv") with { DirectoryPublicId = child },
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ClearMetadataAsync(
                MediaFileScope.ForDirectory(parent, includeSubdirectories: true),
                CancellationToken.None);

            // Assert
            affected.Should().Be(2,
                "because the whole tree beneath the directory is in scope");

            var suppressed = await context.Files.AsNoTracking()
                .Where(static f => f.IsMetadataSuppressed)
                .Select(static f => f.FileName)
                .ToListAsync(CancellationToken.None);
            suppressed.Should().BeEquivalentTo(["deep.mkv", "top.mkv"],
                "because including subdirectories must reach the files below as well as those alongside");
        }

        [Test]
        public async Task ClearThumbnailsAsync_ForOneFile_DeletesTheRowAndStopsItBeingMadeAgain()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);
            var scope = MediaFileScope.ForFile(stored[0].PublicId);

            // Metadata is cleared too, so what remains pending can only be thumbnail work.
            _ = await repository.ClearMetadataAsync(scope, CancellationToken.None);

            // Act
            var affected = await repository.ClearThumbnailsAsync(scope, CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because the scope named one indexed file");

            var thumbnails = await context.Thumbnails.CountAsync(CancellationToken.None);
            thumbnails.Should().Be(0,
                "because a thumbnail row surviving a cleared stamp would be served as though it were current");

            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().BeEmpty(
                "because a cleared thumbnail must not be regenerated by the next pass");
        }

        [Test]
        public async Task RecreateThumbnailsAsync_QueuesTheFileAgain()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);
            var scope = MediaFileScope.ForFile(stored[0].PublicId);
            _ = await repository.ClearMetadataAsync(scope, CancellationToken.None);
            _ = await repository.ClearThumbnailsAsync(scope, CancellationToken.None);

            // Act
            var affected = await repository.RecreateThumbnailsAsync(scope, CancellationToken.None);

            // Assert
            affected.Should().Be(1,
                "because the same single file is in scope");

            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);
            pending.Should().ContainSingle(
                "because the thumbnail is wanted again even though the metadata is still suppressed");
        }

        [Test]
        public async Task ClearMetadataAsync_ForAnUnknownDirectory_AffectsNothing()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var affected = await repository.ClearMetadataAsync(
                MediaFileScope.ForDirectory(Guid.NewGuid(), includeSubdirectories: true),
                CancellationToken.None);

            // Assert
            affected.Should().Be(0,
                "because a scope naming something that is not indexed must be a no-op, not a match-all");
        }

        /// <summary>
        /// A film with several dubs must keep every audio track, in container order.
        /// </summary>
        /// <remarks>
        /// The regression this guards: audio was a one-to-one relation with a unique index on the file
        /// key, so the second track was not merely dropped by the mapper - the database would have
        /// rejected it.
        /// </remarks>
        [Test]
        public async Task SaveMetadataAsync_WithSeveralAudioTracks_KeepsThemAllInOrder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/dubbed.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult(
                    [
                        CreateAudio(index: 0, language: "eng", isDefault: true),
                        CreateAudio(index: 1, language: "ces"),
                        CreateAudio(index: 2, language: "deu"),
                    ],
                    null,
                    []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Assert
            var details = await repository.GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            details!.AudioStreams.Select(static a => a.Language).Should().Equal(["eng", "ces", "deu"],
                "because every track is kept and the container's order is what a renderer expects");
            details.AudioStreams[0].IsDefault.Should().BeTrue(
                "because the container's default track has to stay identifiable once there are several");
        }

        /// <summary>
        /// Re-probing must replace the tracks rather than merge them, or a file that loses a dub keeps a
        /// stale row for it.
        /// </summary>
        [Test]
        public async Task SaveMetadataAsync_WithFewerAudioTracksThanBefore_DropsTheExtraOnes()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/dubbed.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult(
                    [CreateAudio(index: 0, language: "eng"), CreateAudio(index: 1, language: "ces")],
                    null,
                    []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([CreateAudio(index: 0, language: "eng")], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Assert
            var details = await repository.GetWithDetailsAsync(stored[0].PublicId, CancellationToken.None);

            details!.AudioStreams.Should().ContainSingle(
                "because the tracks are replaced wholesale rather than matched up by index");

            var rows = await context.AudioStreams.CountAsync(CancellationToken.None);
            rows.Should().Be(1,
                "because the dropped track must leave no orphan row behind");
        }

        [Test]
        public async Task SearchAsync_ByAudioLanguage_KeepsFilesCarryingAnyOfThem()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/a.mkv"), CreateFile("/media/b.mkv"), CreateFile("/media/c.mkv")],
                cancellationToken: CancellationToken.None);

            // Resolved by path: AddRangeAsync does not order its result, so its indexes are not the
            // insertion order.
            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/a.mkv"),
                new MediaMetadataResult([CreateAudio(0, "eng"), CreateAudio(1, "ces")], null, []),
                "1024:638000000000000000",
                CancellationToken.None);
            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/b.mkv"),
                new MediaMetadataResult([CreateAudio(0, "deu")], null, []),
                "1024:638000000000000000",
                CancellationToken.None);
            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/c.mkv"),
                new MediaMetadataResult([CreateAudio(0, "fra")], null, []),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { AudioLanguages = ["ces", "deu"] },
                CancellationToken.None);

            // Assert
            found.Select(static f => f.FileName).Should().BeEquivalentTo(["a.mkv", "b.mkv"],
                "because the ticked languages widen the search rather than narrowing it - a file matching "
                + "any one of them is wanted, including on a track that is not its first");
        }

        [Test]
        public async Task SearchAsync_ByAudioAndSubtitleLanguage_RequiresBoth()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/a.mkv"), CreateFile("/media/b.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/a.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "eng")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = "ces" }]),
                "1024:638000000000000000",
                CancellationToken.None);
            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/b.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "eng")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = "deu" }]),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { AudioLanguages = ["eng"], SubtitleLanguages = ["ces"] },
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle("because the two language filters combine with AND, as every other filter does")
                .Which.FileName.Should().Be("a.mkv",
                    "because only that file has both the English audio and the Czech subtitles");
        }

        [Test]
        public async Task GetLanguagesAsync_ReturnsEachLanguageOnceAndSorted()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/a.mkv"), CreateFile("/media/b.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/a.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "eng"), CreateAudio(1, "ces")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = "ces" }]),
                "1024:638000000000000000",
                CancellationToken.None);
            await repository.SaveMetadataAsync(
                await PublicIdOfAsync(repository, "/media/b.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "eng")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = null }]),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var languages = await repository.GetLanguagesAsync(CancellationToken.None);

            // Assert
            languages.Audio.Should().Equal(["ces", "eng"],
                "because the filter list offers each language once, in a stable order");
            languages.Subtitle.Should().Equal(["ces"],
                "because a track with no language tag has nothing to offer as a filter");
        }

        /// <summary>
        /// The language filters are a listing too, so they obey <c>ExcludeFolders</c>.
        /// </summary>
        /// <remarks>
        /// This was the one listing that did not. Reading the stream tables directly meant a hidden
        /// folder's languages populated the search form, which both told the operator what was in there
        /// and offered a tick that then matched nothing visible.
        /// </remarks>
        [Test]
        public async Task GetLanguagesAsync_HidesLanguagesFoundOnlyInsideAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var seed = CreateRepository(context);
            _ = await seed.AddRangeAsync(
                files: [CreateFile("/media/visible.mkv"), CreateFile("/media/Private/hidden.mkv")],
                cancellationToken: CancellationToken.None);

            await seed.SaveMetadataAsync(
                await PublicIdOfAsync(seed, "/media/visible.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "eng")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = "eng" }]),
                "1024:638000000000000000",
                CancellationToken.None);
            await seed.SaveMetadataAsync(
                await PublicIdOfAsync(seed, "/media/Private/hidden.mkv"),
                new MediaMetadataResult(
                    [CreateAudio(0, "ces")],
                    null,
                    [new SubtitleStreamDto { StreamIndex = 0, Language = "ces" }]),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act - lower case, because the case-insensitive match is the half that silently did nothing.
            var repository = CreateRepository(context, "private");
            var languages = await repository.GetLanguagesAsync(CancellationToken.None);

            // Assert
            languages.Audio.Should().Equal(["eng"],
                "because a language that exists only inside a hidden folder must not be offered as a filter");
            languages.Subtitle.Should().Equal(["eng"],
                "because the subtitle list is filtered on the same rule as the audio one");
        }

        /// <summary>
        /// The count overload stores the same rows as the DTO overload, without reading them back.
        /// </summary>
        /// <remarks>
        /// The indexer used the DTO-returning overload and read nothing but <c>Count</c> off it, so a
        /// cold index over 25,504 files re-read every inserted row through a projection carrying three
        /// LEFT JOINs and an ordered correlated subquery - about 15 MB of allocation for a number the
        /// insert already had. Both overloads share one private insert, so this asserts the rows really
        /// land rather than that a number comes back.
        /// </remarks>
        [Test]
        public async Task AddRangeReturningCountAsync_StoresTheFilesAndReturnsHowMany()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var count = await repository.AddRangeReturningCountAsync(
                files: [CreateFile("/media/one.mkv"), CreateFile("/media/two.mkv")],
                cancellationToken: CancellationToken.None);

            // Assert
            count.Should().Be(2,
                "because both files were inserted and the caller uses this number as the added count");

            await using var verify = _database.CreateContext();
            (await verify.Files.CountAsync(CancellationToken.None)).Should().Be(2,
                "because skipping the read-back must not skip the insert");
        }

        [Test]
        public async Task AddRangeReturningCountAsync_WithNoFiles_ReturnsZero()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            // Act
            var count = await repository.AddRangeReturningCountAsync(
                files: [],
                cancellationToken: CancellationToken.None);

            // Assert
            count.Should().Be(0,
                "because a batch with nothing in it must not reach the database");
        }

        /// <summary>
        /// <c>IndexedUtc</c> overrides the row's own indexing date; null leaves it to the store.
        /// </summary>
        /// <remarks>
        /// The null half is the subtle one, and it is why <c>DlnaDbContext.StampTimestamps</c> stamps
        /// <c>CreatedUtc</c> only when it is <c>default</c> - the repository passes the default through
        /// and lets that fill it. Making the stamp unconditional there would silently undo the whole
        /// first-fill fix, so this test is really guarding that seam.
        /// </remarks>
        [Test]
        public async Task AddRangeAsync_WithIndexedUtc_StoresItAndOtherwiseStampsNow()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var supplied = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc);

            // Act
            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/dated.mkv") with { IndexedUtc = supplied },
                    CreateFile("/media/undated.mkv"),
                ],
                cancellationToken: CancellationToken.None);

            // Assert
            await using var verify = _database.CreateContext();
            var dated = await verify.Files.AsNoTracking()
                .Where(f => f.FullPath == "/media/dated.mkv")
                .Select(static f => f.CreatedUtc)
                .FirstAsync(CancellationToken.None);
            var undated = await verify.Files.AsNoTracking()
                .Where(f => f.FullPath == "/media/undated.mkv")
                .Select(static f => f.CreatedUtc)
                .FirstAsync(CancellationToken.None);

            dated.Should().BeCloseTo(supplied, TimeSpan.FromSeconds(1),
                "because a first fill supplies the filesystem's date and it must reach the column "
                + "Recently added orders by");
            undated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5),
                "because an ordinary arrival supplies nothing and must still be stamped with the "
                + "current time rather than left at default");
        }

        private static async Task<Guid> PublicIdOfAsync(MediaFileRepository repository, string fullPath)
        {
            var file = await repository.GetByPathAsync(fullPath, CancellationToken.None);

            return file!.PublicId;
        }

        private static AudioStreamDto CreateAudio(int index, string? language, bool isDefault = false)
        {
            return new AudioStreamDto
            {
                StreamIndex = index,
                Language = language,
                IsDefault = isDefault,
                Codec = "aac",
                Channels = 2,
            };
        }

        private static async Task<(Guid Parent, Guid Child)> CreateDirectoryPairAsync(
            MediaDirectoryRepository directories)
        {
            var parents = await directories.AddRangeAsync(
                directories:
                [
                    new MediaDirectoryCreateDto
                    {
                        FullPath = "/media",
                        Name = "media",
                        Depth = 1,
                        IsSourceRoot = true,
                    },
                ],
                cancellationToken: CancellationToken.None);

            var children = await directories.AddRangeAsync(
                directories:
                [
                    new MediaDirectoryCreateDto
                    {
                        FullPath = "/media/sub",
                        Name = "sub",
                        Depth = 2,
                        IsSourceRoot = false,
                        ParentDirectoryPublicId = parents[0].PublicId,
                    },
                ],
                cancellationToken: CancellationToken.None);

            return (parents[0].PublicId, children[0].PublicId);
        }

        /// <summary>
        /// The customer-reported defect, asserted through SQL rather than through the in-memory matcher.
        /// </summary>
        /// <remarks>
        /// The read side is a LIKE inside the query, so <c>PathExclusionTest</c> cannot prove it - a
        /// segment-aligned rule in C# and a substring in SQL would pass every test there and still ship
        /// the bug. This also proves the predicate TRANSLATES: it wraps the column in a <c>replace</c>
        /// and two concatenations, and EF Core throws rather than falling back to client evaluation, so a
        /// shape SQLite cannot express fails here loudly. See trap 2 - an analyzer's advice that did not
        /// survive expression-tree translation cost a rewrite on this very predicate once already.
        /// </remarks>
        [TestCase("/media/path1/keep.mkv", false)]
        [TestCase("/media/path1/deeper/keep.mkv", false)]
        [TestCase("/media/path1L/keep.mkv", true)]
        [TestCase("/media/path10/keep.mkv", true)]
        [TestCase("/media/Xpath1/keep.mkv", true)]
        public async Task SearchAsync_HidesOnSegmentBoundaries_NotOnASubstring(string fullPath, bool expectedVisible)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var seed = CreateRepository(context);
            _ = await seed.AddRangeAsync(
                files: [CreateFile(fullPath)],
                cancellationToken: CancellationToken.None);

            var repository = CreateRepository(context, "path1");

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { Take = 10 },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Any(f => f.FullPath == fullPath).Should().Be(expectedVisible,
                $"because 'path1' names one folder, so '{fullPath}' must be "
                + (expectedVisible ? "visible" : "hidden"));
        }

        /// <summary>
        /// A partial path hides one branch, in SQL, without hiding folders that share its last name.
        /// </summary>
        [TestCase("/media/Films/Private/one.mkv", false)]
        [TestCase("/media/Series/Private/one.mkv", true)]
        [TestCase("/media/Films/PrivateL/one.mkv", true)]
        public async Task SearchAsync_WithAPartialPathExclusion_HidesOnlyThatBranch(
            string fullPath,
            bool expectedVisible)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var seed = CreateRepository(context);
            _ = await seed.AddRangeAsync(
                files: [CreateFile(fullPath)],
                cancellationToken: CancellationToken.None);

            var repository = CreateRepository(context, "Films/Private");

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { Take = 10 },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Any(f => f.FullPath == fullPath).Should().Be(expectedVisible,
                $"because the entry names two consecutive folders, so '{fullPath}' must be "
                + (expectedVisible ? "visible" : "hidden"));
        }

        /// <summary>
        /// An entry written with the other separator still matches, in SQL.
        /// </summary>
        /// <remarks>
        /// A <c>config.json</c> is carried between the NAS and a Windows machine. Refusing the foreign
        /// separator would fail silently - nothing hidden, nothing said - which is the same failure
        /// direction as the original defect.
        /// </remarks>
        [TestCase("Films/Private")]
        [TestCase("Films\\Private")]
        public async Task SearchAsync_WithEitherSeparatorInTheEntry_HidesTheSameBranch(string entry)
        {
            // Arrange
            await using var context = _database.CreateContext();
            var seed = CreateRepository(context);
            _ = await seed.AddRangeAsync(
                files: [CreateFile("/media/Films/Private/one.mkv")],
                cancellationToken: CancellationToken.None);

            var repository = CreateRepository(context, entry);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { Take = 10 },
                cancellationToken: CancellationToken.None);

            // Assert
            found.Should().BeEmpty(
                $"because '{entry}' names the same folder whichever separator it uses");
        }

        /// <summary>
        /// The SQL predicate and the in-memory matcher return the same answer for the same entry,
        /// whatever surrounding whitespace or separators it carries.
        /// </summary>
        /// <remarks>
        /// <b>This is the assertion M1 was missing, and its absence is why M1 shipped.</b> The matcher
        /// trimmed an entry and the three SQL sites did not, so a configured <c>/path1/</c> built the
        /// pattern <c>%//path1//%</c> - a doubled separator no stored path holds - and the entry hid
        /// nothing at all. Scanning still refused to import the folder, so the two halves failed in
        /// opposite directions and every already-indexed row stayed visible and streamable.
        /// <para>
        /// The old test covered <c>/path1/</c> against <see cref="PathExclusion.IsHidden"/> alone, which
        /// is the half that already worked - it could never have caught this. Asserting the two halves
        /// <b>agree</b> is what makes a future divergence fail here rather than on a television.
        /// </para>
        /// </remarks>
        [TestCase("Films/Private", TestName = "plain partial path")]
        [TestCase("/Films/Private/", TestName = "surrounding separators")]
        [TestCase("  Films/Private  ", TestName = "surrounding whitespace")]
        [TestCase("Films\\Private\\", TestName = "foreign separator and a trailing one")]
        [TestCase(" /Films/Private\\ ", TestName = "both, mixed")]
        [TestCase("Private", TestName = "single segment")]
        [TestCase("/Private/", TestName = "single segment, wrapped")]
        public async Task ExcludeHidden_ForAnyFormOfTheSameEntry_AgreesWithPathExclusion(string entry)
        {
            // Arrange
            const string hiddenPath = "/media/Films/Private/one.mkv";
            const string visiblePath = "/media/Films/PrivateL/two.mkv";

            await using var context = _database.CreateContext();
            var seed = CreateRepository(context);
            _ = await seed.AddRangeAsync(
                files: [CreateFile(hiddenPath), CreateFile(visiblePath)],
                cancellationToken: CancellationToken.None);

            var repository = CreateRepository(context, entry);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest { Take = 10 },
                cancellationToken: CancellationToken.None);

            var visibleInSql = found.Select(static f => f.FullPath).ToList();

            // Assert
            foreach (var path in new[] { hiddenPath, visiblePath })
            {
                var hiddenInMemory = PathExclusion.IsHidden(path, [entry]);
                var hiddenInSql = !visibleInSql.Contains(path);

                hiddenInSql.Should().Be(hiddenInMemory,
                    $"because the SQL predicate and the in-memory matcher must agree on '{path}' for "
                    + $"the entry '{entry}' - a divergence here is what hid nothing while scanning "
                    + "refused to import, and left the folder streamable");
            }

            // Pinned separately from the agreement check: two halves that agree on the WRONG answer
            // would satisfy the loop above and still ship the customer's bug.
            visibleInSql.Should().Equal([visiblePath],
                $"because '{entry}' must hide the folder it names and nothing that merely shares its "
                + "name's prefix");
        }

        /// <summary>
        /// Replaces the preview page reading the whole folder to find two neighbours. The ordering and
        /// the definition of "playable" must be what the page did for itself, or the buttons step through
        /// a different sequence than before.
        /// </summary>
        [Test]
        public async Task GetPlayableNeighboursAsync_OrdersByTitleAndSkipsUnplayableFiles()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context);
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var directoryId = stored[0].PublicId;

            var repository = CreateRepository(context);
            var added = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/Films/c.mkv") with { DirectoryPublicId = directoryId },
                    CreateFile("/media/Films/a.mkv") with { DirectoryPublicId = directoryId },
                    CreateFile("/media/Films/b.srt") with
                    {
                        DirectoryPublicId = directoryId,
                        Mime = DlnaMime.SubtitleSubrip,
                        UpnpClass = DlnaItemClass.TextItem,
                    },
                    CreateFile("/media/Films/d.mkv") with { DirectoryPublicId = directoryId },
                ],
                cancellationToken: CancellationToken.None);

            var middle = added.Single(f => f.FullPath.EndsWith("c.mkv", StringComparison.Ordinal));

            // Act
            var actual = await repository.GetPlayableNeighboursAsync(
                directoryId,
                middle.PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Count.Should().Be(expected: 3,
                "because the subtitle file is not playable and must not be stepped through");
            actual.Index.Should().Be(1,
                "because a, c and d are the playable files in title order and c is the second");
            actual.PreviousPublicId.Should().Be(
                added.Single(f => f.FullPath.EndsWith("a.mkv", StringComparison.Ordinal)).PublicId,
                "because a sorts before c once the subtitle is filtered out");
            actual.NextPublicId.Should().Be(
                added.Single(f => f.FullPath.EndsWith("d.mkv", StringComparison.Ordinal)).PublicId,
                "because d sorts after c");
        }

        [Test]
        public async Task GetPlayableNeighboursAsync_ForTheOnlyFile_HasNeitherNeighbour()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context);
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/Films", "Films", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var repository = CreateRepository(context);
            var added = await repository.AddRangeAsync(
                files: [CreateFile("/media/Films/only.mkv") with { DirectoryPublicId = stored[0].PublicId }],
                cancellationToken: CancellationToken.None);

            // Act
            var actual = await repository.GetPlayableNeighboursAsync(
                stored[0].PublicId,
                added[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Index.Should().Be(0, "because it is the first playable file");
            actual.Count.Should().Be(expected: 1, "because it is the only one");
            actual.PreviousPublicId.Should().BeNull("because nothing precedes it");
            actual.NextPublicId.Should().BeNull("because nothing follows it");
        }

        /// <summary>
        /// Hidden folders are hidden from the admin pages too, so a file under one must not appear in the
        /// sequence the preview buttons walk.
        /// </summary>
        [Test]
        public async Task GetPlayableNeighboursAsync_OmitsFilesUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context, "@Recycle");
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/@Recycle", "@Recycle", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var repository = CreateRepository(context, "@Recycle");
            var added = await repository.AddRangeAsync(
                files: [CreateFile("/media/@Recycle/old.mkv") with { DirectoryPublicId = stored[0].PublicId }],
                cancellationToken: CancellationToken.None);

            // Act
            var actual = await repository.GetPlayableNeighboursAsync(
                stored[0].PublicId,
                added[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Count.Should().Be(expected: 0, "because the whole folder is hidden");
            actual.Index.Should().Be(-1, "because the file itself is not in the visible sequence");
        }

        [Test]
        public async Task GetByDirectoryAsync_WithExcludeHidden_OmitsFilesUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context, "@Recycle");
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/@Recycle", "@Recycle", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var repository = CreateRepository(context, "@Recycle");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/@Recycle/old.mkv") with { DirectoryPublicId = stored[0].PublicId }],
                cancellationToken: CancellationToken.None);

            // Act
            var actual = await repository.GetByDirectoryAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Should().BeEmpty(
                "because ExcludeFolders hides content that is already indexed, not only new imports");
        }

        /// <summary>
        /// The exclusion match must fold case. It did not: <c>Contains</c> becomes SQLite's
        /// <c>instr</c>, and a collation applies only to comparison operators - so <c>COLLATE NOCASE</c>
        /// on the column was silently ignored and an entry whose case differed from the path hid nothing,
        /// while the admin UI de-duplicates the list case-insensitively and implies otherwise.
        /// </summary>
        [Test]
        public async Task GetByDirectoryAsync_WithAnExcludedFolderInADifferentCase_StillOmitsTheFiles()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context, "private");
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/Private", "Private", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var repository = CreateRepository(context, "private");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/Private/old.mkv") with { DirectoryPublicId = stored[0].PublicId }],
                cancellationToken: CancellationToken.None);

            // Act
            var actual = await repository.GetByDirectoryAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Should().BeEmpty(
                "because an operator who excludes 'private' expects /media/Private hidden - and a "
                + "television that keeps streaming it is the worst possible failure direction");
        }

        /// <summary>
        /// The escaping the LIKE pattern needs. Filenames are full of underscores, and an unescaped one
        /// is a single-character wildcard - so an entry of <c>a_b</c> would hide <c>aXb</c> as well.
        /// </summary>
        [Test]
        public async Task GetByDirectoryAsync_WithAnExcludedNameHoldingAWildcard_MatchesItLiterally()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var directories = CreateDirectoryRepository(context, "a_b");
            var stored = await directories.AddRangeAsync(
                directories: [CreateDirectory("/media/aXb", "aXb", depth: 2, isSourceRoot: false)],
                cancellationToken: CancellationToken.None);
            var repository = CreateRepository(context, "a_b");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/aXb/film.mkv") with { DirectoryPublicId = stored[0].PublicId }],
                cancellationToken: CancellationToken.None);

            // Act
            var actual = await repository.GetByDirectoryAsync(
                stored[0].PublicId,
                cancellationToken: CancellationToken.None);

            // Assert
            actual.Should().HaveCount(1,
                "because '_' in an exclusion entry is a literal character, not a wildcard");
        }

        /// <summary>
        /// Every listing hides an excluded file from everybody, so lookup by identifier is the only way
        /// back to one - and it has to keep working, or a renderer already streaming a file would be cut
        /// off the moment its folder was hidden.
        /// </summary>
        [Test]
        public async Task GetByPublicIdAsync_StillReturnsAFileUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/@Recycle/old.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            found.Should().NotBeNull(
                "because hiding governs listings only - delivery by identifier is exempt");
        }

        /// <summary>
        /// A temporarily hidden folder is the opposite case: delivery by identifier is refused.
        /// </summary>
        /// <remarks>
        /// The exemption above exists so a renderer mid-stream is not cut off when a folder is retired.
        /// Here it would defeat the setting outright - a television keeps the identifiers it saw in an
        /// earlier listing, so a file still answered by identifier is a file still playable.
        /// </remarks>
        [Test]
        public async Task GetByPublicIdAsync_ForAFileUnderATemporarilyHiddenFolder_ReturnsNothing()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepositoryHiding(context, temporarilyHidden: "Private");
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/Films/Private/hidden.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);

            // Assert
            found.Should().BeNull(
                "because hiding these must also stop a link kept from an earlier listing from working");
        }

        /// <remarks>
        /// The indexer's own read, and the reason it cannot join the one above: it decides what to insert,
        /// so a hidden row it cannot see is re-inserted against the unique index on the path.
        /// </remarks>
        [Test]
        public async Task GetByPathAsync_ForAFileUnderATemporarilyHiddenFolder_StillReturnsIt()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepositoryHiding(context, temporarilyHidden: "Private");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/Films/Private/hidden.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.GetByPathAsync(
                "/media/Films/Private/hidden.mkv",
                CancellationToken.None);

            // Assert
            found.Should().NotBeNull(
                "because the scan looks a file up by path to decide whether it is already indexed");
        }

        /// <remarks>
        /// The one place the two kinds of hiding differ in the other direction. A temporarily hidden file
        /// stays current, so it must still be given its metadata and its thumbnail - otherwise revealing
        /// the folder would show a wall of blank tiles until some later pass caught up.
        /// </remarks>
        [Test]
        public async Task GetPendingProcessingAsync_StillOffersAFileUnderATemporarilyHiddenFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepositoryHiding(context, temporarilyHidden: "Private");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/Films/Private/hidden.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);

            // Assert
            pending.Select(static f => f.FullPath).Should().Contain("/media/Films/Private/hidden.mkv",
                "because these folders are indexed and kept current, which is the whole difference from "
                + "an excluded one");
        }

        /// <summary>
        /// The search page is a listing like any other, so it answers with what a renderer would be shown.
        /// </summary>
        /// <remarks>
        /// It used to be exempt, which is how a search for "all, no filters" returned content the browse
        /// tree had already hidden.
        /// </remarks>
        [Test]
        public async Task SearchAsync_OmitsFilesUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            _ = await repository.AddRangeAsync(
                files:
                [
                    CreateFile("/media/@Recycle/old.mkv"),
                    CreateFile("/media/movies/kept.mkv"),
                ],
                cancellationToken: CancellationToken.None);

            // Act
            var found = await repository.SearchAsync(
                new MediaFileSearchRequest(),
                CancellationToken.None);

            // Assert
            found.Should().ContainSingle(f => f.FileName == "kept.mkv",
                "because the admin search and a renderer's browse must answer with the same library");
        }

        /// <summary>
        /// A file waiting out its retry delay is left out of the claim, so it cannot fill the batch.
        /// </summary>
        [Test]
        public async Task GetPendingProcessingAsync_LeavesOutAnExcludedFileThatHasFailed()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/broken.mkv"), CreateFile("/media/next.mkv")],
                cancellationToken: CancellationToken.None);
            var broken = stored.Single(static f => f.FileName == "broken.mkv");

            await repository.RecordProcessingFailureAsync(
                publicId: broken.PublicId,
                metadataFailed: true,
                thumbnailFailed: true,
                cancellationToken: CancellationToken.None);

            // Act
            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                excludedPublicIds: [broken.PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            pending.Should().ContainSingle("because only the file that is not waiting may be claimed")
                .Which.FileName.Should().Be("next.mkv",
                    "because the excluded file failed and its retry delay has not passed");
        }

        /// <summary>
        /// Recreating or letting a file back in resets its counts, and that must not wait out a delay.
        /// </summary>
        [Test]
        public async Task GetPendingProcessingAsync_StillOffersAnExcludedFileWhoseFailuresWereReset()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/film.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                excludedPublicIds: [stored[0].PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            pending.Should().ContainSingle(
                "because a row carrying no failure is an operator's retry or new content, not a failure to wait out");
        }

        [Test]
        public async Task GetPendingProcessingAsync_OmitsFilesUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/@Recycle/old.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var pending = await repository.GetPendingProcessingAsync(
                maxCount: 10,
                maxFailureCount: 3,
                cancellationToken: CancellationToken.None);

            // Assert
            pending.Should().BeEmpty(
                "because retiring a folder through ExcludeFolders must also stop ffprobe and thumbnail work on it");
        }

        [Test]
        public async Task GetExistingPathsAsync_StillSeesFilesUnderAnExcludedFolder()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context, "@Recycle");
            _ = await repository.AddRangeAsync(
                files: [CreateFile("/media/@Recycle/old.mkv")],
                cancellationToken: CancellationToken.None);

            // Act
            var existing = await repository.GetExistingPathsAsync(
                fullPaths: ["/media/@Recycle/old.mkv"],
                cancellationToken: CancellationToken.None);

            // Assert
            existing.Should().Contain("/media/@Recycle/old.mkv",
                "because this is what the indexer uses to decide what to insert - hiding a row from it "
                + "would make the next scan re-insert the same path and violate the unique index");
        }

        /// <summary>
        /// Replacing a thumbnail must take its stored bytes with it.
        /// </summary>
        /// <remarks>
        /// The foreign key used to sit on <c>Thumbnails</c>, making the blob the principal, so a cascade
        /// ran the wrong way and every replaced or deleted thumbnail left its bytes unreachable in a
        /// database that is never vacuumed.
        /// </remarks>
        [Test]
        public async Task SaveThumbnailAsync_WhenReplacingAThumbnail_DeletesTheOldStoredBytes()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Replaced.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/aa/bb/first.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: [1, 2, 3],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/aa/bb/second.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 4,
                    Content: [4, 5, 6, 7],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            // Assert
            var blobCount = await context.Set<ThumbnailContentEntity>()
                .CountAsync(CancellationToken.None);

            blobCount.Should().Be(1,
                "because the replaced thumbnail's bytes must be deleted with it, not orphaned");
        }

        /// <summary>
        /// Deleting a media file must take the thumbnail bytes with it, through two levels of cascade.
        /// </summary>
        [Test]
        public async Task RemoveByPublicIdsAsync_WhenTheFileHadAStoredThumbnail_DeletesTheStoredBytes()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Doomed.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/cache/cc/dd/doomed.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: [9, 9, 9],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            _ = await repository.RemoveByPublicIdsAsync(
                publicIds: [stored[0].PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            var blobCount = await context.Set<ThumbnailContentEntity>()
                .CountAsync(CancellationToken.None);

            blobCount.Should().Be(0,
                "because a deleted file cascades to its thumbnail, which must cascade to its bytes");
        }

        /// <summary>
        /// The image beside the media outlives the row, so the path has to leave with the delete.
        /// </summary>
        /// <remarks>
        /// Nothing else ever finds one of these: previews live in a folder scanning skips, so an image
        /// whose media file is gone is unreachable and stays on the disc forever.
        /// </remarks>
        [Test]
        public async Task RemoveByPublicIdsAsync_WhenTheFileHadAPreview_NamesTheImageLeftOnDisc()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);
            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/movies/Doomed.mkv"), CreateFile("/media/movies/Kept.mkv")],
                cancellationToken: CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[0].PublicId,
                new GeneratedThumbnail(
                    "/media/movies/.@__thumb/Doomed.mkv.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: [9, 9, 9],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            await repository.SaveThumbnailAsync(
                stored[1].PublicId,
                new GeneratedThumbnail(
                    "/media/movies/.@__thumb/Kept.mkv.jpg",
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: [9, 9, 9],
                    WasAdopted: false),
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var (removed, abandonedThumbnailPaths) = await repository.RemoveByPublicIdsAsync(
                publicIds: [stored[0].PublicId],
                cancellationToken: CancellationToken.None);

            // Assert
            removed.Should().Be(1, "because only the doomed file was named");
            abandonedThumbnailPaths.Should().ContainSingle(
                    "because exactly one of the two deleted-or-kept files had a preview to abandon")
                .Which.Should().Be("/media/movies/.@__thumb/Doomed.mkv.jpg",
                    "because the caller deletes the image and can only do so if it is named");
        }

        private static MediaDirectoryCreateDto CreateDirectory(
            string fullPath,
            string name,
            int depth,
            bool isSourceRoot,
            Guid? parent = null)
        {
            return new MediaDirectoryCreateDto
            {
                FullPath = fullPath,
                Name = name,
                Depth = depth,
                IsSourceRoot = isSourceRoot,
                ParentDirectoryPublicId = parent,
            };
        }

        private static MediaFileCreateDto CreateFile(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);

            return new MediaFileCreateDto
            {
                FullPath = fullPath,
                FileName = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ContentStamp = "1024:638000000000000000",
            };
        }

        /// <remarks>
        /// Every listing hides excluded folders unconditionally, so a test that exercises hiding passes
        /// the names it wants hidden; every other test gets an empty list and is unaffected.
        /// </remarks>
        private static MediaFileRepository CreateRepository(
            DlnaDbContext context,
            params string[] excludeFolders)
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = excludeFolders;

            return new MediaFileRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(options),
                new FixedFolderVisibility(hiddenFromListings: excludeFolders, hiddenFromDelivery: []));
        }

        /// <remarks>
        /// The temporarily hidden half, whose two lists differ: it is hidden from listings <b>and</b> from
        /// delivery, where an excluded folder is hidden from listings alone. Excluded folders stay out of
        /// the way here so a failure names the setting under test.
        /// </remarks>
        private static MediaFileRepository CreateRepositoryHiding(
            DlnaDbContext context,
            string temporarilyHidden)
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = [];
            options.Library.TemporarilyHiddenFolders = [temporarilyHidden];

            return new MediaFileRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(options),
                new FixedFolderVisibility(
                    hiddenFromListings: [temporarilyHidden],
                    hiddenFromDelivery: [temporarilyHidden]));
        }

        private static MediaDirectoryRepository CreateDirectoryRepository(
            DlnaDbContext context,
            params string[] excludeFolders)
        {
            var options = new DlnaOptions();
            options.Library.ExcludeFolders = excludeFolders;

            return new MediaDirectoryRepository(
                context,
                new StaticOptionsMonitor<DlnaOptions>(options),
                new FixedFolderVisibility(hiddenFromListings: excludeFolders, hiddenFromDelivery: []));
        }


        [Test]
        public async Task SaveMetadataAsync_StoresTheContainerTagsAndReadsThemBack()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Burn It Up.mp3")],
                cancellationToken: CancellationToken.None);

            var metadata = new MediaMetadataResult([], null, [])
            {
                Tags =
                [
                    new MediaFileTagDto { StreamIndex = null, Name = "artist", Value = "Some Artist" },
                    new MediaFileTagDto { StreamIndex = 0, Name = "language", Value = "eng" },
                ],
            };

            // Act
            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                metadata,
                "1024:638000000000000000",
                CancellationToken.None);

            // Assert
            var tags = await repository.GetTagsAsync(stored[0].PublicId, CancellationToken.None);

            tags.Should().HaveCount(2, "because both tags belong to the file that was probed");
            tags.Should().Contain(t => t.Name == "artist" && t.StreamIndex == null,
                "because a container tag describes the whole file");
            tags.Should().Contain(t => t.Name == "language" && t.StreamIndex == 0,
                "because a track's own tag has to keep the track it came from");
        }

        /// <remarks>
        /// Replaced rather than merged, so a tag removed from the file disappears from the row. There is
        /// no stable identity to match a tag on - a container may repeat one name per track - so the only
        /// correct merge is a rewrite.
        /// </remarks>
        [Test]
        public async Task SaveMetadataAsync_RunTwice_ReplacesTheTagsRatherThanAccumulatingThem()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Retagged.mp3")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, [])
                {
                    Tags = [new MediaFileTagDto { Name = "artist", Value = "Before" }],
                },
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, [])
                {
                    Tags = [new MediaFileTagDto { Name = "album", Value = "After" }],
                },
                "1024:638000000000000000",
                CancellationToken.None);

            // Assert
            var tags = await repository.GetTagsAsync(stored[0].PublicId, CancellationToken.None);

            tags.Should().ContainSingle("because the second read replaces the first, it does not add to it");
            tags[0].Name.Should().Be("album", "because that is what the file says now");
        }


        /// <remarks>
        /// The distinction that matters between the two maintenance buttons: purging removes the tags and
        /// re-reads nothing, so a library whose files are all processed stays processed and no ffprobe
        /// pass is triggered by pressing it.
        /// </remarks>
        [Test]
        public async Task PurgeAllTagsAsync_RemovesTheTagsAndLeavesTheFilesProcessed()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Purged.mp3")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, [])
                {
                    Tags = [new MediaFileTagDto { Name = "artist", Value = "Some Artist" }],
                },
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var removed = await repository.PurgeAllTagsAsync(CancellationToken.None);

            // Assert
            removed.Should().Be(1, "because one tag row was stored");

            var tags = await repository.GetTagsAsync(stored[0].PublicId, CancellationToken.None);
            tags.Should().BeEmpty("because purging deletes what is stored");

            var file = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            file!.MetadataStamp.Should().NotBeNull(
                "because purging must not re-queue the library for an ffprobe pass it did not ask for");
        }

        [Test]
        public async Task RecreateAllTagsAsync_RemovesTheTagsAndReQueuesTheFile()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Rebuilt.mp3")],
                cancellationToken: CancellationToken.None);

            await repository.SaveMetadataAsync(
                stored[0].PublicId,
                new MediaMetadataResult([], null, [])
                {
                    Tags = [new MediaFileTagDto { Name = "artist", Value = "Some Artist" }],
                },
                "1024:638000000000000000",
                CancellationToken.None);

            // Act
            var requeued = await repository.RecreateAllTagsAsync(CancellationToken.None);

            // Assert
            requeued.Should().Be(1, "because the one processed file has to be read again");

            var tags = await repository.GetTagsAsync(stored[0].PublicId, CancellationToken.None);
            tags.Should().BeEmpty("because the stored tags are dropped before the file is re-read");

            var file = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            file!.MetadataStamp.Should().BeNull(
                "because clearing the stamp is the only thing that re-queues the file, and tags can only "
                + "be obtained by probing it again");
        }


        [Test]
        public async Task ClearTagsAsync_ForOneFile_RemovesOnlyThatFilesTags()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/One.mp3"), CreateFile("/media/music/Two.mp3")],
                cancellationToken: CancellationToken.None);

            foreach (var file in stored)
            {
                await repository.SaveMetadataAsync(
                    file.PublicId,
                    new MediaMetadataResult([], null, [])
                    {
                        Tags = [new MediaFileTagDto { Name = "artist", Value = "Some Artist" }],
                    },
                    "1024:638000000000000000",
                    CancellationToken.None);
            }

            // Act
            var removed = await repository.ClearTagsAsync(
                MediaFileScope.ForFile(stored[0].PublicId),
                CancellationToken.None);

            // Assert
            removed.Should().Be(1, "because the scope names one file and it had one tag");

            var cleared = await repository.GetTagsAsync(stored[0].PublicId, CancellationToken.None);
            cleared.Should().BeEmpty("because that file's tags were the ones asked for");

            var untouched = await repository.GetTagsAsync(stored[1].PublicId, CancellationToken.None);
            untouched.Should().ContainSingle("because a scoped clear must not reach the rest of the library");

            var cleanedFile = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            cleanedFile!.MetadataStamp.Should().NotBeNull(
                "because Clear leaves it gone rather than queueing the file to be read again");
        }

        [Test]
        public async Task RecreateTagsAsync_ForOneFile_RemovesItsTagsAndReQueuesOnlyThatFile()
        {
            // Arrange
            await using var context = _database.CreateContext();
            var repository = CreateRepository(context);

            var stored = await repository.AddRangeAsync(
                files: [CreateFile("/media/music/Left.mp3"), CreateFile("/media/music/Right.mp3")],
                cancellationToken: CancellationToken.None);

            foreach (var file in stored)
            {
                await repository.SaveMetadataAsync(
                    file.PublicId,
                    new MediaMetadataResult([], null, [])
                    {
                        Tags = [new MediaFileTagDto { Name = "artist", Value = "Some Artist" }],
                    },
                    "1024:638000000000000000",
                    CancellationToken.None);
            }

            // Act
            var requeued = await repository.RecreateTagsAsync(
                MediaFileScope.ForFile(stored[0].PublicId),
                CancellationToken.None);

            // Assert
            requeued.Should().Be(1, "because one file was named");

            var target = await repository.GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            target!.MetadataStamp.Should().BeNull(
                "because tags can only be obtained by probing the file, so it has to be read again");

            var other = await repository.GetByPublicIdAsync(stored[1].PublicId, CancellationToken.None);
            other!.MetadataStamp.Should().NotBeNull("because the other file was out of scope");

            var otherTags = await repository.GetTagsAsync(stored[1].PublicId, CancellationToken.None);
            otherTags.Should().ContainSingle("because a scoped rebuild must not delete anyone else's tags");
        }

    }
}
