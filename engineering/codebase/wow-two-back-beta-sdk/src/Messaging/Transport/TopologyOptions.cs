using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Topology shape — how endpoints are named and which routing keys each one binds.</summary>
public sealed record TopologyOptions
{
    /// <summary>Endpoint shape. Default <see cref="TopologyStyle.SharedEndpoint"/>, which keeps an existing deployment's queue names.</summary>
    public TopologyStyle Style { get; set; } = TopologyStyle.SharedEndpoint;

    /// <summary>Queue name for <see cref="TopologyStyle.SharedEndpoint"/>. Null lets the transport supply its own configured queue name.</summary>
    public string? SharedEndpointName { get; set; }

    /// <summary>Dead-letter queue for the shared endpoint. Null derives it from <see cref="IEndpointNameMapper.DeadLetter"/>.</summary>
    public string? SharedDeadLetterQueueName { get; set; }

    /// <summary>Prefix handed to the default <see cref="IEndpointNameMapper"/> for generated endpoint names (e.g. <c>wt.events</c>). Ignored when a custom formatter is registered.</summary>
    public string? EndpointPrefix { get; set; }

    /// <summary>
    /// Also bind the pre-topology routing key — the message type's simple name — alongside the stable type token.
    /// Default true: a publisher that has not been upgraded still emits the short name, and without this binding the
    /// exchange would drop those messages as unroutable. Turn it off once every publisher is on the new key.
    /// </summary>
    public bool BindLegacyTypeNameKeys { get; set; } = true;

    /// <summary>Bind each endpoint queue under its own name, so an explicit <see cref="IEventBus.SendAsync{TEvent}"/> to that name lands point-to-point. Default true.</summary>
    public bool BindEndpointNameKeys { get; set; } = true;

    /// <summary>
    /// Bind every declared <see cref="DestinationBinding"/> whose message type this process consumes, so a send to
    /// that logical address is delivered rather than dropped as unroutable. Default true. Turn it off only when an
    /// external topology already binds those aliases and a duplicate binding would double-deliver.
    /// </summary>
    public bool BindDestinationAliasKeys { get; set; } = true;
}
