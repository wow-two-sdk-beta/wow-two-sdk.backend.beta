# Data graph copying

Use `FastCloner.FastCloner.DeepClone(source)` when an owned, detached, in-memory data graph needs independent
mutable state. The SDK pins `FastCloner` `3.5.6` as a direct package dependency; call its runtime API directly.

```csharp
var candidate = FastCloner.FastCloner.DeepClone(current);
```

The supported data-graph contract preserves runtime subtypes, cycles, shared aliases, private/init/get-only
state, collection comparers and usable cloned hash keys. Mutating the clone must leave the original graph intact.

Use `with` for an intentionally shallow record variant. A deep copy preserves an entity's database key and is
a detached candidate or snapshot; it is not a new row or a replacement for a same-key tracked entity. Validate
the candidate where an operation accepts it.

Exclude service providers, EF contexts and proxies, streams, native handles, synchronization objects, callbacks
and other live resources. The runtime API does not rerun constructors or validate copied data. NativeAOT support
and the source-generated `FastDeepClone` API require separate verification.
