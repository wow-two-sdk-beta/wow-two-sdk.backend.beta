using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>Model conventions for Postgres-specific entity contracts.</summary>
public static class PostgresModelBuilderExtensions
{
    /// <summary>Maps the Postgres <c>xmin</c> system column as the concurrency token for every <see cref="IHasXmin"/> entity. Owned types are skipped.</summary>
    public static ModelBuilder ApplyNpgsqlConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (!typeof(IHasXmin).IsAssignableFrom(entityType.ClrType)) continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IHasXmin.Xmin))
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }

        return modelBuilder;
    }
}
