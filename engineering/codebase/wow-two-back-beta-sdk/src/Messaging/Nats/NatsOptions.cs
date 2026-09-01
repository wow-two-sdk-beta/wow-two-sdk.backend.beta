using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>Options for the NATS JetStream event-bus adapter.</summary>
public sealed record NatsOptions
{
    /// <summary>NATS server URL. Default <c>nats://localhost:4222</c>.</summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>JetStream stream that persists events (and dead-letters). Created if absent. Default <c>wt-events</c>.</summary>
    public string Stream { get; set; } = "wt-events";

    /// <summary>Subject events are published to / consumed from. Default <c>wt.events</c>.</summary>
    public string Subject { get; set; } = "wt.events";

    /// <summary>Durable consumer name — survives restarts and tracks acked offsets. Default <c>wt-consumers</c>.</summary>
    public string DurableConsumer { get; set; } = "wt-consumers";

    /// <summary>Dead-letter subject — JetStream has no native DLQ, so exhausted/poison messages are re-published here (emulated DLQ). Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterSubject { get; set; } = "wt.events.dlq";

    /// <summary>Max JetStream delivery attempts before the message is abandoned by the broker (crash safety-net; in-process retries are handled by the SDK resilience pipeline). Default 5.</summary>
    public int MaxDeliver { get; set; } = 5;

    /// <summary>
    /// Route each message to the subject <see cref="ITopologyService"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of publishing everything to <see cref="Subject"/>. Routed
    /// subjects are nested under <see cref="Subject"/> (<c>wt.events.order-placed</c>). Default false, which keeps an
    /// existing deployment on its single subject.
    /// </summary>
    /// <remarks>
    ///   - migrate consumers first (rollout in <c>Nats.md</c>)
    ///   - widens a provisioned stream's subject list with <c>{Subject}.&gt;</c>
    ///   - needs nats-server 2.10 or newer for the multi-subject filter
    /// </remarks>
    public bool RouteByDestination { get; set; }
}
