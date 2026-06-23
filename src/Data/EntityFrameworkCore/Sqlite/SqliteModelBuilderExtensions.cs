using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;

/// <summary>Sqlite-only model conventions that keep a Sqlite-backed model behaviourally aligned with Postgres.</summary>
public static class SqliteModelBuilderExtensions
{
    /// <summary>Stores every <see cref="DateTimeOffset"/> (and <see cref="Nullable{DateTimeOffset}"/>) property as a binary <see cref="long"/> via <see cref="DateTimeOffsetToBinaryConverter"/>, so range reads and <c>ORDER BY</c> match Postgres; Sqlite has no native <see cref="DateTimeOffset"/>, while Npgsql maps it natively — apply only when the provider is Sqlite (typically test hosts).</summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    public static ModelBuilder ApplyDateTimeOffsetToBinaryConversion(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var converter = new DateTimeOffsetToBinaryConverter();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(converter);
            }
        }

        return modelBuilder;
    }
}
