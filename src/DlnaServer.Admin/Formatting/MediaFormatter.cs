using System.Globalization;
using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Formatting
{
    /// <inheritdoc cref="IMediaFormatter"/>
    internal sealed class MediaFormatter : IMediaFormatter
    {
        private const double BytesPerMegabyte = 1024.0 * 1024.0;

        private const long BitsPerMegabit = 1_000_000L;

        private const long BitsPerKilobit = 1_000L;

        public string Unknown => "-";

        public string Megabytes(long bytes)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / BytesPerMegabyte:0.#} MB");
        }

        /// <remarks>
        /// The default <see cref="TimeSpan.ToString()"/> renders a half-minute clip as
        /// <c>00:00:29.9600000</c>, which is what this exists to avoid.
        /// </remarks>
        public string Duration(TimeSpan? duration)
        {
            if (duration is not { } value || value <= TimeSpan.Zero)
            {
                return Unknown;
            }

            // Rounded to the nearest second first: truncating renders a 59.6-second clip as 0:59, and the
            // format string cannot round on its own.
            var rounded = TimeSpan.FromSeconds(Math.Round(value.TotalSeconds));

            // Days carried explicitly. "h" is hours-WITHIN-day, so a 25 hour recording - a long
            // audiobook, or a duration ffprobe read wrongly from a damaged container - rendered as
            // 1:00:00 and looked like one hour.
            if (rounded.TotalDays >= 1)
            {
                var minutesAndSeconds = rounded.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{(int)rounded.TotalHours}:{minutesAndSeconds}");
            }

            return rounded.TotalHours >= 1
                ? rounded.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : rounded.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }

        public string Elapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
            {
                return Unknown;
            }

            if (elapsed.TotalMinutes < 1)
            {
                return "less than a minute";
            }

            // Two units and never three: the third is noise at every scale this shows. Days and hours
            // once it has been up a day, hours and minutes below that, minutes alone below an hour.
            if (elapsed.TotalDays >= 1)
            {
                return Join(Unit(elapsed.Days, "day"), Unit(elapsed.Hours, "hour"));
            }

            return elapsed.TotalHours >= 1
                ? Join(Unit(elapsed.Hours, "hour"), Unit(elapsed.Minutes, "minute"))
                : Unit(elapsed.Minutes, "minute");
        }

        /// <remarks>
        /// The smaller unit is dropped when it is zero, so a server up for exactly two days reads
        /// <c>2 days</c> rather than <c>2 days, 0 hours</c>.
        /// </remarks>
        private static string Join(string larger, string smaller)
        {
            return smaller.StartsWith("0 ", StringComparison.Ordinal)
                ? larger
                : $"{larger}, {smaller}";
        }

        private static string Unit(int value, string name)
        {
            var plural = value == 1
                ? string.Empty
                : "s";

            return string.Create(CultureInfo.InvariantCulture, $"{value} {name}{plural}");
        }

        public string Bitrate(long? bitsPerSecond)
        {
            if (bitsPerSecond is not { } value || value <= 0)
            {
                return Unknown;
            }

            return value >= BitsPerMegabit
                ? string.Create(CultureInfo.InvariantCulture, $"{value / (double)BitsPerMegabit:0.##} Mbps")
                : string.Create(CultureInfo.InvariantCulture, $"{value / BitsPerKilobit} kbps");
        }

        public string Number(int? value)
        {
            return value?.ToString(CultureInfo.InvariantCulture) ?? Unknown;
        }

        public string Text(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? Unknown : value;
        }

        public string Clock(DateTimeOffset? instant)
        {
            return instant is not { } value
                ? string.Empty
                : value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        public string Day(DateTime instant)
        {
            return instant.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public string Kind(DlnaItemClass itemClass)
        {
            return itemClass switch
            {
                DlnaItemClass.ContainerMusicAlbum or DlnaItemClass.ContainerAlbum => "Album",
                DlnaItemClass.ContainerMovie => "Film folder",
                DlnaItemClass.ContainerVideo => "Video folder",
                DlnaItemClass.ContainerPhoto => "Photo folder",
                DlnaItemClass.Container or DlnaItemClass.ContainerStorageFolder => "Folder",

                DlnaItemClass.AudioItemMusicTrack => "Music track",
                DlnaItemClass.AudioItemPodcast => "Podcast",
                DlnaItemClass.AudioItemSoundClip => "Sound clip",
                DlnaItemClass.AudioItemSpeech => "Spoken word",
                DlnaItemClass.AudioItem => "Audio",

                DlnaItemClass.VideoItemMovie => "Film",
                DlnaItemClass.VideoItemMusicVideoClip => "Music video",
                DlnaItemClass.VideoItemTvShow or DlnaItemClass.VideoItemEpisode => "Television episode",
                DlnaItemClass.VideoItemMovieClip or DlnaItemClass.VideoItemTrailer => "Clip",
                DlnaItemClass.VideoItemAnimation => "Animation",
                DlnaItemClass.VideoItem => "Video",

                DlnaItemClass.ImageItemPhoto or DlnaItemClass.ImageItem => "Photo",
                DlnaItemClass.TextItem => "Text",
                DlnaItemClass.Generic => "File",

                // Covers Unknown and any member added to the enum later. Deliberately not a throw: a new
                // wire class must not take down a page whose only job is to name the file.
                _ => Unknown,
            };
        }
    }
}
