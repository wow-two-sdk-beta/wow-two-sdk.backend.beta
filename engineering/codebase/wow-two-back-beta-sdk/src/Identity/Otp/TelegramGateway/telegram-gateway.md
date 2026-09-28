# Identity.Otp.TelegramGateway

`TelegramGatewayOtpDeliveryHandler` — the `telegram-gateway` channel: the Telegram Gateway API delivers the code to
the Telegram account behind a phone number, with no bot chat to link first.

```csharp
services.AddTelegramGatewayOtpDelivery(o => o.AccessToken = token);   // or Identity:Otp:TelegramGateway:AccessToken
```

- Codes must be 4–8 digits (`OtpCodeKind.Numeric`); others fail with `telegram_gateway_needs_4_to_8_digits`.
- The lifetime goes as the TTL, clamped to the Gateway's 30–3600 seconds; `SenderUsername` and `CallbackUrl` pass through.
- The registration returns the `IHttpClientBuilder` for resilience or a test handler.
