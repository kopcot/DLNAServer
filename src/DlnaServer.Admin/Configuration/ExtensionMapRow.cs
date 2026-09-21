using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Configuration
{
    /// <summary>
    /// One line of the file-type editor, in the shapes its inputs bind to.
    /// </summary>
    /// <remarks>
    /// Mutable, and holding a <see cref="DlnaMime"/> rather than its name, because a dropdown binds to
    /// the value while <see cref="Core.Configuration.MediaExtensionOptions"/> stores the member name as
    /// text. That difference is worth a type: the indexer silently skips an extension whose stored name
    /// it cannot parse, so a typo there costs a whole file type with nothing reporting it.
    /// </remarks>
    /// <remarks>
    /// Public only because a Razor component's parameters are part of a generated public class, so an
    /// internal row type cannot be handed to <c>ExtensionMapEditor</c>. Nothing outside this project
    /// uses it.
    /// </remarks>
    public sealed class ExtensionMapRow
    {
        public string Extension { get; set; } = string.Empty;

        public DlnaMime Mime { get; set; } = DlnaMime.Undefined;

        public string? ProfileName { get; set; }
    }
}
