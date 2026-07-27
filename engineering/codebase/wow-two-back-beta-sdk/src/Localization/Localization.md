# Localization

*Request-culture resolution, resx string localization, and human-friendly text/time phrasing.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Localization`. Built on the ASP.NET Core localization stack (shared framework) + Humanizer — no new dependencies.

## Surface

| Concern | Entry point |
|---|---|
| Request culture | `AddRequestLocalizationConventions(o => …)` + `app.UseRequestLocalizationConventions()` |
| Resx strings | `AddResxLocalization("Resources")` → inject `IStringLocalizer<T>` |
| Humanizing | `AddHumanizing()` → `IRelativeTimeFormatter`, `ITextHumanizer` (see `Humanizing/humanizing.md`) |

## Quickstart

```csharp
builder.Services
    .AddRequestLocalizationConventions(o =>
    {
        o.DefaultCulture = "en";
        o.SupportedCultures.Add("en");
        o.SupportedCultures.Add("uz");
        o.SupportedCultures.Add("ru");
    })
    .AddResxLocalization()   // .resx under /Resources
    .AddHumanizing();

var app = builder.Build();
app.UseRequestLocalizationConventions();   // early — before endpoints

app.MapGet("/greeting", (IStringLocalizer<Greetings> t) => t["Hello"].Value);
```

## Resolution order

Per request: `?culture=`/`?ui-culture=` query → culture cookie → `Accept-Language` header → `DefaultCulture`. Toggle any provider via the `Enable*Provider` flags. The resolved culture flows to `CultureInfo.CurrentCulture`/`CurrentUICulture`, so `IStringLocalizer`, `IRelativeTimeFormatter`, and `ITextHumanizer` all localize automatically.

## Roadmap (not yet built)

- ICU MessageFormat plurals/gender (needs a MessageFormat dependency) — for languages Humanizer's inflection doesn't cover.
- Portable-object (`.po`) / JSON resource providers as alternatives to `.resx`.

## See also

`Humanizing/humanizing.md`
