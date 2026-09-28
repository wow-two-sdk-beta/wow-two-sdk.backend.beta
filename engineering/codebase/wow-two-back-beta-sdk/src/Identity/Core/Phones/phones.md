# Identity.Core.Phones

Phone slice over the shipped `IOtpService`: set a number, confirm it by one-time code, sign in by code.
Codes are created here and delivered by the host's `IOtpDeliveryHandler` (SMS, Telegram, …).

## Quick start

```csharp
identity.AddPhoneNumbers().AddSignIn();

await phones.SetPhoneNumberAsync(user, "+998901234567");            // unconfirmed; rotates the stamp
var created = await phones.CreateConfirmationCodeAsync(user);       // deliver created.Code
await phones.ConfirmPhoneNumberAsync(user, code);

var signInCode = await phones.CreateSignInCodeAsync(phone);          // null → deliver nothing, answer the same
var user = await phones.VerifySignInCodeAsync(phone, code);
if (user is not null) await signIn.SignInAsync(user);
```

## Notes

- Store numbers in E.164; lookups compare exactly and only confirmed numbers sign in.
- Confirmation codes bind to the user id and the number, so a code cannot confirm another account or a changed number.
- Code length, lifetime, attempts and rate limits come from `OtpOptions`.
