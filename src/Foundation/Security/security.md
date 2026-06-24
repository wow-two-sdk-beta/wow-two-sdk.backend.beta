# Foundation.Security

Envelope cryptography — AES-256-GCM authenticated encryption with a two-tier key model. A long-lived
master key (KEK) wraps many short-lived data keys (DEKs); each DEK encrypts the actual values. The
master-key source is abstracted (env var now, KMS/HSM later). Application-agnostic — no domain vocabulary.

| Seam | Default | Role |
|---|---|---|
| `ICryptoCore` | `CryptoCore` | Encrypt/decrypt a value under a supplied DEK + AAD (data plane) |
| `ISealKeeper` | `MasterKeySealKeeper` | Hold the KEK in memory; generate, wrap, unwrap DEKs (key plane) |
| `IMasterKeyProvider` | `EnvironmentMasterKeyProvider` | Where the KEK comes from — base64 256-bit key from an env var |

```csharp
builder.Services.AddEnvelopeCryptography(o => o.MasterKeyEnvironmentVariable = "MASTER_KEY");

// at startup, after the host is built:
app.Services.GetRequiredService<ISealKeeper>().Unseal();   // loads the KEK into memory

// encrypt a value (per-record DEK, wrapped under the KEK):
var dek     = sealKeeper.GenerateDataKey();
var wrapped = sealKeeper.WrapDataKey(dek, $"owner:{ownerId}");          // store wrapped beside the data
var payload = crypto.Encrypt(plaintext, dek, $"{ownerId}/{recordKey}"); // store payload
CryptographicOperations.ZeroMemory(dek);                               // clear the plaintext DEK

// decrypt:
var dek2      = sealKeeper.UnwrapDataKey(wrapped, $"owner:{ownerId}");
var recovered = crypto.Decrypt(payload, dek2, $"{ownerId}/{recordKey}");
CryptographicOperations.ZeroMemory(dek2);
```

- **AAD is yours to choose.** The module takes associated-data as a parameter and binds it into the GCM
  tag (authenticated, not encrypted). A common pattern: `"owner:{id}"` for the wrapped DEK and
  `"{owner}/{key}"` for the value — so a blob can't be replayed under a different owner or key.
- **Random 96-bit nonce per encryption**, never reused under one key; 128-bit tag. Decrypt throws
  `AuthenticationTagMismatchException` on any tamper / wrong key / wrong AAD — never returns corrupt bytes.
- **Sealed until unsealed.** No key configured → stays sealed (wrap/unwrap throw). A malformed key throws
  `MasterKeyFormatException` (key-free message, safe to log). Restart → sealed again.
- **Swap the key source** by registering your own `IMasterKeyProvider` before `AddEnvelopeCryptography`
  (it uses `TryAddSingleton`). Env now, KMS/HSM later — nothing else changes.
- **Generate a key:** `openssl rand -base64 32` → set as the env var.

## See also

- Spec: [`Security.spec.md`](./Security.spec.md)
- Underlying primitive: [`System.Security.Cryptography.AesGcm`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm)
