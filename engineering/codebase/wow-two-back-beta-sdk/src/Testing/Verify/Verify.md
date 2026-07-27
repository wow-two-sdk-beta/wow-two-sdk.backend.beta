# WoW.Two.Sdk.Backend.Beta.Testing.Verify

> [Verify](https://github.com/VerifyTests/Verify) snapshot-testing defaults for the Wow Two backend SDK.

Brings:
- `Verify.Xunit`
- `Verify.AspNetCore`
- `Verify.Http`
- `Verify.EntityFramework`

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Testing.Verify
```

## Usage

```csharp
// In your test project, add a ModuleInitializer:
internal static class TestSetup
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Init() => VerifyDefaults.Initialize();
}
```

Then write snapshot tests:

```csharp
[Fact]
public Task UserResponse_matches_snapshot()
{
    var resp = await Client.GetAsync("/users/42");
    return Verify(resp);
}
```

## See also

- [Verify docs](https://github.com/VerifyTests/Verify)
