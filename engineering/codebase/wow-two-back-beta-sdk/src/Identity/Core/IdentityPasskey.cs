using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>
/// One WebAuthn credential (passkey) a user registered: its id, public key and signature counter. Consumed by the
/// passkey slice; the private key never leaves the user's authenticator.
/// </summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityPasskey<TKey> : IKeyedEntity<Guid>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The owning user.</summary>
    public TKey UserId { get; set; } = default!;

    /// <summary>The credential id the authenticator reports; unique.</summary>
    public byte[] CredentialId { get; set; } = [];

    /// <summary>The COSE-encoded public key signatures verify against.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>The user handle the credential was registered for.</summary>
    public byte[] UserHandle { get; set; } = [];

    /// <summary>The signature counter from the last use; a counter that fails to grow signals a cloned authenticator.</summary>
    public uint SignCount { get; set; }

    /// <summary>The authenticator model id.</summary>
    public Guid AaGuid { get; set; }

    /// <summary>The label the user gave it, such as "MacBook".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When it was registered (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When it last signed in (UTC); null before first use.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}
