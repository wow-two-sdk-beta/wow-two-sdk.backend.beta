# Two-factor methods — build track

*Last updated: 2026-09-28*

> Delivered-code second factors (SMS, WhatsApp, Telegram, email) beside the authenticator app, each routed through a
> named broker, with configurable code types and message templates — all settable in code or host configuration.

## Analysis

- Today: the two-factor slice verifies only authenticator (TOTP) and recovery codes; OTP delivery exists separately
  (`IOtpService`, Telegram and SMS handlers) but is not wired into sign-in.
- A method = channel (how a code travels) + broker (which provider carries it) + code spec (type, length, lifetime)
  + message template (per culture). Methods are named in configuration, so a host can switch `sms` from Twilio to
  Eskiz, or add `whatsapp`, without code.
- Codes are stored through `IOtpService` (attempts, expiry, rate limit), keyed by user and method, so any code type works.
- Addresses: SMS and WhatsApp use the confirmed phone, email the confirmed email, Telegram the linked Telegram login.
- The user's chosen method is a stored token; `SignInService` reports it with the two-factor ticket.

## Rulings

- Brokers register keyed by name (`twilio`, `vonage`, `eskiz`, `whatsapp`, `telegram`) and the first registered of a
  kind is the default; a method names its broker or takes the default.
- Code generation moves to `OtpCodeSpec` (numeric, alphanumeric, letters; length) so each method sets its own.
- Formatting sits one level down, as `IOtpMessageFormatter`, so phone confirmation and passwordless codes share it;
  two-factor codes use the purpose `two-factor`. Per-culture ICU templates (`{code}`, `{minutes}`, `{app}`), culture
  parents, then the default culture, then built-in `en`/`ru`/`uz`.
- Channels are keyed `IOtpDeliveryHandler`s (`sms`, `whatsapp`, `telegram`, `telegram-gateway`, `email`); a method names
  a channel and optionally a broker. `IOtpDeliveryHandler` keeps its shape, so enumerating consumers (Haven) keep working.
- Brokers per capability register keyed (`ISmsBroker`, `IWhatsAppBroker`); `Add{Capability}Brokers(configuration)`
  registers each provider whose section exists, so the host configuration alone adds or reroutes one.
- Options: `TwoFactorOptions` (code `Action<>` + host section `Identity:TwoFactor`), host configuration applied last.

## Status

- [x] M1 — module-options recipe `AddModuleOptions<TOptions>`: code `Action<TOptions>` + host section, host last,
  live reload; first consumer `ConfigureValidation` (error translation)
- [x] M2 — OTP code specs: numeric / alphanumeric / letters generator, per-call spec and lifetime; forgiving verify
- [x] M3 — channels: keyed SMS brokers, WhatsApp (Meta Cloud API, Twilio), Telegram Gateway, email; formatter
- [x] M4 — two-factor methods: `UserTwoFactorMethodService` send / verify / enable / preferred; sign-in wiring
- [x] M5 — account API: login challenge names the method and auto-sends; `manage/2fa/methods/{method}/send|enable`,
  `manage/2fa/preferred`; 21 new tests incl. a host-configured email method end to end
