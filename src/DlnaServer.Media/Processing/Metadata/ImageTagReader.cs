using DlnaServer.Core.Contracts;
using MetadataExtractor;

namespace DlnaServer.Media.Processing.Metadata
{
    /// <summary>
    /// Reads the tags a picture carries about itself - EXIF, IPTC and XMP.
    /// </summary>
    /// <remarks>
    /// Not ffprobe, which is what every other kind of file uses. ffprobe reports a JPEG as an
    /// <c>image2</c> container holding one <c>mjpeg</c> stream and walks straight past the EXIF block, so
    /// a photograph came back with no tags at all - which is what a camera actually records and the whole
    /// reason anyone opens this panel on a picture.
    /// <para>
    /// SkiaSharp is already here and cannot do it either: it exposes dimensions, colour type and the EXIF
    /// <i>orientation</i>, and nothing else of the block.
    /// </para>
    /// <para>
    /// Reading a picture therefore spawns no process at all, where the ffprobe path spawns one per file.
    /// </para>
    /// </remarks>
    internal static class ImageTagReader
    {
        /// <summary>
        /// Directories that describe the reader rather than the picture, or that hold nothing a person
        /// can read.
        /// </summary>
        /// <remarks>
        /// A maker note is a vendor-private blob rendered as an unreadable byte count, and the thumbnail
        /// directory describes the embedded preview rather than the photograph.
        /// </remarks>
        private static readonly HashSet<string> _skippedDirectories = new(StringComparer.Ordinal)
        {
            "Exif Thumbnail",
            "Makernote",
        };

        public static IReadOnlyList<MediaFileTagDto> Read(string filePath)
        {
            IReadOnlyList<MetadataExtractor.Directory> directories;

            try
            {
                directories = ImageMetadataReader.ReadMetadata(filePath);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A format the reader does not know, or a file that has gone away. Neither is worth
                // failing the file's whole metadata pass over - the picture is still indexed and
                // previewed.
                //
                // Broad on purpose, matching MediaProcessor and ImageThumbnailGenerator. The filter used
                // to name ImageProcessingException, IOException and UnauthorizedAccessException, and
                // MetadataExtractor throws neither of the first two for a malformed IFD - it can raise an
                // ordinary ArgumentException or IndexOutOfRangeException from inside a directory reader.
                // Those escaped, counted as a processing failure, and retired an otherwise perfectly
                // indexable picture through MaxFailureCount.
                return [];
            }

            var tags = new List<MediaFileTagDto>();

            foreach (var directory in directories)
            {
                if (IsSkipped(directory))
                {
                    continue;
                }

                foreach (var tag in directory.Tags)
                {
                    if (tags.Count >= ContainerTagReader.MaxTags)
                    {
                        return tags;
                    }

                    // Description, not the raw value: the reader turns an exposure into "1/120 sec" and a
                    // coordinate into a readable one, which is the point of showing these at all.
                    if (tag.Description is not { Length: > 0 } value || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    tags.Add(new MediaFileTagDto
                    {
                        // No stream to attribute a picture's tags to. The directory carries the grouping
                        // instead - "Exif IFD0", "GPS" - and it is qualified into the name because a name
                        // alone repeats across directories.
                        StreamIndex = null,
                        Name = Truncate(
                            $"{directory.Name} - {tag.Name}",
                            ContainerTagReader.MaxNameLength),
                        Value = Truncate(value.Trim(), ContainerTagReader.MaxValueLength),
                    });
                }
            }

            return tags;
        }

        private static bool IsSkipped(MetadataExtractor.Directory directory)
        {
            return _skippedDirectories.Contains(directory.Name)
                || directory.Name.EndsWith("Makernote", StringComparison.Ordinal);
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }
    }
}
