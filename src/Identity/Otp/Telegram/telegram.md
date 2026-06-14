# Identity.Otp.Telegram

Telegram delivery channel for `Identity/Otp`. `OtpDeliveryEnvelope.DeliveryAddress` must be the
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
- Template placeholders: `{0}` scope display name · `{1}` code · `{2}` lifetime minutes.
- Failures return `OtpDeliveryResult(false, reason)` — `invalid_chat_id` for malformed addresses,
  Telegram API errors pass through as the reason message.
- Handlers are additive (`TryAddEnumerable`) — register SMS/email channels alongside later.
