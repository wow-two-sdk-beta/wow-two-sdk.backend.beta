using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

/// <summary>Registers SignalR with conventional keep-alive, timeout, size, and JSON-protocol defaults.</summary>
public static class SignalRConventions
{
    /// <summary>
    /// Adds SignalR configured from <see cref="SignalRConventionOptions"/> — bounded keep-alive/timeout,
    /// a receive-size cap, and the SDK JSON wire contract (camelCase) as the payload protocol — and
    /// registers the in-memory <see cref="IUserConnectionTracker"/> for presence. Returns the
    /// <see cref="ISignalRServerBuilder"/> so callers can chain a scale-out backplane.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional overrides for the conventional defaults.</param>
    /// <returns>The SignalR server builder for further chaining (e.g. <c>AddRedisBackplane</c>).</returns>
    public static ISignalRServerBuilder AddConventionalSignalR(this IServiceCollection services, Action<SignalRConventionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new SignalRConventionOptions();
        configure?.Invoke(options);

        var builder = services.AddSignalR(hub =>
        {
            hub.KeepAliveInterval = options.KeepAliveInterval;
            hub.ClientTimeoutInterval = options.ClientTimeoutInterval;
            hub.HandshakeTimeout = options.HandshakeTimeout;
            hub.MaximumReceiveMessageSize = options.MaximumReceiveMessageSize;
            hub.EnableDetailedErrors = options.EnableDetailedErrors;
        });

        // Copy the preset — SignalR mutates PayloadSerializerOptions (adds converters) and would throw
        // if handed the frozen shared instance.
        var payloadOptions = new JsonSerializerOptions(options.JsonOptions ?? JsonOptionsPresets.Default);
        builder.AddJsonProtocol(json => json.PayloadSerializerOptions = payloadOptions);

        services.TryAddSingleton<IUserConnectionTracker, InMemoryUserConnectionTracker>();
        return builder;
    }
}
