using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Refers to how consumed message types are mapped onto broker endpoints (queues).</summary>
public enum TopologyStyle
{
    /// <summary>
    /// One queue for the whole service, bound to one routing key per consumed message type. The queue name is the
    /// transport's configured one, so an existing deployment keeps its queue and its dead-letter queue.
    /// </summary>
    SharedEndpoint = 0,

    /// <summary>
    /// One queue per consumed message type, named by <see cref="IEndpointNameMapper"/>, each with its own
    /// dead-letter queue. Gives per-type prefetch, isolation and DLQ inspection; needs new queues, so it is opt-in.
    /// </summary>
    EndpointPerMessageType = 1,
}
