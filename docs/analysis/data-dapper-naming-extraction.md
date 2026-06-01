# Data layer — Dapper + naming/casing extraction (Haven → SDK)

*Status: brainstorm / draft · 2026-05-31*

Mining Haven's real-world Dapper + Postgres data layer for what belongs in the SDK's P3 `data` packages. The recurring theme is **casing**: every storage concern (columns, enum labels, JSON keys) is snake_case, wired four different ways. A single casing authority is the unlock for most of the extractions below.

Haven source paths are relative to `…/workbench/ventures/10x-ven-haven/platform/src/backend-services/`.

---

## 1. What Haven's Dapper layer actually is

Two Dapper consumers — `Haven.Common.Persistence` (shared queries/commands) and per-service `Infrastructure/.../Services` read models. Everything rides Npgsql + a shared `NpgsqlDataSource`.

| Piece | Haven file | Role |
|---|---|---|
| Connection factory | `Haven.Common/Persistence/DbConnectionFactory.cs` | `Create()` / `CreateOpenAsync()` off the shared `NpgsqlDataSource` (pooled, singleton) |
| Type handlers | `Haven.Common.Persistence/Extensions/DapperTypeHandlers.cs` | `ListTypeHandler<T>` (`List<T>` ↔ `T[]`), `DateOnlyTypeHandler` (`DateOnly` ↔ `DateTime`) |
| Convention switch | `Haven.Common.Persistence/Extensions/ServiceCollectionExtensions.cs:26` | `DefaultTypeMap.MatchNamesWithUnderscores = true` |
| Query objects | `Haven.Common.Persistence/Queries/{UnclassifiedListingsQuery,SupplySourceUrlCommands}.cs` | Hand-written SQL; chunked `INSERT … ON CONFLICT DO NOTHING`; `= ANY(@array)` for enum arrays |
| Read models | `Haven.Channels.Supply/Infrastructure/Listings/Core/Services/ListingQueryService.cs`, `…/Stats/Services/ChannelPipelineStatsService.cs` | Paginated filter/sort, multi-query + in-memory stitch, `FILTER (WHERE …)` rollups |
| Identifier helpers | `…/Listings/Core/Services/ListingFilterWhereBuilder.cs:16-38`, `Haven.Common.Domain/Common/Extensions/DatabaseSchemaExtensions.cs` | `Col(prop,alias)`, `Par(prop)`, `Tab<T>()`, `PgEnum<T>`, `PgType<T>`, `AddEnumClause<T>` |
| Table-name contract | `Haven.Common.Domain/Common/Entities/IEntity.cs` | `static abstract string TableName` — each of 16 entities returns a snake_case literal |

**Enums reach SQL three different ways** (none via a Dapper type handler):
- **Npgsql-native** — `MapEnum<T>(name.ToSnakeCase())` at driver level (params + arrays).
- **`::cast` param** — `Par()` sends the label as text, SQL casts `@p::contact_type` (`WhereBuilder:29-38`). Needed because Dapper boxes enums as `int`, which PG rejects.
- **Raw string interpolation** — `'{pendingVal}'` baked into the SQL string (`ChannelPipelineStatsService:46,55,148`).

## 2. Already extracted to the SDK (done — don't redo)

`src/data/dapper/` already mirrors Haven's primitives:

| SDK type | = Haven's |
|---|---|
| `IDbConnectionFactory` + `DataSourceConnectionFactory` | `DbConnectionFactory` (now provider-agnostic on `DbDataSource`) |
| `ListTypeHandler<T>`, `DateOnlyTypeHandler` | same |
| `AddDapperConventions()` | `MatchNamesWithUnderscores` + both handlers, idempotent |

Connection + type handlers + snake-read are **done**. Casing, identifier building, enum-to-SQL, and table/column resolution are **not**.

## 3. Gaps / extraction opportunities

### 3a. A casing engine — the core unlock
Haven casing = one hand-rolled file (`Haven.Common/Extensions/StringFormattingExtensions.cs`): only `ToSnakeCase` + `ToCamelCase`, called **121×** across 10 files. No sentence/kebab/pascal/title/screaming. The converter is naive — splits on uppercase only:
- `HTTPStatus` → `h_t_t_p_status` (acronym runs break). Haven silently works around this by **never using all-caps acronyms** — it writes `Usd`, `Ai`, `Olx`, `Cm2`. The naming discipline is *imposed by the converter's weakness*, not chosen.
- Not reversible: `SnakeCaseEnumConverter.cs:21` needs an underscore-stripping fallback (`open_ai`→`openai`→`OpenAi`) because `ToSnakeCase`→`Enum.Parse` doesn't round-trip.

### 3b. SQL identifier helpers — 3 incompatible copies
`Col`/`Par`/`Tab` exist in `DatabaseSchemaExtensions` (no alias) **and** `ListingFilterWhereBuilder` (with alias) — and `ChannelPipelineStatsService` ignores both, re-inlining `nameof(x).ToSnakeCase()` 16×. One typed identifier builder collapses all three.

### 3c. Table-name resolution
`IEntity.TableName` (static abstract); 16 entities hardcode snake_case literals. `Tab<T>()`/`Entity.TableName` read it for `FROM`/`JOIN`. Extract as an SDK contract; optional convention default (`type → pluralize → snake`) with per-entity override.

### 3d. Enum mapping — duplicated registry
The ~19-enum list is written **twice** — `MapEnums(builder)` and the `UseNpgsql` block (`ServiceCollectionExtensions.cs:46-112`) — with the comment *"Must mirror the MapEnum calls above."* Manual sync hazard. A reflection-based `MapEnums(assembly, style)` registers once.

### 3e. JSON options — tripled
Identical `JsonSerializerOptions` (SnakeCaseLower + `JsonStringEnumConverter(SnakeCaseLower)`) defined three times: `JsonbConverter`, `JsonbValueComparer`, `DbJsonOptions`. One shared preset.

### 3f. Generic EF converters — straight lifts
`SnakeCaseEnumConverter<TEnum>`, `JsonbConverter<T>`, `JsonbValueComparer<T>` are fully generic and reusable.

## 4. The casing-wiring problem

The same "snake_case" decision is wired through **four independent mechanisms** that only coincidentally agree:

| Layer | Mechanism | Source of casing |
|---|---|---|
| EF columns | `.UseSnakeCaseNamingConvention()` | EFCore.NamingConventions (own algo) |
| Dapper **read** | `MatchNamesWithUnderscores` | Dapper strips `_` when matching — *not* snake-aware |
| Dapper **write** | `Col()` → `ToSnakeCase()` | hand-rolled converter |
| Npgsql enum labels | `MapEnum(name.ToSnakeCase())` | hand-rolled converter |
| JSON / JSONB | `JsonNamingPolicy.SnakeCaseLower` | .NET built-in (a *third* snake impl) |

Two consequences to design around:
- **Read ≠ write.** Read path (`MatchNamesWithUnderscores`) and write path (`ToSnakeCase`) are different code with different edge cases. They agree today only because the schema avoids acronyms.
- **Three snake_case implementations** (hand-rolled, EFCore.NamingConventions, `JsonNamingPolicy`) can disagree on digits/acronyms. Switching casing (or adding a second style) means editing all four sites in lockstep.

**Fix:** a single casing authority every layer reads from — set the style once; EF naming + Dapper `Col` + enum labels + JSON policy all derive from it.

## 5. Sentence case + other casings

A `CaseStyle` enum + robust tokenizer (split on case boundaries **and** digits/underscores/hyphens/spaces, with acronym-run handling), reversible so enum round-trips drop the fallback hack:

`Snake · ScreamingSnake · Camel · Pascal · Kebab · Train · Sentence · Title · Lower · Upper`

Key distinction to bake in — **storage casing vs display casing**:
- snake/camel/pascal/kebab → **storage** (DB columns, JSON keys, enum labels) — what the data layer wires.
- **Sentence / Title** → **display** (today the frontend's `EnumDef` job; `EnumOption.cs` deliberately punts labels to the frontend). Useful for server-rendered labels/exports, but must not leak into column naming.

**One decision first:** `Humanizer.Core` is *already* a dependency (`foundation/time`). It ships `Underscore`/`Pascalize`/`Camelize`/`Kebaberize`/`Titleize`/`Humanize` (sentence) **and `Pluralize`** — exactly what table-name derivation (3c) needs. So: lean the casing engine on Humanizer, or hand-roll a focused, dependency-light converter? Humanizer is already in the graph → leaning on it adds nothing and covers casing + pluralization + the date/number phrases.

## 6. Recommended SDK shape

- **`Foundation.Naming`** (new, foundation tier) — `CaseStyle` enum, `ToCase(style)` + reversible `ParseFrom(style)`, tokenizer. Wraps Humanizer or hand-rolls. *The unlock for everything else.*
- **`Data.Dapper`** (extend) — `SqlNaming`/`ISqlIdentifierConvention` (→ `Col`/`Par`/`Table`), a string-backed `EnumTypeHandler<T>` for non-Npgsql providers, `Tab<TEntity>()` reading the entity contract. Decide: Npgsql-native enums vs portable string handler (SqlServer/Sqlite have no native enum).
- **`Data.Abstractions`** (extend) — add `IHasTableName` (`static abstract TableName`) beside `IEntity`/`IKeyedEntity`.
- **`Data.EntityFrameworkCore`** (extend) — port `SnakeCaseEnumConverter<T>`, `JsonbConverter<T>`, `JsonbValueComparer<T>`, a shared DB-JSON options preset, and reflection `MapEnums(assembly, style)` to kill the double registry.

## 7. Correctness smells (found in Haven)

- Double enum registry must be hand-synced (`ServiceCollectionExtensions.cs:46-112`).
- Enum labels string-interpolated into SQL (`ChannelPipelineStatsService:55,148`) — safe only because values are enum-derived; the `::cast` param path is the cleaner default.
- Non-reversible casing forces the enum-parse fallback (`SnakeCaseEnumConverter.cs:21`) and an acronym-free naming rule across the whole schema.

## 8. Open questions (for brainstorm)

1. **Humanizer vs hand-roll** for the casing engine — lean on the existing dep, or keep Foundation dependency-free and hand-roll a robust tokenizer?
2. **Enum-to-SQL in Dapper** — commit to Npgsql-native `MapEnum` (portable-unfriendly) or ship a string-backed `EnumTypeHandler<T>` as the cross-provider default?
3. **Table names** — convention-derived (`Pluralize`+`snake`) with override, or keep Haven's explicit `static abstract TableName` literal per entity?
4. **Where does casing live** — `Foundation.Naming` (general string utility) vs a data-only naming helper? General is more reusable but widens Foundation's surface.
5. **Identifier builder ergonomics** — string-based `Col("PropName")` vs expression-based `Col<TEntity>(e => e.PropName)` (compile-safe, refactor-friendly, but heavier).
