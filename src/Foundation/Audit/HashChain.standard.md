# HashChain — standard

*Last updated: 2026-06-24*

> **Behavioral contract.** RFC 2119 (MUST / SHOULD / MAY). Changes only when the *contract* does, not when implementation moves.

## Purpose

Provide tamper-evident integrity over an ordered, append-only set of consumer records: each entry's hash binds its own fields plus the prior entry's hash, so altering, inserting, removing a non-tail entry, or reordering breaks the chain at a detectable point (tail truncation is a non-goal — see below). The SDK owns the chain mechanics; the consumer owns the field set.

## Canonical payload

- The encoder **MUST** be unambiguous: every appended field **MUST** carry a type tag and an explicit length, so no value can shift a field boundary or alias an adjacent field.
- A `null` **MUST** be encoded distinctly from an empty string and from an empty byte array.
- `DateTimeOffset` **MUST** be normalized to UTC before encoding, so the same instant hashes identically regardless of stored offset.
- The encoding of a given field tuple **MUST** be deterministic and stable across processes and runs.

## Sealing

- `Seal` **MUST** set `Sequence` to `previous.Sequence + 1`, or `1` when `previous` is `null` (genesis).
- `Seal` **MUST** set `PreviousHash` to `previous.Hash`, or an empty array when `previous` is `null`.
- The hashed payload **MUST** begin with the SDK-owned fields — scheme version, then sequence, then previous hash — **before** any consumer field, so a consumer canonicalizer **MUST NOT** be able to omit a chain field or weaken the proof.
- After the chain fields, `Seal` **MUST** append exactly the consumer canonicalizer's output, in the order the canonicalizer wrote it.
- `Seal` **MUST NOT** read or require any persistence; it operates purely on the two in-memory entries.
- `Seal` **MUST** throw on a `null` entry.

## Verification

- `Verify` **MUST** process entries in the supplied order and assume that order is ascending by sequence.
- For each entry, `Verify` **MUST** check, in this order: the sequence advances by exactly one from its predecessor (first expected `1`); the `PreviousHash` equals the predecessor's `Hash` (empty for the first); the stored `Hash` equals the hash recomputed from the same payload `Seal` would produce.
- On the first failed check, `Verify` **MUST** return a result naming the reason (`SequenceGap` / `BrokenLink` / `HashMismatch`) and the offending entry's sequence and zero-based index, and **MUST** stop.
- An empty collection **MUST** verify as intact.
- `Verify` **MUST** use a full, length-independent comparison for hash bytes (no early-out that leaks position); it **MUST NOT** mutate any entry.
- `Verify` **MUST** throw on a `null` collection or a `null` element.

## Scheme version

- The scheme version **MUST** be part of the hashed payload, so entries sealed under different field sets never collide.
- A consumer changing which fields are hashed **SHOULD** bump `SchemeVersion` and keep the prior canonicalizer available, then verify each historical segment with the canonicalizer that sealed it.
- The SDK **MUST NOT** assume a single global version — version selection is the consumer's responsibility across mixed-version history. A given verifier instance carries one canonicalizer; mixed-version history is verified segment-by-segment, one verifier per version.

## Algorithm

- The default algorithm **MUST** be SHA-256, matching the reference vault chain.
- The algorithm **MUST** be host-selectable via `HashChainOptions`; SHA-384 and SHA-512 **MUST** be supported.
- Changing the algorithm for an existing chain invalidates prior hashes; the SDK **MUST NOT** silently migrate — the host owns that choice per chain.

## Registration

- `AddHashChain` **MUST** register the sealer and verifier for the entry type, plus the consumer canonicalizer.
- Registrations **MUST** be additive/idempotent (`TryAdd`) so a repeated call does not double-register.
- Defaults **MUST** be safe with no further config (SHA-256, options bound).

## Non-goals

- Does **not** persist, query, order, or load entries — the consumer owns storage and supplies entries already ordered.
- Does **not** define domain fields, an entity base class, table names, or EF mappings.
- Does **not** sign, encrypt, or key the hash (no HMAC/MAC) — it detects tampering, it does not by itself prove *who* tampered against an attacker who can rewrite the whole chain. Pair with a signed checkpoint or external anchoring for that.
- Does **not** detect **tail truncation** (dropping entries from the end) — the chain has no length or head anchor, so a well-linked shorter prefix still verifies as intact. Record the expected length/head out-of-band, or pair with a signed checkpoint, to close it.
- Does **not** authenticate callers or authorize the audited operation.

## See also

- Spec: [`HashChain.spec.md`](./HashChain.spec.md)
- Folder lead: [`audit.md`](./audit.md)
- Underlying API: [`System.Security.Cryptography.SHA256`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.sha256)
