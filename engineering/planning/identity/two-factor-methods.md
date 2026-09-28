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
- Formatting is `ITwoFactorMessageFormatter`; the default fills per-culture templates (ICU placeholders `{code}`,
  `{minutes}`, `{app}`) and falls back to the method's default culture.
- Options: `TwoFactorOptions` (code `Action<>` + host section `Identity:TwoFactor`), host configuration applied last.

## Status

- [x] M1 — module-options recipe `AddModuleOptions<TOptions>`: code `Action<TOptions>` + host section, host last,
  live reload; first consumer `ConfigureValidation` (error translation)
- [ ] M2 — OTP code specs: numeric / alphanumeric / letters generator, per-call spec and lifetime
- [ ] M3 — channel brokers: keyed SMS brokers, WhatsApp Cloud API broker, Telegram Bot API broker
- [ ] M4 — two-factor methods: issue + verify per method, formatter, user method choice, sign-in wiring
- [ ] M5 — account API: auto-send the chosen method's code on login; enable / choose a delivered method
