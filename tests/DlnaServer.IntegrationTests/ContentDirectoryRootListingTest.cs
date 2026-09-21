using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;
using DlnaServer.Host.Upnp.Control;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// The root listing surfaces the most recently indexed files alongside the source folders, and their
    /// order is the indexing date - newest first - whatever a renderer asks to sort by.
    /// </summary>
    /// <remarks>
    /// A customer-reported defect: the sort ran after the recently added files had been appended, so the
    /// list came back in title order and <em>Recently added</em> stopped meaning anything. The fix is the
    /// order of two statements inside <c>ComposeRoot</c>, which nothing about the type system protects -
    /// hence this fixture.
    /// </remarks>
    [TestFixture]
    internal sealed class ContentDirectoryRootListingTest
    {
        /// <summary>
        /// The regression itself: a renderer asking for a title sort must not reorder the list.
        /// </summary>
        [Test]
        public void ComposeRoot_WhenTheRendererAsksForATitleSort_KeepsRecentlyAddedInTheOrderItWasGiven()
        {
            // Arrange - titles deliberately in the reverse of alphabetical order, so a title sort is
            // visible rather than coincidental.
            var roots = CreateRoots("videos", "audio");
            var recentlyAdded = CreateRecentlyAdded("zulu", "mike", "alpha");
            var options = CreateOptions(sortCriteria: "+dc:title");

            // Act
            var (containers, files) = ContentDirectoryService.ComposeRoot(roots, recentlyAdded, options);

            // Assert
            files.Select(static f => f.Title).Should().ContainInOrder(
                ["zulu", "mike", "alpha"],
                "because recently added carries the indexing order and a title sort must not touch it");

            containers.Select(static c => c.Name).Should().ContainInOrder(
                ["audio", "videos"],
                "because the folders themselves are still sorted the way the renderer asked");
        }

        /// <summary>
        /// The same holds for a date sort, which is the other thing SortCriteria can ask for.
        /// </summary>
        /// <remarks>
        /// Worth its own case because a date sort orders files by <c>FileCreatedUtc</c> - the file's own
        /// date - while recently added is ordered by <c>CreatedUtc</c>, the indexing time. The two are
        /// different columns, so a date sort is not harmlessly equivalent here.
        /// </remarks>
        [Test]
        public void ComposeRoot_WhenTheRendererAsksForADateSort_StillKeepsRecentlyAddedInTheOrderItWasGiven()
        {
            // Arrange
            var roots = CreateRoots("videos");
            var recentlyAdded = CreateRecentlyAdded("zulu", "mike", "alpha");
            var options = CreateOptions(sortCriteria: "+dc:date");

            // Act
            var (_, files) = ContentDirectoryService.ComposeRoot(roots, recentlyAdded, options);

            // Assert
            files.Select(static f => f.Title).Should().ContainInOrder(
                ["zulu", "mike", "alpha"],
                "because the indexing order survives a date sort as well as a title sort");
        }

        /// <summary>
        /// A deployment with the list turned off must still compose a root.
        /// </summary>
        [Test]
        public void ComposeRoot_WithNothingRecentlyAdded_ReturnsTheSourceFoldersAlone()
        {
            // Arrange
            var roots = CreateRoots("videos", "audio");

            // Act
            var (containers, files) = ContentDirectoryService.ComposeRoot(
                roots,
                [],
                CreateOptions(sortCriteria: null));

            // Assert
            files.Should().BeEmpty("because RecentlyAddedCount of zero leaves nothing to append");
            containers.Should().HaveCount(2, "because the source folders are the whole of the root then");
        }

        private static BrowseRequest CreateOptions(string? sortCriteria)
        {
            return BrowseRequest.Parse(
                browseFlag: "BrowseDirectChildren",
                filter: "*",
                startingIndex: 0,
                requestedCount: 100,
                sortCriteria: sortCriteria,
                maximumCount: 100);
        }

        private static List<MediaDirectoryDto> CreateRoots(params string[] names)
        {
            var roots = new List<MediaDirectoryDto>(names.Length);

            foreach (var name in names)
            {
                roots.Add(new MediaDirectoryDto
                {
                    PublicId = Guid.NewGuid(),
                    FullPath = $"/media/{name}",
                    Name = name,
                    Depth = 1,
                    IsSourceRoot = true,
                    CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                });
            }

            return roots;
        }

        /// <remarks>
        /// Built newest-indexed first, the way the repository returns them. Both dates descend along the
        /// list, which matters: an <em>ascending</em> sort by either one therefore reverses it, so a sort
        /// that should not have run cannot reproduce the given order by coincidence. An earlier version
        /// of this fixture had the file date ascending and the date-sort case could not see the defect.
        /// </remarks>
        private static List<MediaFileDto> CreateRecentlyAdded(params string[] titles)
        {
            var files = new List<MediaFileDto>(titles.Length);

            for (var index = 0; index < titles.Length; index++)
            {
                files.Add(new MediaFileDto
                {
                    PublicId = Guid.NewGuid(),
                    FullPath = $"/media/videos/{titles[index]}.mkv",
                    FileName = $"{titles[index]}.mkv",
                    Title = titles[index],
                    Extension = ".mkv",
                    Mime = DlnaMime.VideoXMatroska,
                    UpnpClass = DlnaItemClass.VideoItem,
                    SizeInBytes = 1024,
                    FileCreatedUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-index),
                    FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-index),
                    IsExcludedFromCache = false,
                    ContentStamp = "1024:638000000000000000",
                });
            }

            return files;
        }
    }
}
