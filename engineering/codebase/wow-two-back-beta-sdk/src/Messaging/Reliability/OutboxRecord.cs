namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>A staged outgoing message in a transactional outbox.</summary>
public sealed record OutboxRecord
{
    /// <summary>Outbox row id.</summary>
    public required string Id { get; init; }

    /// <summary>Logical message type/name.</summary>
    public required string Type { get; init; }

    /// <summary>Serialized message body.</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>When the message was produced.</summary>
    public required DateTimeOffset OccurredOnUtc { get; init; }

    /// <summary>Headers to attach on dispatch.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
