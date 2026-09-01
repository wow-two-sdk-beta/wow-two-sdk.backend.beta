using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Optional options for <see cref="IEventBus.SendAsync{TEvent}"/>. Transport hints are abstract — each adapter maps them to its native mechanism or ignores them.</summary>
public sealed record SendOptions
{
    /// <summary>Explicit message id (idempotency key). Generated when null.</summary>
    public string? MessageId { get; set; }

    /// <summary>Correlation id for the business flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Conversation id for a request/response exchange.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Address a reply should be sent to (transport-abstract); null for a one-way message. See <see cref="EventEnvelope.ReplyTo"/>.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Id of the event that caused this one.</summary>
    public string? CausationId { get; set; }

    /// <summary>Delay before the event becomes deliverable (scheduled delivery).</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Partition / ordering key (transport-abstract).</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Persist the message so it survives a broker restart (transport-abstract).</summary>
    public bool? Durable { get; set; }

    /// <summary>Relative priority hint (transport-abstract).</summary>
    public int? Priority { get; set; }

    /// <summary>Time-to-live after which the message expires undelivered (transport-abstract).</summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Custom headers to attach.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
