# Messaging.Reliability.Ef

EF-backed **transactional outbox** — stage an event in the same DB transaction as the business write, so the two
commit atomically (no dual-write, no distributed transaction). A dispatcher (NEXT) drains pending rows to the `IEventBus`.

> No `IHasDomainEvents` / entity-event-scanning: events are staged **explicitly** via `IOutbox.EnqueueAsync`. Plain
> events, no marker on aggregates.

## Wire-up

```csharp
// 1. map the outbox + inbox entities (bespoke migrator owns the DDL; EF just maps over it)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyOutboxModel();          // → outbox_messages
    modelBuilder.ApplyInboxModel();           // → inbox_messages
}

// 2. register
builder.Services.AddEfOutbox<AppDbContext>();               // IOutbox (staging)
builder.Services.AddEfOutboxDispatcher<AppDbContext>(o => o.PollInterval = TimeSpan.FromSeconds(5));
builder.Services.ReplaceWithPostgresSkipLockedOutboxClaim<AppDbContext>();  // scale-out (Postgres) — see Multi-instance dispatch below
builder.Services.AddEfInbox<AppDbContext>();                // IInboxStore (dedupe)

// 3. stage inside a command (commits atomically with the entity write)
await _outbox.EnqueueAsync(
    new OutboxRecord(Guid.NewGuid().ToString(), typeof(OrderPlaced).FullName!, payloadBytes, now, headers), ct);
await _db.SaveChangesAsync(ct);               // order row + outbox row commit together
// → OutboxDispatcher polls, resolves `type` via IMessageTypeResolver, deserializes `payload` via IMessageSerializer,
//   publishes to IEventBus, stamps processed_on_utc.
```

Staging records the registered serializer's content type on the row (`content_type`), the same way a transport stamps it
on the wire envelope; dispatch fails a row loudly if that format no longer matches the registered `IMessageSerializer`
rather than deserializing it to garbage. `type` is whatever token the caller passes — `typeof(T).FullName!` above.
Resolution goes through `IMessageTypeResolver`, so a registered stable token, a plain full name, and an
assembly-qualified name written by an older build all still resolve; an unresolvable one fails the row (error retained,
`Attempts` bumped) instead of being dropped.

## Headers on dispatch (`headers_json`)

The record's `Headers` are staged to `headers_json` and rebuilt into `PublishOptions` at dispatch — the outbox hop is
header-transparent. Reserved (`wt-`) control headers are **lifted onto their typed field** rather than passed through
raw, because every adapter drops caller-supplied reserved keys when writing wire headers and re-stamps its own from the
envelope:

| Staged header | Dispatched as |
|---|---|
| `wt-message-id` | `PublishOptions.MessageId` — else the row `id`, so a redelivery after a crash reuses the id the consumer's inbox dedupes on |
| `wt-reply-to` | `PublishOptions.ReplyTo` |
| `wt-conversation-id` | `PublishOptions.ConversationId` |
| `wt-partition-key` | `PublishOptions.PartitionKey` |
| `wt-event-type` · `wt-content-type` · `wt-dl-*` | dropped — the row's `type` / `content_type` columns own the first two, death headers describe a past dead-letter |
| everything else (`traceparent`, `tracestate`, app metadata) | `PublishOptions.Headers`, verbatim |

**W3C trace context survives the hop.** The bus stamps `traceparent` from the ambient `Activity`, which inside the
dispatcher's background loop is the dispatcher's — not the producer's. So dispatch first starts an `outbox dispatch`
activity parented to the row's staged `traceparent`/`tracestate`, and the publish it wraps lands in the producer's trace
instead of starting an orphan one. With no `ActivitySource` listener the staged header simply passes through untouched
and carries the trace by itself.

A caller id that is not a `Guid` cannot be the row's `Guid` primary key; staging carries it through `wt-message-id` so
it is not lost to the generated one. Malformed `headers_json` fails the row loudly (error retained, `Attempts` bumped)
rather than dispatching it without its headers.

## Migration (author for the bespoke migrator — `Migrations/NNN-add-outbox/`)

`Apply.sql`:

```sql
CREATE TABLE outbox_messages (
    id               uuid         NOT NULL,
    type             varchar(500) NOT NULL,
    payload          bytea        NOT NULL,
    content_type     varchar(100) NOT NULL DEFAULT 'application/json',
    occurred_on_utc  timestamptz  NOT NULL,
    headers_json     text         NOT NULL DEFAULT '{}',
    processed_on_utc timestamptz  NULL,
    attempts         int          NOT NULL DEFAULT 0,
    error            text         NULL,
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
);
CREATE INDEX ix_outbox_messages_pending ON outbox_messages (occurred_on_utc) WHERE processed_on_utc IS NULL;

CREATE TABLE inbox_messages (                      -- IInboxStore dedupe (exactly-once effect)
    message_id  varchar(200) NOT NULL,
    seen_at_utc timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id)
);
```

`Rollback.sql`:

```sql
DROP TABLE IF EXISTS inbox_messages;
DROP TABLE IF EXISTS outbox_messages;
```

### Already have an `outbox_messages` table? Add `content_type`

`content_type` is newer than the original DDL above. Existing deployments need a follow-up migration — every pre-existing
row was staged by the System.Text.Json serializer, so backfill it as such:

```sql
ALTER TABLE outbox_messages ADD COLUMN content_type varchar(100) NOT NULL DEFAULT 'application/json';
```

Without it the dispatcher's `SELECT *` claim (and the EF materializer) fails on the missing column. Leaving the column
empty rather than backfilling is also valid — dispatch then skips the format check for those rows.

## Types

| Type | Role |
|---|---|
| `OutboxMessageEntity` · `ApplyOutboxModel()` | maps `outbox_messages` |
| `EfOutbox<TContext>` · `AddEfOutbox<TContext>()` | `IOutbox` — adds the row to `TContext` (atomic staging) |
| `OutboxDispatcher<TContext>` · `AddEfOutboxDispatcher<TContext>(…)` | claims a batch (`IOutboxClaimStrategy`) → resolves `type` (`IMessageTypeResolver`) → deserializes `payload` (`IMessageSerializer`) → rebuilds `PublishOptions` from `headers_json` (`OutboxDispatchHeaders`) → typed-publish to `IEventBus` → stamps `processed_on_utc`; polling hosted service |
| `IOutboxClaimStrategy` · `PollingOutboxClaimStrategy` · `PostgresSkipLockedOutboxClaimStrategy` | pending-row claim seam — default polls (single-instance); `ReplaceWithPostgresSkipLockedOutboxClaim<TContext>()` swaps in the Postgres `FOR UPDATE SKIP LOCKED` claim for multi-instance |
| `InboxMessageEntity` · `ApplyInboxModel()` · `EfInboxProcessor<TContext>` · `AddEfInbox<TContext>()` | `IInboxProcessor` — inbox row + handler in **one transaction** (true exactly-once) |

## Multi-instance dispatch (scale-out)

The default `PollingOutboxClaimStrategy` claims oldest-first **without locking** — safe for a single dispatcher, but two instances polling the same outbox would both claim the same rows (double-dispatch). For scale-out, swap in the Postgres locking claim:

```csharp
builder.Services.AddEfOutboxDispatcher<AppDbContext>();
builder.Services.ReplaceWithPostgresSkipLockedOutboxClaim<AppDbContext>();  // Replace() the IOutboxClaimStrategy singleton
```

`PostgresSkipLockedOutboxClaimStrategy` claims with `SELECT … WHERE processed_on_utc IS NULL ORDER BY occurred_on_utc FOR UPDATE SKIP LOCKED LIMIT @batch` (raw SQL, via `FromSqlRaw` over `OutboxMessageEntity` — EF Core abstractions only, no direct Npgsql dependency). Concurrent dispatchers **skip each other's locked rows**, so every pending row is claimed by exactly one instance. It opens the transaction at claim time and holds the row locks until the dispatcher stamps `processed_on_utc` and its `SaveChanges` commits — bridged onto the context's `SavedChanges` event so the dispatcher stays unchanged (the two strategies are drop-in interchangeable). Requires a real Postgres transaction, so pair it with the Npgsql provider. Covered by `Messaging.Tests/OutboxSkipLockedTests.cs` (two loops drain one outbox concurrently → every row claimed once).

## See also

- Ports: [`../MessagingReliability.cs`](../MessagingReliability.cs) (`IOutbox` / `OutboxRecord` / `IOutboxDispatcher`)
- Pluggable EF interceptors: `Data/EntityFrameworkCore/Interceptors/` (`AddEfSaveChangesInterceptor<T>()`)
