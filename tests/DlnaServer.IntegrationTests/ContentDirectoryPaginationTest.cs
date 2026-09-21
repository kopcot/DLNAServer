using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Upnp.Control;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers the one place a renderer's page index reaches <c>List&lt;T&gt;.GetRange</c>, which refuses an
    /// index past the end even for a count of zero - its precondition is <c>Count - index &lt; count</c>.
    /// Clamping only the count was not enough, so paging past the last item threw
    /// <see cref="ArgumentException"/> out of Browse while the response was already being written.
    /// </summary>
    [TestFixture]
    internal sealed class ContentDirectoryPaginationTest
    {
        /// <summary>
        /// The shape a fresh deployment reaches immediately: RecentlyAddedCount of 0 leaves the file list
        /// empty, so any starting index past the source-folder count lands here.
        /// </summary>
        [Test]
        public void Paginate_StartingPastTheEndWithNoFiles_ReturnsEmptyRatherThanThrowing()
        {
            // Arrange
            var containers = CreateContainers(3);
            var files = new List<MediaFileDto>();
            var options = CreateOptions(startingIndex: 15, requestedCount: 5);

            // Act
            var (pagedContainers, pagedFiles) = ContentDirectoryService.Paginate(containers, files, options);

            // Assert
            pagedContainers.Should().BeEmpty("because the starting index is past every container");
            pagedFiles.Should().BeEmpty("because there are no files to page into");
        }

        [Test]
        public void Paginate_StartingPastTheEndWithFewerFilesThanTheOffset_ReturnsEmptyRatherThanThrowing()
        {
            // Arrange - 3 containers + 10 files, renderer asks for items 15..19.
            var containers = CreateContainers(3);
            var files = CreateFiles(10);
            var options = CreateOptions(startingIndex: 15, requestedCount: 5);

            // Act
            var (pagedContainers, pagedFiles) = ContentDirectoryService.Paginate(containers, files, options);

            // Assert
            pagedContainers.Should().BeEmpty("because the starting index is past every container");
            pagedFiles.Should().BeEmpty(
                "because the file offset of 12 is past the 10 files, and an out-of-range index throws "
                + "even when the count is zero");
        }

        [Test]
        public void Paginate_SpanningContainersAndFiles_FillsThePageFromBoth()
        {
            // Arrange - items 2..5 across 3 containers then 10 files.
            var containers = CreateContainers(3);
            var files = CreateFiles(10);
            var options = CreateOptions(startingIndex: 2, requestedCount: 4);

            // Act
            var (pagedContainers, pagedFiles) = ContentDirectoryService.Paginate(containers, files, options);

            // Assert
            pagedContainers.Should().HaveCount(1, "because only the last container is at or past index 2");
            pagedFiles.Should().HaveCount(3, "because the rest of the page is taken from the files");
        }

        [Test]
        public void Paginate_WithinTheFilesOnly_StartsAtTheRightOffset()
        {
            // Arrange
            var containers = CreateContainers(2);
            var files = CreateFiles(5);
            var options = CreateOptions(startingIndex: 3, requestedCount: 2);

            // Act
            var (_, pagedFiles) = ContentDirectoryService.Paginate(containers, files, options);

            // Assert
            pagedFiles.Should().HaveCount(2, "because two files remain from offset 1 of five");
            pagedFiles[0].Title.Should().Be("file-1",
                "because a starting index of 3 with 2 containers begins at the second file");
        }

        private static BrowseRequest CreateOptions(int startingIndex, int requestedCount)
        {
            return BrowseRequest.Parse(
                browseFlag: "BrowseDirectChildren",
                filter: "*",
                startingIndex: startingIndex,
                requestedCount: requestedCount,
                sortCriteria: null,
                maximumCount: 100);
        }

        private static List<MediaDirectoryDto> CreateContainers(int count)
        {
            var containers = new List<MediaDirectoryDto>(count);

            for (var index = 0; index < count; index++)
            {
                containers.Add(new MediaDirectoryDto
                {
                    PublicId = Guid.NewGuid(),
                    FullPath = $"/media/folder-{index}",
                    Name = $"folder-{index}",
                    Depth = 1,
                    IsSourceRoot = true,
                    CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                });
            }

            return containers;
        }

        private static List<MediaFileDto> CreateFiles(int count)
        {
            var files = new List<MediaFileDto>(count);

            for (var index = 0; index < count; index++)
            {
                files.Add(new MediaFileDto
                {
                    PublicId = Guid.NewGuid(),
                    FullPath = $"/media/file-{index}.mkv",
                    FileName = $"file-{index}.mkv",
                    Title = $"file-{index}",
                    Extension = ".mkv",
                    Mime = DlnaMime.VideoXMatroska,
                    UpnpClass = DlnaItemClass.VideoItem,
                    SizeInBytes = 1024,
                    FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsExcludedFromCache = false,
                    ContentStamp = "1024:638000000000000000",
                });
            }

            return files;
        }
    }
}
