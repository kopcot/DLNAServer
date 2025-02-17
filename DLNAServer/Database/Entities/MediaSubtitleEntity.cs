using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(FilePhysicalFullPath), IsUnique = true)]
    [Table(nameof(DlnaDbContext.MediaSubtitleEntities))] // needed as in DlnaDbContext is in plural 
    public sealed class MediaSubtitleEntity : BaseEntity
    {
        [MaxLength(4096, ErrorMessage = $"File full path cannot exceed 4096 characters. Property {nameof(FilePhysicalFullPath)}")]
        public string FilePhysicalFullPath { get; set; }
        /// <summary>
        /// Subtitle language
        /// </summary>
        [MaxLength(128, ErrorMessage = $"Language cannot exceed 128 characters. Property {nameof(Language)}")]
        public string? Language { get; set; }
        /// <summary>
        /// Codec
        /// </summary>
        [MaxLength(128, ErrorMessage = $"Codec cannot exceed 128 characters. Property {nameof(Codec)}")]
        public string? Codec { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
