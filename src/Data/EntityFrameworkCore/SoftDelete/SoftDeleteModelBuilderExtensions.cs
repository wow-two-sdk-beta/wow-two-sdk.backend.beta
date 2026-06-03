using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete;

/// <summary>
/// Model-builder extensions that install a global query filter on every
/// <see cref="ISoftDeletable"/> entity type, excluding rows where
/// <see cref="ISoftDeletable.IsDeleted"/> is <c>true</c>.
/// </summary>
public static class SoftDeleteModelBuilderExtensions
{
    /// <summary>
    /// Applies <c>HasQueryFilter(e =&gt; !e.IsDeleted)</c> to every CLR entity type that
    /// implements <see cref="ISoftDeletable"/>. Call inside <c>OnModelCreating</c> after
    /// <c>base.OnModelCreating(modelBuilder)</c>.
    /// </summary>
    public static ModelBuilder ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(
                Expression.Convert(parameter, typeof(ISoftDeletable)),
                nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }

        return modelBuilder;
    }
}
