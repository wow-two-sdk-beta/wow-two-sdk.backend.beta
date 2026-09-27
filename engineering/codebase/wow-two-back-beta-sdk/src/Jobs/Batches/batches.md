# Batch pipelines

> In-memory, bounded, best-effort work that a background loop writes in batches — scan events, audit trails, counters.

```csharp
builder.Services.AddBatchPipeline<ScanRecordModel, ScanBatchHandler>(options =>
{
    options.Capacity = 10_000;                            // waiting items before producers are refused
    options.BatchSize = 100;                              // most items per handler call
    options.MaxBatchDelay = TimeSpan.FromMilliseconds(250); // wait to fill a partial batch
});

// Request path: never waits.
if (!pipeline.TryWrite(scan)) { /* dropped and counted */ }

public sealed class ScanBatchHandler(AppDbContext db) : IBatchHandler<ScanRecordModel>
{
    public async Task HandleAsync(IReadOnlyList<ScanRecordModel> batch, CancellationToken cancellationToken) { /* one transaction */ }
}
```

- Producers never block: a full or stopping pipeline drops the item, counts it and logs at powers of two.
- Each batch runs in its own DI scope; a thrown exception fails that batch only, and the loop continues.
- Shutdown refuses new items, drains until the host's shutdown budget expires, then counts the rest as failed.
- Metrics (`WoW.Two.Sdk.Backend.Beta.Jobs.Batches`): `batch.items.dropped`, `.processed`, `.failed`, tagged `pipeline`.
- Delivery is best-effort: a crash loses accepted items. Durable work belongs in the outbox or a job store.
