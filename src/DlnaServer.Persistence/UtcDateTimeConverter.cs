using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DlnaServer.Persistence
{
    /// <summary>
    /// Stores a <see cref="DateTime"/> as UTC and returns it with <see cref="DateTimeKind.Utc"/> set.
    /// </summary>
    /// <remarks>
    /// SQLite has no native date type, so values round-trip as <see cref="DateTimeKind.Unspecified"/>.
    /// Without this, a value read back compares unequal to the one written.
    /// </remarks>
    internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                static value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
                static value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }
    }
}
