using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Responder-side helpers — reply to the request a handler is processing.</summary>
public static class RequestResponseExtensions
{
    /// <summary>
    /// Reply to the message being handled. The response goes to the request's <see cref="EventContext{TEvent}.ReplyTo"/>
    /// and inherits its conversation id, which is what pairs it with the waiting requester; the reply address itself is
    /// deliberately not inherited, so the response is one-way.
    /// </summary>
    /// <typeparam name="TRequest">The request contract being handled.</typeparam>
    /// <typeparam name="TResponse">The response contract.</typeparam>
    /// <param name="context">The handler's context.</param>
    /// <param name="response">The response payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The message carries no reply address — it was published one-way, not requested.</exception>
    public static ValueTask RespondAsync<TRequest, TResponse>(this EventContext<TRequest> context, TResponse response, CancellationToken cancellationToken = default)
        where TRequest : class, IEvent
        where TResponse : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(response);

        if (context.ReplyTo is not { Length: > 0 } replyTo)
        {
            throw new InvalidOperationException(
                $"Message '{context.MessageId}' of type '{typeof(TRequest).Name}' carries no ReplyTo, so there is nowhere to respond. It was published one-way rather than sent by an IRequestClient; use TryRespondAsync for a handler that serves both.");
        }

        return context.SendAsync(replyTo, response, cancellationToken);
    }

    /// <summary>
    /// Reply if the message asked for one. For a handler that serves both a request and a plain published event: returns
    /// false and does nothing when there is no reply address, instead of throwing.
    /// </summary>
    /// <typeparam name="TRequest">The request contract being handled.</typeparam>
    /// <typeparam name="TResponse">The response contract.</typeparam>
    /// <param name="context">The handler's context.</param>
    /// <param name="response">The response payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a response was sent.</returns>
    public static async ValueTask<bool> TryRespondAsync<TRequest, TResponse>(this EventContext<TRequest> context, TResponse response, CancellationToken cancellationToken = default)
        where TRequest : class, IEvent
        where TResponse : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(response);

        if (context.ReplyTo is not { Length: > 0 } replyTo)
            return false;

        await context.SendAsync(replyTo, response, cancellationToken);
        return true;
    }
}
