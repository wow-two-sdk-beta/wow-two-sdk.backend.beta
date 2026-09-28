using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
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

    /// <summary>
    /// Registers <see cref="RedisIdempotencyRepository"/> as the durable store: atomic across hosts, with in-progress keys
    /// expiring after their lease. It uses a registered <c>IConnectionMultiplexer</c>, else connects with
    /// <see cref="RedisIdempotencyOptions.ConnectionString"/>. Options come from <paramref name="configure"/>, then the host
    /// section <c>Mediator:Idempotency:Redis</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Connection, key prefix, lease and serializer settings.</param>
    public static IServiceCollection AddRedisIdempotencyRepository(this IServiceCollection services, Action<RedisIdempotencyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            RedisIdempotencyOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.PendingLease > TimeSpan.Zero, "RedisIdempotencyOptions.PendingLease must be positive.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.KeyPrefix), "RedisIdempotencyOptions.KeyPrefix must not be empty."));
        services.TryAddSingleton<IConnectionMultiplexer>(serviceProvider =>
            ConnectionMultiplexer.Connect(serviceProvider.GetRequiredService<RedisIdempotencyOptions>().ConnectionString
                ?? throw new InvalidOperationException("Redis idempotency needs RedisIdempotencyOptions.ConnectionString or a registered IConnectionMultiplexer.")));
        services.RemoveAll<IIdempotencyRepository>();
        services.AddSingleton<RedisIdempotencyRepository>();
        services.AddSingleton<IIdempotencyRepository>(serviceProvider => serviceProvider.GetRequiredService<RedisIdempotencyRepository>());
        return services;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex TableNamePattern();
}
