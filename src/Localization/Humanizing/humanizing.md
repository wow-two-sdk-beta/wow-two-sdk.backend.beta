# Localization.Humanizing

*Human-friendly time and text phrasing — injectable, testable facades over Humanizer.*

| Type | Role |
|---|---|
| `IRelativeTimeFormatter` / `RelativeTimeFormatter` | "3 hours ago" / "in 2 days"; "now" comes from the injected `TimeProvider` (deterministic under test) |
| `ITextHumanizer` / `TextHumanizer` | `Ordinalize(int)` · `Quantity(word, count)` · `Pluralize` / `Singularize` |

```csharp
builder.Services.AddHumanizing();   // also registers TimeProvider.System if absent

string ago  = relTime.Format(comment.CreatedAt);        // "5 minutes ago"
string nth  = text.Ordinalize(3);                       // "3rd"
string many = text.Quantity("file", 12);                // "12 files"
```

## Notes

- Culture-sensitive operations honour the ambient `CultureInfo.CurrentCulture` — set per request by the localization middleware, so no culture argument is needed in a web request (pass one explicitly to `Format` outside a request scope).
- `RelativeTimeFormatter` takes "now" from `TimeProvider`; register the real clock via `AddTimeProviders()` or rely on the `TimeProvider.System` fallback `AddHumanizing()` provides.
- Inflection (`Pluralize`/`Singularize`) is English-oriented — for other languages prefer ICU MessageFormat (roadmap) or explicit resx strings.
