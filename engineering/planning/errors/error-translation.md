# Error translation — build track

*Last updated: 2026-09-28*

> Translated error messages as an opt-in, config-activated capability of the default error mappers.
> Supersedes the July deferral of the resx field-message resolver (owner greenlight 2026-09-28).

## Rulings — 2026-09-28

- Off by default: with no `ErrorTranslation` section every message renders exactly as authored.
- The host enables it through configuration alone (`ErrorTranslation:Enabled`), no code change; reload applies live.
- The default `ErrorMessageMapper` / `FieldErrorMessageMapper` consult `IErrorTranslationService`; a product's own
  mapper replaces them and opts out.
- Culture: the request-localization feature when the middleware ran, else `Accept-Language`, else the default culture.
  Only `SupportedCultures` are honoured; the default culture keeps authored (English) text.
- Lookup order — field errors: catalog `Code` → FluentValidation's language pack for the code → authored message.
  Top-level errors: catalog `messageKey` metadata → catalog `Type` → built-in `Type` text → authored message.
- Templates fill `{Name}` placeholders from `FieldError.Params` or `AppError.Metadata`; a missing argument keeps the authored message.
- Catalog entries live in configuration (`ErrorTranslation:Messages:{culture}:{key}`), so JSON files per culture work.

## Status

- [x] I1 — settings, culture resolution, catalog + FluentValidation + built-in type messages, default mappers wired; 5 tests
- [ ] I2 — response models: `AppError.ToApiFailure<T>(HttpContext)` and identity errors as translatable field errors
- [ ] I3 — `AddApiDefaults` hosts: config-only activation proven end to end through the exception handler

---

## I1 — Translation service

- `ErrorTranslationSettings` bound from `ErrorTranslation` via the container's `IConfiguration`; missing → disabled.
- `IErrorTranslationService.TryTranslate(context, key, arguments, out message)` for products' own messages.
- Built-in `AppErrorType` texts for `ru` and `uz` (Latin); catalogs override them.
- Acceptance: disabled renders authored text; `ru` renders Russian type text and FluentValidation field text; a
  catalog key overrides both; unsupported cultures fall back to authored text.

## I2 — Response models

- `ToApiFailure<T>` builds `ApiResponse<T>.Failure` with the mapped status and the translated message.
- `IdentityResult` failures become `FieldError`s keyed by `IdentityError.Code`, so catalogs translate them.

## I3 — Host activation

- A web host with `AddApiDefaults` translates a thrown `AppException` after only a configuration change.
