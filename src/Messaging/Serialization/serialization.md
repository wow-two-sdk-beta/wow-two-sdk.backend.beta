# Messaging.Serialization

Two swappable seams the transports hang the wire format on: **`IMessageSerializer`** (body ↔ bytes) and
**`IMessageTypeResolver`** (event type ↔ stable wire token). Extracting these from the adapters kills the
`AssemblyQualifiedName` → `Type.GetType` fragility that silently dropped cross-service / renamed messages.

## Why the resolver matters

The wire type token used to be `BodyType.AssemblyQualifiedName`, resolved with `Type.GetType(token)`. That only
resolves when the consumer has the **exact same type in the exact same assembly name** — so it broke on cross-service
consumption (the whole point of a broker), any rename/namespace-move/assembly-rename, and cross-language. A failed
resolve returned `null` → the message was discarded, not dead-lettered.

Now: `AddEventHandlersFromAssemblies` registers every `IEvent` contract (published + handled) in a `MessageTypeRegistry`
under its stable `FullName` token. `DefaultMessageTypeResolver` resolves registry → aliases → AQN fallback (back-compat).

## Wire-up (automatic)

`AddEventResilienceDefaults` (called by every `Add*EventBus`) registers `SystemTextJsonMessageSerializer` +
`DefaultMessageTypeResolver`. Nothing to do for the defaults.

```csharp
// swap the serializer (once MessagePack/Protobuf impls land):
services.AddMessageSerializer<MessagePackMessageSerializer>();

// swap the resolver (e.g. URN- or schema-registry-backed):
services.AddMessageTypeResolver<UrnMessageTypeResolver>();

// give a contract an explicit/stable token, or alias an old name after a rename:
services.MapMessageType<OrderPlaced>("urn:acme:orders:order-placed");
```

## Contract

- **Serializer**: `string ContentType` (stamped as the `wt-content-type` wire header) · `byte[] Serialize(object, Type)` ·
  `object? Deserialize(ReadOnlySpan<byte>, Type)`. Default = System.Text.Json through `Foundation/Serialization/JsonOptionsPresets`.
- **Resolver**: `string ToTypeToken(Type)` (default = registered `FullName`, else AQN) · `Type? ResolveType(string)`
  (registry → alias → `Type.GetType` → assembly scan). Returns null on unknown — the transport dead-letters (does not drop).
- **Registry**: `MessageTypeRegistry.Register(type, token?)` / `AddAlias(alias, type)` — populated at registration.

## Adopted by

`RabbitMq/` · `Kafka/` · `Nats/` send + reconstruct paths. In-memory doesn't serialize (passes objects). Outbox/webhooks
serialization adoption is a follow-up.

## See also

- [`../Messaging.md`](../Messaging.md) · [`../Transport/TransportContracts.cs`](../Transport/TransportContracts.cs) (`ISendTransport` owns the wire format)
- Maturity backlog §3.2: [`../../docs/planning/messaging/events-maturity-backlog.md`](../../docs/planning/messaging/events-maturity-backlog.md)
