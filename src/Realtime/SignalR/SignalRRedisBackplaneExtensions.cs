using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>
/// Adds a Redis scale-out backplane to a SignalR server so hub messages fan out across every app
/// instance sharing the Redis instance — the standard way to run SignalR behind more than one server.
/// Chain after <see cref="SignalRConventions.AddConventionalSignalR"/>.
/// </summary>
public static class SignalRRedisBackplaneExtensions
{
    /// <summary>Registers the StackExchange.Redis SignalR backplane using a connection string.</summary>
    /// <param name="builder">The SignalR server builder to extend.</param>
    /// <param name="connectionString">The Redis connection string (StackExchange.Redis format).</param>
    /// <param name="configure">Optional backplane options (channel prefix, connection factory, …).</param>
    /// <returns>The same <see cref="ISignalRServerBuilder"/> for chaining.</returns>
    public static ISignalRServerBuilder AddRedisBackplane(this ISignalRServerBuilder builder, string connectionString, Action<RedisOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.AddStackExchangeRedis(connectionString, configure ?? (_ => { }));
    }

    /// <summary>Registers the StackExchange.Redis SignalR backplane configured entirely through options.</summary>
    /// <param name="builder">The SignalR server builder to extend.</param>
    /// <param name="configure">The backplane options (must set a configuration or connection factory).</param>
    /// <returns>The same <see cref="ISignalRServerBuilder"/> for chaining.</returns>
    public static ISignalRServerBuilder AddRedisBackplane(this ISignalRServerBuilder builder, Action<RedisOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return builder.AddStackExchangeRedis(configure);
    }
}
