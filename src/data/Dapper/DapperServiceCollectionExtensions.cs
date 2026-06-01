using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>
/// Registration helpers for Dapper conventions.
/// </summary>
public static class DapperServiceCollectionExtensions
{
    private static int _conventionsApplied;

    /// <summary>
    /// Applies the SDK's Dapper conventions:
    /// <list type="bullet">
    ///   <item>snake_case column → PascalCase property mapping (<c>image_urls</c> → <c>ImageUrls</c>)</item>
    ///   <item><see cref="DateOnlyTypeHandler"/> for <c>DATE</c> ↔ <see cref="DateOnly"/></item>
    ///   <item><see cref="ListTypeHandler{T}"/> for <c>TEXT[]</c> ↔ <see cref="List{T}"/> of string</item>
    /// </list>
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public static IServiceCollection AddDapperConventions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (System.Threading.Interlocked.Exchange(ref _conventionsApplied, 1) == 0)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            SqlMapper.AddTypeHandler(new ListTypeHandler<string>());
        }

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IDbConnectionFactory"/> implementation.
    /// </summary>
    public static IServiceCollection AddDbConnectionFactory<TFactory>(this IServiceCollection services)
        where TFactory : class, IDbConnectionFactory
    {
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
