using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>Service-collection helpers repointing a host's <see cref="DbContext"/> off its production provider onto a test provider.</summary>
public static class DbContextProviderSwapExtensions
{
    /// <summary>Removes every descriptor binding <typeparamref name="TContext"/> to its provider, including the internal options-configuration, so a fresh registration wires cleanly.</summary>
    /// <param name="services">The service collection to strip.</param>
    /// <typeparam name="TContext">The context whose provider registrations are removed.</typeparam>
    public static IServiceCollection RemoveAllForDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        // IDbContextOptionsConfiguration<T> is internal — match it by open-generic name, not a type reference.
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceType = services[i].ServiceType;
            var isContext = serviceType == typeof(TContext);
            var isOptions = serviceType == typeof(DbContextOptions) || serviceType == typeof(DbContextOptions<TContext>);
            var isOptionsConfig = serviceType.IsGenericType
                && serviceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                && serviceType.GetGenericArguments() is [var arg] && arg == typeof(TContext);

            if (isContext || isOptions || isOptionsConfig)
                services.RemoveAt(i);
        }

        return services;
    }

    /// <summary>Strips <typeparamref name="TContext"/>'s provider then re-adds it via <paramref name="configure"/> — the one-call repoint a test host uses to swap onto a test provider.</summary>
    /// <param name="services">The service collection to repoint.</param>
    /// <param name="configure">Configures the replacement provider, e.g. <c>options =&gt; options.UseSqlite(connection)</c>.</param>
    /// <typeparam name="TContext">The context to repoint.</typeparam>
    public static IServiceCollection RepointDbContext<TContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.RemoveAllForDbContext<TContext>();
        services.AddDbContext<TContext>(configure);
        return services;
    }
}
