using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Configuration
{
    /// <summary>
    /// One line of the subtitle-type editor, in the shapes its inputs bind to.
    /// </summary>
    /// <remarks>
    /// Public only because a Razor component's parameters are part of a generated public class, so an
    /// internal row type cannot be handed to <c>SubtitleTypeEditor</c>. Nothing outside this project uses it.
    /// </remarks>
    public sealed class SubtitleTypeRow
    {
        public string Extension { get; set; } = string.Empty;

        public DlnaMedia Kind { get; set; } = DlnaMedia.Video;
    }
}
