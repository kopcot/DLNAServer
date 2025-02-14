using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(FilePhysicalFullPath), IsUnique = true)]
    [Table(nameof(DlnaDbContext.MediaSubtitleEntities))] // needed as in DlnaDbContext is in plural 
    public sealed class MediaSubtitleEntity : BaseEntity
    {
        public string FilePhysicalFullPath { get; set; }
        /// <summary>
        /// Subtitle language
        /// </summary>
        public string? Language { get; set; }
        /// <summary>
        /// Codec
        /// </summary>
        public string? Codec { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
