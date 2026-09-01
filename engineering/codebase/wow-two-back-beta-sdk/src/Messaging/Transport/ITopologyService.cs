using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Declares what a process consumes and how an outgoing message is addressed. This is what replaces a catch-all
/// binding: bindings are derived from the registered handler set, one routing key per message type, so a service
/// receives only the types it actually handles instead of filtering the whole exchange in-process.
/// </summary>
public interface ITopologyService
{
    /// <summary>Every endpoint this process declares, binds and consumes. Empty when the process handles nothing.</summary>
    IReadOnlyList<EndpointTopology> ConsumeEndpoints { get; }

    /// <summary>The routing key a message of <paramref name="messageType"/> is published under.</summary>
    /// <param name="messageType">The message contract type.</param>
    string RoutingKeyFor(Type messageType);

    /// <summary>The routing key for one outgoing envelope — the type key for a publish, the destination address for an explicit send.</summary>
    /// <param name="envelope">The envelope being sent.</param>
    string ResolveRoutingKey(EventEnvelope envelope);

    /// <summary>
    /// Whether <paramref name="routingKey"/> is bound to an endpoint of <em>this</em> process — a message addressed to
    /// it arrives here. The default answer walks <see cref="ConsumeEndpoints"/>; a provider that binds patterns rather
    /// than literal keys overrides it.
    /// </summary>
    /// <remarks>
    ///   - false says only that this process does not bind the key — another service may
    ///   - to tell whether a send drops, pair it with <see cref="DestinationBinding"/>, which records the owner
    /// </remarks>
    /// <param name="routingKey">A routing key, as returned by <see cref="RoutingKeyFor"/> or <see cref="ResolveRoutingKey"/>.</param>
    bool BindsRoutingKey(string routingKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(routingKey);

        foreach (var endpoint in ConsumeEndpoints)
            if (endpoint.RoutingKeys.Contains(routingKey, StringComparer.Ordinal))
                return true;

        return false;
    }
}
