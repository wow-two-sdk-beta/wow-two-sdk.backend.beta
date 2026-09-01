using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>Options for the Kafka event-bus adapter.</summary>
public sealed record KafkaOptions
{
    /// <summary>Bootstrap servers (host:port[,host:port]). Default local.</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Topic events are produced to / consumed from. Default <c>wt.events</c>.</summary>
    public string Topic { get; set; } = "wt.events";

    /// <summary>Consumer group id. Default <c>wt-consumers</c>.</summary>
    public string GroupId { get; set; } = "wt-consumers";

    /// <summary>Dead-letter topic — Kafka has no native DLQ, so exhausted/poison messages are re-produced here. Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterTopic { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Route each message to the topic <see cref="ITopologyService"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of producing everything to <see cref="Topic"/>. Default
    /// false, which keeps an existing deployment on its single topic.
    /// </summary>
    /// <remarks>
    ///   - migrate consumers first
    ///   - a consumer with this on subscribes to <see cref="Topic"/> as well as every routed topic
    ///   - turn it on for the producers once every consumer is across (rollout in <c>Kafka.md</c>)
    /// </remarks>
    public bool RouteByDestination { get; set; }
}
