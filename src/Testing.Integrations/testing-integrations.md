# Testing.Integrations

Integration-client test doubles for the backend SDK — the companion to the dependency-light `Testing` package, for the outbound-integrations tier.

Holds configurable fakes for the SDK's integration clients (`WoW.Two.Sdk.Backend.Beta.Integrations.*`), which implement contracts that live in the core mono-lib — so the base `Testing` package never takes that dependency (mirrors `Testing.Data` for the data tier). Reference `WoW2.Sdk.Backend.Beta.Testing.Integrations` from a test project only; it pulls core transitively.

Swap a fake in for the real client and every GitHub / GHCR probe is short-circuited — no live REST call, no OAuth token — so the not-found / unauthorized / failed branches are deterministic.

## Doubles

| Fake | Implements | Replaces |
|---|---|---|
| `FakeGitHubClient` | `IGitHubClient` | the real GitHub REST client (and its per-request OAuth token) |
| `FakeContainerRegistryClient` | `IContainerRegistryClient` | the real GHCR manifest probe |

Both default to a sensible baseline and expose a settable + fluent (`With…`) surface for canned responses.

### `FakeGitHubClient`

Defaults to the happy path: `Repo = Exists`, `File = Present`, no `Releases`, `WorkflowRun = None`.

- `Repo` / `WithRepo(RepoCheck)` — canned `RepoExistsAsync` outcome.
- `File` / `WithFile(FileCheck)` — canned `FileExistsAsync` outcome.
- `Releases` / `WithReleases(params ReleaseInfo[])` — releases (newest first) driving both `GetLatestReleaseAsync` (head only) and `GetReleasesAsync` (paged by `limit`); forces `ReleaseOutcome = Found`.
- `ReleaseOutcome` / `WithReleaseOutcome(ReleaseLookup)` — force `Unauthorized` / `Failed` / `None` on the release lookups.
- `WorkflowRun` / `WithWorkflowRun(BuildRunCheck)` — canned `GetLatestWorkflowRunAsync` outcome.

### `FakeContainerRegistryClient`

Defaults to `Missing` for every tag. Per-probe resolution order: `Override` → `{repo}:{tag}` in `ExistingImages` → bare `tag` in `ExistingTags`.

- `ExistingTags` / `WithTag(tag)` — mark a tag published for **any** repo (the common single-repo suite).
- `ExistingImages` / `WithImage(repo, tag)` — mark one `{repo}:{tag}` published (when a test spans repos).
- `Override` / `WithOverride(ImageCheck)` — force one outcome everywhere (the `Unauthorized` / `Failed` paths).

## Usage

Register the fake in place of the real client in the test host, hold a reference, and flip it per test:

```csharp
var gitHub = new FakeGitHubClient();        // happy path by default
var registry = new FakeContainerRegistryClient().WithTag("v1.2.0");

// in the test host's service overrides:
services.RemoveAll<IGitHubClient>();
services.AddSingleton<IGitHubClient>(gitHub);
services.RemoveAll<IContainerRegistryClient>();
services.AddSingleton<IContainerRegistryClient>(registry);

// drive a failure branch:
gitHub.WithRepo(RepoCheck.Unauthorized);
registry.WithOverride(ImageCheck.Failed);
```

## See also

- `../Integrations/integrations.md` — the real clients these doubles stand in for.
- `../Testing.Data/Testing.Data.md` — the data-tier companion this mirrors.
