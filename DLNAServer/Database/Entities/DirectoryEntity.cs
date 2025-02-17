using DLNAServer.Helpers.Attributes;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(DirectoryFullPath), IsUnique = false)]
    [Index(propertyName: nameof(LC_DirectoryFullPath), IsUnique = false)]
    [Table(nameof(DlnaDbContext.DirectoryEntities))] // needed as in DlnaDbContext is in plural
    public class DirectoryEntity : BaseEntity
    {
        [MaxLength(4096, ErrorMessage = $"Directory full path cannot exceed 4096 characters. Property {nameof(DirectoryFullPath)}")]
        public string DirectoryFullPath { get; set; }
        [Lowercase(nameof(DirectoryFullPath))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [MaxLength(4096, ErrorMessage = $"Directory full path cannot exceed 4096 characters. Property {nameof(LC_DirectoryFullPath)}")]
        public string LC_DirectoryFullPath { get; set; }
        [MaxLength(4096, ErrorMessage = $"Directory name cannot exceed 4096 characters. Property {nameof(Directory)}")]
        public string Directory { get; set; }
        [Lowercase(nameof(Directory))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [MaxLength(4096, ErrorMessage = $"Directory name cannot exceed 4096 characters. Property {nameof(LC_Directory)}")]
        public string LC_Directory { get; set; }
        [ForeignKey("ParentDirectory")]
        public Guid? ParentDirectoryId { get; set; }
        public virtual DirectoryEntity? ParentDirectory { get; set; }
        public int Depth { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

}
