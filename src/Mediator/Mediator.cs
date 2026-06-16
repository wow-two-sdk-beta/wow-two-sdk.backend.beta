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
    public ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = _requestDispatchers.GetOrAdd(request.GetType(), BuildRequestDispatcher);
        // The dispatcher boxes the ValueTask<TResponse> once (reflection erases the static type); unbox and return.
        return (ValueTask<TResponse>)dispatcher(serviceProvider, request, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Unit> SendAsync(IRequest request, CancellationToken cancellationToken = default)
        => SendAsync<Unit>(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        var dispatcher = _publishDispatchers.GetOrAdd(notification.GetType(), BuildPublishDispatcher);
        return dispatcher(serviceProvider, notification, cancellationToken);
    }

    private delegate object RequestDispatcher(IServiceProvider sp, object request, CancellationToken ct);

    private delegate ValueTask PublishDispatcher(IServiceProvider sp, object notification, CancellationToken ct);

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

        // method returns ValueTask<TResponse>; reflection boxes it to object (the closed return type is
        // unknown to the cache). SendAsync<TResponse> unboxes it back — one box per dispatch, no Task wrap.
        return (sp, req, ct) => method.Invoke(null, [sp, req, ct])!;
    }

    private static ValueTask<TResponse> DispatchTyped<TRequest, TResponse>(IServiceProvider sp, TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToArray();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(request, ct);
        foreach (var behavior in behaviors)
        {
            var current = pipeline;
            pipeline = () => behavior.HandleAsync(request, current, ct);
        }

        // No await here — return the head of the pipeline directly so a sync-completing chain stays sync.
        // Each behavior/handler awaits its own `next()` exactly once (await-once discipline).
        return pipeline();
    }

    private static PublishDispatcher BuildPublishDispatcher(Type notificationType)
    {
        var method = typeof(Mediator)
            .GetMethod(nameof(PublishTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(notificationType);

        return (sp, n, ct) => (ValueTask)method.Invoke(null, [sp, n, ct])!;
    }

    private static async ValueTask PublishTyped<TNotification>(IServiceProvider sp, TNotification notification, CancellationToken ct)
        where TNotification : INotification
    {
        var handlers = sp.GetServices<INotificationHandler<TNotification>>();
        // Sequential by default — predictable order, simpler error handling. A throwing handler aborts the rest.
        foreach (var h in handlers)
            await h.HandleAsync(notification, ct).ConfigureAwait(false);
    }
}
