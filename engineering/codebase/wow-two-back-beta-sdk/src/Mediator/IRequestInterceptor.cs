namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Pipeline behavior wrapping a request handler.</summary>
public interface IRequestInterceptor<in TRequest, TResponse> where TRequest : notnull
{
    /// <summary>Invoke the next behavior or the handler. Await <paramref name="nextStep"/> exactly once.</summary>
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken);
}

/// <summary>Continuation in a pipeline — invokes the next behavior or the handler. Await exactly once.</summary>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();
