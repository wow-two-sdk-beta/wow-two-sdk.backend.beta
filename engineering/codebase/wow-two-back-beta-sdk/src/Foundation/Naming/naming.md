# WoW.Two.Sdk.Backend.Beta.Naming

> One casing authority for the whole SDK — DB columns, enum labels, JSON keys, SQL identifiers all derive from here. Zero dependencies.

Namespace: `WoW.Two.Sdk.Backend.Beta.Naming`

## Why

The same "snake_case" decision is otherwise wired through four independent mechanisms (EF naming convention, Dapper column matching, enum labels, JSON policy) that only coincidentally agree. This package is the single source so they can't drift, and so switching/adding a style is one change.

## Pieces

| Type | Role |
|---|---|
| `CaseStyle` | enum: `Snake · ScreamingSnake · Kebab · Train · Camel · Pascal · Sentence · Title · Lower · Upper` |
| `WordTokenizer.Split(s)` | identifier → lowercased words; handles acronym runs + digit boundaries |
| `CaseConverter.ToCase(s, style)` | the converter; plus `ToSnakeCase/ToCamelCase/ToPascalCase/ToKebabCase` shorthands |
| `CaseStringExtensions` | `"OrderLine".ToSnakeCase()` extension form |
| `EnumNameConverter<TEnum>` | **reversible** enum ↔ label (`ToLabel` / `Parse` / `TryParse`) |

## Tokenizer edge cases (what Haven's converter got wrong)

```
HTTPStatusCode  → http status code     (acronym run → word)
orderLine2      → order line 2         (digit boundary)
order_line-item → order line item      (mixed existing separators)
IOStream        → io stream
```

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Naming;

"OrderLineItem".ToSnakeCase();              // order_line_item
CaseConverter.ToCase("user_id", CaseStyle.Pascal);   // UserId
CaseConverter.ToCase("APIKey", CaseStyle.Sentence);  // Api key

// Reversible enums — no underscore-stripping fallback needed
EnumNameConverter<AiProvider>.ToLabel(AiProvider.OpenAi, CaseStyle.Snake); // open_ai
EnumNameConverter<AiProvider>.Parse("open_ai", CaseStyle.Snake);          // AiProvider.OpenAi
```

## Round-trip guarantee

`EnumNameConverter` builds its reverse map from the enum's own members, so `Parse(ToLabel(v)) == v` for every member — even `OpenAi`/`IOStream`-style names that don't survive a naive `ToSnakeCase → Enum.Parse`.

## Storage vs display

`Snake / Camel / Pascal / Kebab` are **storage** styles (columns, JSON keys, enum labels). `Sentence / Title` are **display** styles — fine for server-rendered labels and exports, but don't feed them into column naming.
