namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Defines behavior that configures the storage spec of <typeparamref name="TEntity"/>; one class per entity, like <c>IEntityTypeConfiguration&lt;T&gt;</c>.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntitySpecConfiguration<TEntity>
    where TEntity : class
{
    /// <summary>Configures the spec.</summary>
    /// <param name="builder">The spec builder.</param>
    void Configure(EntitySpecBuilder<TEntity> builder);
}
