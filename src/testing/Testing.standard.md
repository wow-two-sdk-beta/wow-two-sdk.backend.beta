# Testing — standard

*Last updated: 2026-05-04*

> Behavioral contract for the testing scaffold. RFC 2119.

## Purpose

Provide a low-friction integration-test scaffold for ASP.NET Core APIs that consume any other `WoW.Two.Sdk.Backend.Beta.*` wrapper, plus reusable async fixture interfaces for container-backed test infrastructure.

## Behavior

### `WebApiTestHost<TEntryPoint>`

- The class **MUST** extend `WebApplicationFactory<TEntryPoint>`.
- The class **MUST** expose a `FakeTimeProvider Clock` field controllable from tests.
- The class **MUST** register `Clock` as the singleton `TimeProvider` in the host's service collection, *replacing* any pre-existing registration.
- The class **MUST** allow consumers to mutate `IServiceCollection` via `ConfigureServicesHook`.
- The class **MUST** allow consumers to mutate `IHostBuilder` via `ConfigureHostHook`.
- The class **MUST NOT** force a specific environment (consumers control via `ConfigureHostHook`); default is `Production`.

### `WebApiTestBase<TEntryPoint>`

- The class **MUST** implement `IAsyncLifetime` and `IDisposable`.
- The class **MUST** expose `Host`, `Client`, and `Clock` properties.
- The class **MUST** lazily build the host on first access.
- The class **SHOULD** be inheritable so consumers can override `BuildHost()` to compose fixtures.
- `DisposeAsync` **MUST** dispose the host if created.

### `IAsyncTestFixture`

- The interface **MUST** define `Name`, `StartAsync`, `ResetAsync`, and inherit `IAsyncDisposable`.
- Implementations **MUST** be safe to call `StartAsync` multiple times (idempotent on already-started state).
- Implementations **MUST** propagate cancellation tokens to underlying ops.

### `AsyncFixtureCollection`

- **MUST** start fixtures in the order they were added.
- **MUST** reset fixtures in the order they were added.
- **MUST** dispose fixtures in *reverse* order.

## Failure modes

- If `WebApiTestHost.CreateClient()` is called before any `ConfigureServicesHook` registration runs, the hook **MUST** still execute as part of the host build — i.e. lazy access **MUST** trigger configuration application.
- If a fixture in a collection fails during `StartAsync`, already-started fixtures **MUST** still be disposed.

## Non-goals

- This package does NOT include xUnit `[Collection]` attributes — collections are the consumer's choice.
- This package does NOT spin up containers itself — that's the job of `WoW.Two.Sdk.Backend.Beta.Testing.Containers.*`.

## See also

- [Testing.spec.md](./Testing.spec.md)
- `Microsoft.AspNetCore.Mvc.Testing`
