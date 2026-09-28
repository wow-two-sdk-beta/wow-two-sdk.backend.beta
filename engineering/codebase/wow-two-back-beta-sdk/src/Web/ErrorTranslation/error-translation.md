# Web.ErrorTranslation

Translated error messages, switched on by configuration alone. Off by default: without the `ErrorTranslation`
section every message renders as authored. The default error mappers apply it to problem details, field errors and
validation advisories; no code change is needed.

## Enable

```jsonc
// appsettings.json — nothing else changes in the host
"ErrorTranslation": {
  "Enabled": true,
  "DefaultCulture": "en",                  // requests for it keep the authored text
  "SupportedCultures": [ "en", "ru", "uz" ],
  "Messages": {
    "ru": {
      "NotFound": "Не найдено.",             // error type → top-level message
      "OrderMissing": "Заказ {orderId} не найден.",  // AppError metadata "messageKey"
      "NotEmptyValidator": "Заполните поле «{PropertyName}».",  // field-error code
      "DuplicateEmail": "Этот email уже занят."        // identity code
    }
  }
}
```

## Lookup

| Message | Order |
|---|---|
| top-level (`AppError`) | catalog `messageKey` → catalog error type → built-in type text (`ru`, `uz`) → authored |
| field (`FieldError`) | catalog code → FluentValidation language pack → authored |

- Culture: the request-localization feature when that middleware ran, else `Accept-Language`, else the default culture.
- `SupportedCultures` empty accepts any culture a translation exists for; `uz` picks FluentValidation's Latin pack.
- Placeholders `{Name}` / `{Name:format}` fill from `FieldError.Params` or `AppError.Metadata`; a template naming a
  missing argument falls back to the authored message.
- `TranslateByErrorType: false` keeps specific authored messages unless a `messageKey` matches.
- The section reloads live with the configuration.
- Products translate their own strings with `IErrorTranslationService.TryTranslate(context, key, arguments, out message)`.
