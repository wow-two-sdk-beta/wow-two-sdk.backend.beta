using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>Provides registration helpers for the <see cref="IDbConnectionFactory"/> connection seam.</summary>
public static class ConnectionFactoryServiceCollectionExtensions
{
    /// <summary>Registers a custom <see cref="IDbConnectionFactory"/> implementation.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddDbConnectionFactory<TFactory>(this IServiceCollection services)
        where TFactory : class, IDbConnectionFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDbConnectionFactory, TFactory>();
        return services;
    }

    /// <summary>Registers <see cref="DataSourceConnectionFactory"/> as the <see cref="IDbConnectionFactory"/>, backed by a registered <see cref="System.Data.Common.DbDataSource"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddDataSourceConnectionFactory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDbConnectionFactory, DataSourceConnectionFactory>();
        return services;
    }
}
