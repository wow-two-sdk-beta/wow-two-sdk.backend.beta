using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Registration helper.</summary>
public static partial class IdempotencyBehaviorServiceCollectionExtensions
{
    /// <summary>Register idempotency pipeline behavior with the in-memory store (single-instance).</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorDeduplicatingInterceptor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        return services.AddMediatorInterceptor(typeof(DeduplicatingInterceptor<,>));
    }

    /// <summary>
    /// Replace the in-memory repository with <see cref="SqlIdempotencyRepository"/> over the registered
    /// <c>IDbConnectionFactory</c>: acquisitions are visible to every host and stored responses survive restarts.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional table, lease and serializer settings.</param>
    public static IServiceCollection AddSqlIdempotencyRepository(this IServiceCollection services, Action<SqlIdempotencyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => TableNamePattern().IsMatch(o.TableName), "SqlIdempotencyOptions.TableName must be an identifier, optionally schema-qualified.")
                .Validate(o => o.PendingLease > TimeSpan.Zero, "SqlIdempotencyOptions.PendingLease must be positive."));
        services.TryAddSingleton(TimeProvider.System);
        services.RemoveAll<IIdempotencyRepository>();
        services.AddSingleton<SqlIdempotencyRepository>();
        services.AddSingleton<IIdempotencyRepository>(serviceProvider => serviceProvider.GetRequiredService<SqlIdempotencyRepository>());
        return services;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex TableNamePattern();
}
