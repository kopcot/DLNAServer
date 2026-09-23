using DlnaServer.Core.Delivery;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Diagnostics;
using DlnaServer.Host.Diagnostics;
using DlnaServer.Core.Contracts;
using DlnaServer.Core.Contracts.Processing;
using DlnaServer.Core.Dlna;
using DlnaServer.Core.Hosting;
using DlnaServer.Host.Configuration;
using DlnaServer.Host.Indexing;
using DlnaServer.Media;
using DlnaServer.Persistence;
using DlnaServer.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Exercises indexing against a real temporary folder and a real SQLite database.
    /// </summary>
    [TestFixture]
    internal sealed class LibraryIndexerTest
    {
        private string _mediaRoot = null!;
        private string _databasePath = null!;
        private ServiceProvider _provider = null!;

        [SetUp]
        public async Task SetUp()
        {
            _mediaRoot = Directory.CreateTempSubdirectory("dlna-index-").FullName;
            _databasePath = Path.Combine(Path.GetTempPath(), $"dlna-index-{Guid.NewGuid():N}.sqlite");

            var services = new ServiceCollection();

            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();

            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(
                new StaticOptionsMonitor<DlnaOptions>(CreateOptions()));

            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            Directory.Delete(_mediaRoot, recursive: true);

            foreach (var file in Directory.EnumerateFiles(
                Path.GetDirectoryName(_databasePath)!,
                Path.GetFileName(_databasePath) + "*"))
            {
                File.Delete(file);
            }
        }

        [Test]
        public async Task IndexAsync_AddsFilesAndBuildsTheDirectoryTree()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "action", "film.mkv"), sizeInBytes: 100);
            CreateFile("photo.jpg", sizeInBytes: 50);

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesAdded.Should().Be(2, "because both files are new to the index");
            result.TotalDirectories.Should().Be(3,
                "because the source root plus 'movies' and 'movies/action' are all browsable containers");

            var directories = await Directories().GetSourceRootsAsync(CancellationToken.None);
            directories.Should().ContainSingle("because exactly one source folder is configured");
        }

        /// <summary>
        /// The live server produced 1390 unique-constraint failures a day by re-inserting directories
        /// it had already indexed.
        /// </summary>
        /// <summary>
        /// The dashboard's Library panel adds the kinds up and shows the sum as the total, so the two have
        /// to come from one query rather than from two that can disagree.
        /// </summary>
        [Test]
        public async Task CountByKindAsync_SplitsTheLibraryAndSumsToTheTotal()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("movies", "second.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("music", "song.mp3"), sizeInBytes: 60);
            CreateFile("photo.jpg", sizeInBytes: 50);
            _ = await IndexAsync();

            // Act
            var counts = await Files().CountByKindAsync(CancellationToken.None);

            // Assert
            counts.Video.Should().Be(2, "because both .mkv files map to the video kind through their MIME");
            counts.Audio.Should().Be(1, "because the .mp3 maps to the audio kind");
            counts.Image.Should().Be(1, "because the .jpg maps to the image kind");
            counts.Total.Should().Be(counts.Video + counts.Audio + counts.Image + counts.Other,
                "because the panel shows the total as the sum of the parts, so a file counted in no kind "
                + "would make the tiles contradict each other");
        }

        [Test]
        public async Task IndexAsync_RunTwice_AddsNothingTheSecondTime()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = await IndexAsync();

            // Act
            var second = await IndexAsync();

            // Assert
            second.FilesAdded.Should().Be(0, "because nothing on disk changed between the two passes");
            second.TotalFiles.Should().Be(1, "because the existing row was kept, not duplicated");
            second.TotalDirectories.Should().Be(2, "because directories are resolved before being inserted");
        }

        [Test]
        public async Task IndexAsync_LinksEverySubdirectoryToItsParent()
        {
            // Arrange
            CreateFile(Path.Combine("a", "b", "c", "film.mkv"), sizeInBytes: 100);

            // Act
            _ = await IndexAsync();

            // Assert
            var root = (await Directories().GetSourceRootsAsync(CancellationToken.None)).Single();
            var children = await Directories().GetChildrenAsync(
                root.PublicId,
                cancellationToken: CancellationToken.None);

            children.Should().ContainSingle(d => d.Name == "a",
                "because directories are inserted shallowest first so a child can resolve its parent");
        }

        [Test]
        public async Task IndexAsync_WhenAFileIsDeleted_RemovesItFromTheIndex()
        {
            // Arrange
            var path = CreateFile("film.mkv", sizeInBytes: 100);
            CreateFile("keep.mkv", sizeInBytes: 100);
            _ = await IndexAsync();

            File.Delete(path);

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesRemoved.Should().Be(1, "because a file that no longer exists must leave the index");
            result.TotalFiles.Should().Be(1, "because the remaining file is untouched");
        }

        /// <summary>
        /// A deleted file takes its preview image with it.
        /// </summary>
        /// <remarks>
        /// <b>Reported by a customer.</b> Previews live beside the media in a folder scanning is
        /// configured to skip, so nothing ever looks at one again: the row cascaded away and the image
        /// stayed on the disc for good. The move path already cleaned up after itself; the delete path
        /// did not.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenAFileIsDeleted_DeletesThePreviewImageBesideIt()
        {
            // Arrange
            var doomed = CreateFile("film.mkv", sizeInBytes: 100);
            CreateFile("keep.mkv", sizeInBytes: 100);
            _ = await IndexAsync();

            var doomedPreview = await CreatePreviewForAsync(doomed);
            var keptPreview = await CreatePreviewForAsync(Path.Combine(_mediaRoot, "keep.mkv"));

            File.Delete(doomed);

            // Act
            _ = await IndexAsync();

            // Assert
            File.Exists(doomedPreview).Should().BeFalse(
                "because the media it was made from is gone, and nothing else would ever find it again");
            File.Exists(keptPreview).Should().BeTrue(
                "because the file it belongs to is still indexed");
        }

        /// <remarks>
        /// The preview is housekeeping, and housekeeping must never cost the pass. A locked or
        /// read-only image would otherwise abort reconciliation with rows already deleted.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenThePreviewImageCannotBeDeleted_StillRemovesTheRow()
        {
            // Arrange
            var doomed = CreateFile("film.mkv", sizeInBytes: 100);
            _ = await IndexAsync();

            var preview = await CreatePreviewForAsync(doomed);

            File.Delete(doomed);

            // Two mechanisms, because they are genuinely different operating systems. A held handle blocks
            // File.Delete on Windows; POSIX unlink ignores open handles entirely, so on Linux - the
            // production target, and where the container runs the suite - only a directory the process
            // cannot write to makes the delete fail. Without this the catch branch had no Linux coverage
            // and the test passed for the wrong reason.
            var previewFolder = Path.GetDirectoryName(preview)!;
            FileStream? held = null;

            if (OperatingSystem.IsWindows())
            {
                held = new FileStream(preview, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                File.SetUnixFileMode(
                    previewFolder,
                    UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            try
            {
                // Act
                var result = await IndexAsync();

                // Assert
                result.FilesRemoved.Should().Be(1,
                    "because a preview that cannot be deleted is a tidiness problem, not a reason to keep the row");
                File.Exists(preview).Should().BeTrue(
                    "because the delete must have failed for the catch branch to be what this test covers");
            }
            finally
            {
                held?.Dispose();

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        previewFolder,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
        }

        /// <remarks>
        /// <b>Reported by a customer.</b> Moving <c>Music\Suno_com\Burn It Up.mp3</c> into an
        /// <c>Eng</c> sub-folder produced a second row rather than following the file. Identity is the
        /// full path, so the arrival was an insert and the departure a delete, and everything hanging off
        /// the row - its identifier, and therefore its DLNA ObjectID and every renderer bookmark, plus its
        /// extracted metadata and the date it entered the library - went with the deleted half.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenAFileMoves_KeepsTheSameRowRatherThanAddingAnother()
        {
            // Arrange
            var original = CreateFile(Path.Combine("music", "Burn It Up.mp3"), sizeInBytes: 100);
            _ = await IndexAsync();

            var before = await Files().GetByPathAsync(original, CancellationToken.None);

            var moved = Path.Combine(_mediaRoot, "music", "eng", "Burn It Up.mp3");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
            File.Move(original, moved);

            // Act
            var result = await IndexAsync();

            // Assert
            result.TotalFiles.Should().Be(1,
                "because a move is one file at a new path, not a new file plus a departed one");

            var after = await Files().GetByPathAsync(moved, CancellationToken.None);

            after.Should().NotBeNull("because the row must follow the file to its new path");
            after!.PublicId.Should().Be(before!.PublicId,
                "because the identifier is the DLNA ObjectID and the admin URL - a move must not break either");
            after.CreatedUtc.Should().Be(before.CreatedUtc,
                "because the file entered the library when it was first indexed, not when it was moved");

            var atOldPath = await Files().GetByPathAsync(original, CancellationToken.None);
            atOldPath.Should().BeNull("because nothing is left behind at the path the file came from");
        }

        [Test]
        public async Task IndexAsync_WhenAFileIsRenamedInPlace_KeepsTheSameRow()
        {
            // Arrange
            var original = CreateFile(Path.Combine("music", "track01.mp3"), sizeInBytes: 100);
            _ = await IndexAsync();

            var before = await Files().GetByPathAsync(original, CancellationToken.None);

            var renamed = Path.Combine(_mediaRoot, "music", "Burn It Up.mp3");
            File.Move(original, renamed);

            // Act
            var result = await IndexAsync();

            // Assert
            result.TotalFiles.Should().Be(1, "because renaming a file does not add one");

            var after = await Files().GetByPathAsync(renamed, CancellationToken.None);

            after.Should().NotBeNull("because the row follows the new name");
            after!.PublicId.Should().Be(before!.PublicId,
                "because a rename inside one folder is the same file and must keep its identifier");
            after.FileName.Should().Be("Burn It Up.mp3", "because the indexed name follows the file");
            after.Title.Should().Be("Burn It Up", "because the title is derived from the new name");
        }

        /// <remarks>
        /// The guard on the pairing above. A content stamp is size and modification time, not a hash, so
        /// two byte-identical files share one - and without requiring the destination to be a path this
        /// pass actually inserted, deleting one copy renames the deleted row onto the surviving copy
        /// and takes that untouched file's own row away with it.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenOneOfTwoIdenticalFilesIsDeleted_LeavesTheOtherRowAlone()
        {
            // Arrange
            var first = CreateFile(Path.Combine("music", "copy one.mp3"), sizeInBytes: 100);
            var second = CreateFile(Path.Combine("music", "copy two.mp3"), sizeInBytes: 100);
            SetDates(first, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            SetDates(second, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
            _ = await IndexAsync();

            var survivorBefore = await Files().GetByPathAsync(second, CancellationToken.None);

            File.Delete(first);

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesRemoved.Should().Be(1,
                "because the deleted copy is a deletion, however exactly its stamp matches the survivor");

            var survivorAfter = await Files().GetByPathAsync(second, CancellationToken.None);

            survivorAfter.Should().NotBeNull("because the surviving file was never touched");
            survivorAfter!.PublicId.Should().Be(survivorBefore!.PublicId,
                "because a file nobody moved must keep its identifier");
        }

        [Test]
        public async Task IndexAsync_WhenAFileChanges_ReStampsItForReprocessing()
        {
            // Arrange
            var path = CreateFile("film.mkv", sizeInBytes: 100);
            _ = await IndexAsync();

            var before = await Files().GetByPathAsync(path, CancellationToken.None);

            File.WriteAllBytes(path, new byte[500]);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesUpdated.Should().Be(1, "because the file's size and timestamp both moved");

            var after = await Files().GetByPathAsync(path, CancellationToken.None);
            after!.ContentStamp.Should().NotBe(before!.ContentStamp,
                "because the stamp is derived from size and modification time");
            after.SizeInBytes.Should().Be(500, "because the indexed size follows the file");
            after.MetadataStamp.Should().BeNull(
                "because clearing the stamp is what re-queues the file for metadata and thumbnail work");
        }

        [Test]
        public async Task IndexAsync_WhenADirectoryIsDeleted_RemovesItAndItsFiles()
        {
            // Arrange
            CreateFile(Path.Combine("gone", "film.mkv"), sizeInBytes: 100);
            CreateFile("keep.mkv", sizeInBytes: 100);
            _ = await IndexAsync();

            Directory.Delete(Path.Combine(_mediaRoot, "gone"), recursive: true);

            // Act
            var result = await IndexAsync();

            // Assert
            result.DirectoriesRemoved.Should().Be(1, "because the folder no longer exists");
            result.TotalFiles.Should().Be(1,
                "because deleting a directory takes its files with it by cascade");
        }

        [Test]
        public async Task IndexAsync_IgnoresExcludedFolders()
        {
            // Arrange
            CreateFile(Path.Combine("@Recycle", "deleted.mkv"), sizeInBytes: 100);
            CreateFile("keep.mkv", sizeInBytes: 100);

            // Act
            var result = await IndexAsync();

            // Assert
            result.TotalFiles.Should().Be(1, "because files under an excluded folder are not media");
        }

        /// <summary>
        /// Configuring a folder and one of its subfolders is an easy mistake, and it makes every file
        /// under the subfolder appear twice in one pass - which fails the unique index on the path.
        /// </summary>
        [Test]
        public async Task IndexAsync_WithOverlappingSourceFolders_IndexesEachFileOnce()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);

            var options = CreateOptions();
            options.Library.SourceFolders = [_mediaRoot, Path.Combine(_mediaRoot, "movies"), _mediaRoot];

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(new StaticOptionsMonitor<DlnaOptions>(options));

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Act
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            result.TotalFiles.Should().Be(1,
                "because a nested source folder must not cause the same file to be indexed twice");
        }

        /// <summary>
        /// Narrowing the source folders to two subfolders of the old one must move the library down to
        /// them, and must not take the files with the old root when it goes.
        /// </summary>
        /// <remarks>
        /// The regression this exists for: both new roots were already indexed as children, so nothing on
        /// the insert path reconsidered them, and the old parent still existed on disc so nothing removed
        /// it. The library kept showing the old folder and a restart made no difference.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenSourceFoldersNarrowToSubfolders_MovesTheRootsDownAndKeepsTheirFiles()
        {
            // Arrange
            CreateFile(Path.Combine("Anime", "episode.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("Screenshots", "shot.jpg"), sizeInBytes: 50);
            CreateFile(Path.Combine("Private", "secret.mkv"), sizeInBytes: 70);

            var options = CreateOptions();
            var monitor = new StaticOptionsMonitor<DlnaOptions>(options);

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(monitor);

            await using var provider = services.BuildServiceProvider();

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);
            }

            // Act - the operator narrows the library to two of its subfolders.
            options.Library.SourceFolders =
            [
                Path.Combine(_mediaRoot, "Anime"),
                Path.Combine(_mediaRoot, "Screenshots"),
            ];

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
            var roots = await directories.GetSourceRootsAsync(CancellationToken.None);

            roots.Select(static d => d.Name).Should().BeEquivalentTo(["Anime", "Screenshots"],
                "because the configured folders are the library's top level, whether or not they were "
                + "already indexed as children of the folder they replaced");

            roots.Should().OnlyContain(d => d.ParentDirectoryPublicId == null,
                "because a source root has nothing above it, and leaving the old parent attached is what "
                + "would let a cascade delete take these folders with it");

            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var remaining = await files.SearchAsync(new MediaFileSearchRequest(), CancellationToken.None);

            remaining.Select(static f => f.FileName).Should().BeEquivalentTo(["episode.mkv", "shot.jpg"],
                "because the files under the new roots survive and the ones outside them stop being served");
        }

        /// <summary>
        /// The nesting rule has to hold at any depth, not just for an immediate child.
        /// </summary>
        [Test]
        public async Task IndexAsync_WithADeeplyNestedSourceFolder_KeepsOnlyTheOutermostAsTheRoot()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "2024", "film.mkv"), sizeInBytes: 100);

            var options = CreateOptions();
            options.Library.SourceFolders = [_mediaRoot, Path.Combine(_mediaRoot, "movies", "2024")];

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(new StaticOptionsMonitor<DlnaOptions>(options));

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Act
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            result.TotalFiles.Should().Be(1,
                "because a source folder nested two levels down must not index its files a second time");

            var roots = await scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>()
                .GetSourceRootsAsync(CancellationToken.None);

            roots.Should().ContainSingle("because the outer folder already covers the inner one")
                .Which.FullPath.Should().Be(_mediaRoot,
                    "because the library starts from the outermost configured folder");
        }

        /// <summary>
        /// A folder that has stopped holding media is removed, and the same set decides what is inserted,
        /// so it cannot come back on the next pass and be removed again.
        /// </summary>
        [Test]
        public async Task IndexAsync_WhenAFoldersLastFileIsDeleted_RemovesTheFolderAndStaysStable()
        {
            // Arrange
            var film = CreateFile(Path.Combine("movies", "action", "film.mkv"), sizeInBytes: 100);
            CreateFile("photo.jpg", sizeInBytes: 50);
            _ = await IndexAsync();

            File.Delete(film);

            // Act
            var afterDelete = await IndexAsync();
            var afterSecondPass = await IndexAsync();

            // Assert
            afterDelete.FilesRemoved.Should().Be(1, "because the film is no longer on disc");
            afterDelete.TotalDirectories.Should().Be(1,
                "because 'movies' and 'movies/action' now lead to nothing playable and only the source "
                + "root survives");
            afterSecondPass.DirectoriesRemoved.Should().Be(0,
                "because a folder pruned on one pass must not be re-inserted by the next - insertion and "
                + "removal read the same discovered set, which is what makes the pass idempotent");
        }

        /// <summary>
        /// Hiding a folder retires it without discarding its rows, so reconciliation must leave an
        /// excluded subtree alone even though the scanner never enumerates it.
        /// </summary>
        /// <remarks>
        /// Without the exemption, adding a name to <c>ExcludeFolders</c> would delete that subtree's
        /// metadata and thumbnails, and un-hiding it later would cost a full re-read of every file.
        /// </remarks>
        [Test]
        public async Task IndexAsync_LeavesAnExcludedFoldersRowsInPlace()
        {
            // Arrange - indexed while it is still included, then hidden.
            CreateFile(Path.Combine("Personal", "episode.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);

            var options = CreateOptions();
            var monitor = new MutableOptionsMonitor<DlnaOptions>(options);

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(monitor);

            await using var provider = services.BuildServiceProvider();

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);
            }

            // Act
            options.Library.ExcludeFolders = ["@Recycle", "Personal"];

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
            var hidden = await directories.GetByPathAsync(
                Path.Combine(_mediaRoot, "Personal"),
                CancellationToken.None);

            hidden.Should().NotBeNull(
                "because exclusion hides a folder on read and is explicitly not a delete - the rows have "
                + "to survive for un-hiding to be free");
        }

        /// <summary>
        /// Hiding the only folder in a branch that holds media leaves its parent holding nothing
        /// discovered - and pruning that parent would cascade into the folder that was merely hidden.
        /// </summary>
        /// <remarks>
        /// Found by running the server rather than by reading: excluding <c>Private</c> left
        /// <c>Films/Private</c> exempt and correctly kept, then <c>Films</c> was pruned as leading to no
        /// media and took the exempt folder and its file with it - so the exemption held for one pass and
        /// then undid itself. Pruning leaves only is what closes that: the exempt folder is never removed,
        /// so its parent never becomes a leaf.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenOnlyAnExcludedChildHoldsMedia_KeepsBothItAndItsParent()
        {
            // Arrange
            CreateFile(Path.Combine("Films", "Private", "hidden.mkv"), sizeInBytes: 100);
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);

            var options = CreateOptions();
            var monitor = new MutableOptionsMonitor<DlnaOptions>(options);

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(monitor);

            await using var provider = services.BuildServiceProvider();

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);
            }

            // Act - the operator retires the one folder that branch's media sits in.
            options.Library.ExcludeFolders = ["@Recycle", "Private"];

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();

            var excluded = await directories.GetByPathAsync(
                Path.Combine(_mediaRoot, "Films", "Private"),
                CancellationToken.None);
            excluded.Should().NotBeNull(
                "because hiding a folder is explicitly not a delete");

            var parent = await directories.GetByPathAsync(
                Path.Combine(_mediaRoot, "Films"),
                CancellationToken.None);
            parent.Should().NotBeNull(
                "because deleting the parent cascades into the child, so the exemption only holds if the "
                + "parent survives too - it is hidden from listings instead, by having no visible media");

            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var hidden = await files.GetByPathAsync(
                Path.Combine(_mediaRoot, "Films", "Private", "hidden.mkv"),
                CancellationToken.None);
            hidden.Should().NotBeNull(
                "because its metadata and thumbnail are what un-hiding the folder is supposed to get back "
                + "for free");

            // And the point of keeping them: neither is listed.
            var visible = await directories.GetChildrenAsync(
                (await directories.GetByPathAsync(_mediaRoot, CancellationToken.None))!.PublicId,
                CancellationToken.None);
            visible.Select(static d => d.Name).Should().Equal(["movies"],
                "because Films now leads to nothing a renderer would be shown");
        }

        /// <summary>
        /// Deletion must not inherit the read-time exclusion: a file removed from disc leaves the index
        /// even when the folder it sat in is hidden, or the row is stranded for good.
        /// </summary>
        [Test]
        public async Task IndexAsync_WhenAHiddenFolderesFileIsDeleted_StillRemovesItsRow()
        {
            // Arrange - @Recycle is excluded by CreateOptions, so the row can only get there by having
            // been indexed before the exclusion, which is what the direct insert stands in for.
            var files = Files();
            var stored = await files.AddRangeAsync(
                files:
                [
                    new MediaFileCreateDto
                    {
                        FullPath = Path.Combine(_mediaRoot, "@Recycle", "gone.mkv"),
                        FileName = "gone.mkv",
                        Title = "gone",
                        Extension = ".mkv",
                        Mime = DlnaMime.VideoXMatroska,
                        UpnpClass = DlnaItemClass.VideoItem,
                        SizeInBytes = 100,
                        FileCreatedUtc = DateTime.UtcNow,
                        FileModifiedUtc = DateTime.UtcNow,
                        ContentStamp = "100:1",
                    },
                ],
                cancellationToken: CancellationToken.None);
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesRemoved.Should().Be(1,
                "because the file is not on disc, and hiding its folder must not stop the row going with "
                + "it - filtering the indexer's own reads would strand it permanently");

            var gone = await Files().GetByPublicIdAsync(stored[0].PublicId, CancellationToken.None);
            gone.Should().BeNull("because the row was deleted, not merely hidden");
        }

        private async Task<LibraryIndexResult> IndexAsync()
        {
            using var scope = _provider.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);
        }

        /// <summary>
        /// One pass against a different configuration, over the same database and media root.
        /// </summary>
        /// <remarks>
        /// Its own provider rather than a mutable monitor on the fixture's, matching what the other
        /// configuration-varying tests in this file already do - the fixture's monitor is static and
        /// several tests depend on that.
        /// </remarks>
        private Task<LibraryIndexResult> IndexAsync(DlnaOptions options)
        {
            return IndexAsync(options, new SourceFolderChecker());
        }

        /// <summary>
        /// One pass against a substituted source-folder checker, over the fixture's own configuration.
        /// </summary>
        private Task<LibraryIndexResult> IndexWithCheckerAsync(ISourceFolderChecker checker)
        {
            return IndexAsync(CreateOptions(), checker);
        }

        /// <remarks>
        /// The provider both overloads above are built on. The checker is a parameter because the
        /// unusable-folder guard is only reachable through one that reports a folder unusable, and
        /// <see cref="SourceFolderChecker"/> is stateless, so an instance passed in and one the container
        /// creates are indistinguishable.
        /// </remarks>
        private async Task<LibraryIndexResult> IndexAsync(DlnaOptions options, ISourceFolderChecker checker)
        {
            var services = new ServiceCollection();

            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton(checker);
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(
                new StaticOptionsMonitor<DlnaOptions>(options));

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);
        }

        private IMediaDirectoryRepository Directories()
        {
            return _provider.CreateScope().ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();
        }

        private IMediaFileRepository Files()
        {
            return _provider.CreateScope().ServiceProvider.GetRequiredService<IMediaFileRepository>();
        }

        /// <summary>
        /// An unreachable source folder must not empty the index.
        /// </summary>
        /// <remarks>
        /// Both reconcile passes delete every indexed row whose file is not on disc, cascading through
        /// streams and thumbnails. A NAS volume that is spinning up, an unmounted share or a mistyped
        /// path fails every one of those checks at once, so without the guard one transient condition
        /// destroys the whole library index and forces a full re-ffprobe.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenASourceFolderIsUnreachable_RemovesNothing()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = await IndexAsync();

            var options = CreateOptions();
            options.Library.SourceFolders = [Path.Combine(_mediaRoot, "does-not-exist")];

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(new StaticOptionsMonitor<DlnaOptions>(options));

            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            // Act
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            result.FilesRemoved.Should().Be(0,
                "because reconciliation must not run while a source folder cannot be read");
            result.DirectoriesRemoved.Should().Be(0,
                "because reconciliation must not run while a source folder cannot be read");
            result.TotalFiles.Should().Be(1,
                "because the already-indexed file must survive a pass over an unreachable folder");
        }

        /// <summary>
        /// The case the gate above missed for as long as it existed. A QNAP volume that fails to mount
        /// leaves its mountpoint behind as an existing, readable, EMPTY directory - so the checker found
        /// nothing wrong, both reconcile passes ran, and every indexed row was deleted. The sibling test
        /// covers a path that does not exist, which is the one shape the original check did catch.
        /// </summary>
        [Test]
        public async Task IndexAsync_WhenASourceFolderIsPresentButEmpty_RemovesNothing()
        {
            // Arrange
            CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = await IndexAsync();

            // The mountpoint is still there and still readable; only its contents are gone.
            Directory.Delete(Path.Combine(_mediaRoot, "movies"), recursive: true);

            // Act
            var result = await IndexAsync();

            // Assert
            result.FilesRemoved.Should().Be(0,
                "because a source folder that is readable but empty is what an unmounted share looks "
                + "like, and reconciling against it deletes the whole index");
            result.DirectoriesRemoved.Should().Be(0,
                "because the same gate has to stop the directory pass, which cascades into files");
            result.TotalFiles.Should().Be(1,
                "because the indexed row must survive until the share is genuinely readable again");
        }

        /// <summary>
        /// The first fill of an empty index takes each row's indexed date from the filesystem.
        /// </summary>
        /// <remarks>
        /// <b>Reported by a customer</b> after recreating the database. Recently added orders by the
        /// row's own indexing time, so a bulk fill gave all 25,504 rows one timestamp and the list
        /// degenerated into insertion order. Asserted through <c>GetRecentlyAddedAsync</c> as well as the
        /// column, because the ordering is the thing that was actually broken.
        /// </remarks>
        [Test]
        public async Task IndexAsync_OnAFirstFill_TakesTheIndexedDateFromTheFilesystem()
        {
            // Arrange
            var older = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            var newer = new DateTime(2023, 8, 9, 10, 11, 12, DateTimeKind.Utc);

            CreateFileDated(Path.Combine("movies", "old.mkv"), older);
            CreateFileDated(Path.Combine("movies", "new.mkv"), newer);

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDatesAsync();

            byPath[Path.Combine(_mediaRoot, "movies", "old.mkv")].Should().BeCloseTo(older, TimeSpan.FromSeconds(2),
                "because on a first fill the file's own date is the only ordering information there is");
            byPath[Path.Combine(_mediaRoot, "movies", "new.mkv")].Should().BeCloseTo(newer, TimeSpan.FromSeconds(2),
                "because every row in the fill takes its own date, not one shared timestamp");

            using var scope = _provider.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var recent = await files.GetRecentlyAddedAsync(10, CancellationToken.None);

            recent.Select(static f => f.FileName).Should().Equal(["new.mkv", "old.mkv"],
                "because Recently added is the list the customer lost - newest first, by the file's date");
        }

        /// <summary>
        /// With <c>UseFileCreationDateTime</c> on, an implausible creation time falls back to the write
        /// time rather than reaching the sortable column.
        /// </summary>
        /// <remarks>
        /// The plausibility check was written for the first-fill column and never applied to this one,
        /// though this is the field <c>Browse</c>'s date sort orders by and the one the
        /// <c>(DirectoryId, FileCreatedUtc)</c> index was added for. Many Linux filesystems record no
        /// birth time and .NET then reports the epoch, so turning the setting on pinned every file to
        /// 1970 in exactly the field the sort uses - a working-looking feature that sorted by nothing.
        /// <para>
        /// The epoch is simulated by setting the creation time to it directly, because a filesystem that
        /// genuinely lacks birth time cannot be arranged on demand and Windows records one for every file.
        /// </para>
        /// </remarks>
        [Test]
        public async Task IndexAsync_WithFileCreationDatesAndNoBirthTime_FallsBackToTheWriteTime()
        {
            // Arrange
            var writeTime = new DateTime(2022, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            var path = CreateFile(Path.Combine("movies", "nobirth.mkv"), sizeInBytes: 400);

            // Creation time first: Linux has no API to set a birth time, so .NET sets the write time
            // instead, and doing it second would overwrite the write time this test falls back to.
            File.SetCreationTimeUtc(path, DateTime.UnixEpoch);
            File.SetLastWriteTimeUtc(path, writeTime);

            var options = CreateOptions();
            options.Library.UseFileCreationDateTime = true;

            // Act
            _ = await IndexAsync(options);

            // Assert
            using var scope = _provider.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var stored = await files.GetByPathAsync(path, CancellationToken.None);

            stored.Should().NotBeNull(
                "because the file is under the source folder and must have been indexed");
            stored!.FileCreatedUtc.Should().BeCloseTo(writeTime, TimeSpan.FromSeconds(2),
                "because an epoch creation time means the filesystem records none, and dating the "
                + "column Browse sorts by to 1970 sorts by nothing at all");
        }

        /// <summary>
        /// A pass over a source folder that is present, readable and empty does not consume its
        /// first-fill status.
        /// </summary>
        /// <remarks>
        /// <b>The customer's bug, reached through a third path.</b> The NAS starts the server before the
        /// volume mounts, so the first pass sees an existing, readable, empty mountpoint - the exact
        /// scenario the empty-source-folder guard was written for.
        /// <c>LibraryScanner.EnumerateDirectories</c> yields a readable root unconditionally and the
        /// directory pass runs before the unusable-folder gate, so that pass created the root's row.
        /// First-fill detection then asked whether that row existed, decided the folder had already been
        /// filled, and stamped every file with one identical date once the volume appeared - which is the
        /// destruction of <i>Recently added</i> the whole feature exists to prevent.
        /// <para>
        /// The fix is to ask the FILES, so this test is what pins it: no file rows means no fill has
        /// happened, whatever the folder's own row says.
        /// </para>
        /// </remarks>
        [Test]
        public async Task IndexAsync_AfterAPassOverAnEmptySourceFolder_StillTreatsItAsAFirstFill()
        {
            // Arrange - a pass over the mountpoint before anything is on it.
            _ = await IndexAsync();

            var fileDate = new DateTime(2022, 2, 3, 4, 5, 6, DateTimeKind.Utc);

            // The volume mounts and the media appears.
            CreateFileDated(Path.Combine("movies", "mounted.mkv"), fileDate);

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDatesAsync();

            byPath[Path.Combine(_mediaRoot, "movies", "mounted.mkv")].Should().BeCloseTo(
                fileDate,
                TimeSpan.FromSeconds(2),
                "because the earlier pass indexed no files, so this IS the folder's first fill - asking "
                + "whether the folder's own row existed is what let an unmounted volume consume it");
        }

        /// <summary>
        /// A file arriving after the fill is genuinely new, and takes the current time.
        /// </summary>
        [Test]
        public async Task IndexAsync_ForAFileArrivingAfterTheFill_TakesTheCurrentDate()
        {
            // Arrange
            CreateFileDated(Path.Combine("movies", "first.mkv"), new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            _ = await IndexAsync();

            // A file whose own date is old, but which arrives into a folder already indexed.
            CreateFileDated(Path.Combine("movies", "later.mkv"), new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDatesAsync();

            byPath[Path.Combine(_mediaRoot, "movies", "later.mkv")].Should().BeCloseTo(
                DateTime.UtcNow,
                TimeSpan.FromMinutes(5),
                "because a file appearing in a folder that is already indexed was added now, whatever "
                + "date it carries - which is what makes Recently added mean anything after the fill");
        }

        /// <summary>
        /// A source folder added later is its own first fill.
        /// </summary>
        /// <remarks>
        /// The second half of the customer's report. Without this, adding a folder dumped its entire
        /// contents into Recently added at one timestamp and pushed everything real off the list.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenASourceFolderIsAddedLater_TreatsOnlyThatFolderAsAFirstFill()
        {
            // Arrange
            CreateFileDated(Path.Combine("movies", "existing.mkv"), new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            _ = await IndexAsync();

            var secondRoot = Directory.CreateTempSubdirectory("dlna-index-2nd-").FullName;

            try
            {
                var addedDate = new DateTime(2022, 6, 7, 8, 9, 10, DateTimeKind.Utc);
                var addedPath = Path.Combine(secondRoot, "arrived.mkv");
                File.WriteAllBytes(addedPath, new byte[300]);
                SetDates(addedPath, addedDate);

                var options = CreateOptions();
                options.Library.SourceFolders = [_mediaRoot, secondRoot];

                // Act
                _ = await IndexAsync(options);

                // Assert
                var byPath = await ReadIndexedDatesAsync();

                byPath[addedPath].Should().BeCloseTo(addedDate, TimeSpan.FromSeconds(2),
                    "because a source folder indexed for the first time is a bulk import, not a stream "
                    + "of arrivals, whatever the rest of the library has already done");
            }
            finally
            {
                Directory.Delete(secondRoot, recursive: true);
            }
        }

        /// <summary>
        /// A folder already indexed as a child is not a first fill when it becomes a source folder.
        /// </summary>
        /// <remarks>
        /// The reason the test is on "does a row exist" rather than on <c>IsSourceRoot</c>: its files are
        /// already indexed, so re-dating them would rewrite history that was recorded correctly.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenAnIndexedChildIsPromotedToASourceFolder_IsNotAFirstFill()
        {
            // Arrange
            var childPath = CreateFileDated(
                Path.Combine("movies", "child.mkv"),
                new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            _ = await IndexAsync();

            var before = (await ReadIndexedDatesAsync())[childPath];

            var options = CreateOptions();
            options.Library.SourceFolders = [Path.Combine(_mediaRoot, "movies")];

            // Act
            _ = await IndexAsync(options);

            // Assert
            (await ReadIndexedDatesAsync())[childPath].Should().BeCloseTo(before, TimeSpan.FromSeconds(2),
                "because the row already existed - promotion changes where it sits, not when it arrived");
        }

        /// <summary>
        /// The first fill dates each FOLDER from the filesystem too, not only its files.
        /// </summary>
        /// <remarks>
        /// The folder half of the first-fill rule, and it was missing entirely:
        /// <c>MediaDirectoryCreateDto</c> carried no date, so every row took the one <c>nowUtc</c> that
        /// <c>DlnaDbContext.StampTimestamps</c> computes per save and a whole batch shared a single
        /// timestamp. Browse's date sort over containers reads that column, so a bulk import left folder
        /// ordering as insertion order.
        /// </remarks>
        [Test]
        public async Task IndexAsync_OnAFirstFill_TakesEachFolderDateFromTheFilesystem()
        {
            // Arrange
            var older = new DateTime(2019, 4, 5, 6, 7, 8, DateTimeKind.Utc);
            var newer = new DateTime(2022, 9, 10, 11, 12, 13, DateTimeKind.Utc);

            _ = CreateFile(Path.Combine("movies", "old", "film.mkv"), sizeInBytes: 100);
            _ = CreateFile(Path.Combine("movies", "new", "clip.mkv"), sizeInBytes: 100);
            SetDirectoryDates(Path.Combine(_mediaRoot, "movies", "old"), older);
            SetDirectoryDates(Path.Combine(_mediaRoot, "movies", "new"), newer);

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDirectoryDatesAsync();

            byPath[Path.Combine(_mediaRoot, "movies", "old")].Should().BeCloseTo(older, TimeSpan.FromSeconds(2),
                "because on a first fill a folder's own date is the only ordering information there is");
            byPath[Path.Combine(_mediaRoot, "movies", "new")].Should().BeCloseTo(newer, TimeSpan.FromSeconds(2),
                "because every folder in the fill takes its own date, not one shared timestamp");
        }

        /// <summary>
        /// A folder appearing after the fill is genuinely new and takes the current time.
        /// </summary>
        [Test]
        public async Task IndexAsync_ForAFolderArrivingAfterTheFill_TakesTheCurrentDate()
        {
            // Arrange
            _ = CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = await IndexAsync();

            _ = CreateFile(Path.Combine("arrived", "clip.mkv"), sizeInBytes: 100);
            SetDirectoryDates(
                Path.Combine(_mediaRoot, "arrived"),
                new DateTime(2001, 1, 2, 3, 4, 5, DateTimeKind.Utc));

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDirectoryDatesAsync();

            byPath[Path.Combine(_mediaRoot, "arrived")].Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5),
                "because the source folder already held indexed files, so this folder is an arrival "
                + "rather than part of a bulk import - which is what makes Recently added mean anything");
        }

        /// <summary>
        /// A folder is dated when its row is created, and never re-dated afterwards.
        /// </summary>
        /// <remarks>
        /// Back-dating the folder on disc between two passes must not move the stored date. Reading the
        /// filesystem on every pass would let a folder touched by an unrelated write jump to the top of
        /// the date sort, and would undo the fill dates on the pass after the fill.
        /// </remarks>
        [Test]
        public async Task IndexAsync_OnASecondPass_LeavesAnAlreadyIndexedFolderDateAlone()
        {
            // Arrange
            var atFill = new DateTime(2020, 6, 7, 8, 9, 10, DateTimeKind.Utc);

            _ = CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            SetDirectoryDates(Path.Combine(_mediaRoot, "movies"), atFill);
            _ = await IndexAsync();

            SetDirectoryDates(
                Path.Combine(_mediaRoot, "movies"),
                new DateTime(2024, 12, 31, 23, 0, 0, DateTimeKind.Utc));

            // Act
            _ = await IndexAsync();

            // Assert
            var byPath = await ReadIndexedDirectoryDatesAsync();

            byPath[Path.Combine(_mediaRoot, "movies")].Should().BeCloseTo(atFill, TimeSpan.FromSeconds(2),
                "because the date belongs to the row and is set once, when the folder was first indexed");
        }

        /// <summary>
        /// An unusable source folder stops reconciliation, so nothing indexed is deleted.
        /// </summary>
        /// <remarks>
        /// The M2 guard, verified live but never covered: an unmounted share is present, readable and
        /// empty, and "every file is gone" is exactly what that looks like to reconciliation. Reaching it
        /// needs no unreadable folder - <c>LibraryIndexer.FindUnusableSourceFolders</c> asks
        /// <see cref="ISourceFolderChecker"/>, so a checker reporting the folder unusable exercises the
        /// guard on Windows and on the NAS alike. <c>PLAN.md</c> recorded this as not portable; the seam
        /// is what makes it portable.
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenASourceFolderIsUnusable_DeletesNothingItHadIndexed()
        {
            // Arrange
            _ = CreateFile(Path.Combine("movies", "film.mkv"), sizeInBytes: 100);
            _ = CreateFile("photo.jpg", sizeInBytes: 50);
            _ = await IndexAsync();

            File.Delete(Path.Combine(_mediaRoot, "movies", "film.mkv"));
            File.Delete(Path.Combine(_mediaRoot, "photo.jpg"));

            // Act
            var result = await IndexWithCheckerAsync(new UnusableSourceFolderChecker());

            // Assert
            result.FilesRemoved.Should().Be(0,
                "because an unusable source folder must not be read as every file having been deleted");

            using var scope = _provider.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            (await files.CountAsync(CancellationToken.None)).Should().Be(2,
                "because the rows are the only record of the library while the folder is unavailable - "
                + "deleting them is what an unmounted share used to cost");
        }

        /// <summary>
        /// Every indexed directory's path and the date the row records as its own indexing time.
        /// </summary>
        private async Task<Dictionary<string, DateTime>> ReadIndexedDirectoryDatesAsync()
        {
            using var scope = _provider.CreateScope();
            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();

            var indexed = await directories.GetIndexedPageAsync(
                afterFullPath: null,
                take: 500,
                CancellationToken.None);
            var byPath = new Dictionary<string, DateTime>(indexed.Count, StringComparer.Ordinal);

            for (var index = 0; index < indexed.Count; index++)
            {
                var row = await directories.GetByPathAsync(indexed[index].FullPath, CancellationToken.None);

                if (row is not null)
                {
                    byPath[row.FullPath] = row.CreatedUtc;
                }
            }

            return byPath;
        }

        /// <remarks>
        /// Both timestamps, and only after the folder's contents exist: creating a file inside a folder
        /// moves that folder's write time, and creation time is not settable - or even recorded - on every
        /// filesystem, which is why <see cref="DlnaServer.Core.Files.FileSystemDate.Resolve"/> falls back
        /// to the write time.
        /// </remarks>
        private static void SetDirectoryDates(string fullPath, DateTime dateUtc)
        {
            Directory.SetCreationTimeUtc(fullPath, dateUtc);
            Directory.SetLastWriteTimeUtc(fullPath, dateUtc);
        }

        /// <summary>
        /// Every indexed file's path and the date the row records as its own indexing time.
        /// </summary>
        private async Task<Dictionary<string, DateTime>> ReadIndexedDatesAsync()
        {
            using var scope = _provider.CreateScope();
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            var indexed = await files.GetIndexedPageAsync(afterFullPath: null, take: 500, CancellationToken.None);
            var byPath = new Dictionary<string, DateTime>(indexed.Count, StringComparer.Ordinal);

            for (var index = 0; index < indexed.Count; index++)
            {
                var row = await files.GetByPathAsync(indexed[index].FullPath, CancellationToken.None);

                if (row is not null)
                {
                    byPath[row.FullPath] = row.CreatedUtc;
                }
            }

            return byPath;
        }

        /// <summary>
        /// Creates a media file and back-dates it, so "the file's date" and "now" cannot be confused.
        /// </summary>
        private string CreateFileDated(string relativePath, DateTime dateUtc)
        {
            var fullPath = CreateFile(relativePath, sizeInBytes: 400);

            SetDates(fullPath, dateUtc);

            return fullPath;
        }

        /// <remarks>
        /// Both timestamps, because creation time is not settable - or even recorded - on every
        /// filesystem. <c>LibraryScanner.ResolveFileSystemDate</c> falls back to the write time for
        /// exactly that reason, so setting both makes the assertion hold on Windows and on Linux.
        /// </remarks>
        private static void SetDates(string fullPath, DateTime dateUtc)
        {
            File.SetCreationTimeUtc(fullPath, dateUtc);
            File.SetLastWriteTimeUtc(fullPath, dateUtc);
        }

        /// <summary>
        /// A file moved into an excluded folder stops being browsable, and its row goes with it.
        /// </summary>
        /// <remarks>
        /// This is what deleting a file on a QNAP actually does: it moves it into <c>@Recycle</c>, a
        /// default exclusion living inside the source folder.
        /// <para>
        /// The row is <b>removed</b>, which is a different outcome from
        /// <see cref="IndexAsync_LeavesAnExcludedFoldersRowsInPlace"/> and worth being explicit about.
        /// There, a folder already indexed became excluded by a configuration change, and its rows stay
        /// so that un-hiding costs nothing. Here the file leaves the part of the tree that is scanned at
        /// all: the destination is never enumerated, so nothing can re-point the row at it, and
        /// reconciliation finds the old path definitely absent. Removing it is the right answer -
        /// the operator deleted the file.
        /// </para>
        /// </remarks>
        [Test]
        public async Task IndexAsync_WhenAFileIsMovedIntoAnExcludedFolder_RemovesTheRowAndHidesItFromBrowse()
        {
            // Arrange - indexed and browsable where it started.
            var visiblePath = CreateFile(Path.Combine("Films", "film.mkv"), sizeInBytes: 100);
            _ = CreateFile(Path.Combine("movies", "other.mkv"), sizeInBytes: 100);

            var monitor = new MutableOptionsMonitor<DlnaOptions>(CreateOptions());
            await using var provider = CreateProvider(monitor);

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);

                (await IsVisibleInBrowseAsync(first.ServiceProvider, visiblePath)).Should().BeTrue(
                    "because the file has to start out browsable for the move to prove anything");
            }

            // Act - the shape a QNAP delete takes.
            var recycledPath = Path.Combine(_mediaRoot, "@Recycle", "film.mkv");
            Move(visiblePath, recycledPath);

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            (await IsVisibleInBrowseAsync(scope.ServiceProvider, visiblePath)).Should().BeFalse(
                "because the operator deleted it, and it must stop appearing on the television");
            (await IsVisibleInBrowseAsync(scope.ServiceProvider, recycledPath)).Should().BeFalse(
                "because the recycle bin is not part of the library either - hiding the old path and "
                + "then showing the new one would be the same file back under a worse name");

            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            (await files.GetByPathAsync(visiblePath, CancellationToken.None)).Should().BeNull(
                "because the destination is never scanned, so nothing can re-point the row at it and "
                + "reconciliation finds the old path definitely absent");
            (await files.GetByPathAsync(recycledPath, CancellationToken.None)).Should().BeNull(
                "because an excluded folder is not indexed, so the file has no row at its new path either");
            (await files.CountAsync(CancellationToken.None)).Should().Be(1,
                "because only the untouched file in movies/ is left");
        }

        /// <summary>
        /// A file moved out of an excluded folder becomes browsable, and is added when it was never
        /// indexed.
        /// </summary>
        [Test]
        public async Task IndexAsync_WhenAnUnknownFileIsMovedOutOfAnExcludedFolder_AddsItAndShowsIt()
        {
            // Arrange - it only ever existed inside the exclusion, so nothing indexed it.
            var recycledPath = CreateFile(Path.Combine("@Recycle", "restored.mkv"), sizeInBytes: 100);
            _ = CreateFile(Path.Combine("movies", "other.mkv"), sizeInBytes: 100);

            var monitor = new MutableOptionsMonitor<DlnaOptions>(CreateOptions());
            await using var provider = CreateProvider(monitor);

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);

                var files = first.ServiceProvider.GetRequiredService<IMediaFileRepository>();

                (await files.GetByPathAsync(recycledPath, CancellationToken.None)).Should().BeNull(
                    "because scanning skips an excluded folder outright, so there is no row to update "
                    + "later - this is the case that has to INSERT rather than move a row");
            }

            // Act
            var visiblePath = Path.Combine(_mediaRoot, "Films", "restored.mkv");
            Move(recycledPath, visiblePath);

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var indexed = await scope.ServiceProvider.GetRequiredService<IMediaFileRepository>()
                .GetByPathAsync(visiblePath, CancellationToken.None);

            indexed.Should().NotBeNull("because a file that was never indexed has to be added");
            (await IsVisibleInBrowseAsync(scope.ServiceProvider, visiblePath)).Should().BeTrue(
                "because it is in the library now, and un-hiding has to be as complete as hiding was");
        }

        /// <summary>
        /// The same move, for a file the library already knew - it is updated rather than duplicated,
        /// and comes back browsable.
        /// </summary>
        [Test]
        public async Task IndexAsync_WhenAKnownFileReturnsFromAnExcludedFolder_UpdatesTheRowAndShowsIt()
        {
            // Arrange - indexed while visible, so a row already exists.
            var visiblePath = CreateFile(Path.Combine("Films", "film.mkv"), sizeInBytes: 100);
            _ = CreateFile(Path.Combine("movies", "other.mkv"), sizeInBytes: 100);

            var monitor = new MutableOptionsMonitor<DlnaOptions>(CreateOptions());
            await using var provider = CreateProvider(monitor);

            Guid originalId;

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);

                var seeded = await first.ServiceProvider.GetRequiredService<IMediaFileRepository>()
                    .GetByPathAsync(visiblePath, CancellationToken.None);

                originalId = seeded!.PublicId;
            }

            // Act - out to the recycle bin, then back again.
            var recycledPath = Path.Combine(_mediaRoot, "@Recycle", "film.mkv");
            Move(visiblePath, recycledPath);

            using (var second = provider.CreateScope())
            {
                _ = await second.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);
            }

            Move(recycledPath, visiblePath);

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();
            var restored = await files.GetByPathAsync(visiblePath, CancellationToken.None);

            restored.Should().NotBeNull("because the file is back in the library");
            (await IsVisibleInBrowseAsync(scope.ServiceProvider, visiblePath)).Should().BeTrue(
                "because a round trip through the recycle bin must leave it exactly as browsable as it "
                + "was before");

            // Two files were created and neither was deleted, so a third row would mean the return trip
            // inserted beside the existing one rather than updating it.
            (await files.CountAsync(CancellationToken.None)).Should().Be(2,
                "because the row is updated rather than a second one inserted beside it - a duplicate "
                + "would violate the unique index and show the file twice on a television");

            // A NEW identifier, and the contrast is the point. A move WITHIN the library keeps its
            // PublicId - that is the move/rename feature, and LibraryIndexerTest pins it elsewhere. A
            // trip through an excluded folder cannot: the destination is never scanned, so the row is
            // removed on the way out and there is nothing left to carry the identity back. A renderer
            // holding the old ObjectID gets a 404 until it browses again, which is the same recovery as
            // any deleted-and-restored file.
            restored!.PublicId.Should().NotBe(originalId,
                "because the row did not survive the excluded folder, so what came back is a new entry "
                + "for the same bytes rather than the original following the file");
        }

        /// <summary>
        /// Builds a provider whose exclusion list a test can change between passes.
        /// </summary>
        private ServiceProvider CreateProvider(IOptionsMonitor<DlnaOptions> monitor)
        {
            var services = new ServiceCollection();

            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton(monitor);

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Walks the library the way a renderer does - source roots, then children, then files - and
        /// reports whether the path turns up.
        /// </summary>
        /// <remarks>
        /// Deliberately not a repository lookup by path. Those are documented as unfiltered, so asking
        /// one whether something is hidden would answer a different question from the one a television
        /// asks; only the listing methods apply <c>ExcludeFolders</c>.
        /// </remarks>
        private static async Task<bool> IsVisibleInBrowseAsync(IServiceProvider scope, string fullPath)
        {
            var directories = scope.GetRequiredService<IMediaDirectoryRepository>();
            var files = scope.GetRequiredService<IMediaFileRepository>();

            var pending = new Queue<MediaDirectoryDto>(
                await directories.GetSourceRootsAsync(CancellationToken.None));

            while (pending.Count > 0)
            {
                var directory = pending.Dequeue();

                var listed = await files.GetByDirectoryPageAsync(
                    directory.PublicId,
                    skip: 0,
                    take: 1_000,
                    sortByDate: false,
                    descending: false,
                    cancellationToken: CancellationToken.None);

                if (listed.Any(file => string.Equals(file.FullPath, fullPath, StringComparison.Ordinal)))
                {
                    return true;
                }

                var children = await directories.GetChildrenPageAsync(
                    directory.PublicId,
                    skip: 0,
                    take: 1_000,
                    sortByDate: false,
                    descending: false,
                    cancellationToken: CancellationToken.None);

                foreach (var child in children)
                {
                    pending.Enqueue(child);
                }
            }

            return false;
        }

        private static void Move(string fromPath, string toPath)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);
            File.Move(fromPath, toPath);
        }

        /// <summary>
        /// The whole difference between the two kinds of hiding: a temporarily hidden folder is scanned,
        /// indexed and reconciled exactly as any other, and only the answers change.
        /// </summary>
        /// <remarks>
        /// An excluded folder is never enumerated, so nothing under it is imported and a change to it is
        /// never noticed. If that were true here as well the setting would be pointless - revealing it
        /// would show whatever the library looked like when the folder was first hidden.
        /// </remarks>
        [Test]
        public async Task IndexAsync_ForATemporarilyHiddenFolder_IndexesItAndKeepsItCurrent()
        {
            // Arrange
            _ = CreateFile(Path.Combine("Private", "first.mkv"), sizeInBytes: 100);
            var removed = CreateFile(Path.Combine("Private", "second.mkv"), sizeInBytes: 100);

            var options = CreateOptions();
            options.Library.TemporarilyHiddenFolders = ["Private"];

            var monitor = new MutableOptionsMonitor<DlnaOptions>(options);

            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton<ITemporaryFolderVisibility, TemporaryFolderVisibility>();
            _ = services.AddDlnaPersistence($"Data Source={_databasePath}");
            _ = services.AddDlnaMedia();
            _ = services.AddScoped<ILibraryIndexer, LibraryIndexer>();
            _ = services.AddSingleton<ISourceFolderChecker, SourceFolderChecker>();
            _ = services.AddSingleton<IServedFileCache, NoOpServedFileCache>();
            _ = services.AddSingleton<ILibraryIndexLock, LibraryIndexLock>();
            _ = services.AddSingleton<IOptionsMonitor<DlnaOptions>>(monitor);

            await using var provider = services.BuildServiceProvider();

            using (var first = provider.CreateScope())
            {
                _ = await first.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                    .IndexAsync(CancellationToken.None);
            }

            // Act - the library carries on changing while the folder is hidden.
            _ = CreateFile(Path.Combine("Private", "third.mkv"), sizeInBytes: 100);
            File.Delete(removed);

            using var scope = provider.CreateScope();
            _ = await scope.ServiceProvider.GetRequiredService<ILibraryIndexer>()
                .IndexAsync(CancellationToken.None);

            // Assert
            var files = scope.ServiceProvider.GetRequiredService<IMediaFileRepository>();

            var arrived = await files.GetByPathAsync(
                Path.Combine(_mediaRoot, "Private", "third.mkv"),
                CancellationToken.None);
            arrived.Should().NotBeNull(
                "because a file added to a temporarily hidden folder is still scanned and indexed - this "
                + "is exactly what an excluded folder does not do");

            var gone = await files.GetByPathAsync(
                Path.Combine(_mediaRoot, "Private", "second.mkv"),
                CancellationToken.None);
            gone.Should().BeNull(
                "because reconciliation still removes a row whose file has been deleted, so the index "
                + "does not rot behind the hiding");

            var directories = scope.ServiceProvider.GetRequiredService<IMediaDirectoryRepository>();

            var listed = await directories.GetChildrenAsync(
                (await directories.GetByPathAsync(_mediaRoot, CancellationToken.None))!.PublicId,
                CancellationToken.None);
            listed.Select(static d => d.Name).Should().NotContain("Private",
                "because everything above happens while the folder is still absent from every listing");
        }

        private DlnaOptions CreateOptions()
        {
            var options = new DlnaOptions();
            options.Library.SourceFolders = [_mediaRoot];
            // DlnaOptionsDefaults ALWAYS adds Thumbnails.SubFolderName in production, whether or not it is
            // listed, and the fixture hands options straight to StaticOptionsMonitor without it. Without
            // this line a pass indexes every preview as media, which is not the shipped arrangement.
            options.Library.ExcludeFolders = ["@Recycle", options.Thumbnails.SubFolderName];
            options.Library.MediaFileExtensions[".mkv"] = new MediaExtensionOptions { Mime = "VideoXMatroska" };
            options.Library.MediaFileExtensions[".jpg"] = new MediaExtensionOptions { Mime = "ImageJpeg" };
            options.Library.MediaFileExtensions[".mp3"] = new MediaExtensionOptions { Mime = "AudioMpeg" };

            return options;
        }

        /// <summary>
        /// Registers a preview image for an indexed file, and writes it where the repository says it is.
        /// </summary>
        private async Task<string> CreatePreviewForAsync(string mediaPath)
        {
            var previewPath = Path.Combine(
                Path.GetDirectoryName(mediaPath)!,
                ".@__thumb",
                Path.GetFileName(mediaPath) + ".jpg");

            _ = Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
            await File.WriteAllBytesAsync(previewPath, [1, 2, 3], CancellationToken.None);

            var files = Files();
            var indexed = await files.GetByPathAsync(mediaPath, CancellationToken.None);

            await files.SaveThumbnailAsync(
                indexed!.PublicId,
                new GeneratedThumbnail(
                    previewPath,
                    DlnaMime.ImageJpeg,
                    Width: 480,
                    Height: 270,
                    SizeInBytes: 3,
                    Content: null,
                    WasAdopted: false),
                indexed.ContentStamp,
                CancellationToken.None);

            return previewPath;
        }

        private string CreateFile(string relativePath, int sizeInBytes)
        {
            var fullPath = Path.Combine(_mediaRoot, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, new byte[sizeInBytes]);

            return fullPath;
        }
    }
}
