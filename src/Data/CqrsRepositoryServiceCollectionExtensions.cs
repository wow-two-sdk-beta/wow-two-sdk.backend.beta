using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Repositories;

namespace WoW.Two.Sdk.Backend.Beta.Data;

/// <summary>
/// Registers the CQRS repository split — <b>reads via Dapper</b> (<see cref="IReadRepository{TEntity, TId}"/>),
/// <b>writes via EF Core</b> (<see cref="IWriteRepository{TEntity, TId}"/>). Additive and non-enforcing: apps may
/// still use the unified <see cref="IRepository{TEntity, TId}"/> via <c>AddEfRepositories</c> / <c>AddDapperRepository</c>.
/// </summary>
public static class CqrsRepositoryServiceCollectionExtensions
{
    /// <summary>
    /// Wire the read/write split for a single entity: <see cref="IReadRepository{TEntity, TId}"/> → Dapper,
    /// <see cref="IWriteRepository{TEntity, TId}"/> → EF Core over <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The primary-key type.</typeparam>
    /// <typeparam name="TContext">The DbContext backing the write side.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime. Default scoped.</param>
    public static IServiceCollection AddCqrsRepository<TEntity, TId, TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TEntity : class, IKeyedEntity<TId>, IHasTableName
        where TId : notnull, IEquatable<TId>
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEfWriteRepositories<TContext>(lifetime);
        services.AddDapperReadRepository<TEntity, TId>(lifetime);
        return services;
    }
}
