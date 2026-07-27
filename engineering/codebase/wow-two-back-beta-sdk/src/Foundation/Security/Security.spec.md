# Security (Envelope Cryptography) — spec

*Last updated: 2026-06-24*

> **Concrete API + usage.** Updated when public surface changes.

NuGet: `WoW.Two.Sdk.Backend.Beta` (mono-lib) · namespace `WoW.Two.Sdk.Backend.Beta.Foundation.Security`.

## Quick start

```csharp
using WoW.Two.Sdk.Backend.Beta.Foundation.Security;

builder.Services.AddEnvelopeCryptography();                                   // env-var key source (MASTER_KEY)
builder.Services.AddEnvelopeCryptography(o => o.MasterKeyEnvironmentVariable = "APP_MASTER_KEY");

// at startup:
app.Services.GetRequiredService<ISealKeeper>().Unseal();
```

## Public API

### `SecurityServiceCollectionExtensions`

| Method | Returns | Notes |
|---|---|---|
| `AddEnvelopeCryptography(this IServiceCollection, Action<EnvelopeCryptographyOptions>? configure = null)` | `IServiceCollection` | Registers `ICryptoCore`, `ISealKeeper`, and the default `IMasterKeyProvider` as singletons (all `TryAdd*`). Idempotent. |

### `ICryptoCore` (default `CryptoCore`, singleton)

| Member | Returns | Notes |
|---|---|---|
| `Encrypt(byte[] plaintext, byte[] dataKey, string associatedData)` | `EncryptedPayload` | AES-256-GCM under the DEK; AAD authenticated, not encrypted. |
| `Decrypt(EncryptedPayload payload, byte[] dataKey, string associatedData)` | `byte[]` | Throws `AuthenticationTagMismatchException` on tamper / wrong key / wrong AAD. |

### `ISealKeeper` (default `MasterKeySealKeeper`, singleton)

| Member | Returns | Notes |
|---|---|---|
| `IsSealed` | `bool` | `true` until the KEK is loaded. |
| `Unseal()` | `void` | Loads the KEK via `IMasterKeyProvider`; no-op (stays sealed) when none configured; throws `MasterKeyFormatException` when malformed. |
| `GenerateDataKey()` | `byte[]` | Fresh random 256-bit DEK. |
| `WrapDataKey(byte[] dataKey, string associatedData)` | `EncryptedPayload` | Encrypts the DEK under the KEK. Throws `InvalidOperationException` if sealed. |
| `UnwrapDataKey(EncryptedPayload wrapped, string associatedData)` | `byte[]` | Decrypts the DEK. Throws if sealed / AAD mismatch. |

### `IMasterKeyProvider` (default `EnvironmentMasterKeyProvider`, singleton)

| Member | Returns | Notes |
|---|---|---|
| `TryLoadKey()` | `byte[]?` | `null` when no key configured; throws `MasterKeyFormatException` on bad base64 / wrong length. Register your own impl before `AddEnvelopeCryptography` to use a KMS/HSM. |

### `EncryptedPayload` (sealed record)

Positional: `Nonce` (12 bytes), `Tag` (16 bytes), `CipherText` (`byte[]`). Persist all three together.

### `EnvelopeCryptographyOptions` (sealed class · section `EnvelopeCryptography`)

| Member | Type | Default | Notes |
|---|---|---|---|
| `MasterKeyEnvironmentVariable` | `string` | `"MASTER_KEY"` | Env var holding the base64 256-bit KEK (used by the default provider). |

### `MasterKeyFormatException` (sealed, `Exception`)

Thrown for a present-but-malformed key. Message is key-free (safe to log). A *missing* key is not an error.

## Custom key source (KMS / HSM)

```csharp
internal sealed class KmsMasterKeyProvider(IKmsClient kms) : IMasterKeyProvider
{
    public byte[]? TryLoadKey() => kms.Decrypt(/* wrapped KEK */);   // null if not provisioned
}

builder.Services.AddSingleton<IMasterKeyProvider, KmsMasterKeyProvider>(); // before AddEnvelopeCryptography
builder.Services.AddEnvelopeCryptography();                               // TryAdd keeps yours
```

## Associated-data (AAD) patterns

AAD is a consumer choice, passed per call and bound into the tag. Typical:

- Wrapped DEK → `"owner:{ownerId}"` (binds a DEK to its owner; can't be replayed under another).
- Value → `"{ownerId}/{recordKey}"` (binds ciphertext to its logical slot).

## Notes

- Random 96-bit nonce per `Encrypt` (never reused); 128-bit tag (max GCM strength).
- DEK plaintext is the caller's to clear (`CryptographicOperations.ZeroMemory`); the KEK is cleared on keeper `Dispose`.
- BCL-only (`System.Security.Cryptography`); no third-party crypto.

## See also

- Folder lead: [`security.md`](./security.md)
- Underlying lib: [`System.Security.Cryptography.AesGcm`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm)
