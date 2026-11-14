using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Files
{
    /// <summary>
    /// Covers the separator facts read off a stored path rather than off the running host.
    /// </summary>
    [TestFixture]
    internal sealed class StoredPathTest
    {
        /// <summary>
        /// Both separators are separators, on every platform.
        /// </summary>
        /// <remarks>
        /// The indexer carried its own <c>IsSeparator</c> built from
        /// <see cref="System.IO.Path.DirectorySeparatorChar"/>, which on Linux excludes <c>\</c> - one
        /// name, two meanings, and the host-dependent one stopped recognising the segment boundaries of a
        /// path some Windows machine had indexed.
        /// </remarks>
        [TestCase('/', true)]
        [TestCase('\\', true)]
        [TestCase('a', false)]
        [TestCase('.', false)]
        [TestCase(':', false)]
        public void IsSeparator_AcceptsBothSeparatorsAndNothingElse(char value, bool expected)
        {
            // Assert
            StoredPath.IsSeparator(value).Should().Be(expected,
                $"because '{value}' must mean the same thing whichever operating system is asking");
        }

        [TestCase("/share/Media/film.mkv", '/')]
        [TestCase("C:\\Media\\film.mkv", '\\')]
        [TestCase("film.mkv", null)]
        public void SeparatorOf_TakesTheSeparatorFromThePathItself(string fullPath, char? expected)
        {
            // Assert
            StoredPath.SeparatorOf(fullPath).Should().Be(expected ?? Path.DirectorySeparatorChar,
                "because a stored path carries the separator of whichever machine indexed it, and only a "
                + "path with none at all leaves nothing to disagree with");
        }

        [TestCase("/share/Media", "/share/Media", true)]
        [TestCase("/share/Media/film.mkv", "/share/Media", true)]
        [TestCase("/share/Media/film.mkv", "/share/Media/", true)]
        [TestCase("/share/MediaOld/film.mkv", "/share/Media", false)]
        [TestCase("/share/path1L/film.mkv", "/share/path1", false)]
        [TestCase("/share/Media/film.mkv", "/", true)]
        [TestCase("D:\\Films\\film.mkv", "D:\\", true)]
        [TestCase("D:\\Films\\film.mkv", "D:\\Films", true)]
        [TestCase("D:\\Filmsy\\film.mkv", "D:\\Films", false)]
        [TestCase("E:\\Films\\film.mkv", "D:\\", false)]
        [TestCase("D:\\Films/film.mkv", "D:\\Films", true)]
        [TestCase("/share/media/film.mkv", "/share/Media", false)]
        public void IsUnder_MatchesTheFolderAndItsSubtreeOnSegmentBoundaries(string fullPath, string folder, bool expected)
        {
            // Act
            var isUnder = StoredPath.IsUnder(fullPath, folder);

            // Assert
            isUnder.Should().Be(expected,
                "because a folder covers itself and what lies under it, a root such as '/' or 'D:\\' covers "
                + "everything on it, and a sibling sharing a prefix or differing in case is a different folder");
        }
    }
}
