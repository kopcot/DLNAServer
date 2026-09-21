using DlnaServer.Core.Files;

namespace DlnaServer.UnitTests.Media
{
    /// <summary>
    /// Covers <c>Library.ExcludeFolders</c> matching, which is <b>one</b> rule for both halves.
    /// </summary>
    /// <remarks>
    /// This summary used to describe the opposite - that scanning matched whole segments while reading
    /// matched a substring, that the asymmetry was deliberate, and that "a test that let the two
    /// converge would remove the guarantee". All three are now false, and every method below already
    /// contradicted them: the substring rule was the customer-reported defect, and convergence is the
    /// fix rather than a loss.
    /// <para>
    /// These tests cover the in-memory matcher only. The SQL mirror is covered separately in
    /// <c>MediaFileRepositoryTest</c>, deliberately - a segment-aligned rule in C# with a substring left
    /// in SQL would pass every test here and still ship the bug, which is exactly what happened. The
    /// test that pins the two halves against each other lives there, not here.
    /// </para>
    /// </remarks>
    [TestFixture]
    internal sealed class PathExclusionTest
    {
        [Test]
        public void IsExcluded_WhenASegmentMatches_IsTrue()
        {
            // Assert
            PathExclusion.IsExcluded("/media/Films/@Recycle/gone.mkv", ["@Recycle"]).Should().BeTrue(
                "because the excluded name is a whole segment of the path");
        }

        [Test]
        public void IsExcluded_WhenOnlyPartOfASegmentMatches_IsFalse()
        {
            // Assert
            PathExclusion.IsExcluded("/media/My@RecycleShow/ep.mkv", ["@Recycle"]).Should().BeFalse(
                "because scanning matches whole segments - the reference used a substring test here and a "
                + "short exclusion could hide most of a library");
        }

        /// <summary>
        /// The customer-reported defect: the read side used to hide a folder nobody had excluded.
        /// </summary>
        /// <remarks>
        /// This test asserted the OPPOSITE, because the read side deliberately kept the reference's
        /// substring test and hid slightly more than scanning skipped. That asymmetry is what the
        /// customer hit - see <see cref="IsHidden_DoesNotHideAFolderWhoseNameMerelyStartsWithAnEntry"/>
        /// for the reported shape. Both halves now share one segment-aligned rule.
        /// </remarks>
        [Test]
        public void IsHidden_WhenOnlyPartOfASegmentMatches_IsFalse()
        {
            // Assert
            PathExclusion.IsHidden("/media/My@RecycleShow/ep.mkv", ["@Recycle"]).Should().BeFalse(
                "because an entry names a folder, and My@RecycleShow is not the folder the operator named");
        }

        /// <summary>
        /// Reported by a customer: an entry hid every folder whose name merely began with it.
        /// </summary>
        [TestCase("/share/Media/path1/x.mkv", true)]
        [TestCase("/share/Media/path1/deeper/x.mkv", true)]
        [TestCase("/share/Media/path1L/x.mkv", false)]
        [TestCase("/share/Media/path10/x.mkv", false)]
        [TestCase("/share/Media/Xpath1/x.mkv", false)]
        public void IsHidden_DoesNotHideAFolderWhoseNameMerelyStartsWithAnEntry(string fullPath, bool expected)
        {
            // Assert
            PathExclusion.IsHidden(fullPath, ["path1"]).Should().Be(expected,
                $"because 'path1' names one folder, so it must hide '{fullPath}' only when a whole "
                + "segment matches");
        }

        /// <summary>
        /// A partial path is a usable entry, and it is matched on segment boundaries like any other.
        /// </summary>
        /// <remarks>
        /// The whole point of allowing one: <c>Films/Private</c> hides that branch without hiding every
        /// other folder called <c>Private</c> elsewhere in the library. Validation used to refuse an
        /// entry containing a separator outright, which was the other half of the customer's report.
        /// </remarks>
        [TestCase("/share/Media/Films/Private/one.mkv", true)]
        [TestCase("/share/Media/Films/PrivateL/one.mkv", false)]
        [TestCase("/share/Media/Series/Private/one.mkv", false)]
        [TestCase("/share/Media/Films/sub/Private/one.mkv", false)]
        public void IsHidden_WithAPartialPath_MatchesTheWholeRunOfSegments(string fullPath, bool expected)
        {
            // Assert
            PathExclusion.IsHidden(fullPath, ["Films/Private"]).Should().Be(expected,
                $"because the entry names two consecutive folders, so '{fullPath}' matches only when both do");
        }

        /// <summary>
        /// Either separator may be used in an entry, and in the stored path.
        /// </summary>
        /// <remarks>
        /// One <c>config.json</c> is carried between the NAS and a Windows development machine, and a
        /// database indexed on one can be opened on the other - <c>StoredPath.SeparatorOf</c> exists in
        /// the repositories for the same reason. Refusing the foreign separator would fail silently:
        /// nothing would be hidden and nothing would say so.
        /// </remarks>
        [TestCase("path1/path_1", "/share/Media/path1/path_1/x.mkv")]
        [TestCase("path1\\path_1", "/share/Media/path1/path_1/x.mkv")]
        [TestCase("path1/path_1", "C:\\Media\\path1\\path_1\\x.mkv")]
        [TestCase("path1\\path_1", "C:\\Media\\path1\\path_1\\x.mkv")]
        public void IsHidden_TreatsBothSeparatorsAsEquivalent(string entry, string fullPath)
        {
            // Assert
            PathExclusion.IsHidden(fullPath, [entry]).Should().BeTrue(
                $"because '{entry}' and '{fullPath}' name the same folder whichever separator each uses");
        }

        /// <summary>
        /// An entry matching the last segment hides the folder itself, not only things under it.
        /// </summary>
        [Test]
        public void IsHidden_WhenTheEntryIsTheFinalSegment_IsTrue()
        {
            // Assert
            PathExclusion.IsHidden("/share/Media/path1", ["path1"]).Should().BeTrue(
                "because a directory row's own path ends at its name, and hiding a folder is the point");
        }

        [Test]
        public void IsHidden_WithSurroundingSeparatorsOnTheEntry_StillMatches()
        {
            // Assert
            PathExclusion.IsHidden("/share/Media/path1/x.mkv", ["/path1/"]).Should().BeTrue(
                "because an operator copying a fragment out of a path brings its separators with it");
        }

        [Test]
        public void IsHidden_IgnoresCapitals()
        {
            // Assert
            PathExclusion.IsHidden("/media/@RECYCLE/gone.mkv", ["@recycle"]).Should().BeTrue(
                "because the settings page takes free text and a capital must not defeat the exclusion");
        }

        [Test]
        public void IsHidden_WithBlankEntries_IgnoresThem()
        {
            // Assert
            PathExclusion.IsHidden("/media/Films/one.mkv", ["", "   "]).Should().BeFalse(
                "because a blank entry matches every path, which would hide the whole library");
        }

        [Test]
        public void IsHidden_WithNoEntries_IsFalse()
        {
            // Assert
            PathExclusion.IsHidden("/media/Films/one.mkv", []).Should().BeFalse(
                "because nothing is excluded when nothing is named");
        }
    }
}
