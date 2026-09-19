# WoW.Two.Sdk.Backend.Beta.Validation

> FluentValidation registration helpers — assembly scanning by convention.

## Install

```
dotnet add package WoW.Two.Sdk.Backend.Beta.Validation
```

## Usage

```csharp
// Scan calling assembly for validators
builder.Services.AddFluentValidatorsFromAssemblies();

// Or specify assemblies explicitly
builder.Services.AddFluentValidatorsFromAssemblies(typeof(Program).Assembly);
```

Define validators normally:

```csharp
public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(13, 120);
    }
}
```

Use external validation for request, entity, and value-object data by default. Validate a copied or deserialized candidate at the boundary that accepts it for use or persistence. Constructor guards remain for programmer preconditions; constructor data checks require an exceptional type contract that documents how copies and deserialization preserve the invariant.

## What a failure carries

`ValidationError.Failures` is a list of `FieldError`:

| Member | What | Source |
|---|---|---|
| `Property` | the member path that failed | `ValidationFailure.PropertyName` |
| `Message` | the display message | `ValidationFailure.ErrorMessage` |
| `Code` | the stable rule code | `ValidationFailure.ErrorCode` (`NotEmptyValidator`, …) |
| `Params` | the rule's operands, or `null` | `ValidationFailure.FormattedMessagePlaceholderValues` |

`Code` + `Params` exist so a consumer can **re-render** the failure — in its own wording or its own language —
instead of displaying `Message` verbatim. The frontend SDK does exactly that
(`@wow-two-beta/ui` `foundation/validation` `createMessageResolver`), which is how a client-caught and a
server-caught failure of one rule end up reading identically.

`PropertyName` in `Params` is the **display** name — `.WithName()` makes it diverge from `Property`.

## Sensitive members

FluentValidation hangs the **rejected value** on every failure's placeholders. `PropertyValue` and
`PropertyPath` are never published — no switch, no config.

Everything else is per-validator, because sensitivity belongs to the **operation**, not the member name:

```csharp
// sign-up — the user cannot satisfy a length rule nobody told them about
public sealed class SignUpValidator : AbstractValidator<Credentials>
{
    public SignUpValidator() => RuleFor(c => c.Password).MinimumLength(12);
}

// sign-in — same member, same rule, nothing about the secret leaves
public sealed class SignInValidator : AbstractValidator<Credentials>, ISensitiveMembers
{
    public IReadOnlySet<string> SensitiveMembers { get; } = new HashSet<string> { nameof(Credentials.Password) };

    public SignInValidator() => RuleFor(c => c.Password).MinimumLength(12);
}
```

A declared member reports `Params = null` — key-level redaction is not enough, since `TotalLength` alone
discloses the secret's length. `Code` and `Message` are unaffected, so the failure still renders.

## Advisory failures

`.WithSeverity(Severity.Warning)` marks a rule that advises without blocking.

| Call | Sees | Verdict |
|---|---|---|
| `Validate` / `ValidateAndThrow` | `Error` only | fails the request |
| `Inspect` | every severity, rule order | none — returns `IReadOnlyList<FieldError>` |

`Inspect` is the read for an advisory endpoint — same rules, same codes, same paths, no verdict. Wire it to a
sub-resource (`POST /codes/validate`), not a new HTTP verb; none of the registered verbs means "check this".
Debounce on the client and render `Severity.Warning` entries as hints the user may ignore.

## Localize the field messages

`IErrorMessageMapper` resolves the top-level `detail`; `IFieldErrorMessageMapper` resolves each
`errors[].message`. Both default to a passthrough — the seam ships wired, the translation does not.

```csharp
public sealed class ResxFieldErrorMessageMapper(IStringLocalizer<ValidationMessages> localizer)
    : IFieldErrorMessageMapper
{
    public string Map(FieldError error, HttpContext context)
    {
        var template = localizer[error.Code];
        return template.ResourceNotFound ? error.Message : template.Value;
    }
}

// register BEFORE AddErrorHttpStatusMapping — the SDK's default is TryAdd
builder.Services.AddSingleton<IFieldErrorMessageMapper, ResxFieldErrorMessageMapper>();
```

The request culture is already on `CultureInfo.CurrentUICulture` when `UseRequestLocalizationConventions`
(`src/Localization/`) is in the pipeline, so the localizer needs no extra wiring.

A validator's `.WithMessage("…")` literal is the **only** place an authored string enters, so it is the one
place worth routing through a localizer per-rule when the code-keyed table is not enough.

## See also

- [FluentValidation docs](https://docs.fluentvalidation.net/)
- `wow-two-ws/ideas/shared-validation-spec.md` — the client/server one-voice vector
- `wow-two-ws/ideas/validation-wrapper-analysis.md` — FluentValidation's global hooks, and why no wrapper type
