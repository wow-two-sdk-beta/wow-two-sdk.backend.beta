using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>
/// Registration helpers for the <see cref="IDbConnectionFactory"/> connection seam.
/// Web-free — depends only on <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>, so a
/// <c>dotnet tool</c> CLI can wire a factory without pulling in the ASP.NET Core shared framework.
/// </summary>
public static class ConnectionFactoryServiceCollectionExtensions
{
    /// <summary>
    /// Registers a custom <see cref="IDbConnectionFactory"/> implementation.
    /// </summary>
    public static IServiceCollection AddDbConnectionFactory<TFactory>(this IServiceCollection services)
        where TFactory : class, IDbConnectionFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDbConnectionFactory, TFactory>();
        return services;
    }

    /// <summary>Registers <see cref="DataSourceConnectionFactory"/> as the <see cref="IDbConnectionFactory"/>, backed by a registered <see cref="System.Data.Common.DbDataSource"/>.</summary>
    public static IServiceCollection AddDataSourceConnectionFactory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDbConnectionFactory, DataSourceConnectionFactory>();
        return services;
    }
}
