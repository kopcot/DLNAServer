using DLNAServer.Helpers.Attributes;
using DLNAServer.Types.DLNA;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(FilePhysicalFullPath), IsUnique = true)]
    [Index(propertyName: nameof(ThumbnailDataId), IsUnique = true)]
    [Index(propertyName: nameof(ThumbnailFilePhysicalFullPath), IsUnique = true)]
    [Table(nameof(DlnaDbContext.ThumbnailEntities))] // needed as in DlnaDbContext is in plural 
    public class ThumbnailEntity : BaseEntity
    {
        public string FilePhysicalFullPath { get; set; }
        public string ThumbnailFilePhysicalFullPath { get; set; }
        [Lowercase(nameof(ThumbnailFilePhysicalFullPath))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string LC_ThumbnailFilePhysicalFullPath { get; set; }
        public DlnaMime ThumbnailFileDlnaMime { get; set; }
        public string? ThumbnailFileDlnaProfileName { get; set; }
        public string ThumbnailFileExtension { get; set; }
        public long ThumbnailFileSizeInBytes { get; set; }
        public Guid? ThumbnailDataId { get; set; }
        [ForeignKey(nameof(ThumbnailDataId))]
        public ThumbnailDataEntity? ThumbnailData { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
