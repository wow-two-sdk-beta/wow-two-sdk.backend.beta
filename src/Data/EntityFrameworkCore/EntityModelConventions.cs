using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Applies SDK contract-driven model conventions — soft-delete query filter and the <see cref="IVersioned"/> concurrency token.</summary>
public static class EntityModelConventions
{
    /// <summary>Applies the SDK conventions to every entity type that implements the relevant contract. Owned types are skipped.</summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    public static ModelBuilder ApplyConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;

            var clrType = entityType.ClrType;

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
                modelBuilder.Entity(clrType).HasQueryFilter(BuildIsNotDeletedFilter(clrType));

            if (typeof(IVersioned).IsAssignableFrom(clrType))
                modelBuilder.Entity(clrType).Property(nameof(IVersioned.Version)).IsConcurrencyToken();
        }

        return modelBuilder;
    }

    private static LambdaExpression BuildIsNotDeletedFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var isDeleted = Expression.Property(
            Expression.Convert(parameter, typeof(ISoftDeletable)),
            nameof(ISoftDeletable.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }
}
