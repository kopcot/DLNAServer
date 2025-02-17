using DLNAServer.Helpers.Attributes;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(FilePhysicalFullPath), IsUnique = true)]
    [Index(propertyName: nameof(ThumbnailFilePhysicalFullPath), IsUnique = true)]
    [Table(nameof(DlnaDbContext.ThumbnailDataEntities))] // needed as in DlnaDbContext is in plural
    public class ThumbnailDataEntity : BaseEntity
    {
        [MaxLength(4096, ErrorMessage = $"File full path cannot exceed 4096 characters. Property {nameof(FilePhysicalFullPath)}")]
        public string FilePhysicalFullPath { get; set; }
        [Lowercase(nameof(FilePhysicalFullPath))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [MaxLength(4096, ErrorMessage = $"File full path cannot exceed 4096 characters. Property {nameof(LC_FilePhysicalFullPath)}")]
        public string LC_FilePhysicalFullPath { get; set; }
        [MaxLength(4096, ErrorMessage = $"Thumbnail file full path cannot exceed 4096 characters. Property {nameof(ThumbnailFilePhysicalFullPath)}")]
        public string ThumbnailFilePhysicalFullPath { get; set; }
        [Lowercase(nameof(ThumbnailFilePhysicalFullPath))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [MaxLength(4096, ErrorMessage = $"Thumbnail file full path cannot exceed 4096 characters. Property {nameof(LC_ThumbnailFilePhysicalFullPath)}")]
        public string LC_ThumbnailFilePhysicalFullPath { get; set; }
        public byte[]? ThumbnailData { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
