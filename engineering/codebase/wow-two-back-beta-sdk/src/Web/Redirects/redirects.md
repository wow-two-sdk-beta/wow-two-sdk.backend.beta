# Redirect targets

> Advisory checks on where a stored redirect sends people; findings never block a write.

```csharp
public sealed class UrlContentValidator : AbstractValidator<UrlContent>
{
    public UrlContentValidator()
    {
        // Structural rule: an unusable value is an error.
        RuleFor(content => content.Url).Cascade(CascadeMode.Stop).NotEmpty().Must(IsAbsoluteHttpUrl);
        // Advisories: a separate rule, so a stopping cascade cannot hide them.
        RuleFor(content => content.Url).AdviseOnRedirectTarget();
    }
}
```

| Code | Severity | Finding |
|---|---|---|
| `RedirectTargetPrivateNetwork` | Warning | private or reserved IP, `localhost`, a single-label host or an intranet suffix |
| `RedirectTargetReservedName` | Warning | `.test`, `.example` or `.invalid` |
| `RedirectTargetInsecure` | Warning | `http` instead of `https` |
| `RedirectTargetCredentials` | Warning | user information inside the URL |
| `RedirectTargetIpAddress` | Info | a public IP literal instead of a domain |
| `RedirectTargetPort` | Info | a non-default port |
| `RedirectTargetInternationalHost` | Info | a punycode or Unicode label |

- A target behind a VPN, login or intranet can be legitimate, so the checks advise rather than reject.
- Values that are not absolute `http` or `https` URLs pass here; the structural rule owns them.
- The checks make no network calls: they read the URL only.

## Returning the findings

The mediator's validating interceptor records warnings and suggestions in the scoped
`IValidationAdvisoryTracker` and lets the request continue. The API maps them with the request culture:

```csharp
var result = await sender.SendAsync(command, ct);
return result.Match(
    success => Ok(ApiResponse<CodeDto>.Ok(success.Data.ToDto(), advisories.Map(HttpContext))),
    failure => this.ToProblem(failure.Error));
```

The response carries them beside the payload, in the `errors[]` item shape plus `severity`:

```json
{ "data": { "id": "…" }, "warnings": [{ "property": "Url", "code": "RedirectTargetPrivateNetwork", "severity": "Warning", "message": "…" }] }
```
