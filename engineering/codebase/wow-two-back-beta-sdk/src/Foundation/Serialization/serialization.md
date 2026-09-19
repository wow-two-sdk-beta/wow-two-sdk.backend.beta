# WoW.Two.Sdk.Backend.Beta.Foundation.Serialization

> Pre-built `JsonSerializerOptions` for the HTTP wire contract and independent stored documents.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Serialization
```

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Web.Json;

builder.Services.AddControllersWithSdkJson();

// Or use directly
var json = JsonSerializer.Serialize(model, JsonOptionsConstants.Default);
var pretty = JsonSerializer.Serialize(model, JsonOptionsConstants.Indented);
```

## Defaults

- camelCase property + dictionary keys
- Ignore null on writes
- Numbers can be read from strings, NaN/Infinity allowed
- Trailing commas allowed, comments skipped
- NodaTime types serialized via `NodaTime.Serialization.SystemTextJson`
- Enums serialized as camelCase strings; numeric values are rejected
- `TimeSpan` serialized as an ISO 8601 duration
- Relaxed JS escaping for cleaner output (XSS-safe per `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` semantics)

## Stored JSON

A column, a cache entry or a file holds a type under `StoredJsonConstants.Default`, never under the wire preset — the two
change on different schedules. The stored preset writes nulls (a `required` nullable member round-trips), reads metadata
in any order (jsonb reorders keys), and keeps camelCase string enums and NodaTime.

```csharp
// EF — the default is the stored preset
builder.Property(c => c.Content).HasJsonConversion().HasColumnType("jsonb");
```

A polymorphic base reaches the preset one of two ways:

- **on the type** — `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` + one `[JsonDerivedType]` per subtype;
  nothing to register, the compiler sees every subtype.
- **outside the type** — a `SubtypeRegistry<TBase, TKind>` beside the base binds each enum member to its subtype and
  proves the set complete; `StoredJsonOptionsFactory.Create(registry.ToJsonModifier())` creates the configured
  options in the owning composition scope, handed to `HasJsonConversion(options)`.

```csharp
var storedOptions = StoredJsonOptionsFactory.Create(
    CodeRuleValueObject.Subtypes.ToJsonModifier(),
    CodeContentValueObject.Subtypes.ToJsonModifier());

builder.Property(c => c.Content)
    .HasJsonConversion(storedOptions)
    .HasColumnType("jsonb");
```

Register a reusable custom format as a keyed profile. The registration accepts a pinned options instance or
the factory modifiers directly. Unknown keys fail explicitly.

```csharp
builder.Services.AddStoredJsonOptionsProfile(
    "code-content",
    CodeRuleValueObject.Subtypes.ToJsonModifier(),
    CodeContentValueObject.Subtypes.ToJsonModifier());

var storedOptions = serviceProvider.GetRequiredStoredJsonOptionsProfile("code-content");

builder.Property(c => c.Content)
    .HasJsonConversion(storedOptions)
    .HasColumnType("jsonb");
```

The same resolved instance drives EF writes, reads, equality, hashing and snapshots. Direct serialization uses
that instance too. A format profile is configuration; it does not require a per-type serializer wrapper or a
dedicated options-holder class.

A base carrying both declarations is checked when the registry is built — a token that disagrees fails at type load.
