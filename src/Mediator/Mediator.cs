using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Provides the default <see cref="IMediator"/> — resolves handlers and pipeline behaviors via DI, caching the dispatcher delegate per request type.</summary>
/// <param name="serviceProvider">The provider that resolves handlers and pipeline behaviors.</param>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestDispatcher> _requestDispatchers = new();
    private static readonly ConcurrentDictionary<Type, PublishDispatcher> _publishDispatchers = new();

    /// <inheritdoc />
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = _requestDispatchers.GetOrAdd(request.GetType(), BuildRequestDispatcher);
        // Dispatcher boxes the ValueTask<TResponse> once; unbox and return.
        return (ValueTask<TResponse>)dispatcher(serviceProvider, request, cancellationToken);
    }

    /// <inheritdoc />
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public ValueTask<Unit> SendAsync(IRequest request, CancellationToken cancellationToken = default)
        => SendAsync<Unit>(request, cancellationToken);

    /// <inheritdoc />
    /// <param name="notification">The notification to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
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
        // Find the IRequest<TResponse> the request implements.
        var iface = requestType
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?? throw new InvalidOperationException($"{requestType} does not implement IRequest<>.");

        var responseType = iface.GenericTypeArguments[0];

        var method = typeof(Mediator)
            .GetMethod(nameof(DispatchTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(requestType, responseType);

        // Returns ValueTask<TResponse> boxed to object; SendAsync<TResponse> unboxes it — one box per dispatch, no Task wrap.
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

        // Return the pipeline head directly (no await) so a sync-completing chain stays sync; each step awaits nextStep() once.
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
        // Sequential by default; a throwing handler aborts the rest.
        foreach (var h in handlers)
            await h.HandleAsync(notification, ct).ConfigureAwait(false);
    }
}
