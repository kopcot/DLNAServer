using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DlnaServer.Persistence
{
    /// <inheritdoc cref="UtcDateTimeConverter"/>
    internal sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                static value => value.HasValue
                    ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime())
                    : null,
                static value => value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                    : null)
        {
        }
    }
}
