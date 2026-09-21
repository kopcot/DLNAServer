using System.Text.Json;
using DlnaServer.Core.Contracts;

namespace DlnaServer.Media.Processing.Metadata
{
    /// <summary>
    /// Turns ffprobe's JSON into the tags a container carries about itself and its tracks.
    /// </summary>
    /// <remarks>
    /// Separate from the typed stream mapping because it answers a different question. That mapping asks
    /// for the handful of values DLNA and browsing need, and gives each one a column; this asks the file
    /// what it happens to say about itself, which is open-ended and belongs to the operator rather than
    /// to the protocol.
    /// <para>
    /// Read out of the JSON rather than through the Xabe object model, which surfaces only the few tags
    /// it has properties for and silently drops the rest - including lyrics, album, and everything a
    /// tagging tool invented.
    /// </para>
    /// </remarks>
    internal static class ContainerTagReader
    {
        /// <summary>
        /// Longest value kept, matching the database column. Lyrics are why it is not shorter.
        /// </summary>
        public const int MaxValueLength = 8192;

        /// <summary>
        /// Longest tag name kept, matching the database column.
        /// </summary>
        public const int MaxNameLength = 128;

        /// <summary>
        /// Most tags one file may contribute.
        /// </summary>
        /// <remarks>
        /// A ceiling on a strange or hostile container, not a limit anyone should reach - a heavily
        /// tagged music file carries perhaps thirty. Without it, one such file could put tens of
        /// thousands of rows into the database during a pass nobody is watching.
        /// </remarks>
        public const int MaxTags = 256;

        public static IReadOnlyList<MediaFileTagDto> Parse(string? probeJson)
        {
            if (string.IsNullOrWhiteSpace(probeJson))
            {
                return [];
            }

            using var document = JsonDocument.Parse(probeJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var tags = new List<MediaFileTagDto>();

            if (root.TryGetProperty("format", out var format))
            {
                AddTagsFrom(format, streamIndex: null, tags);
            }

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    AddTagsFrom(stream, ResolveStreamIndex(stream), tags);
                }
            }

            return tags;
        }

        private static int? ResolveStreamIndex(JsonElement stream)
        {
            return stream.ValueKind == JsonValueKind.Object
                && stream.TryGetProperty("index", out var index)
                && index.TryGetInt32(out var value)
                    ? value
                    : null;
        }

        private static void AddTagsFrom(JsonElement owner, int? streamIndex, List<MediaFileTagDto> tags)
        {
            if (owner.ValueKind != JsonValueKind.Object
                || !owner.TryGetProperty("tags", out var block)
                || block.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var tag in block.EnumerateObject())
            {
                if (tags.Count >= MaxTags)
                {
                    return;
                }

                var value = ReadValue(tag.Value);

                if (string.IsNullOrWhiteSpace(tag.Name) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                tags.Add(new MediaFileTagDto
                {
                    StreamIndex = streamIndex,
                    Name = Truncate(tag.Name.Trim(), MaxNameLength),
                    Value = Truncate(value.Trim(), MaxValueLength),
                });
            }
        }

        /// <remarks>
        /// ffprobe writes tag values as JSON strings, but nothing stops a container carrying one that
        /// serialises as a number or a boolean, and <c>GetString</c> throws on those - which would cost
        /// the whole file its metadata over a single odd tag.
        /// </remarks>
        private static string? ReadValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                _ => null,
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }
    }
}
