namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>A staged outgoing message in a transactional outbox.</summary>
/// <param name="Id">Outbox row id.</param>
/// <param name="Type">Logical message type/name.</param>
/// <param name="Payload">Serialized message body.</param>
/// <param name="OccurredOnUtc">When the message was produced.</param>
/// <param name="Headers">Headers to attach on dispatch.</param>
public sealed record OutboxRecord(
    string Id,
    string Type,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset OccurredOnUtc,
    IReadOnlyDictionary<string, string> Headers);
