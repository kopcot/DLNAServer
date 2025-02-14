using DLNAServer.Helpers.Attributes;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Index(propertyName: nameof(DirectoryFullPath), IsUnique = false)]
    [Index(propertyName: nameof(LC_DirectoryFullPath), IsUnique = false)]
    [Table(nameof(DlnaDbContext.DirectoryEntities))] // needed as in DlnaDbContext is in plural
    public class DirectoryEntity : BaseEntity
    {
        public string DirectoryFullPath { get; set; }
        [Lowercase(nameof(DirectoryFullPath))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string LC_DirectoryFullPath { get; set; }
        public string Directory { get; set; }
        [Lowercase(nameof(Directory))]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string LC_Directory { get; set; }
        [ForeignKey("ParentDirectory")]
        public Guid? ParentDirectoryId { get; set; }
        public virtual DirectoryEntity? ParentDirectory { get; set; }
        public int Depth { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

}
