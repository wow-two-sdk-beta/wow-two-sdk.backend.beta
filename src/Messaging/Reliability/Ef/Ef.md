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
builder.Services.AddEfInbox<AppDbContext>();                // IInboxStore (dedupe)

// 3. stage inside a command (commits atomically with the entity write)
await _outbox.EnqueueAsync(
    new OutboxRecord(Guid.NewGuid().ToString(), typeof(OrderPlaced).FullName!, payloadBytes, now, headers), ct);
await _db.SaveChangesAsync(ct);               // order row + outbox row commit together
// → OutboxDispatcher polls, deserializes by `type`, publishes to IEventBus, stamps processed_on_utc.
```

## Migration (author for the bespoke migrator — `Migrations/NNN-add-outbox/`)

`Apply.sql`:

```sql
CREATE TABLE outbox_messages (
    id               uuid         NOT NULL,
    type             varchar(500) NOT NULL,
    payload          bytea        NOT NULL,
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

## Types

| Type | Role |
|---|---|
| `OutboxMessageEntity` · `ApplyOutboxModel()` | maps `outbox_messages` |
| `EfOutbox<TContext>` · `AddEfOutbox<TContext>()` | `IOutbox` — adds the row to `TContext` (atomic staging) |
| `OutboxDispatcher<TContext>` · `AddEfOutboxDispatcher<TContext>(…)` | claims a batch (`IOutboxClaimStrategy`) → typed-publish to `IEventBus` → stamps `processed_on_utc`; polling hosted service |
| `IOutboxClaimStrategy` · `PollingOutboxClaimStrategy` | pending-row claim seam — default polls; register a `FOR UPDATE SKIP LOCKED` impl (`Replace`) for multi-instance |
| `InboxMessageEntity` · `ApplyInboxModel()` · `EfInboxProcessor<TContext>` · `AddEfInbox<TContext>()` | `IInboxProcessor` — inbox row + handler in **one transaction** (true exactly-once) |

## NEXT

- **Multi-instance dispatch** — seam exists (`IOutboxClaimStrategy`); ship the Postgres `SELECT … FOR UPDATE SKIP LOCKED` claim impl (raw SQL) + `Replace` it in.

## See also

- Ports: [`../MessagingReliability.cs`](../MessagingReliability.cs) (`IOutbox` / `OutboxRecord` / `IOutboxDispatcher`)
- Pluggable EF interceptors: `Data/EntityFrameworkCore/Interceptors/` (`AddEfSaveChangesInterceptor<T>()`)
