using DlnaServer.Core.Dlna;

namespace DlnaServer.Admin.Formatting
{
    /// <summary>
    /// Turns the raw numbers a media file carries into the forms an operator reads.
    /// </summary>
    /// <remarks>
    /// Injected rather than static. The static predecessor existed for the same reason - its own remark
    /// recorded that three copies of a duration formatter had drifted apart the moment one was corrected -
    /// and two fresh copies appeared beside it anyway, because nothing forces a caller to use a static
    /// helper. A dependency a component has to take is what stops the next copy.
    /// </remarks>
    internal interface IMediaFormatter
    {
        /// <summary>
        /// A placeholder for a value the metadata does not carry, so a table never has an empty cell.
        /// </summary>
        string Unknown { get; }

        /// <summary>
        /// A byte count in megabytes.
        /// </summary>
        string Megabytes(long bytes);

        /// <summary>
        /// A running time, as <c>h:mm:ss</c> once it reaches an hour and <c>m:ss</c> below that.
        /// </summary>
        string Duration(TimeSpan? duration);

        /// <summary>
        /// A span of time in words, to two units - <c>3 days, 4 hours</c>.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Duration"/> rather than an overload of it, because they answer
        /// different questions and read differently. A running time is a clock an operator compares
        /// against a seek bar; how long the server has been up is prose, and <c>76:41:03</c> is arithmetic
        /// nobody should have to do to learn that it restarted yesterday.
        /// </remarks>
        string Elapsed(TimeSpan elapsed);

        /// <summary>
        /// A stream bitrate in the unit that keeps it to a few digits.
        /// </summary>
        string Bitrate(long? bitsPerSecond);

        /// <summary>
        /// A number for a table cell, or the placeholder when the metadata does not carry it.
        /// </summary>
        string Number(int? value);

        /// <summary>
        /// A text value for a table cell, or the placeholder when the metadata does not carry it.
        /// </summary>
        string Text(string? value);

        /// <summary>
        /// A local time of day, as <c>HH:mm</c>.
        /// </summary>
        /// <remarks>
        /// Renders nothing for null rather than <see cref="Unknown"/>, which is what the call sites did
        /// before this method existed: one of them null-propagates inside a branch where the value cannot
        /// be null, and a placeholder there would put a stray dash into a sentence.
        /// </remarks>
        string Clock(DateTimeOffset? instant);

        /// <summary>
        /// A local calendar date, as <c>yyyy-MM-dd</c>.
        /// </summary>
        string Day(DateTime instant);

        /// <summary>
        /// What kind of thing a file is, in the words an operator would use.
        /// </summary>
        /// <remarks>
        /// <see cref="DlnaItemClass"/> is a wire vocabulary, so rendering it directly put a bare
        /// identifier such as <c>VideoItemMusicVideoClip</c> on a page meant for someone who does not
        /// read code. Several members map onto one phrase deliberately: the distinctions the protocol
        /// draws between an item and its container, or between a clip and a trailer, are not ones these
        /// pages have any use for.
        /// </remarks>
        string Kind(DlnaItemClass itemClass);
    }
}
