# Component registry — idea capture, research + verdict

*2026-07-25. Owner's idea recorded verbatim, then researched. Empirical claims below were measured on .NET 10 in a scratch probe, not recalled — the measured ones are tagged **[m]**.*

## Verdict

**Build narrower.** The idea as stated is three features welded together. Two of the three are already free, and one of those two ships in the BCL today.

| Pillar | Obtainable post-hoc? | Verdict |
|---|---|---|
| **Positive space** — what got registered, contract → impl, lifetime | yes, and Microsoft already emits it | **don't build** |
| **Effective configuration** — bound options, redacted | yes — ~30 lines, no cooperation from any `Add*` | **don't build into the SDK.** A consumer-side snippet, not a vector |
| **Negative space** — `TryAdd`-skipped, `Replace`d, plug-in slot never drained | **no — unobtainable by any post-hoc means** | **build this, and only this** |

The negative space is the whole product. It is also the only part that cannot be recovered later: a `TryAdd` that skipped leaves **no trace anywhere** in the built container. If it isn't recorded at the moment it happens, the information is gone.

Ship **`Ledger` + drift assertions** (§10). Not a registry, not an endpoint, not a dashboard.

---

## 1. The idea as captured

> logging of the components loaded with what configuration in the application runtime. this helps us to understand what was loaded and what's not from the logs of a running app (any env).
>
> why? — let's say we have `IDoSmth` interface and a class implements it but it doesn't take effect until the component / service that works with it is wired — until that it's just an interface. well how do we check how and which components are loaded? we could log each wired component in the logs as they register into the app host.

---

## 2. Why this is a real problem here, not a general nice-to-have

Measured over `src/`, test projects excluded:

| Fact | Count |
|---|---|
| distinct `Add*` extension-method names, shipping surface | **175** (178 incl. the `Testing.*` helper libs). The brief said ~40 — off by 4.4× |
| descriptor-adding call sites | 448 |
| `TryAdd*` call sites — each a silent-skip candidate | **184** |
| `Replace` call sites in shipping code | 12 |
| `RemoveAll` sites in shipping code | 2 |
| `Options` classes | 59 |
| options-binding sites (`AddOptions<T>` / `Configure<T>`) | 44 |
| …of those, guarded by `ValidateOnStart` | **5** |

184 `TryAdd` sites is the number that matters. Every one is a slot where "the SDK's default lost to something already there" and "the SDK's default won because nothing else registered" are **indistinguishable at runtime**.

### The failure class, with the receipts

| # | Defect | Mechanism | Status |
|---|---|---|---|
| D5 | `AddPostgresPersistence` drops every `AddEfInterceptor` registration | it calls `services.AddDbContext<TContext>` with its own lambda that never enumerates `IEnumerable<IInterceptor>`; `AddEntityFrameworkCore` **does** (`EntityFrameworkCoreServiceCollectionExtensions.cs:66`) | live |
| — | `AddMessageSerializer<T>` used `Replace`, so a second serializer was silently unregisterable | `Replace` removes the **first** match; MS DI resolves the **last** | fixed — now positional |
| — | messaging adapters stripped the whole `wt-` namespace on send | second-level retry + DLQ redrive cap silently dead behind every broker | fixed — by a **contract test**, not a registry |

**The live one, and it is the argument for the whole feature.** `AddTenantRowStamping()` (`Tenancy/PerRow/TenantStampInterceptor.cs:72`) plugs in via `AddEfSaveChangesInterceptor<TenantStampInterceptor>`. Its XML doc promises *"every SDK-registered DbContext auto-stamps the current tenant"*. Under `AddPostgresPersistence` that is false.

```
AddTenancy(...) + AddTenantRowStamping() + AddPostgresPersistence<AppDbContext>()
  -> tenant stamping is registered, resolvable, and never invoked
```

Three green calls in the composition root, zero effect, no log line, no exception. Rows insert with a null tenant. **5 consumer apps call `AddPostgresPersistence`** (smart-qr ×2, drydock, secrets-vault, transcript-forge); none yet calls `AddTenantRowStamping`, so this is latent, not burning. It is latent only because nobody has turned tenancy on.

Note what the third defect says about the alternative: the header-stripping bug was killed by `Messaging.Tests/AdapterOwnedHeaderContract.cs`, a contract test asserted against every broker. **A test caught it. A registry would only have described it.** That constrains how much credit this idea can claim — see §9.

---

## 3. Prior art

### Spring Boot — `ConditionEvaluationReport` (the mature answer to exactly this question)

- Recording is a **side effect inside condition evaluation**, not a listener over the graph: `SpringBootCondition.matches()` calls `recordConditionEvaluation(source, condition, outcome)` during `@Configuration` parsing at context refresh.
- Report = `positiveMatches` · `negativeMatches` · `exclusions` · `unconditionalClasses`. Source granularity is class **or** `Class#beanMethod`.
- A negative match carries the reason: `notMatched: [{condition: "OnClassCondition", message: "@ConditionalOnClass did not find required class 'com.fasterxml.jackson.dataformat.xml.XmlMapper'"}]`.
- `unconditionalClasses` is **derived by subtraction** — import candidates minus everything that recorded a condition minus exclusions.
- **Always collected**, regardless of `debug` or actuator; retained as a singleton for the context's lifetime. No documented off switch.
- `--debug` prints `CONDITIONS EVALUATION REPORT` on `ContextRefreshedEvent` **and on `ApplicationFailedEvent`** — the crash path is where it earns its keep.
- Blind spots worth copying honestly: only conditions extending `SpringBootCondition` are recorded (`@Profile` is not); component-scan misses, un-imported configs, and constructor failures are invisible.
- **Bean overriding appears in no report at all.** With `spring.main.allow-bean-definition-overriding=true` the override is silent; `/actuator/beans` shows only the winner. Even the mature answer does not solve the override case — which is over half of what we need.

**Redaction — steal this verbatim.** `Sanitizer.sanitize(data, showUnsanitized)` returns `******` *before any custom function runs*. `management.endpoint.{env,configprops}.show-values` defaults to `never`. `management.endpoints.web.exposure.include` defaults to `health` — `conditions`/`beans`/`configprops`/`env` are not HTTP-reachable unless opted in. The `SanitizingFunction` key heuristics are documented as **non-exhaustive**. Fail-closed by construction, exposure gated on role, never on key naming.

### Quarkus — build-time augmentation

- **No condition report, because there is no runtime question.** ArC resolves and validates the bean graph at build time; unsatisfied injection fails the **build**, not startup.
- `@IfBuildProperty` / `@IfBuildProfile` are build-time only — docs say runtime properties have *"absolutely no effect"*.
- Unused beans are removed at build (`quarkus.arc.remove-unused-beans=all`); removals visible at DEBUG on `io.quarkus.arc.processor`, in Dev UI, and at `GET /q/arc/removed-beans`.
- All of `/q/arc/*` and `/q/dev-ui` are **dev-mode only** — trivially production-safe, and zero production introspection.

### Micronaut — compile-time DI

- **No conditions report, no conditions endpoint, no startup report.** The only equivalent is logging: `io.micronaut.context.condition` at DEBUG emits `"Bean [{}] will not be loaded due to failing conditions:"` + one `* {}` per failure. Push-to-log, zero retention — miss it and you rerun the app.
- Correct a common misreading: `@Requires` is evaluated at **runtime**; AOT generates the `BeanDefinition` and metadata, the decision still runs against the live environment.
- **The structural insight that transfers to .NET:** Micronaut cannot produce a positive/negative partition because there is no classpath scan and no auto-config import list — *there is no candidate set to diff against.* Same is true of `IServiceCollection`. See §5.

### .NET

| Thing | What it actually gives | Limit |
|---|---|---|
| `IServiceCollection` enumeration | `ServiceType`, `Lifetime`, sometimes `ImplementationType` | **[m]** in a modest ASP.NET Core app, 239 descriptors, **14% expose no implementation type** (factory-registered) |
| `DependencyInjectionEventSource` | **[m]** `Microsoft-Extensions-DependencyInjection`, event 8 `ServiceProviderDescriptors` emits the **entire collection as chunked JSON** at build; also `ServiceProviderBuilt`, `CallSiteBuilt`, `ServiceResolved`, `ServiceRealizationFailed`, `ScopeDisposed` | positive space only; free when nobody listens; consume via `EventListener` in-proc or `dotnet-trace` out-of-proc |
| `IServiceProviderIsService` | "is there a descriptor for this type" | **[m]** true for closed generics, false for open (`IOpen<>`) |
| `ValidateOnBuild` | **[m]** catches ctor-injected missing deps + captive scoped-in-singleton | **[m]** misses factory lambdas and open generics entirely — and this SDK registers its plug-in slots via factories |
| `IConfigurationRoot.GetDebugView` | key = value + provenance provider per key | **[m]** masking overload confirmed: `GetDebugView(root, Func<ConfigurationDebugViewContext,string>)`, context = `{Path, Key, Value, ConfigurationProvider}` |
| Lamar `WhatDoIHave()` | closest .NET analogue — textual report of a running container's config; filters by assembly / service type / namespace / type name. `WhatDidIScan()` covers type-scanning specifically | positive space only. Docs do not claim it reports overridden or never-registered services. Actively maintained (last push 2026-05-29) — **not** archived, contrary to a common assumption |
| Scrutor | scanning + decoration | no registration diagnostics |
| Aspire | **resource**-level topology — projects, containers, executables defined in the AppHost, plus their config and health | tracks the distributed app at macro level; **does not** report which `Add*` ran *inside* a process. Wrong altitude for this problem |
| OTel resource attributes | `service.name` / `service.version` / `service.instance.id` | wrong vehicle: resource attrs are joined onto every signal; a component list there is unbounded-cardinality metadata |

**Nothing in .NET records the negative space.** Not the EventSource, not `ValidateOnBuild`, not Lamar. No NuGet package found that does either — the closest hits are registration *helpers* (`Scrutor`, `NetCore.AutoRegisterDi`, `ServiceCollector`), none of which report what they skipped. Treat that as "not found", not as proof of absence.

---

## 4. Measured DI semantics the design rests on

All **[m]**, .NET 10.

```
TryAdd that SKIPPED   : COUNT | READ[0] | READ[0]                          <- no Add. no record. anywhere.
TryAdd that SUCCEEDED : COUNT | READ[0] | ADD IDisposable -> factory
Replace               : COUNT | READ[0] | READ[0] | REMOVEAT[0] IThing -> ThingA | ADD IThing -> ThingB
RemoveAll             : COUNT | READ[0] | REMOVEAT[0] IThing -> ThingB
```

- **A `TryAdd` skip is invisible.** It is a scan that ends in nothing. A collection decorator sees index reads with no `Add` — indistinguishable from any other code scanning the collection (this SDK's own `LastIndexOfMessageSerializer` scans exactly that way). Detectable only as a fragile heuristic, never as a contract.
- **`Replace` / `RemoveAll` are cleanly observable** — the victim descriptor *and* its replacement both cross the interface.
- Factory descriptors are opaque: `ImplementationType` is `null`; `ImplementationFactory.Method.DeclaringType` is the compiler closure (`Probe+<>c`) and `ReturnType` is the *interface*. `AddEfInterceptor<T>` registers `AddSingleton<IInterceptor>(sp => sp.GetRequiredService<T>())` — so post-hoc enumeration cannot even name the interceptor. **Enumeration fails exactly on D5's subject matter.**
- Resolution: singular = **last wins**; `IEnumerable<T>` = all, in registration order.
- `TryAddEnumerable` on a factory throws `ArgumentException: Implementation type cannot be 'IThing' because it is indistinguishable from other services registered for 'IThing'`.
- Trap for anyone writing the naive dump: `KeyedImplementationType` / `KeyedImplementationFactory` **throw** `InvalidOperationException` on a non-keyed descriptor. Guard on `IsKeyedService`.
- **`builder.Services` is get-only** on both `WebApplicationBuilder` and `HostApplicationBuilder` (`CanWrite=False`). A transparent recording decorator **cannot be swapped in** under the standard host. This kills option B in §6 outright.

---

## 5. What to capture

### 5a. Positive space — don't build

`ServiceProviderDescriptors` already emits it. A consumer wanting more gets it in three lines:

```csharp
foreach (var d in builder.Services)
    log.LogInformation("{Svc} -> {Impl} ({Life})", d.ServiceType,
        (d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType)?.Name ?? "factory", d.Lifetime);
```

It answers the owner's literal question — "which components are loaded" — and it is worth **almost nothing**, because 14% of rows say `factory` and those are disproportionately the interesting ones.

### 5b. Effective configuration — don't build into the SDK

Fully solvable post-hoc, no cooperation from any `Add*`. **[m]** verified end to end:

- discover every bound `TOptions` from the `IConfigureOptions<T>` / `IPostConfigureOptions<T>` / `IValidateOptions<T>` descriptors already in the collection
- resolve `IOptions<T>` **after** build — that yields the *effective* value, config-bind and code `Configure` already folded in (probe: `Host` from config, `Port=587` from a later code override, both correct)
- redact by property-name convention

That is ~30 lines and it needs nothing from the 175 `Add*` methods. **The effective-configuration pillar does not justify instrumenting the SDK.**

### 5c. Negative space — the product

Four questions, none answerable post-hoc:

| Question | Recoverable later? |
|---|---|
| which `Add*` ran | no — a descriptor does not know its caller |
| what did a `TryAdd` **skip**, and who held the slot | **no — leaves no trace at all** |
| what did a `Replace` displace | only if observed as it happened |
| a plug-in slot was filled but **never read** (D5) | no — requires knowing the slot exists |

The fourth is the one that bites, and it is not a DI question. `IEnumerable<IInterceptor>` had entries; the consumer never enumerated it. No container can see that. It needs a **declared expectation**: "this slot exists, and someone must drain it."

**This is where Micronaut's structural point lands.** Spring can partition positive/negative because auto-configuration ships an explicit candidate list to diff against. `IServiceCollection` has no candidate set — so a .NET conditions report is impossible *unless the SDK declares its own expectations*. That declaration is the actual deliverable, and it is authored, not derived.

---

## 6. Where it hooks

| Option | Gets negative space? | Cost | Verdict |
|---|---|---|---|
| **A. SDK-internal recorder** — each `Add*` reports what it did | yes, and it is the only option that does | 175 methods × 1–3 lines | **pick** |
| **B. Recording `IServiceCollection` decorator** | partially — `Replace`/`RemoveAll` yes, `TryAdd` skips no | zero consumer effort… | **impossible** — **[m]** `builder.Services` is get-only |
| **C. Post-hoc reflection at build time** | no | ~30 lines total | already free (§5a/§5b) — do this for the positive half |
| **D. `IHostedService` that dumps at startup** | no — it only reads C's data later | trivial | an *output* choice, not a capture mechanism |

A and D compose: **A records, D flushes.** They are not alternatives.

Anticipating the objection to B: `UseServiceProviderFactory` does hand you the collection via `CreateBuilder(IServiceCollection)` — but that fires *after* every `Add*` has run. You receive the final state, which is option C, not the sequence. The ordering information is already gone by then.

Two constraints force the shape:

- **You cannot resolve `ILogger<T>` during `ConfigureServices`** — the provider isn't built yet. Serilog's static bootstrap logger is reachable (this SDK calls `UseSerilogConventional`), but writing there means the real sinks, enrichers and log level aren't applied yet. Either way the recorder must buffer in the collection and flush after build — exactly Spring's shape: record at evaluation, log at `ContextRefreshedEvent`.
- **The buffer needs no new machinery.** `GetOrAdd{X}(IServiceCollection)` — scan for a singleton instance, create and register if absent — is already the house idiom, used twice in `MessagingServiceCollectionExtensions` (`MessageTypeRegistry`, handler registry). The ledger is a third instance of a pattern already in the codebase.

---

## 7. Output shape

| Sink | Verdict |
|---|---|
| **structured log at boot** | yes — one summary event, plus one warning per anomaly. Matches the owner's ask ("from the logs of a running app, any env") |
| **`/diagnostics/components` endpoint** | **no.** New authn/authz surface, a second redaction path, and it answers a question you ask once per deploy. Spring defaults this endpoint *off* over HTTP; that default is the tell |
| **startup summary table** | dev only, behind the environment check |
| **OTel resource attribute** | no — wrong vehicle, unbounded cardinality on every signal |
| **fail-fast at boot** | yes, opt-in — the highest-value sink of all (§10) |

### Production safety — the hard rule

**Never log an options object.** 59 `Options` classes carry `ClientSecret`, `Password`, `ApiKey`, `ConnectionString` — `RabbitMqOptions.ConnectionString` literally defaults to `amqp://guest:guest@localhost:5672/`, credentials inline. A generic "dump the bound options" feature is a credential-exfiltration feature with a diagnostics label on it.

Copy Spring's posture exactly:

- **fail closed** — redacted is the default; values are opt-in, never opt-out
- **short-circuit before the custom hook runs**, so a buggy redactor cannot leak
- **allowlist, never denylist.** A `Password`/`Secret`/`Key` name-match denylist misses `Dsn`, `Sas`, `Token`, `Uri`, `WebhookUrl` — and Spring's own docs call their heuristics non-exhaustive
- prefer **shape over value**: `ConnectionString: set (len 84, host=db.internal)` beats any redaction of the string itself
- connection strings never appear, redacted or not

---

## 8. Cost

**[m]** Measured, .NET 10:

| Operation | Cost |
|---|---|
| enumerate + format 239 descriptors (modest real app) | **~530 µs** |
| enumerate + format 608 descriptors | ~790 µs |
| `new StackTrace(1, false)` + `GetMethod()` | **~1.5 µs** each → ~0.7 ms across all 448 SDK sites |
| `ServiceProviderDescriptors` EventSource when unsubscribed | free |

**Cost is a non-issue and should not shape the design.** Sub-millisecond, once, at boot. The only genuinely expensive option would be per-`Add*` stack capture for attribution — and at 0.7 ms total, even that is affordable. Prefer an explicit feature name over a stack capture anyway, for legibility, not for speed.

---

## 9. Honest counter-arguments

- **A test beats a report.** The header-stripping defect died to a contract test asserted against every broker. A registry would have *described* the wiring; the test *forbade* the regression. Where a defect class can be pinned by a test, write the test.
- **D5 is a bug, not a missing diagnostic.** The direct fix is one loop in `AddPostgresPersistence`, or better, deleting its bespoke `AddDbContext` call so it delegates to `AddEntityFrameworkCore`. Ship that regardless. A registry that reports a bug you could have fixed is worse than the fix.
- **The owner's literal ask is already free** and worth little — §5a.
- **This does not pay for itself on one product.** It pays across 5 consumer services and 175 registration methods, and only if the ledger is *asserted on*, not merely printed. A log line nobody greps has negative value: it is boot noise that implies coverage.
- **Scope discipline.** 175 methods × even 1 line is a diff across most of the SDK. Do not do that up front — see the staging in §10.

---

## 10. The narrowest version worth shipping

Not a registry. A **ledger plus assertions**, staged so each step is independently useful and each can be abandoned.

### Step 1 — `RegistrationLedger` + the two `TryAdd`/`Replace` wrappers *(the whole idea, ~150 lines)*

An SDK-internal ledger obtained via the existing `GetOrAdd` idiom, plus two internal helpers the SDK's own `Add*` methods call instead of the BCL ones:

```
services.SdkTryAddSingleton<IFoo, Foo>(feature: "AddMessaging")
   -> records: taken   { slot: IFoo, by: Foo, feature: AddMessaging }
   -> or:      SKIPPED { slot: IFoo, wanted: Foo, incumbent: <the descriptor that won>, feature: AddMessaging }

services.SdkReplace<IBar, Bar>(feature: "AddMessageTypeResolver")
   -> records: REPLACED { slot: IBar, displaced: DefaultBar, with: Bar, feature: AddMessageTypeResolver }
```

Two call-site changes, mechanical, and they capture the information that is otherwise destroyed. Roll out **only across `Data/`, `Messaging/`, `Identity/`, `Tenancy/`** — the four areas where every known defect lives. The rest stay untouched until they earn it.

### Step 2 — slot-drain assertions *(catches D5 — the actual live bug)*

Declare the plug-in slots and assert someone drains them:

```
services.DeclareSlot<IInterceptor>(drainedBy: "AddEntityFrameworkCore");
// AddPostgresPersistence never drains it -> boot warning, naming both sides:
// "IInterceptor has 1 registration (TenantStampInterceptor) and no consumer.
//  AddPostgresPersistence does not enumerate it. Tenant stamping is not running."
```

This is the piece with no prior art in .NET and the piece that would have caught D5, the serializer defect, and the tenancy hole. **If only one step ships, ship this one.**

### Step 3 — one boot log event + `ValidateComponents()`

- one structured `Information` event: counts by area, plus the full anomaly list (skips, replacements, undrained slots)
- one `Warning` per anomaly, each naming the losing feature and the incumbent — a grep target, not a wall
- `builder.ValidateComponents()` — opt-in, **throws** on any undrained slot or unexpected skip. This is what turns a log nobody reads into a gate. Model it on `ValidateOnStart`, and default it on in Development

### Explicitly not in scope

no HTTP endpoint · no options-value dump (shape only, §7) · no OTel resource attributes · no dashboard · no coverage of the `Add*` methods outside the four hot areas

### Do first, independent of all the above

Fix D5. One loop, or delete the bespoke `AddDbContext` in `AddPostgresPersistence` so it routes through `AddEntityFrameworkCore`. The ledger is how you find the *next* D5; it is not how you fix this one.
