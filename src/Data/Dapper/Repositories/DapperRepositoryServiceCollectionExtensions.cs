using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Registration helpers for the Dapper thin repository.</summary>
public static class DapperRepositoryServiceCollectionExtensions
{
    /// <summary>Registers <see cref="DapperRepository{TEntity, TId}"/> as both <see cref="IRepository{TEntity, TId}"/> and <see cref="IReadRepository{TEntity, TId}"/> for a single entity. Requires an <see cref="IDbConnectionFactory"/> to be registered.</summary>
    /// <remarks>Applies <see cref="DapperServiceCollectionExtensions.AddDapperConventions"/> (idempotent) so the snake_case-column → PascalCase-property mapping the repo's <c>SELECT *</c> relies on is guaranteed — without it, multi-word columns (e.g. <c>price_usd</c>) silently bind as default values.</remarks>
    /// <typeparam name="TEntity">The entity type the repository serves.</typeparam>
    /// <typeparam name="TId">The primary-key type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="lifetime">The service lifetime for the registered repository. Default scoped.</param>
    public static IServiceCollection AddDapperRepository<TEntity, TId>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IKeyedEntity<TId>, IHasTableName
        where TId : notnull, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDapperConventions();
        services.Add(new ServiceDescriptor(typeof(IRepository<TEntity, TId>), typeof(DapperRepository<TEntity, TId>), lifetime));
        services.Add(new ServiceDescriptor(typeof(IReadRepository<TEntity, TId>), typeof(DapperRepository<TEntity, TId>), lifetime));
        return services;
    }

    /// <summary>Registers a concrete repository <typeparamref name="TRepository"/> (a subclass of <see cref="DapperRepository{TEntity, TId}"/>) under its repository interfaces. Use when an entity needs custom queries or excluded-column overrides beyond the generic surface.</summary>
    /// <typeparam name="TRepository">The concrete repository subclass to register.</typeparam>
    /// <typeparam name="TEntity">The entity type the repository serves.</typeparam>
    /// <typeparam name="TId">The primary-key type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="lifetime">The service lifetime for the registered repository. Default scoped.</param>
    public static IServiceCollection AddDapperRepository<TRepository, TEntity, TId>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRepository : DapperRepository<TEntity, TId>
        where TEntity : class, IKeyedEntity<TId>, IHasTableName
        where TId : notnull, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDapperConventions();
        services.Add(new ServiceDescriptor(typeof(TRepository), typeof(TRepository), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRepository<TEntity, TId>), static sp => sp.GetRequiredService<TRepository>(), lifetime));
        services.Add(new ServiceDescriptor(typeof(IReadRepository<TEntity, TId>), static sp => sp.GetRequiredService<TRepository>(), lifetime));
        return services;
    }
}
