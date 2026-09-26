using DlnaServer.Core.Dlna;

namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// A media file in one folder, as <see cref="SubtitleMatcher"/> needs to see it.
    /// </summary>
    /// <typeparam name="TKey">Whatever identifies the file to the caller.</typeparam>
    public readonly record struct SubtitleMedia<TKey>(TKey Key, string FileName, DlnaMedia Kind);
}
