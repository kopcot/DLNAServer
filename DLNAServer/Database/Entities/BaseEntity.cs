using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DLNAServer.Database.Entities
{
    [Index(propertyName: nameof(Id), IsUnique = true)]
    public abstract class BaseEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        [Required]
        public Guid Id { get; set; }
        public DateTime CreatedInDB { get; set; } = DateTime.Now;
        public DateTime? ModifiedInDB { get; set; }
    }
}
