using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>DI registration for request/response over the event bus.</summary>
public static class RequestClientServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IRequestClient{TRequest, TResponse}"/> (open generic) and the consume filter that matches
    /// replies to pending requests. A process that never resolves a request client pays one reply-address check per
    /// message.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional defaults — response timeout, reply address.</param>
    /// <remarks>
    ///   - call after the transport registration (<c>AddInMemoryEventBus</c>, <c>AddRabbitMqEventBus</c>, …)
    ///   - call before any <c>AddConsumeInterceptor&lt;T&gt;()</c> whose filter should not see replies
    ///   - repeat calls are idempotent
    /// </remarks>
    public static IServiceCollection AddRequestClient(this IServiceCollection services, Action<RequestClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatedOptions<RequestClientOptions>(
            configure,
            builder => builder
                .Validate(options => options.Timeout == Timeout.InfiniteTimeSpan || options.Timeout > TimeSpan.Zero, "RequestClientOptions.Timeout must be positive or infinite.")
                .Validate(options => options.ReplyAddress is null || !string.IsNullOrWhiteSpace(options.ReplyAddress), "RequestClientOptions.ReplyAddress must not be empty when supplied."));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PendingRequestRegistry>();
        services.TryAddSingleton<IReplyAddressService, ReplyAddressService>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IRequestClient<,>), typeof(RequestClient<,>)));

        // TryAddEnumerable keeps a repeat call from putting a second copy of the filter in the chain.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConsumeInterceptor, ReplyingConsumeInterceptor>());
        return services;
    }

    /// <summary>Replace the reply-address provider — for a per-instance reply queue, a shared reply endpoint, or a broker-native inbox.</summary>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddReplyAddressService<TProvider>(this IServiceCollection services)
        where TProvider : class, IReplyAddressService
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IReplyAddressService, TProvider>());
        return services;
    }
}
