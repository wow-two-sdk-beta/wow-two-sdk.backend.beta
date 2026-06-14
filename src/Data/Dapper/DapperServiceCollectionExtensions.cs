using Dapper;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

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
    /// Registers a string-backed <see cref="EnumTypeHandler{TEnum}"/> so <typeparamref name="TEnum"/>
    /// maps to/from a case-styled text column (default <see cref="CaseStyle.Snake"/>). Portable across
    /// providers — for Postgres native enum types, use Npgsql's <c>MapEnum</c> instead.
    /// </summary>
    public static IServiceCollection AddEnumTypeHandler<TEnum>(this IServiceCollection services, CaseStyle style = CaseStyle.Snake)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(services);
        SqlMapper.AddTypeHandler(new EnumTypeHandler<TEnum>(style));
        return services;
    }

    // IDbConnectionFactory registration (AddDbConnectionFactory / AddDataSourceConnectionFactory) moved to
    // ConnectionFactoryServiceCollectionExtensions in the web-free WoW.Two.Sdk.Backend.Beta.Data.Abstractions
    // project, alongside IDbConnectionFactory + DataSourceConnectionFactory. Same namespace — no consumer churn.
}
