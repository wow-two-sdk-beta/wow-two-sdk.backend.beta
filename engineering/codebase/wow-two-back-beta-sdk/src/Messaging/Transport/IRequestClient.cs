using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Sends a request and awaits the correlated response. The reply address and the conversation id are the client's
/// business, not the caller's: it stamps both, parks the call until a message carrying that conversation id comes back
/// through the ordinary consume pipeline, and gives up after a timeout.
/// </summary>
/// <remarks>
///   - the exchange rides the same <see cref="IEventBus"/> — no second connection, no second consumer, no transport-specific path
///   - respond from an ordinary <see cref="IEventHandler{TEvent}"/> through <see cref="RequestResponseExtensions"/>'s <c>RespondAsync</c>
///   - the reply is intercepted before dispatch, so the requesting process needs no handler for its own responses
/// </remarks>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The expected response contract.</typeparam>
public interface IRequestClient<in TRequest, TResponse>
    where TRequest : class, IEvent
    where TResponse : class, IEvent
{
    /// <summary>Send <paramref name="request"/> and await its correlated <typeparamref name="TResponse"/>.</summary>
    /// <param name="request">The request payload.</param>
    /// <param name="options">Per-call overrides (timeout, explicit destination, correlation, headers); null uses the configured defaults.</param>
    /// <param name="cancellationToken">Cancellation token. Cancelling abandons the request; a late response is discarded.</param>
    /// <returns>The response.</returns>
    /// <exception cref="RequestTimeoutException">No response arrived within the timeout.</exception>
    /// <exception cref="RequestFaultException">A response arrived that is not a <typeparamref name="TResponse"/>.</exception>
    ValueTask<TResponse> GetResponseAsync(TRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default);
}
