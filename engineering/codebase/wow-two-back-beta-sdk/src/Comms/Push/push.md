# Comms.Push

Push-notification seam: `IPushBroker.SendAsync(PushMessage) → PushSendResult` (result-typed, no exceptions except
cancellation). Plain HTTP with self-signed provider JWTs — no Firebase or APNs SDK.

| Provider | Registration | Auth |
|---|---|---|
| APNs (iOS) | `AddApnsPushBroker(o => { o.TeamId; o.KeyId; o.PrivateKeyPem; o.BundleId; })` | ES256 provider token from the `.p8` key, reused 50 min |
| FCM (Android, web, iOS via Firebase) | `AddFcmPushBroker(o => o.ServiceAccountJson = …)` | RS256 service-account assertion → OAuth token, cached until 5 min before expiry |

```csharp
services.AddApnsPushBroker(o =>
{
    o.TeamId = cfg["Apns:TeamId"]!; o.KeyId = cfg["Apns:KeyId"]!;
    o.PrivateKeyPem = File.ReadAllText(cfg["Apns:KeyPath"]!); o.BundleId = "uz.acme.app";
    o.UseSandbox = env.IsDevelopment();
});

var result = await push.SendAsync(new PushMessage { DeviceToken = token, Title = "Order shipped", Body = "Arrives Friday" }, ct);
if (result.TokenInvalid) await devices.RemoveAsync(token, ct);
```

- A message without title and body is silent: APNs `content-available` at priority 5, FCM data-only.
- `TokenInvalid` covers APNs `410`/`BadDeviceToken`/`Unregistered` and FCM `UNREGISTERED`; drop those tokens.
- `CollapseKey` → `apns-collapse-id` / Android `collapse_key`; `TimeToLive` → `apns-expiration` / Android `ttl`.
- APNs requests ask for HTTP/2; each registration returns the `IHttpClientBuilder` for resilience or test handlers.
- Web Push (VAPID) is not implemented yet.
