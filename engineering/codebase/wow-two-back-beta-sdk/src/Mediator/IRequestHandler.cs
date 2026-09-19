namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Defines behavior that handles a request and produces a response.</summary>
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>Handle the request. Sync-completing handlers may return a completed <see cref="ValueTask{TResult}"/>.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Defines behavior that handles a request that produces no response.</summary>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit> where TRequest : IRequest<Unit>;
