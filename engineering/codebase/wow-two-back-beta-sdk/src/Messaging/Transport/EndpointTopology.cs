using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>One broker endpoint: the queue to declare and consume, its dead-letter queue, the routing keys bound to it, and the message types it carries.</summary>
public sealed record EndpointTopology
{
    /// <summary>The queue to declare and consume from.</summary>
    public required string Queue { get; init; }

    /// <summary>The dead-letter queue messages from <see cref="Queue"/> are moved to.</summary>
    public required string DeadLetterQueue { get; init; }

    /// <summary>
    /// Routing keys bound from the exchange to <see cref="Queue"/> — the stable type token of every type in
    /// <see cref="MessageTypes"/>, the queue's own name (so an explicit send addresses it point-to-point), and
    /// optionally the pre-topology short type name.
    /// </summary>
    public required IReadOnlyList<string> RoutingKeys { get; init; }

    /// <summary>The message types this endpoint consumes.</summary>
    public required IReadOnlyList<Type> MessageTypes { get; init; }
}
