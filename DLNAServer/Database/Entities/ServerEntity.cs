using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [Table(nameof(DlnaDbContext.ServerEntities))] // needed as in DlnaDbContext is in plural
    public sealed class ServerEntity : BaseEntity
    {
        [MaxLength(128, ErrorMessage = $"Machine name cannot exceed 128 characters. Property {nameof(MachineName)}")]
        public string MachineName { get; set; }
        public DateTime LasAccess { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
