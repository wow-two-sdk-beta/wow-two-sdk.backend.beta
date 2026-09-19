# Foundation.Audit

> **Hash-chain** primitives that check the consistency of supplied records: sequence, links and recomputed hashes. An intact result does not prove completeness or authenticity; tail truncation and a consistently rewritten chain can pass without a trusted external checkpoint. **Pure library**: no EF, no persistence, BCL-only.

The SDK locks **no domain field**. It owns only the chain (`Sequence`, `PreviousHash`, `Hash`); the consumer declares exactly which of its own fields are part of the proof via a canonicalizer. This is the canonical-payload seam.

## Why a canonicalizer (and not a `'|'`-joined string)

A naive `a|b|c` hash is ambiguous — `("a","b|c")` and `("a|b","c")` produce the same bytes, so a tamper can shift a field boundary undetected. The `ICanonicalPayloadBuilder` writes **type tag + length + bytes** per field, making the encoding injective over the field tuple.

## Parts

| Type | Role |
|---|---|
| `IHashChainedEntry` | The only shape the SDK knows — `Sequence` · `PreviousHash` · `Hash`. Your entry implements it. |
| `IChainedEntryCanonicalizer<TEntry>` | **You** write which domain fields are hashed, and in what order, plus a `SchemeVersion`. |
| `ICanonicalPayloadBuilder` / `CanonicalPayloadBuilder` | Unambiguous typed/length-prefixed encoder. |
| `IHashChainSealer<TEntry>` | `Seal(entry, previous)` — stamps `Sequence`, `PreviousHash`, `Hash` before persist. |
| `IHashChainValidator<TEntry>` | `Validate(orderedEntries)` — recomputes + checks links; returns the first break. |
| `HashChainOptions` / `HashChainAlgorithm` | Pluggable hash (SHA-256 default / 384 / 512). |

## Quick start

```csharp
// 1. Your entry carries the chain fields + whatever domain fields you choose.
public sealed class AuditEntry : IHashChainedEntry
{
    public long Sequence { get; set; }
    public byte[] PreviousHash { get; set; } = [];
    public byte[] Hash { get; set; } = [];
    public DateTimeOffset TimestampUtc { get; set; }
    public string Actor { get; set; } = "";
    public string? Detail { get; set; }
}

// 2. You declare which fields are hashed — the SDK names none.
public sealed class AuditEntryCanonicalizer : IChainedEntryCanonicalizer<AuditEntry>
{
    public int SchemeVersion => 1;
    public void Write(AuditEntry e, ICanonicalPayloadBuilder b) =>
        b.Append(e.TimestampUtc).Append(e.Actor).Append(e.Detail);
}

// 3. Register.
builder.Services.AddHashChain<AuditEntry, AuditEntryCanonicalizer>();      // SHA-256 default
// builder.Services.AddHashChain<AuditEntry, AuditEntryCanonicalizer>(o => o.Algorithm = HashChainAlgorithm.Sha512);

// 4. Append: seal against the last entry, then persist however you like.
sealer.Seal(entry, lastEntry);    // sets Sequence, PreviousHash, Hash

// 5. Audit: replay the chain.
var result = validator.Validate(entriesOrderedBySequence);
if (!result.IsIntact)
    log.Warning("Chain broke at sequence {Seq}: {Reason}", result.BrokenSequence, result.Reason);
```

The sealer always prepends `SchemeVersion + Sequence + PreviousHash` to the hashed payload, so a consumer **cannot** omit the chain fields and weaken tamper-evidence. A field-set change starts a new chain with a new `SchemeVersion`; retain the old canonicalizer for the prior chain. Each validator checks one chain from sequence 1 and an empty previous hash.

## See also

- Spec: [`HashChain.spec.md`](./HashChain.spec.md) — concrete API + snippets
- Standard: [`HashChain.standard.md`](./HashChain.standard.md) — RFC 2119 contract
- Reference impl this generalizes: `secrets-vault` `EfAuditLog` (`'|'`-delimited, EF-bound) — this module is the EF-free, ambiguity-free successor.
