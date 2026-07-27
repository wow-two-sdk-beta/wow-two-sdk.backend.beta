namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Marker for a request that produces a response of type <typeparamref name="TResponse"/>.</summary>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface IRequest<TResponse> : IBaseRequest;

/// <summary>Marker for a request that produces no response (returns <see cref="Unit"/>).</summary>
public interface IRequest : IRequest<Unit>;

/// <summary>Common base marker for all requests.</summary>
public interface IBaseRequest;

/// <summary>Marker for a notification (fan-out, no response).</summary>
public interface INotification;

/// <summary>Handles a request and produces a response.</summary>
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>Handle the request. Sync-completing handlers may return a completed <see cref="ValueTask{TResult}"/>.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Handles a request that produces no response.</summary>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit> where TRequest : IRequest<Unit>;

/// <summary>Handles a notification — invoked once per registered handler.</summary>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>Handle the notification.</summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>Pipeline behavior wrapping a request handler.</summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    /// <summary>Invoke the next behavior or the handler. Await <paramref name="nextStep"/> exactly once.</summary>
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken);
}

/// <summary>Continuation in a pipeline — invokes the next behavior or the handler. Await exactly once.</summary>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Sender — fire a request and await its response.</summary>
public interface ISender
{
    /// <summary>Send a strongly-typed request.</summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>Send a request with no response.</summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<Unit> SendAsync(IRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Publisher — fire a notification to all registered handlers.</summary>
public interface IPublisher
{
    /// <summary>Publish a notification.</summary>
    /// <param name="notification">The notification to dispatch.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}

/// <summary>Combined sender and publisher.</summary>
public interface IMediator : ISender, IPublisher;

/// <summary>Void-equivalent type for `IRequest` (no response). Mirrors MediatR's `Unit`.</summary>
public readonly record struct Unit
{
    /// <summary>The single value.</summary>
    public static readonly Unit Value;

    /// <summary>Completed task wrapping <see cref="Value"/>.</summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>Completed <see cref="ValueTask{TResult}"/> wrapping <see cref="Value"/>.</summary>
    public static ValueTask<Unit> ValueTask => new(Value);
}
