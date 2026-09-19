using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines behavior that maps a message type to the endpoint (queue) that consumes it, and an endpoint to its dead-letter queue. Replace it
/// via <see cref="MessageTopologyServiceCollectionExtensions.AddEndpointNameFormatter{TFormatter}"/> to impose a house
/// naming scheme (per-team prefix, environment segment, an existing broker convention).
/// </summary>
public interface IEndpointNameMapper
{
    /// <summary>The queue name of the endpoint that consumes <paramref name="messageType"/>.</summary>
    /// <param name="messageType">The message contract type.</param>
    string Endpoint(Type messageType);

    /// <summary>The dead-letter queue name for <paramref name="endpointName"/>.</summary>
    /// <param name="endpointName">An endpoint (queue) name, typically one returned by <see cref="Endpoint"/>.</param>
    string DeadLetter(string endpointName);
}
