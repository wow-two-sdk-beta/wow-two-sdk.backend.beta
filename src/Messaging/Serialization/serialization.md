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
// swap the serializer everything is SENT with:
services.AddMessageSerializer<MessagePackMessageSerializer>();

// additionally UNDERSTAND a format on receive, without changing what we send:
services.AddReceiveOnlyMessageSerializer<CloudEventsMessageSerializer>();

// swap the resolver (e.g. URN- or schema-registry-backed):
services.AddMessageTypeResolver<UrnMessageTypeResolver>();

// give a contract an explicit/stable token, or alias an old name after a rename:
services.MapMessageType<OrderPlaced>("urn:acme:orders:order-placed");
```

## Shipped serializers

| Serializer | `ContentType` | Use it for |
|---|---|---|
| `SystemTextJsonMessageSerializer` *(default)* | `application/json` | Readable on the broker; the default nothing has to opt into. |
| `MessagePackMessageSerializer` | `application/x-msgpack` | Compact binary — smaller/cheaper than JSON, unreadable on the broker. |
| `CloudEventsMessageSerializer` | `application/cloudevents+json` | Interop with non-.NET producers/consumers (CNCF CloudEvents 1.0). |

**MessagePack** defaults to the contractless resolver, so plain event records work with no `[MessagePackObject]`
annotation and a contract that worked under JSON keeps working. `MessagePackSecurity.UntrustedData` is applied — broker
payloads are untrusted input. The **typeless** resolvers are deliberately not used: they instantiate whatever CLR type
the sender names (an RCE vector). Type identity belongs to `IMessageTypeResolver` and the `wt-event-type` header.
Caveat: the contractless resolver builds formatters through `System.Reflection.Emit` at runtime, so it does not survive
Native AOT or aggressive trimming — an AOT host needs MessagePack's source generator and `[MessagePackObject]` contracts,
passed in via the ctor's `MessagePackSerializerOptions`. This is the one spot the SDK's *source-gen first* rule is unmet.

**CloudEvents** emits the **structured** JSON event format (attributes + data as one JSON document), not binary mode —
binary mode puts each attribute in its own `ce-*` transport header, and this seam returns bytes and cannot write
headers. Attribute mapping: `specversion` = `1.0` · `type` = `IMessageTypeResolver.ToTypeToken(bodyType)` (so
`MapMessageType<T>("com.acme.orders.placed")` is how a contract gets a reverse-DNS CloudEvents type) · `source` /
`subject` / `dataschema` from `CloudEventsSerializerOptions` · `time` RFC 3339 UTC · `datacontenttype`
`application/json`. Registration needs two lines, because `AddMessageSerializer<T>()` takes no options argument:

```csharp
services.AddSingleton(new CloudEventsSerializerOptions { Source = new Uri("https://acme.example/orders") });
services.AddMessageSerializer<CloudEventsMessageSerializer>();
```

## Seam friction (found while building the 2nd and 3rd impls)

`IMessageSerializer` had exactly one implementation from BATCH 1 until now, and it leaked System.Text.Json's shape.
None of these are worked around silently — each is listed with what the impl does instead.

1. ~~**Content type is stamped but never honoured on receive.**~~ **Closed.** `MessageSerializerRegistry` now selects the
   deserializer from the received `wt-content-type` on every adapter's reconstruct path, falling back to the default for
   an absent or unregistered value. Registering the *second* format was the missing half: `AddMessageSerializer<T>()`
   only ever swapped the default, so the registry could not hold more than one entry and the selection had nothing to
   select between — `AddReceiveOnlyMessageSerializer<T>()` is the additive path that fills it.
2. **`Deserialize` takes `ReadOnlySpan<byte>`, which only STJ can consume directly.** MessagePack's entry points take
   `ReadOnlyMemory<byte>` / `ReadOnlySequence<byte>` / `Stream`; none can wrap a span, so the impl pays a
   `ToArray()` copy per message. `ReadOnlySequence<byte>` is the transport-neutral shape (it is also what broker
   client libraries and pipelines hand you, and it degrades to a span cheaply for the single-segment case).
   **Worked around** with the copy.
3. **The seam cannot express a header-carried wire format.** CloudEvents binary mode, and any `content-encoding`
   compression scheme, need to write transport headers alongside the bytes. `Serialize` returns `byte[]` and the
   adapter stamps headers from `ContentType` alone, so binary mode is unimplementable here — hence structured mode.
4. **`Serialize`/`Deserialize` see the body, never the envelope.** CloudEvents requires `id`, and `id` + `source` is the
   consumer's dedupe key; `EventEnvelope.MessageId` is unreachable from the seam, so the impl generates a fresh GUID
   that the SDK never sees again. Inbound `id`/`source`/`time`/`subject` from a non-.NET producer are parsed and
   dropped for the same reason, and `EventEnvelope.MessageId` instead comes from `wt-message-id`, which a CloudEvents
   producer never sets. So the two systems deduplicate on different keys. **Partially worked around**:
   `CloudEventsSerializerOptions.IdFactory` lets a product derive `id` from a business key on the body.
5. **`AddMessageSerializer<T>()` has no options overload.** It is `Replace(ServiceDescriptor.Singleton<IMessageSerializer, T>())`,
   so a serializer needing configuration — CloudEvents `source` is REQUIRED by the spec — has to have its options
   smuggled in through a separate `AddSingleton`. An `AddMessageSerializer<T>(T instance)` / `Func<IServiceProvider, T>`
   overload would close it. **Worked around** via the optional-ctor-parameter pattern the STJ default already uses.
6. ~~**Empty-payload contract is documented but not met by the default.**~~ **Closed — the code moved, not the doc.**
   STJ now returns null on an empty span, matching MessagePack, CloudEvents and the interface. A non-empty but
   malformed body still throws; only genuine emptiness is null.

   The alternative — keep the throw and re-document it — was rejected on what the call sites already do. Null is not a
   value that reaches a handler here: all three callers convert it to an explicit failure. The adapters'
   `TryReconstruct` returns a null envelope and takes the unparseable path (`Kafka` re-produces to the DLQ topic,
   `RabbitMq` nacks `requeue: false`, `AzureServiceBus` native-dead-letters with a reason); `Transport/ClaimCheck.cs`
   throws `ClaimCheckPayloadException`; `Reliability/Ef/OutboxDispatcher.cs` marks the row failed. Throwing was
   therefore not the *louder* option but the fatal one: `TryReconstruct` is called outside the adapters' try/catch
   (`KafkaTransport.cs:311` — the loop's only `catch` wraps `onMessage`), so a `JsonException` escaped `ConsumeLoop` and
   stopped the whole subscription instead of costing the one bad message. STJ also already returned null for a payload
   of the four bytes `null`, so callers could never have relied on "the default never returns null".

## Contract

- **Serializer**: `string ContentType` (stamped as the `wt-content-type` wire header, and what the receiver selects on) ·
  `byte[] Serialize(object, Type)` · `object? Deserialize(ReadOnlySpan<byte>, Type)` — **empty payload returns null, all
  three impls**; malformed throws. Default = System.Text.Json through `Foundation/Serialization/JsonOptionsPresets`.
- **Resolver**: `string ToTypeToken(Type)` (default = registered `FullName`, else AQN) · `Type? ResolveType(string)`
  (registry → alias → `Type.GetType` → assembly scan). Returns null on unknown — the transport dead-letters (does not drop).
- **Type registry**: `MessageTypeRegistry.Register(type, token?)` / `AddAlias(alias, type)` — populated at registration.
- **Serializer registry**: `MessageSerializerRegistry.Resolve(contentType)` → the serializer that declared it, else
  `Default`. Built from the container's `IMessageSerializer` descriptors; the last one is `Default` and the send path.
  `AddMessageSerializer<T>()` swaps `Default` · `AddReceiveOnlyMessageSerializer<T>()` adds an entry and leaves `Default`.

## Adopted by

`RabbitMq/` · `Kafka/` · `Nats/` send + reconstruct paths. In-memory doesn't serialize (passes objects). Outbox/webhooks
serialization adoption is a follow-up.

## See also

- [`../Messaging.md`](../Messaging.md) · [`../Transport/TransportContracts.cs`](../Transport/TransportContracts.cs) (`ISendTransport` owns the wire format)
- Maturity backlog §3.2: [`../../docs/planning/messaging/events-maturity-backlog.md`](../../docs/planning/messaging/events-maturity-backlog.md)
