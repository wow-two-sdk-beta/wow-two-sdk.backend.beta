namespace WoW.Two.Sdk.Backend.Beta.Mediator;

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
