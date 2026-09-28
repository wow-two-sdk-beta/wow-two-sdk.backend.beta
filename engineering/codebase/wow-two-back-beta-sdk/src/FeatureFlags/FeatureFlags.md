# FeatureFlags

*Feature toggles via Microsoft.FeatureManagement (default) with an OpenFeature seam for vendor providers.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.FeatureFlags`.

## Surface

| Folder | Surface | Role |
|---|---|---|
| `Core/` | `AddFeatureFlags()`, `IFeatureFlags` / `FeatureManagerAdapter` | Microsoft.FeatureManagement + vendor-neutral facade |
| `OpenFeature/` | `AddOpenFeatureClient(clientName?)` | OpenFeature `IFeatureClient` seam for vendor SDKs |

## Quickstart — config-driven flags

```csharp
builder.Services.AddFeatureFlags();     // reads the "FeatureManagement" config section; enables [FeatureGate]

public sealed class Checkout(IFeatureFlags flags)
{
    public async Task PayAsync(CancellationToken ct)
    {
        if (await flags.IsEnabledAsync("NewPaymentFlow", ct)) { /* … */ }
    }
}
```

```jsonc
// appsettings.json
"FeatureManagement": { "NewPaymentFlow": true, "BetaBanner": { "EnabledFor": [ { "Name": "Percentage", "Parameters": { "Value": 25 } } ] } }
```

`AddFeatureFlags()` returns the `IFeatureManagementBuilder`, so you can chain `.AddFeatureFilter<MyFilter>()` or
`.WithTargeting()` (user = `User.Identity.Name`, groups = role claims). `[FeatureGate("Name")]` gates MVC actions/controllers.

## Gates

```csharp
app.MapGroup("/pricing").RequireFeatures("NewPricing");          // minimal APIs: 404 while any flag is off

services.AddMediatorFeatureGatingInterceptor();                    // mediator: IFeatureGated requests → NotFound
public sealed record PreviewInvoiceQuery(Guid Id) : IQuery<InvoiceDto>, IFeatureGated
{
    public IReadOnlyList<string> RequiredFeatures => ["NewPricing"];
}
```

A disabled feature answers as missing (`404` / `AppErrorType.NotFound`), so dark features reveal nothing.

## Quickstart — OpenFeature (vendor providers)

```csharp
builder.Services.AddOpenFeatureClient();
// once at startup, wire the vendor provider:
await Api.Instance.SetProviderAsync(new MyVendorProvider(apiKey));

public sealed class Banner(IFeatureClient flags)
{
    public Task<bool> ShowAsync() => flags.GetBooleanValueAsync("beta-banner", false);
}
```

## Roadmap (not yet built)

Vendor adapters as OpenFeature providers: ConfigCat · LaunchDarkly · GrowthBook · Unleash · Esquio. Variant assignment + experiment tracking.
