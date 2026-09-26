namespace DlnaServer.Core.Subtitles
{
    /// <summary>
    /// One subtitle file that belongs to one media file in the same folder.
    /// </summary>
    /// <param name="Key">The media file, as the caller identified it.</param>
    /// <param name="FileName">The subtitle file's name, without a folder.</param>
    /// <param name="Language">The language its name suggests, or null.</param>
    public readonly record struct SubtitleMatch<TKey>(TKey Key, string FileName, string? Language);
}
