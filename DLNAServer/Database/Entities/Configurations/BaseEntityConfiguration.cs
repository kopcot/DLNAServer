using DLNAServer.Helpers.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using System.Reflection;

namespace DLNAServer.Database.Entities.Configurations
{
    public class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : BaseEntity
    {
        public void Configure(EntityTypeBuilder<TEntity> builder)
        {
            ArgumentNullException.ThrowIfNull(nameof(builder));

            _ = builder.HasKey(p => p.Id);

            // Configure Sequential Guid for Id
            _ = builder.Property(static (e) => e.Id)
                .IsRequired(true)
                .HasValueGenerator<SequentialGuidValueGenerator>()
                .ValueGeneratedOnAdd();
            _ = builder.Property(static (e) => e.CreatedInDB)
                .IsRequired(true)
                .ValueGeneratedOnAdd();
            _ = builder.Property(static (e) => e.ModifiedInDB)
                .IsRequired(false)
                .ValueGeneratedOnUpdate();

            // Configure properties of the entity
            var properties = typeof(TEntity).GetProperties();
            foreach (var property in properties)
            {
                {
                    // Check if the property has the LowercaseAttribute
                    var attribute = property.GetCustomAttribute<LowercaseAttribute>();
                    if (attribute != null &&
                        properties.Any(p => p.Name == attribute.PropertyName))
                    {
                        // If the attribute is present, apply the computed column logic to convert it to lowercase
                        _ = builder.Property(property.Name)
                            .HasComputedColumnSql($"LOWER([{attribute.PropertyName}])", stored: true)
                            .ValueGeneratedOnAddOrUpdate();
                    }
                }
            }
        }
    }
}
