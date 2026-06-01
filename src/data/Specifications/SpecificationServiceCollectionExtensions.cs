using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Specifications;

/// <summary>
/// Registration helpers for Ardalis.Specification's generic repository against a concrete DbContext.
/// </summary>
public static class SpecificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRepositoryBase{T}"/> and <see cref="IReadRepositoryBase{T}"/>
    /// backed by the SDK-provided <see cref="DbContextRepository{TContext, T}"/> against the
    /// given DbContext type.
    /// </summary>
    public static IServiceCollection AddSpecificationRepository<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(typeof(IRepositoryBase<>), typeof(DbContextRepository<,>).MakeGenericType(typeof(TContext), typeof(object)));
        services.AddScoped(typeof(IReadRepositoryBase<>), typeof(DbContextRepository<,>).MakeGenericType(typeof(TContext), typeof(object)));

        return services;
    }
}

/// <summary>
/// Generic repository adapter that resolves the consumer's <typeparamref name="TContext"/>
/// from DI and delegates to Ardalis.Specification's <see cref="RepositoryBase{T}"/>.
/// </summary>
public sealed class DbContextRepository<TContext, T> : RepositoryBase<T>
    where TContext : DbContext
    where T : class
{
    /// <summary>Initializes a new instance.</summary>
    public DbContextRepository(TContext context) : base(context)
    {
    }
}
