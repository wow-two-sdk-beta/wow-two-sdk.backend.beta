# Mediator — CQRS + AppResult absorption

*Last updated: 2026-06-13*

> **Planning spec.** Absorb the per-product mediator wrapper (`*.Common/Mediator`, currently in haven + smart-qr) into the backend beta SDK so products consume the SDK mediator's CQRS layer + standard result, and delete their copy. Pairs with the `ApplicationResult → AppResult` rename.
>
> Source of truth for behavior the wrappers add: `conventions/development/backend/messaging/mediator.md` + `conventions/development/backend/foundation/result-pattern.md`. SDK code lives in `src/Mediator/`; ships in the `WoW.Two.Sdk.Backend.Beta` mono-lib.

## Problem

Two products carry a hand-rolled MediatR wrapper under their `Common/Mediator` folder. The two copies are **byte-for-byte identical** except the namespace (`Haven.Common.Mediator` vs `SmartQr.Common.Mediator`) and the registration method name (`AddHavenMediator` vs `AddSmartQrMediator`). It is a copy-paste convention, not a library — every new product clones it again, and the `ApplicationResult → AppResult` rename has to be applied N times.

The SDK already replaced raw MediatR with its own MIT, MediatR-API-compatible mediator (`src/Mediator/`), but it only ships the **primitive layer** (the `IRequest`/`ISender` surface). The product-flavored **CQRS layer** (query/command markers + handlers + the `SendAsync` facade) and the **standard result union** (`AppResult` + markers) are exactly the slice still living in the products. This spec moves that slice up into the SDK.

## What the product wrapper actually contains

Each `Common/Mediator` folder (haven + smart-qr, identical) is 13 files in two groups:

### Group A — CQRS layer (wraps the mediator)

| File | Type | Wraps / extends | Role |
|---|---|---|---|
| `IQuery.cs` | `IQuery<out TResult> : IRequest<TResult>` | MediatR `IRequest<T>` | read marker |
| `ICommand.cs` | `ICommand<out TResult> : IRequest<TResult>` + `ICommand : IRequest` | `IRequest<T>` / `IRequest` | write markers (value + void) |
| `IQueryHandler.cs` | `IQueryHandler<in TQuery,TResult> : IRequestHandler<TQuery,TResult> where TQuery : IQuery<TResult>` | `IRequestHandler<,>` | query handler |
| `ICommandHandler.cs` | `ICommandHandler<in TCommand,TResult>` + `ICommandHandler<in TCommand>` | `IRequestHandler<,>` / `IRequestHandler<>` | command handlers (value + void) |
| `IMediator.cs` | `IMediator` facade with 3 `SendAsync` overloads | — | dispatch surface products inject |
| `MediatRMediator.cs` | `internal sealed MediatRMediator(ISender sender) : IMediator` | MediatR `ISender` | adapter — the **only** MediatR-touching file |
| `MediatorExtensions.cs` | `AddHavenMediator` / `AddSmartQrMediator(params Assembly[])` | `AddMediatR(...)` + `AddScoped<IMediator,MediatRMediator>()` | DI registration |

The `IMediator` facade (note: same name, different shape from the SDK's `IMediator`):

```csharp
public interface IMediator
{
    Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task SendAsync(ICommand command, CancellationToken ct = default);
}
```

`MediatRMediator` just forwards every overload to `ISender.Send(...)`. **No pipeline behaviors, no logging, no validation** — the products add zero cross-cutting over raw MediatR. The wrapper's entire value is (a) the CQRS naming and (b) hiding MediatR behind a product-owned type so the dependency can be swapped (the doc-comment literally says *"Swap this to migrate off MediatR"* — which is precisely what this absorption does).

### Group B — result union (the AppResult target)

| File | Type | Role |
|---|---|---|
| `ApplicationResult.cs` | `abstract record ApplicationResult<TSuccess,TFailure>` (private ctor; nested `Success(TSuccess Data, IApplicationSuccessContext? Context)` + `Failure(TFailure Error, IApplicationFailureContext? Context)`) | the discriminated-union response |
| `ISuccessResult.cs` / `IFailureResult.cs` | empty marker interfaces | constrain `TSuccess` / `TFailure` |
| `IApplicationSuccessContext.cs` / `IApplicationFailureContext.cs` | empty marker interfaces | per-side optional context slot |

Constraints: `where TSuccess : ISuccessResult, where TFailure : IFailureResult`. This is verbatim the `result-pattern.md` model — only the symbol name differs (`ApplicationResult`, to be renamed `AppResult`). There is **no `.Match` method** today; controllers consume the union via raw pattern matching (haven `ExternalListingContentExtractionController` uses a `switch` expression; smart-qr `CodesController` uses `is`-pattern casts). `result-pattern.md` mandates `.Match` as the target — see [§ Add `.Match`](#new-add-match-onsuccess-onfailure).

## What the SDK already has (`src/Mediator/`)

| Concern | SDK type(s) | Notes |
|---|---|---|
| Request markers | `IRequest<TResponse>`, `IRequest` (= `IRequest<Unit>`), `IBaseRequest` | `Contracts.cs` — MediatR-shaped, **no MediatR dep** |
| Notification | `INotification`, `INotificationHandler<>` | fan-out |
| Handlers | `IRequestHandler<TRequest,TResponse>`, `IRequestHandler<TRequest>` | one per request |
| Pipeline | `IPipelineBehavior<TRequest,TResponse>`, `RequestHandlerDelegate<TResponse>` | open-generic |
| Surface | `ISender` (`Send<T>` / `Send`), `IPublisher` (`Publish<T>`), `IMediator : ISender, IPublisher` | **`Send`-named, not `SendAsync`** |
| Impl | `sealed Mediator(IServiceProvider) : IMediator` (`Mediator.cs`) | cached per-type dispatcher delegate; behaviors wrap handler |
| Void | `readonly record struct Unit` (`Value`, `Task`) | mirrors MediatR `Unit` |
| Registration | `AddMediator()` / `AddMediator(params Assembly[])`, `AddMediatorBehavior(typeof(B<,>))` | scans `IRequestHandler<,>` + `INotificationHandler<>` as `Transient`; `IMediator`/`ISender`/`IPublisher` via `TryAdd` |
| Behaviors (built-in) | `LoggingBehavior<,>`, `ValidationBehavior<,>` (uses SDK `IValidator<T>` @ `WoW.Two.Sdk.Backend.Beta.Validation`), `AuthorizationBehavior<,>` (`IRequireAuthorization`), `IdempotencyBehavior<,>` (`IIdempotent`) | each with an `AddMediator{X}Behavior()` extension; registration order = execution order |
| Docs | `Mediator.spec.md`, `Mediator.standard.md`, `mediator.md` | + per-behavior `*.md` |

So the SDK is strictly **richer** at the primitive layer than the wrapper (the products run MediatR with **no** behaviors; the SDK ships 4) — but it is **missing the CQRS-flavored facade and the result union** the products depend on.

## Gap list — SDK has vs needs to absorb the wrapper

| Wrapper mechanism | In SDK today? | Action |
|---|---|---|
| `IRequest<T>` / `IRequest` markers | ✅ `Contracts.cs` | reuse — wrapper's `IQuery`/`ICommand` rebase onto these |
| `IRequestHandler<,>` / `<>` | ✅ `Contracts.cs` | reuse — `IQueryHandler`/`ICommandHandler` rebase onto these |
| Mediator impl + DI scan + behaviors | ✅ richer than wrapper | reuse as-is; **drop** the wrapper's `AddMediatR` + `MediatRMediator` entirely (MediatR leaves the products) |
| `IQuery<out TResult>` | ❌ | **add** `src/Mediator/Cqrs/IQuery.cs` → `: IRequest<TResult>` |
| `ICommand<out TResult>` + `ICommand` | ❌ | **add** `src/Mediator/Cqrs/ICommand.cs` → `: IRequest<TResult>` / `: IRequest` |
| `IQueryHandler<in TQuery,TResult>` | ❌ | **add** `src/Mediator/Cqrs/IQueryHandler.cs` → `: IRequestHandler<TQuery,TResult> where TQuery : IQuery<TResult>` |
| `ICommandHandler<in TCommand,TResult>` + `<in TCommand>` | ❌ | **add** `src/Mediator/Cqrs/ICommandHandler.cs` |
| `SendAsync` facade (`IMediator` with 3 overloads) | ⚠️ name + style clash | **decide** (see § Decision 1) — either add `SendAsync` extension methods over `ISender`, or migrate products to the SDK's `Send`. **Recommend** thin `SendAsync` extension methods so product call-sites change only the namespace. |
| `AppResult<TSuccess,TFailure>` union | ❌ | **add** `src/Mediator/Result/AppResult.cs` — verbatim `ApplicationResult`, renamed |
| `ISuccessResult` / `IFailureResult` | ❌ | **add** `src/Mediator/Result/` (names unchanged) |
| `IApplicationSuccessContext` / `IApplicationFailureContext` | ❌ | **add** `src/Mediator/Result/` (names unchanged) |
| `.Match(onSuccess, onFailure)` | ❌ (not in any repo) | **add new** — mandated by `result-pattern.md`, replaces the `switch`/`is` consume (see § new) |
| `AddHavenMediator` / `AddSmartQrMediator` | ❌ (SDK has `AddMediator`) | **superseded** by `AddMediator(...)`; product call-site swaps the method name |

**Net new SDK code = Group A CQRS markers (4 files) + Group B result union (5 files) + `.Match` + a `SendAsync` decision.** Nothing in Group A's *plumbing* is needed — the SDK's `Mediator`/DI/behaviors already cover it; only the **markers + facade + result** move up.

## Mechanisms the SDK mediator MUST provide (target surface)

### Existing (keep) — primitive layer
`IRequest<T>` · `IRequest` · `INotification` · `IRequestHandler<,>` · `IRequestHandler<>` · `INotificationHandler<>` · `IPipelineBehavior<,>` · `ISender` · `IPublisher` · `IMediator` · `Unit` · `AddMediator(...)` · `AddMediatorBehavior(...)` · the 4 `AddMediator{X}Behavior()` behaviors.

### New — CQRS layer (`src/Mediator/Cqrs/`)

```csharp
public interface IQuery<out TResult> : IRequest<TResult>;

public interface ICommand<out TResult> : IRequest<TResult>;
public interface ICommand : IRequest;

public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;
```

These are pure marker refinements — the existing DI scan already registers them (it binds any closed `IRequestHandler<,>` / `IRequestHandler<>`, and `IQueryHandler`/`ICommandHandler` **are** those). **No scanner change needed.**

> **Note — `mediator.md` convention divergence.** The current `messaging/mediator.md` describes the *primitive* path only (plain `IRequest<TResponse>` use-cases, "the query/command distinction is the name + folder, not a separate marker"). The products use **explicit** `IQuery`/`ICommand` markers. Absorbing the wrapper makes the explicit markers first-class SDK types, so `mediator.md` should be updated to bless `IQuery`/`ICommand`/`IQueryHandler`/`ICommandHandler` as the SDK CQRS layer (resolving § Decision 2). Flag for the convention owner; not blocking the code.

### New — `SendAsync` facade (Decision 1)
**Recommended:** ship `SendAsync` as extension methods on `ISender` so existing product call-sites (`mediator.SendAsync(command, ct)`, `mediator.SendAsync(new XQuery{…}, ct)`) compile after only a `using` swap:

```csharp
public static class SenderCqrsExtensions
{
    public static Task<TResult> SendAsync<TResult>(this ISender sender, IQuery<TResult> query, CancellationToken ct = default)
        => sender.Send(query, ct);
    public static Task<TResult> SendAsync<TResult>(this ISender sender, ICommand<TResult> command, CancellationToken ct = default)
        => sender.Send(command, ct);
    public static Task SendAsync(this ISender sender, ICommand command, CancellationToken ct = default)
        => sender.Send(command, ct);
}
```

Products keep injecting `IMediator` (SDK's, which `: ISender`); `SendAsync` resolves via the extension. Alternative (heavier migration): drop `SendAsync`, rewrite all call-sites to `.Send(...)`. Choose the extension path unless we want products on the canonical `Send` name.

### New — result union (`src/Mediator/Result/`)

```csharp
public abstract record AppResult<TSuccess, TFailure>
    where TSuccess : ISuccessResult
    where TFailure : IFailureResult
{
    private AppResult() { }
    public sealed record Success(TSuccess Data, IApplicationSuccessContext? Context = null) : AppResult<TSuccess, TFailure>;
    public sealed record Failure(TFailure Error, IApplicationFailureContext? Context = null) : AppResult<TSuccess, TFailure>;
}

public interface ISuccessResult;
public interface IFailureResult;
public interface IApplicationSuccessContext;
public interface IApplicationFailureContext;
```

Identical to the products' `ApplicationResult` except the type name. Per `result-pattern.md`: **only the carrier symbol renames** (`ApplicationResult` → `AppResult`); the four markers keep their names.

### New — `.Match(onSuccess, onFailure)`
Mandated by `result-pattern.md` ("the standard, not yet the code") — add the collapse method so controllers stop hand-rolling `switch`/`is`:

```csharp
public static TOut Match<TSuccess, TFailure, TOut>(
    this AppResult<TSuccess, TFailure> result,
    Func<AppResult<TSuccess, TFailure>.Success, TOut> onSuccess,
    Func<AppResult<TSuccess, TFailure>.Failure, TOut> onFailure)
    where TSuccess : ISuccessResult
    where TFailure : IFailureResult
    => result switch
    {
        AppResult<TSuccess, TFailure>.Success s => onSuccess(s),
        AppResult<TSuccess, TFailure>.Failure f => onFailure(f),
        _ => throw new InvalidOperationException("Unreachable — AppResult is a closed union."),
    };
```

(Extension method or instance method — extension keeps the union a pure data record. Either is fine; pick one and document in `Mediator.spec.md`.)

### Open decisions

1. **`SendAsync` vs `Send`** — recommend `SendAsync` extension methods (above) to minimize product churn. Decide before migrating products.
2. **CQRS markers vs plain `IRequest`** — `mediator.md` currently prefers plain `IRequest` + name/folder convention; products use explicit `IQuery`/`ICommand`. Absorption ships the explicit markers; update `mediator.md` to match (or consciously keep both and document when to use which).
3. **`AppResult` lands in the mediator package, or a sibling `Result` slice?** Today it sits *inside* `Common/Mediator` and `result-pattern.md` says it "lives in each product's `Common/Mediator/`". Recommend `src/Mediator/Result/` (one slice, one import) so a single `using WoW.Two.Sdk.Backend.Beta.Mediator` brings both. If a non-mediator consumer ever needs `AppResult`, promote to `Foundation` later.
4. **`.Match` placement** — extension vs instance method (above).

## Target end-state

Per product (haven, smart-qr, and every future product the `create-repo` template stamps):

1. **Reference the SDK** — add `WoW.Two.Sdk.Backend.Beta` package ref (already present where the SDK is consumed; otherwise add).
2. **Delete `{Product}.Common/Mediator/`** — all 13 files (both Group A and Group B) removed; **MediatR package reference dropped** from the product (it now lives nowhere — the SDK mediator is MIT and MediatR-free).
3. **Swap registration** — `AddHavenMediator(asm)` / `AddSmartQrMediator(asm)` → `AddMediator(asm)`. Optionally chain `AddMediatorLoggingBehavior()` / `AddMediatorValidationBehavior()` (free upgrade — products currently run **no** behaviors).
4. **Swap namespaces** — `using Haven.Common.Mediator;` / `using SmartQr.Common.Mediator;` → `using WoW.Two.Sdk.Backend.Beta.Mediator;` across every file that referenced `IQuery`/`ICommand`/`IQueryHandler`/`ICommandHandler`/`IMediator`/`ApplicationResult`/the markers.
5. **Rename `ApplicationResult` → `AppResult`** — folds in here (same edit that swaps the namespace). Renames the carrier symbol only; markers (`ISuccessResult`/`IFailureResult`/`IApplicationSuccessContext`/`IApplicationFailureContext`) keep their names.
6. **Adopt `.Match`** — rewrite the interim controller consume (haven `switch` in `ExternalListingContentExtractionController`; smart-qr `is`-pattern in `CodesController`) to `result.Match(onSuccess, onFailure)`.

Result: products carry **only business logic** (requests, handlers, domain result containers, controllers) — the mediator plumbing + result union come from the SDK, matching the "infra extracts to SDK" principle.

### Migration blast radius (per repo, measured)

| Symbol | haven (files) | smart-qr (files) |
|---|---|---|
| `IQuery<` | 5 | 2 |
| `ICommand` | 10 | 8 |
| `IQueryHandler` / `ICommandHandler` | 10 | 6 |
| `ApplicationResult<` | 25 | 13 |
| `SendAsync` | 11 | 2 |
| `AddHavenMediator` / `AddSmartQrMediator` registration sites | 3 | 2 |

Mostly mechanical (namespace swap + symbol rename); `ApplicationResult → AppResult` is the widest touch (25 / 13 files). A find-replace across each product's `*.cs` handles the bulk; `.Match` adoption is the only hand-edit (a handful of controller actions).

### Sequencing

1. Land Group A + Group B + `.Match` + `SendAsync` decision in the SDK (`src/Mediator/Cqrs/` + `src/Mediator/Result/`); update `Mediator.spec.md` / `Mediator.standard.md` to document the CQRS + result surface.
2. Reconcile `messaging/mediator.md` (Decision 2) — bless explicit `IQuery`/`ICommand` or keep the plain-`IRequest` line and note the CQRS markers as an alternative.
3. Migrate smart-qr first (smaller: 13-file result blast radius, 2 registration sites) as the proving ground; then haven.
4. Add a `check` to `docs/planning/platform-planning.md` backlog: "Absorb product mediator wrapper (CQRS markers + AppResult) — haven/smart-qr dogfood" and update `package-registry.md` once the CQRS+Result surface ships.

## See also

- SDK code: `src/Mediator/` (`Contracts.cs`, `Mediator.cs`, `MediatorServiceCollectionExtensions.cs`, `*/…Behavior.cs`)
- SDK docs: `src/Mediator/Mediator.spec.md` · `src/Mediator/Mediator.standard.md`
- Conventions: `conventions/development/backend/messaging/mediator.md` · `conventions/development/backend/foundation/result-pattern.md` · `conventions/development/backend/presentation/controllers.md`
- Wrapper sources being absorbed: `10x-ven-haven/.../Haven.Common/Mediator/` · `smart-qr-poc/.../SmartQr.Platform.Core/Mediator/`
- Roadmap: `docs/planning/platform-planning.md`
