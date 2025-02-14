using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(FilePhysicalFullPath), IsUnique = true)]
    [Table(nameof(DlnaDbContext.MediaVideoEntities))] // needed as in DlnaDbContext is in plural 
    public sealed class MediaVideoEntity : BaseEntity
    {
        public string FilePhysicalFullPath { get; set; }
        /// <summary>
        /// Duration
        /// </summary>
        public TimeSpan? Duration { get; set; }
        /// <summary>
        /// Width
        /// </summary>
        public int? Width { get; set; }
        /// <summary>
        /// Height
        /// </summary>
        public int? Height { get; set; }
        /// <summary>
        /// Frame rate
        /// </summary>
        public double? Framerate { get; set; }
        /// <summary>
        /// Screen ratio
        /// </summary>
        public string? Ratio { get; set; }
        /// <summary>
        /// Video bitrate
        /// </summary>
        public long? Bitrate { get; set; }
        /// <summary>
        /// Pixel Format
        /// </summary>
        public string? PixelFormat { get; set; }
        /// <summary>
        /// Rotation angle
        /// </summary>
        public int? Rotation { get; set; }
        /// <summary>
        /// Video codec
        /// </summary>
        public string? Codec { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
