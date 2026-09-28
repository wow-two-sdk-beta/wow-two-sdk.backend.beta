# Identity.Otp.Telegram

Telegram delivery channel for `Identity/Otp`. `OtpDeliveryEnvelopeModel.DeliveryAddress` must be the
**numeric chat id** — your bot-link flow (`/start` + Share Contact) captures and stores it; that
flow stays consumer-owned (it's bot UX, not SDK territory).

```csharp
builder.Services
    .AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(config["Auth:BotToken"]!))
    .AddOtpService()
    .AddTelegramOtpDelivery(o =>
    {
        o.ScopeDisplayNames["crm"]   = "Haven CRM";
        o.ScopeDisplayNames["admin"] = "Haven Admin";
    });
```

- The SDK never takes the bot token — register `ITelegramBotClient` yourself (env var, vault, …).
- The envelope's `Text` is sent as is; without it the fallback template applies: `{0}` scope display name · `{1}` code ·
  `{2}` lifetime minutes. Options also bind from `Identity:Otp:Telegram`.
- Failures return `OtpDeliveryResult(false, reason)` — `invalid_chat_id` for malformed addresses,
  Telegram API errors pass through as the reason message.
- Registered keyed as the `telegram` channel and additively (`TryAddEnumerable`) beside the other channels.
- Codes to a phone number without a bot chat: the `telegram-gateway` channel (`Otp/TelegramGateway`).
