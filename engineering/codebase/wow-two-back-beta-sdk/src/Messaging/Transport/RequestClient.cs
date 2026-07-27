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
/// <para>
/// The exchange rides the same <see cref="IEventBus"/> as everything else — a request is an ordinary published (or
/// sent) event that additionally carries <see cref="EventEnvelope.ReplyTo"/> and
/// <see cref="EventEnvelope.ConversationId"/>, and the response is an ordinary event sent to that reply address. So
/// request/response needs no second connection, no second consumer and no transport-specific code path; what it does
/// need is both fields on the wire, which every adapter now maps.
/// </para>
/// <para>
/// The responder is an ordinary <see cref="IEventHandler{TEvent}"/> that calls
/// <see cref="RequestResponseExtensions"/>'s <c>RespondAsync</c>. On the requesting side the reply is
/// intercepted before dispatch, so a process does not need a handler for its own responses.
/// </para>
/// <para>
/// One pending request occupies one entry in a process-wide table, released in a <c>finally</c> — a response that never
/// arrives costs the timeout and nothing after it.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The expected response contract.</typeparam>
/// <example>
/// <code>
/// // requester
/// var quote = await requestClient.GetResponseAsync(new PriceRequested(orderId), cancellationToken: ct);
///
/// // responder
/// public ValueTask HandleAsync(EventContext&lt;PriceRequested&gt; context, CancellationToken ct)
///     =&gt; context.RespondAsync(new PriceQuoted(context.Event.OrderId, 42m), ct);
/// </code>
/// </example>
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

/// <summary>Process-wide defaults for <see cref="IRequestClient{TRequest, TResponse}"/>.</summary>
public sealed class RequestClientOptions
{
    /// <summary>How long a request waits for its response before <see cref="RequestTimeoutException"/>. Default 30s. <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> waits forever (bounded only by the caller's token).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Address responses are sent to. Null (default) derives it from <see cref="ITopologyProvider"/> — this process's
    /// own consume endpoint, which the topology already binds by name, so a response addressed to it lands here without
    /// declaring anything new. Set it to point replies at a queue of your own.
    /// </summary>
    public string? ReplyAddress { get; set; }
}

/// <summary>Per-call overrides for one <see cref="IRequestClient{TRequest, TResponse}"/> request.</summary>
public sealed class RequestOptions
{
    /// <summary>Response timeout for this call; null uses <see cref="RequestClientOptions.Timeout"/>.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Deliver the request point-to-point to this endpoint instead of publishing it by type. Null (default) publishes,
    /// so the request is routed to whichever endpoint binds the request type.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>Correlation id for the wider business flow. The conversation id — which pairs this reply with this request — is always the client's own.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Id of the event that caused this request.</summary>
    public string? CausationId { get; set; }

    /// <summary>Partition / ordering key (transport-abstract).</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Persist the request so it survives a broker restart (transport-abstract).</summary>
    public bool? Durable { get; set; }

    /// <summary>Custom headers to attach to the request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}

/// <summary>No response arrived for a request within its timeout. The pending request has already been released — the exchange is over, not merely late.</summary>
public sealed class RequestTimeoutException : Exception
{
    /// <summary>Create the exception.</summary>
    public RequestTimeoutException()
        : base("The request timed out before a response arrived.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    public RequestTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying timeout.</param>
    public RequestTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A response arrived on the request's conversation, but it is not the expected response contract.</summary>
public sealed class RequestFaultException : Exception
{
    /// <summary>Create the exception.</summary>
    public RequestFaultException()
        : base("The response did not match the expected response contract.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    public RequestFaultException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The underlying fault.</param>
    public RequestFaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Supplies the address this process wants its responses sent to. Replace it to route replies somewhere other than the
/// process's own consume endpoint — a per-instance queue, a shared reply endpoint, a broker-native inbox.
/// </summary>
public interface IReplyAddressProvider
{
    /// <summary>The address a responder should send its reply to. Read once per request, so an implementation may vary it.</summary>
    string ReplyAddress { get; }
}

/// <summary>
/// Default <see cref="IReplyAddressProvider"/> — the configured address, else this process's own consume endpoint from
/// <see cref="ITopologyProvider"/>, else a fixed fallback.
/// </summary>
/// <remarks>
/// <para>
/// The consume endpoint is the right default because the topology already binds every endpoint queue under its own name
/// (<see cref="TopologyOptions.BindEndpointNameKeys"/>), so a reply addressed to it routes back with no extra
/// declaration, no temporary queue and nothing to clean up.
/// </para>
/// <para>
/// It is a <em>shared</em> address, which is the limitation to know: two instances of the same service consume one
/// queue, so a reply can be delivered to the instance that did not send the request. That instance has no matching
/// pending entry, passes the message down the pipeline, and the requester times out. Scale a request/response service
/// out only with a per-instance <see cref="RequestClientOptions.ReplyAddress"/> (bound to that instance's own queue) or
/// a replacement provider.
/// </para>
/// </remarks>
public sealed class DefaultReplyAddressProvider : IReplyAddressProvider
{
    /// <summary>Used when nothing is configured and no topology is registered — the transports in that position (Kafka, NATS) publish to their one configured topic/subject and ignore the address.</summary>
    private const string FallbackReplyAddress = "wt.responses";

    private readonly string _replyAddress;

    /// <summary>Create the provider.</summary>
    /// <param name="options">Request-client options; <see cref="RequestClientOptions.ReplyAddress"/> wins when set.</param>
    /// <param name="topology">The topology, when one is registered — its first consume endpoint is this process's address.</param>
    public DefaultReplyAddressProvider(IOptions<RequestClientOptions> options, ITopologyProvider? topology = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.ReplyAddress;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _replyAddress = configured;
            return;
        }

        // Resolved once: the topology is fixed for the life of the process, and reading it per request would pay for a
        // lazy endpoint build on the request path.
        var endpoints = topology?.ConsumeEndpoints;
        _replyAddress = endpoints is { Count: > 0 } ? endpoints[0].Queue : FallbackReplyAddress;
    }

    /// <inheritdoc />
    public string ReplyAddress => _replyAddress;
}

/// <summary>One request waiting for its response, and the contract that response has to satisfy.</summary>
internal sealed class PendingRequest(Type responseType)
{
    private readonly TaskCompletionSource<EventEnvelope> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The response contract this request expects. A message of any other type is not this request's reply.</summary>
    public Type ResponseType { get; } = responseType;

    /// <summary>Completes with the reply envelope.</summary>
    public Task<EventEnvelope> Completion => _completion.Task;

    /// <summary>Complete the request with a reply; false when it was already completed (a duplicate reply).</summary>
    public bool TryComplete(EventEnvelope envelope) => _completion.TrySetResult(envelope);
}

/// <summary>
/// The in-flight requests of this process, keyed by conversation id. Shared by the client (which registers and releases
/// entries) and the consume filter (which completes them).
/// </summary>
internal sealed class PendingRequestRegistry
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new(StringComparer.Ordinal);

    /// <summary>Register a request before it is sent, so a reply that beats the send still finds an entry.</summary>
    /// <param name="conversationId">The conversation id stamped on the request.</param>
    /// <param name="responseType">The expected response contract.</param>
    public PendingRequest Register(string conversationId, Type responseType)
    {
        var pending = new PendingRequest(responseType);
        _pending[conversationId] = pending;
        return pending;
    }

    /// <summary>Release a request. Called in a <c>finally</c>, so a timed-out, cancelled or faulted request leaves nothing behind.</summary>
    /// <param name="conversationId">The conversation id.</param>
    public void Remove(string conversationId) => _pending.TryRemove(conversationId, out _);

    /// <summary>
    /// Complete the pending request this message answers, if it answers one. Three conditions have to hold together,
    /// and each rules out a specific way a non-reply would otherwise be mistaken for one.
    /// </summary>
    /// <param name="envelope">The received envelope.</param>
    /// <returns>True when the message was consumed as a reply and must not reach a handler.</returns>
    public bool TryComplete(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // A request carries a reply address, a reply does not — EventContext deliberately never inherits ReplyTo onto a
        // follow-on message. Without this, an in-process exchange (the in-memory transport, where request and reply
        // share one channel) would complete the pending request with its own request.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            return false;

        if (envelope.ConversationId is not { Length: > 0 } conversationId || !_pending.TryGetValue(conversationId, out var pending))
            return false;

        // The conversation id propagates onto everything a handler publishes while handling the request, so an
        // unrelated follow-on event shares it and would otherwise be handed back as the response. The contract is what
        // separates them.
        if (!pending.ResponseType.IsAssignableFrom(envelope.BodyType))
            return false;

        return pending.TryComplete(envelope);
    }
}

/// <summary>
/// Intercepts a reply before dispatch: a message that answers a pending request completes it and stops there, so the
/// requesting process needs no handler for its own responses and the pipeline settles the reply normally.
/// </summary>
/// <remarks>
/// Ordered like any other <see cref="IConsumeFilter"/> — first registered is outermost. Registering the request client
/// before other filters keeps replies out of them; registering it after runs them for replies too, which is what a
/// wire-tap or audit filter usually wants.
/// </remarks>
internal sealed partial class RequestResponseConsumeFilter(PendingRequestRegistry pending, ILogger<RequestResponseConsumeFilter> logger) : IConsumeFilter
{
    public ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var envelope = context.Envelope;

        // Short-circuit: returning without calling next skips resilience, dedupe and dispatch, and the pipeline
        // acknowledges as it would for any completed chain. A reply is a delivery to this client, not work for a handler.
        if (pending.TryComplete(envelope))
        {
            LogResponseMatched(envelope.MessageId, envelope.ConversationId);
            return ValueTask.CompletedTask;
        }

        return next(context, cancellationToken);
    }

    [LoggerMessage(EventId = 6061, Level = LogLevel.Debug, Message = "Matched response message {MessageId} to pending request {ConversationId}")]
    private partial void LogResponseMatched(string messageId, string? conversationId);
}

/// <summary>Default <see cref="IRequestClient{TRequest, TResponse}"/> over the shared <see cref="IEventBus"/>.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The expected response contract.</typeparam>
internal sealed class RequestClient<TRequest, TResponse>(
    IEventBus bus,
    PendingRequestRegistry pending,
    IReplyAddressProvider replyAddresses,
    TimeProvider timeProvider,
    IOptions<RequestClientOptions> defaults) : IRequestClient<TRequest, TResponse>
    where TRequest : class, IEvent
    where TResponse : class, IEvent
{
    public async ValueTask<TResponse> GetResponseAsync(TRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversationId = Guid.NewGuid().ToString("N");
        var replyTo = replyAddresses.ReplyAddress;
        var timeout = options?.Timeout ?? defaults.Value.Timeout;

        // Registered before the send, not after: the in-memory transport can deliver the request, run the handler and
        // deliver the reply before PublishAsync returns, and a reply with nowhere to land is a guaranteed timeout.
        var pendingRequest = pending.Register(conversationId, typeof(TResponse));
        try
        {
            await SendRequestAsync(request, conversationId, replyTo, options, cancellationToken);

            EventEnvelope reply;
            try
            {
                reply = await pendingRequest.Completion.WaitAsync(timeout, timeProvider, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                throw new RequestTimeoutException(
                    $"No response of type '{typeof(TResponse).Name}' arrived for request '{typeof(TRequest).Name}' (conversation {conversationId}) within {timeout}.",
                    ex);
            }

            if (reply.Body is TResponse response)
                return response;

            throw new RequestFaultException(
                $"Request '{typeof(TRequest).Name}' (conversation {conversationId}) was answered with '{reply.BodyType.Name}', which is not a '{typeof(TResponse).Name}'.");
        }
        finally
        {
            // The one cleanup point for every exit — response, timeout, cancellation, a throwing send. Nothing else
            // removes an entry, so nothing else can leak one.
            pending.Remove(conversationId);
        }
    }

    private ValueTask SendRequestAsync(TRequest request, string conversationId, string replyTo, RequestOptions? options, CancellationToken cancellationToken)
    {
        // Null destination publishes by type, so the request reaches whichever endpoint binds the request contract; a
        // destination sends point-to-point to that endpoint. Both carry the same reply address and conversation id.
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

/// <summary>DI registration for request/response over the event bus.</summary>
public static class RequestClientServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IRequestClient{TRequest, TResponse}"/> (open generic) and the consume filter that matches
    /// replies to pending requests. Additive: a process that never resolves a request client behaves exactly as before —
    /// the filter's only cost per message is one reply-address check.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional defaults — response timeout, reply address.</param>
    /// <remarks>
    /// Call it after the transport registration (<c>AddInMemoryEventBus</c>, <c>AddRabbitMqEventBus</c>, …), and before
    /// any <c>AddConsumeFilter&lt;T&gt;()</c> whose filter should not see replies. Repeat calls are idempotent.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddInMemoryEventBus(typeof(Program).Assembly);
    /// services.AddRequestClient(o => o.Timeout = TimeSpan.FromSeconds(10));
    /// </code>
    /// </example>
    public static IServiceCollection AddRequestClient(this IServiceCollection services, Action<RequestClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<RequestClientOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PendingRequestRegistry>();
        services.TryAddSingleton<IReplyAddressProvider, DefaultReplyAddressProvider>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IRequestClient<,>), typeof(RequestClient<,>)));

        // TryAddEnumerable, not AddConsumeFilter: the filter set is a multi-registration, and a second AddRequestClient
        // call must not put a second copy of the same filter in the chain.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConsumeFilter, RequestResponseConsumeFilter>());
        return services;
    }

    /// <summary>Replace the reply-address provider — for a per-instance reply queue, a shared reply endpoint, or a broker-native inbox.</summary>
    /// <typeparam name="TProvider">The provider implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddReplyAddressProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IReplyAddressProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IReplyAddressProvider, TProvider>());
        return services;
    }
}
