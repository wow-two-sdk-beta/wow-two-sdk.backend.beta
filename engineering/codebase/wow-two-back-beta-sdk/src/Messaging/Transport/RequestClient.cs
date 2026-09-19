using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Default <see cref="IRequestClient{TRequest, TResponse}"/> over the shared <see cref="IEventBus"/>.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The expected response contract.</typeparam>
internal sealed class RequestClient<TRequest, TResponse>(
    IEventBus bus,
    PendingRequestRegistry pending,
    IReplyAddressService replyAddresses,
    TimeProvider timeProvider,
    RequestClientOptions defaults) : IRequestClient<TRequest, TResponse>
    where TRequest : class, IEvent
    where TResponse : class, IEvent
{
    public async ValueTask<Result<TResponse>> GetResponseAsync(
        TRequest request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversationId = Guid.NewGuid().ToString("N");
        var replyTo = replyAddresses.ReplyAddress;
        var timeout = options?.Timeout ?? defaults.Timeout;

        // Registered before the send — an in-memory reply can arrive before PublishAsync returns.
        var pendingRequest = pending.Register(conversationId, typeof(TResponse));
        try
        {
            await SendRequestAsync(request, conversationId, replyTo, options, cancellationToken);

            EventEnvelopeModel reply;
            try
            {
                reply = await pendingRequest.Completion.WaitAsync(timeout, timeProvider, cancellationToken);
            }
            catch (TimeoutException)
            {
                return Result<TResponse>.Fail(AppErrorFactory.OperationTimeout(
                    $"No response of type '{typeof(TResponse).Name}' arrived for request '{typeof(TRequest).Name}' " +
                    $"(conversation {conversationId}) within {timeout}."));
            }

            if (reply.Body is TResponse response)
                return Result<TResponse>.Ok(response);

            return Result<TResponse>.Fail(AppErrorFactory.SerializationFailed(
                $"Request '{typeof(TRequest).Name}' (conversation {conversationId}) was answered with " +
                $"'{reply.BodyType.Name}', which is not a '{typeof(TResponse).Name}'."));
        }
        finally
        {
            // The one cleanup point for every exit — response, timeout, cancellation, a throwing send.
            pending.Remove(conversationId);
        }
    }

    private ValueTask SendRequestAsync(TRequest request, string conversationId, string replyTo, RequestOptions? options, CancellationToken cancellationToken)
    {
        // Null destination publishes by type, a set one sends point-to-point; both carry the same reply address.
        var destination = options?.Destination;
        if (string.IsNullOrEmpty(destination))
        {
            return bus.PublishAsync(
                request,
                new PublishOptions
                {
                    CorrelationId = options?.CorrelationId,
                    ConversationId = conversationId,
                    CausationId = options?.CausationId,
                    ReplyTo = replyTo,
                    PartitionKey = options?.PartitionKey,
                    Durable = options?.Durable,
                    Headers = options?.Headers,
                },
                cancellationToken);
        }

        return bus.SendAsync(
            destination,
            request,
            new SendOptions
            {
                CorrelationId = options?.CorrelationId,
                ConversationId = conversationId,
                CausationId = options?.CausationId,
                ReplyTo = replyTo,
                PartitionKey = options?.PartitionKey,
                Durable = options?.Durable,
                Headers = options?.Headers,
            },
            cancellationToken);
    }
}
