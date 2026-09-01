using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// A logical destination address this process answers to for one message type — an alias bound alongside the type
/// keys, so an explicit <see cref="IEventBus.SendAsync{TEvent}"/> to that address is delivered instead of dropped.
/// </summary>
/// <remarks>
///   - a routing-slip saga step addresses its output by logical name, never by type
///   - an unbound address resolves to a key nothing matches, and RabbitMQ discards the message without refusing it
///   - the alias binds only on an endpoint that consumes <see cref="MessageType"/>
/// </remarks>
public sealed record DestinationBinding
{
    /// <summary>The logical destination address, as passed to <see cref="IEventBus.SendAsync{TEvent}"/>.</summary>
    public required string Destination { get; init; }

    /// <summary>The message type sent to <see cref="Destination"/>. The alias is bound only on an endpoint carrying this type.</summary>
    public required Type MessageType { get; init; }
}
