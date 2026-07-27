# HashChain — spec

*Last updated: 2026-06-24*

> **Concrete API + usage.** Updated when public surface changes.

NuGet: `WoW.Two.Sdk.Backend.Beta` (mono-lib) · namespace `WoW.Two.Sdk.Backend.Beta.Foundation.Audit`.

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Audit;

builder.Services.AddHashChain<AuditEntry, AuditEntryCanonicalizer>();

// append
var sealer = sp.GetRequiredService<IHashChainSealer<AuditEntry>>();
sealer.Seal(newEntry, lastEntry);   // lastEntry == null for the first (genesis) entry

// audit
var verifier = sp.GetRequiredService<IHashChainVerifier<AuditEntry>>();
var result = verifier.Verify(entriesOrderedBySequence);
```

## Public API

### `IHashChainedEntry`

| Member | Type | Notes |
|---|---|---|
| `Sequence` | `long { get; set; }` | Monotonic, gap-free, starts at 1. |
| `PreviousHash` | `byte[] { get; set; }` | Prior entry's hash; empty for genesis. |
| `Hash` | `byte[] { get; set; }` | This entry's hash; the next entry's `PreviousHash`. |

### `IChainedEntryCanonicalizer<in TEntry>`

| Member | Type | Notes |
|---|---|---|
| `SchemeVersion` | `int { get; }` | Version of the field set; hashed into every entry so versions never collide. Verify each segment with the canonicalizer that sealed it (the consumer selects, not the verifier). |
| `Write(TEntry entry, ICanonicalPayloadBuilder builder)` | `void` | Consumer appends its chosen domain fields, in fixed order. The SDK adds chain fields itself. |

### `ICanonicalPayloadBuilder` (concrete: `CanonicalPayloadBuilder`)

Fluent (each returns `ICanonicalPayloadBuilder`). Each writes `tag + length + bytes`.

| Method | Encoding |
|---|---|
| `Append(string?)` | UTF-8, length-prefixed; `null` → null marker. |
| `Append(int)` | 4-byte big-endian. |
| `Append(long)` | 8-byte big-endian. |
| `Append(DateTimeOffset)` | UTC ticks, 8-byte big-endian (offset-stable). |
| `Append(byte[]?)` | Raw bytes, length-prefixed; `null` → null marker. |
| `Append(bool)` | Single `0x00` / `0x01` byte. |
| `AppendNull()` | Explicit null marker (distinct from empty string / empty array). |

### `IHashChainSealer<TEntry>` where `TEntry : IHashChainedEntry` (concrete: `HashChainSealer<TEntry>`)

| Method | Notes |
|---|---|
| `Seal(TEntry entry, TEntry? previous)` | Sets `Sequence` = `(previous?.Sequence ?? 0) + 1`, `PreviousHash` = `previous?.Hash ?? []`, `Hash` = hash of (`SchemeVersion`, `Sequence`, `PreviousHash`, then canonicalizer output). Throws `ArgumentNullException` on a null `entry`. |

### `IHashChainVerifier<in TEntry>` where `TEntry : IHashChainedEntry` (concrete: `HashChainVerifier<TEntry>`)

| Method | Returns | Notes |
|---|---|---|
| `Verify(IEnumerable<TEntry> orderedEntries)` | `HashChainVerificationResult` | Walks once in order; returns the first break or `Intact`. Throws `ArgumentNullException` on a null collection or null element. |

### `HashChainVerificationResult` (record)

| Member | Type | Notes |
|---|---|---|
| `IsIntact` | `bool` | `true` ⇔ `Reason == None`. |
| `Reason` | `HashChainBreakReason` | `None` · `HashMismatch` · `BrokenLink` · `SequenceGap`. |
| `BrokenSequence` | `long?` | Sequence of the first broken entry; `null` when intact. |
| `BrokenIndex` | `int?` | Zero-based position in the collection; `null` when intact. |
| `Intact` (static) | `HashChainVerificationResult` | The intact singleton. |
| `Broken(reason, brokenSequence, brokenIndex)` (static) | `HashChainVerificationResult` | Builds a break result. |

### `HashChainOptions`

| Member | Type | Default |
|---|---|---|
| `Algorithm` | `HashChainAlgorithm` | `Sha256` |

`HashChainAlgorithm`: `Sha256` (32-byte) · `Sha384` (48-byte) · `Sha512` (64-byte).

### `HashChainServiceCollectionExtensions`

| Method | Returns | Notes |
|---|---|---|
| `AddHashChain<TEntry, TCanonicalizer>(this IServiceCollection, Action<HashChainOptions>? = null)` | `IServiceCollection` | Registers canonicalizer + sealer + verifier as singletons (`TryAdd`). |
| `AddHashChain<TEntry>(this IServiceCollection, IChainedEntryCanonicalizer<TEntry>, Action<HashChainOptions>? = null)` | `IServiceCollection` | Same, with a pre-built canonicalizer instance. |

## Versioning a field-set change

```csharp
// v1 hashed (timestamp, actor). v2 also hashes detail.
public sealed class AuditEntryCanonicalizerV2 : IChainedEntryCanonicalizer<AuditEntry>
{
    public int SchemeVersion => 2;
    public void Write(AuditEntry e, ICanonicalPayloadBuilder b) =>
        b.Append(e.TimestampUtc).Append(e.Actor).Append(e.Detail);
}
```

`SchemeVersion` is hashed, so v1 and v2 entries never collide. Verify each segment with the canonicalizer that sealed it.

## See also

- Standard: [`HashChain.standard.md`](./HashChain.standard.md)
- Folder lead: [`audit.md`](./audit.md)
- Underlying API: [`System.Security.Cryptography.SHA256`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.sha256)
