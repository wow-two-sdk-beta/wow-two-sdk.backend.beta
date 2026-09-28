# Error translation — build track

*Last updated: 2026-09-28*

> Translated error messages as an opt-in, config-activated capability of the default error mappers.
> Supersedes the July deferral of the resx field-message resolver (owner greenlight 2026-09-28).

## Rulings — 2026-09-28

- Off by default: until `Validation:Translation:Enabled` is set every message renders exactly as authored.
- Translation belongs to the validation module: code sets defaults with `ConfigureValidation(o => …)`, the host
  section `Validation:Translation` has the last word and reloads live.
- The default `ErrorMessageMapper` / `FieldErrorMessageMapper` consult `IErrorTranslationService`; a product's own
  mapper replaces them and opts out.
- Culture: the request-localization feature when the middleware ran, else `Accept-Language`, else the default culture.
  Only `SupportedCultures` are honoured; the default culture keeps authored (English) text.
- Lookup order — field errors: catalog `Code` → FluentValidation's language pack for the code → authored message.
  Top-level errors: catalog `messageKey` metadata → catalog `Type` → built-in `Type` text → authored message.
- Templates fill `{Name}` placeholders from `FieldError.Params` or `AppError.Metadata`; a missing argument keeps the authored message.
- Catalog entries live in configuration (`Validation:Translation:Messages:{culture}:{key}`), so JSON files per culture work.

## Status

- [x] I1 — settings, culture resolution, catalog + FluentValidation + built-in type messages, default mappers wired; 5 tests
- [x] I2 — response models: `AppError.ToApiFailure<T>(HttpContext)`; identity errors carry values and become field errors
- [x] I3 — `AddApiDefaults` hosts: config-only activation proven end to end through the exception handler
- [x] I4 — ICU-style plurals with CLDR rules and pseudo-localization (`MessageTemplateMapper`); 27 translation tests
- [x] I5 — translation is part of the validation module: `ValidationOptions.Translation`, set with
  `ConfigureValidation(o => …)` or the host section `Validation:Translation`; the standalone section is gone; 28 tests

---

## I1 — Translation service

- `ValidationTranslationOptions` under `ValidationOptions`, through the module-options recipe; missing → disabled.
- `IErrorTranslationService.TryTranslate(context, key, arguments, out message)` for products' own messages.
- Built-in `AppErrorType` texts for `ru` and `uz` (Latin); catalogs override them.
- Acceptance: disabled renders authored text; `ru` renders Russian type text and FluentValidation field text; a
  catalog key overrides both; unsupported cultures fall back to authored text.

## I2 — Response models

- `ToApiFailure<T>` builds `ApiResponse<T>.Failure` with the mapped status and the translated message.
- `IdentityResult.ToValidationError()` turns failures into `FieldError`s keyed by `IdentityError.Code`, with
  `IdentityError.Params` as placeholder values; built-in `ru`/`uz` texts cover every identity code.

## I3 — Host activation

- A web host with `AddApiDefaults` translates a thrown `AppException` after only a configuration change.

## I5 — Validation module options

- `AddModuleOptions<TOptions>(section, configure)`: defaults → code → host section, validated at boot, live reload.
- `ConfigureValidation` avoids .NET 10's `AddValidation(Action<Microsoft.Extensions.Validation.ValidationOptions>)`.
- Acceptance: code alone enables translation; a host `Enabled: false` overrides the code.
