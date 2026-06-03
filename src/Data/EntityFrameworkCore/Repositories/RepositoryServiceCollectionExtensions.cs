using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Repositories;

/// <summary>Registration helpers for the EF Core thin repository.</summary>
public static class RepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the open-generic <see cref="EfRepository{TEntity, TId}"/> as both
    /// <see cref="IRepository{TEntity, TId}"/> and <see cref="IReadRepository{TEntity, TId}"/>,
    /// resolving the base <see cref="DbContext"/> from <typeparamref name="TContext"/>.
    /// Resolve <c>IRepository&lt;MyEntity, Guid&gt;</c> directly for any mapped entity.
    /// </summary>
    /// <remarks>
    /// Registers a <see cref="DbContext"/> → <typeparamref name="TContext"/> forwarder so the repository's
    /// base-typed constructor binds to the app's concrete context. In a multi-context app, call this for the
    /// primary context and use <see cref="AddEfRepository{TRepository, TEntity, TId}"/> (concrete subclass) for others.
    /// </remarks>
    /// <typeparam name="TContext">The DbContext type backing the repositories.</typeparam>
    public static IServiceCollection AddEfRepositories<TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        // EfRepository takes a base DbContext — forward it to the concrete TContext.
        services.TryAdd(new ServiceDescriptor(typeof(DbContext), static sp => sp.GetRequiredService<TContext>(), lifetime));

        services.Add(new ServiceDescriptor(typeof(IRepository<,>), typeof(EfRepository<,>), lifetime));
        services.Add(new ServiceDescriptor(typeof(IReadRepository<,>), typeof(EfRepository<,>), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a concrete repository <typeparamref name="TRepository"/> (a subclass of
    /// <see cref="EfRepository{TEntity, TId}"/>) under its repository interfaces.
    /// Use when an entity needs custom query methods beyond the generic CRUD surface.
    /// </summary>
    public static IServiceCollection AddEfRepository<TRepository, TEntity, TId>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRepository : EfRepository<TEntity, TId>
        where TEntity : class, IKeyedEntity<TId>
        where TId : notnull, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(new ServiceDescriptor(typeof(TRepository), typeof(TRepository), lifetime));
        services.Add(new ServiceDescriptor(typeof(IRepository<TEntity, TId>), static sp => sp.GetRequiredService<TRepository>(), lifetime));
        services.Add(new ServiceDescriptor(typeof(IReadRepository<TEntity, TId>), static sp => sp.GetRequiredService<TRepository>(), lifetime));
        return services;
    }
}
