using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SqlServer;

/// <summary>Model conventions for SqlServer-specific entity contracts.</summary>
public static class SqlServerModelBuilderExtensions
{
    /// <summary>Maps the <c>rowversion</c> concurrency token for every <see cref="IRowVersioned"/> entity. Owned types are skipped.</summary>
    public static ModelBuilder ApplySqlServerConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (!typeof(IRowVersioned).IsAssignableFrom(entityType.ClrType)) continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IRowVersioned.RowVersion))
                .IsRowVersion();
        }

        return modelBuilder;
    }
}
