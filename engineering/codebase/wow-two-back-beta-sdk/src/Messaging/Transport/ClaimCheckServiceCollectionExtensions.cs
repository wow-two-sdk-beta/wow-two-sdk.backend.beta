using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>DI registration for the claim-check pattern.</summary>
public static class ClaimCheckServiceCollectionExtensions
{
    /// <summary>
    /// Hold bodies over a size threshold in blob storage and send a pointer instead, rehydrating them before dispatch,
    /// so a large payload stops being a publish that the broker rejects outright.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional threshold, retention and sweep settings; the call itself is the opt-in.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    ///   - requires an <see cref="IBlobRepository"/> registration (<c>AddLocalBlobStorage</c> or a cloud adapter)
    ///   - registration order is irrelevant; the processing pipeline always places rehydration closest to dispatch
    ///   - roll out on consumers before producers — a consumer without it hands the handler a body that was never fetched
    /// </remarks>
    public static IServiceCollection AddEventClaimCheck(this IServiceCollection services, Action<ClaimCheckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The call itself turns the feature on, before the caller's configuration runs.
        services.AddValidatedOptions<ClaimCheckOptions>(
            options =>
            {
                options.Enabled = true;
                configure?.Invoke(options);
            },
            builder => builder
                .Validate(options => options.ThresholdBytes > 0, "ClaimCheckOptions.ThresholdBytes must be positive.")
                .Validate(options => options.MaxPayloadBytes >= options.ThresholdBytes, "ClaimCheckOptions.MaxPayloadBytes must be at least ThresholdBytes, or an offloaded body cannot be read back.")
                .Validate(options => options.MaxPayloadBytes <= Array.MaxLength, "ClaimCheckOptions.MaxPayloadBytes cannot exceed the largest single array; a body is rehydrated into one buffer.")
                .Validate(options => options.Retention > TimeSpan.Zero, "ClaimCheckOptions.Retention must be positive.")
                .Validate(options => options.SweepInterval > TimeSpan.Zero, "ClaimCheckOptions.SweepInterval must be positive.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.PathPrefix), "ClaimCheckOptions.PathPrefix must name a blob path prefix."));

        // The wire body of an offloaded message IS a ClaimCheckReference, so the adapter resolves that token before any filter runs.
        GetOrAddMessageTypeRegistry(services).Register(typeof(ClaimCheckReference), ClaimCheckReference.TypeToken);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ClaimCheckPayloadRepository>();
        services.TryAddSingleton<ClaimCheckOffloader>();
        services.AddSingleton<IConsumeInterceptor, ClaimCheckRehydratingConsumeInterceptor>();
        services.AddHostedService<ClaimCheckRetentionSweeper>();
        return services;
    }

    // Looked up by singleton instance, so this call and AddRabbitMqEventBus can run in either order.
    private static MessageTypeRegistry GetOrAddMessageTypeRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(MessageTypeRegistry) && descriptor.ImplementationInstance is MessageTypeRegistry existing)
                return existing;

        var registry = new MessageTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
