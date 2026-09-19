using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

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
    public bool TryComplete(EventEnvelopeModel envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // A request carries a reply address, a reply does not — otherwise an in-process request completes itself.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            return false;

        if (envelope.ConversationId is not { Length: > 0 } conversationId || !_pending.TryGetValue(conversationId, out var pending))
            return false;

        // The conversation id rides follow-on events too, so the response contract is what separates them.
        if (!pending.ResponseType.IsAssignableFrom(envelope.BodyType))
            return false;

        return pending.TryComplete(envelope);
    }
}
