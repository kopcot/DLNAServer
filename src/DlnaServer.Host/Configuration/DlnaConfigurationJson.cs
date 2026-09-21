using System.Text.Json;
using System.Text.Json.Serialization;

namespace DlnaServer.Host.Configuration
{
    /// <summary>
    /// The one set of JSON options everything that reads or writes <c>config.json</c> uses.
    /// </summary>
    /// <remarks>
    /// Both halves were wrong in different directions, and having two copies is what let them differ.
    /// <para>
    /// <b>Reading</b> was stricter than the thing it protects. <c>ConfigurationFileGuard</c> parsed with
    /// the defaults, which reject comments and a trailing comma - but
    /// <c>JsonConfigurationFileParser</c>, the parser the configuration system actually uses, allows
    /// both. So a file the server would have read quite happily was condemned as "not valid JSON",
    /// moved into <c>backup/</c> and replaced with defaults, which is the same lost-settings outcome as
    /// a failed reload and reached by an operator doing something entirely reasonable.
    /// </para>
    /// <para>
    /// <b>Writing</b> disagreed with itself: the guard's options carried a
    /// <see cref="JsonStringEnumConverter"/> and the admin UI's did not, so a recovered file wrote
    /// <c>"VideoMkv"</c> and the next save through the Settings page wrote <c>27</c> into the same
    /// file. The binder accepts both, so nothing broke - it just made the file progressively less
    /// legible to the person expected to hand-edit it.
    /// </para>
    /// </remarks>
    internal static class DlnaConfigurationJson
    {
        /// <summary>
        /// Matches what <c>JsonConfigurationFileParser</c> accepts, so nothing rejects a file the
        /// configuration system would read.
        /// </summary>
        public static JsonDocumentOptions ReadOptions { get; } = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Indented and enum-named, because this file is edited by hand over SSH.
        /// </summary>
        public static JsonSerializerOptions WriteOptions { get; } = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new JsonStringEnumConverter() },
        };
    }
}
