using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>
/// Default <see cref="IMediator"/> implementation. Resolves handlers and pipeline behaviors via DI.
/// Caches the closed-generic dispatcher delegate per request type for hot-path perf.
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestDispatcher> _requestDispatchers = new();
    private static readonly ConcurrentDictionary<Type, PublishDispatcher> _publishDispatchers = new();

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = _requestDispatchers.GetOrAdd(request.GetType(), BuildRequestDispatcher);
        return (Task<TResponse>)dispatcher(serviceProvider, request, typeof(TResponse), cancellationToken);
    }

    /// <inheritdoc />
    public Task Send(IRequest request, CancellationToken cancellationToken = default)
        => Send<Unit>(request, cancellationToken);

    /// <inheritdoc />
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        var dispatcher = _publishDispatchers.GetOrAdd(notification.GetType(), BuildPublishDispatcher);
        await dispatcher(serviceProvider, notification, cancellationToken).ConfigureAwait(false);
    }

    private delegate object RequestDispatcher(IServiceProvider sp, object request, Type responseType, CancellationToken ct);

    private delegate Task PublishDispatcher(IServiceProvider sp, object notification, CancellationToken ct);

    private static RequestDispatcher BuildRequestDispatcher(Type requestType)
    {
        // Find IRequest<TResponse> the request implements.
        var iface = requestType
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?? throw new InvalidOperationException($"{requestType} does not implement IRequest<>.");

        var responseType = iface.GenericTypeArguments[0];

        var method = typeof(Mediator)
            .GetMethod(nameof(DispatchTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(requestType, responseType);

        return (sp, req, _, ct) => method.Invoke(null, [sp, req, ct])!;
    }

    private static async Task<TResponse> DispatchTyped<TRequest, TResponse>(IServiceProvider sp, TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToArray();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(request, ct);
        foreach (var behavior in behaviors)
        {
            var current = pipeline;
            pipeline = () => behavior.Handle(request, current, ct);
        }

        return await pipeline().ConfigureAwait(false);
    }

    private static PublishDispatcher BuildPublishDispatcher(Type notificationType)
    {
        var method = typeof(Mediator)
            .GetMethod(nameof(PublishTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(notificationType);

        return (sp, n, ct) => (Task)method.Invoke(null, [sp, n, ct])!;
    }

    private static async Task PublishTyped<TNotification>(IServiceProvider sp, TNotification notification, CancellationToken ct)
        where TNotification : INotification
    {
        var handlers = sp.GetServices<INotificationHandler<TNotification>>();
        // Sequential by default — predictable order, simpler error handling.
        foreach (var h in handlers)
            await h.Handle(notification, ct).ConfigureAwait(false);
    }
}
