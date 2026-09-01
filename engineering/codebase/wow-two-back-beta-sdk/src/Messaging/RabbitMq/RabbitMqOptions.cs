using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;

/// <summary>Options for the RabbitMQ event-bus adapter.</summary>
public sealed record RabbitMqOptions
{
    /// <summary>AMQP connection URI. Default local guest.</summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>Topic exchange events are published to. Default <c>wt.events</c>.</summary>
    public string Exchange { get; set; } = "wt.events";

    /// <summary>
    /// This service's queue under <see cref="TopologyStyle.SharedEndpoint"/>. Default <c>wt.events.queue</c>.
    /// Ignored under <see cref="TopologyStyle.EndpointPerMessageType"/>, where every queue is named by
    /// <see cref="IEndpointNameMapper"/>.
    /// </summary>
    public string Queue { get; set; } = "wt.events.queue";

    /// <summary>
    /// Dead-letter exchange (native DLQ). Default <c>wt.events.dlx</c>. Declared <c>fanout</c> under
    /// <see cref="TopologyStyle.SharedEndpoint"/> and <c>direct</c> under
    /// <see cref="TopologyStyle.EndpointPerMessageType"/>, which needs to key each dead letter to one DLQ. An exchange
    /// cannot change type, so switching style against an existing exchange needs a new name here.
    /// </summary>
    public string DeadLetterExchange { get; set; } = "wt.events.dlx";

    /// <summary>Dead-letter queue for the shared endpoint. Default <c>wt.events.dlq</c>. Per-type endpoints derive theirs from <see cref="IEndpointNameMapper.DeadLetter"/> instead.</summary>
    public string DeadLetterQueue { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Declare consume queues with <c>x-max-priority</c>, so the broker actually ranks the AMQP <c>priority</c>
    /// property the send path already stamps. Null (default) leaves queues unranked: priority rides the wire and is
    /// ignored, and <see cref="ITransportCapabilities.NativePriority"/> reports false to match.
    /// </summary>
    /// <remarks>
    ///   - fixed at queue creation — a queue that predates the setting stays unranked, never failing startup
    ///   - recreate that queue during a drain window to rank it
    ///   - keep the ceiling small (1–5) — each band costs a sub-queue with its own memory and CPU
    /// </remarks>
    public byte? MaxPriority { get; set; }

    /// <summary>
    /// On start, drop the <c>#</c> catch-all binding from each consume queue. Default false. Migration aid only: a
    /// queue created before per-type bindings existed still carries <c>#</c>, so it keeps receiving every type on the
    /// exchange until that binding goes. Leave it off once the migration has run.
    /// </summary>
    public bool UnbindCatchAllBinding { get; set; }

    /// <summary>
    /// Consumer prefetch (unacked in flight). Default 10. Keep it at least
    /// <see cref="ConcurrencyOptions.MaxConcurrentMessages"/>: the broker never leaves more than this many messages
    /// unacked, so a smaller prefetch becomes the effective concurrency limit however many pump workers are configured.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Wait between automatic reconnect attempts after the connection drops. Default 5s. The client retries
    /// indefinitely at this interval, so this is the floor on how long a recovered broker stays unnoticed.
    /// </summary>
    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);
}
